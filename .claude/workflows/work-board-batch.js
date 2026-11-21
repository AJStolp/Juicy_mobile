export const meta = {
  name: 'work-board-batch',
  description: 'Work a batch of juicysortgame tickets through the board one at a time: build, independent QA, merge into dev, then open the promotion PR for Anthony',
  whenToUse: 'Anthony has queued tickets at ready: agent on the juicysortgame board and wants a lead to work the batch. Pass {tickets: ["T-005", ...]} to fix the order, or nothing to take the frontier.',
  phases: [
    { title: 'Plan', detail: 'read the board, order the batch, respect blocked_by' },
    { title: 'Build', detail: 'implementer per ticket: branch off dev, change, gate, push' },
    { title: 'QA', detail: 'a separate agent proves it against the ticket and uploads evidence' },
    { title: 'Deliver', detail: 'PR into dev, squash merge with a Refs trailer, ticket to done' },
    { title: 'Promote', detail: 'open the dev to main PR and its release ticket, then stop for Anthony' },
  ],
}

// One ticket at a time, never in parallel: only one Unity may have the
// project open, and heavy runs heat the laptop (AGENTS.md, "One Unity at a
// time"). Every stage is a separate agent so the one that builds is never the
// one that judges or merges its own work (headspace WORKFLOW.md).

const BOARD = 'http://100.76.63.94:8000/api/projects/juicysortgame'

const SHARED = `
Repo: /Users/astolp/Desktop/Dev/JuicySortGame (Unity 6000.3.10f1). AGENTS.md there and the headspace
workflow (GET http://100.76.63.94:8000/api/documents/headspace/workflow) are the rules; read them.
Board API: ${BOARD} (no login; /openapi.json describes it). Useful calls:
  GET  ${BOARD}/tickets/{id}
  POST ${BOARD}/tickets/{id}/move   {"status": "...", "actor": "<your role>", "reason": "..."}
  POST ${BOARD}/tickets/{id}/note   {"text": "...", "author": "<your role>"}
  PATCH ${BOARD}/tickets/{id}       {"ready": "human"} and other fields
  POST ${BOARD}/tickets/{id}/claim  {"who": "<your role>"}
  POST ${BOARD}/tickets/{id}/evidence/upload  multipart: file=@path, detail=..., passed=true|false

Constraints that hold for every stage:
- No Unity editor or other Unity process may be running when you start one. Check with
  pgrep -fl "Unity.app/Contents/MacOS/Unity" and wait or stop if one is.
- Unity rewrites .mat files and ProjectSettings on its own. Commit only files the ticket's change
  needs. Before switching branches, discard Unity's own rewrites of files the ticket does not touch
  (git checkout -- <file>) so they never ride along.
- Never commit to dev or main directly. Never merge into main. Never close a ticket.
- "passed" on evidence comes from an exit code or a measured number against the ticket's target,
  never from a judgment that it looks fine. Evidence is an uploaded file, not a path in a note.
- Notes are plain American English prose for Anthony: no hyphens or em dashes as punctuation.
`

const PLAN_SCHEMA = {
  type: 'object',
  properties: {
    order: {
      type: 'array', description: 'tickets in the order to work them',
      items: { type: 'object', properties: { id: { type: 'string' }, blocked_by: { type: 'array', items: { type: 'string' } } }, required: ['id', 'blocked_by'] },
    },
    skipped: { type: 'array', items: { type: 'object', properties: { id: { type: 'string' }, why: { type: 'string' } }, required: ['id', 'why'] } },
  },
  required: ['order', 'skipped'],
}

const BUILD_SCHEMA = {
  type: 'object',
  properties: {
    outcome: { type: 'string', enum: ['built', 'awaiting_pick', 'failed', 'blocked'] },
    branch: { type: 'string' },
    commit: { type: 'string' },
    summary: { type: 'string', description: 'what changed and what the gate said, two or three sentences' },
  },
  required: ['outcome', 'branch', 'summary'],
}

const QA_SCHEMA = {
  type: 'object',
  properties: {
    passed: { type: 'boolean' },
    gate_exit: { type: 'integer' },
    evidence_uploaded: { type: 'array', items: { type: 'string' } },
    misses: { type: 'array', items: { type: 'string' }, description: 'each Proven by line that failed or could not be produced' },
  },
  required: ['passed', 'gate_exit', 'evidence_uploaded', 'misses'],
}

