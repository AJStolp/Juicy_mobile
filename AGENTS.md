# JuicySortGame

A Unity 6 (6000.3.10f1) mobile game. The current state of the game and how its tools run are in
`docs/heist-alley-status.md`; the design is in `docs/game-design.md`.

## Workflow

The branch model, the ticket lifecycle, who may close, and the triage vocabulary are defined
once in headspace `WORKFLOW.md` (read it on the board: `GET /api/documents/headspace/workflow`).
This file carries only what is specific to this repo: the commit gate, how proof is captured,
the release path, and the tools.

- **Board:** project `juicysortgame` at `http://100.76.63.94:8000` (the `headspace-board`
  machine on the tailnet, no login). API description at `/openapi.json`.
- **No factory.** Anthony queues a batch of tickets and a lead agent works it in a session with
  a workflow, release included (headspace `projects.yml`, T-418).
- **Branches:** a ticket branch comes off `dev` and carries the ticket id in its name
  (`T-001-plinko-physics`). Its PR squash merges into `dev` with a `Refs T-001` trailer, and the
  ticket moves to `done`. The promotion PR from `dev` into `main` merges with a merge commit and
  carries `Closes T-001` trailers. After it merges, the tickets it carries move to `closed`.
- **Release:** there is no production build yet, so `main` is the end of the line. The lead
  opens the promotion PR and **does not merge it until Anthony has seen the evidence and said
  go**, because this is design work.
- **Design work goes through prototypes.** When a ticket asks how something should look or
  feel, build switchable options in the running game, capture each, recommend one, and wait for
  Anthony's pick at `ready: human`. After the pick, keep the chosen option and remove the rest.

## Ticket shape

Every ticket an agent works has four sections, and is finished against them:

1. **Engineer notes:** cause, files, constraints.
2. **Checklist:** the steps that finish it.
3. **Expected outcome:** what a player sees, written before any code changes.
4. **Proven by:** the exact photo, video or number that shows it works, failure path included.

## The commit gate

Both must pass before a ticket branch is pushed:

```
U=/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -projectPath . -runTests -testPlatform EditMode -testResults /tmp/juicy-tests.xml -logFile /tmp/juicy-tests.log
```

A run that compiles and passes every EditMode test exits 0. A compile error or a failed test
exits non-zero. Read the failures from the XML, not from the log.

**One Unity at a time.** Batch Unity cannot open the project while the editor has it open, and
heavy runs (tests, bot runs, simulations, Blender renders) heat the laptop. The editor stays
closed while agents work, and no two Unity processes run at once.

## Proof

A passing suite shows nothing else broke. It does not show the change works. Each ticket also
carries its own proof, uploaded to the ticket as a file:

```
curl -s -X POST http://100.76.63.94:8000/api/projects/juicysortgame/tickets/T-001/evidence/upload \
  -F "file=@/tmp/juicy-plinko-after.mp4" -F "detail=200 coin run after the fix" -F "passed=true"
```

`passed` comes from an exit code or a measured number against the ticket's target, never from
a judgment that it looks fine.

- **Stills:** `AlleySceneBuilder.Capture(path)` renders the main camera at 1080x1920. The
  `AlleyBatch` methods use it: `Previews` (vault door sizes, door close up, Plinko open) and
  `WordSignPreviews`. Add a method there for a new view rather than a one-off script.
- **Motion** (a coin drop, a gem shattering): capture a frame sequence with `Capture` in Play
  mode, then `ffmpeg -framerate 30 -i frame_%03d.png -pix_fmt yuv420p out.mp4`.
- **Numbers:** `AlleyBatch.PlinkoSim` writes slot counts to `/tmp/juicy-plinko-sim.txt`,
  `AlleyBatch.Bot` writes a run report to `/tmp/juicy-bot.txt`, and `AlleyBatch.SaveCheck`
  writes to `/tmp/juicy-save-check.txt`.

Run them with Unity closed:

```
$U -batchmode -projectPath . -executeMethod AlleyBatch.Previews -quit
$U -batchmode -projectPath . -executeMethod AlleyBatch.PlinkoSim    # no -quit; exits when the report is written
$U -batchmode -projectPath . -executeMethod AlleyBatch.Bot          # no -quit
```

## Do not commit

Unity rewrites material and project settings files on its own. A ticket commits only the files
its change needs; stray `.mat` or `ProjectSettings` changes stay out unless the ticket is about
them.

## TLDR

Tickets live on the board (`juicysortgame`) and follow headspace `WORKFLOW.md`. Branch off
`dev` with the ticket id in the name, pass the gate (batch Unity compiles and every EditMode test
passes), and upload real photo, video or number proof to the ticket. Squash merge into `dev`
for done. The promotion into `main` (closed) waits for Anthony to see the work. Design
questions get switchable prototypes and wait for his pick. Only one Unity runs at a time, with
the editor closed.