const DELIVER_SCHEMA = {
  type: 'object',
  properties: {
    merged: { type: 'boolean' },
    pr_url: { type: 'string' },
    dev_commit: { type: 'string' },
    problem: { type: 'string' },
  },
  required: ['merged', 'pr_url'],
}

// ---------------------------------------------------------------- Plan
phase('Plan')
const requested = Array.isArray(args?.tickets) ? args.tickets : null
const plan = await agent(`${SHARED}
You are the lead planning a batch. ${requested
    ? `The batch is these tickets, in this order unless blocked_by forces otherwise: ${requested.join(', ')}.`
    : `Take the batch from GET ${BOARD}/frontier, plus any ticket in in_progress at ready: agent (work already started).`}

Acceptance:
- order lists every ticket to work with its blocked_by as the board has it, each one after anything in its blocked_by that is also in the batch.
- A ticket whose blocked_by names an open ticket (backlog, todo, in_progress, qa) that is NOT in the batch goes in skipped with the reason.
- A ticket that is not ready: agent goes in skipped.
- Do not change anything on the board or in git. Read only.`, { label: 'plan batch', phase: 'Plan', schema: PLAN_SCHEMA })

for (const s of plan.skipped) log(`skipping ${s.id}: ${s.why}`)
log(`batch order: ${plan.order.map(t => t.id).join(' → ')}`)

// ---------------------------------------------------------------- Per ticket
const results = []
const notDelivered = new Set()

for (const { id, blocked_by } of plan.order) {
  // A ticket that depends on one that did not reach dev this run cannot start.
  const waiting = blocked_by.filter(b => notDelivered.has(b))
  if (waiting.length) {
    log(`${id}: not started, waits on ${waiting.join(', ')} which did not reach dev this run`)
    notDelivered.add(id)
    results.push({ id, stage: 'skipped', detail: `waits on ${waiting.join(', ')}` })
    continue
  }

  // ---------------- Build
  const build = await agent(`${SHARED}
You are the implementer for ${id}. Read the ticket and its notes in full first.

Acceptance:
- You claimed it (who: "implementer") and it is in in_progress.
- Before changing anything, a note on the ticket states the expected result in the ticket's own pass
  terms (from Expected outcome and Proven by). QA fails a ticket without one.
- The work is on a branch named ${id}-<short-slug>, cut from the latest origin/dev. If a branch for
  ${id} already exists (locally or on origin), continue from it and from what its notes say is done,
  rather than starting over; bring it level with origin/dev first.
- Every Checklist item is done, within the ticket's Engineer notes constraints.
- The gate passes: batch Unity EditMode tests exit 0 (command in AGENTS.md). Read failures from the XML.
- One commit (or a few) on the branch whose newest message states what changed and why, ending with
  "Refs ${id}", and the branch is pushed to origin.
- A note on the ticket says what changed, which files, and the gate result with the test count.

Design tickets (labelled prototype, or whose Proven by ends with waiting at ready: human for a pick):
build the switchable options, capture each as the ticket asks, upload the captures and a
recommendation note to the ticket, set ready to human, and return outcome "awaiting_pick". Do not
remove any option.

If you cannot meet the acceptance, do not push a broken branch: leave the ticket in its column, set
ready to human, write a note with exactly what blocked you, and return outcome "failed" (or "blocked"
if a dependency or a running Unity stopped you before you began).

Do not capture or upload the ticket's proof; QA does that. Do not open a PR.`,
    { label: `${id} build`, phase: 'Build', schema: BUILD_SCHEMA })

  if (!build || build.outcome !== 'built') {
    notDelivered.add(id)
    const stage = build?.outcome === 'awaiting_pick' ? 'awaiting Anthony\'s pick' : (build?.outcome || 'build died')
    log(`${id}: ${stage}`)
    results.push({ id, stage, detail: build?.summary || '' })
    continue
  }
  log(`${id}: built on ${build.branch}`)

  // ---------------- QA
  const qa = await agent(`${SHARED}
You are QA for ${id}. You did not build it and you do not trust the implementer's numbers. Branch:
${build.branch}. Read the ticket, its Expected outcome, its Proven by, and its notes.

Acceptance:
- You checked out ${build.branch} at origin's head and ran the gate yourself. Its exit code is gate_exit,
  and the test XML is uploaded as evidence.
- Every line under Proven by is produced by you from this branch and uploaded as a file with an honest
  detail and a passed value from its exit code or measured number against the ticket's target. That
  includes any "before" artifact: produce it from origin/dev if the ticket asks for one.
- The failure path the ticket names is shown, not just the happy path.
- The ticket is in qa, with a note listing each Proven by line and its result.
- passed is true only if the gate exited 0 and every Proven by line met its target.

If it fails: leave the ticket in qa, set ready to human, write each miss on the ticket. Do not fix the
code and do not send it back for another build. Return passed false with the misses.`,
    { label: `${id} qa`, phase: 'QA', schema: QA_SCHEMA })

  if (!qa || !qa.passed) {
    notDelivered.add(id)
    log(`${id}: QA failed${qa ? ': ' + qa.misses.join('; ') : ' (agent died)'}`)
    results.push({ id, stage: 'qa failed', detail: qa ? qa.misses.join('; ') : 'qa agent died' })
    continue
  }
  log(`${id}: QA passed, ${qa.evidence_uploaded.length} files on the ticket`)

  // ---------------- Deliver
  const deliver = await agent(`${SHARED}
You are the lead delivering ${id}. QA passed it on ${build.branch}.

Acceptance:
- The branch is level with origin/dev. If you had to rebase or merge dev in, the gate was run again and
  exited 0 before going further; if it fails or the merge conflicts in a way you cannot resolve without
  changing behavior, stop: set ready to human, note why, return merged false with the problem.
- A PR from ${build.branch} into dev exists (gh pr create --base dev), titled from the newest commit's
  subject, body summarizing the change and linking the ticket's evidence, ending with "Refs ${id}"
  and the attribution lines from the session. No "Closes" trailer of any kind on it.
- It is squash merged into dev, and the squash commit's message is the branch's newest commit message
  ending in "Refs ${id}". The remote branch is deleted.
- The ticket is in done, with a note naming the PR and the dev commit.`,
    { label: `${id} deliver`, phase: 'Deliver', schema: DELIVER_SCHEMA })

  if (!deliver || !deliver.merged) {
    notDelivered.add(id)
    log(`${id}: not merged${deliver?.problem ? ': ' + deliver.problem : ''}`)
    results.push({ id, stage: 'delivery stopped', detail: deliver?.problem || 'deliver agent died' })
    continue
  }
  log(`${id}: merged into dev, ${deliver.pr_url}`)
  results.push({ id, stage: 'done', detail: deliver.pr_url })
}

// ---------------------------------------------------------------- Promote
phase('Promote')
const delivered = results.filter(r => r.stage === 'done').map(r => r.id)
let promotion = null
if (delivered.length) {
  promotion = await agent(`${SHARED}
You are the lead. These tickets were merged into dev this run: ${delivered.join(', ')}.

Acceptance:
- A promotion PR from dev into main is open (reuse an existing open one and update its body rather than
  opening a second). Its body lists every ticket in the board's done column that has a commit on dev
  not on main, each with its title, and ends with one "Closes T-xxx" line per ticket, then the
  attribution lines from the session.
- It is NOT merged. Anthony merges it after seeing the evidence. Nothing moves to closed.
- Once its body is final, the board has exactly one live release ticket (type release, state draft,
  approved or deploying) and its range names the PR's head sha. Build it from the headspace checkout,
  /Users/astolp/Desktop/Dev/headspace, after git -C /Users/astolp/Desktop/Dev/JuicySortGame fetch origin:
    ./.venv/bin/python -m boardlib.release --project juicysortgame new --version vX.Y.Z --owner AJStolp
  The version is the last release ticket's with the patch number one higher, v0.1.0 if there is none.
  If a live one already names this head, keep it. If one names an older head, mark it
    ./.venv/bin/python -m boardlib.release --project juicysortgame state <id> superseded --by lead --why "dev moved to <sha>"
  and build a new one. Never set a release to approved or success: success comes after Anthony merges
  (AGENTS.md, Release).
Return the PR URL and the release ticket id.`, { label: 'promotion PR', phase: 'Promote' })
}

return { order: plan.order.map(t => t.id), skipped: plan.skipped, results, promotion }
