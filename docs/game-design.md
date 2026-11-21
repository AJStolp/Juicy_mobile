# Heist game design

Last updated 2026-11-18. How the heist fits together beyond the core sort. For what is built today, see `heist-alley-status.md`.

## The overall objective

Every heist is one vault, and the goal is to crack it. The vault door is the progress meter: no bars or numbers, the door itself changes.

| Stage | What happens on the door |
|---|---|
| 1 to 4 | A corner bolt slowly unscrews, then pops out |
| 5 | The outer ring turns and clunks into place |
| 6 | The inner ring turns the other way |
| 7 | The wheel spins and the keyhole flashes green |
| Payoff | The door swings open, light floods the alley, the raccoon celebrates |

The vault is a payoff, not a puzzle. Players never work out a code to open the door; the clue hack at the vault keypad is a bonus round, not a lock.

**Pacing** (backed by `research-retention-and-pacing.md`):

- **The first vault cracks in one sitting.** Most players who leave do it on day one, so they need to see the door open before then.
- **Vaults grow from there,** reaching 3 to 5 days of casual play by about the third heist.
- **A stage is 3 to 5 minutes,** the length of a typical mobile session, and progress inside a stage is saved, so closing the app never costs anything.

## The loop

1. **Second to second, sorting.** Swipe items into the two chests and letters up into the raccoon's sack, keep heat down.
2. **Each vat fill turns the current vault part a little.** Steady, earned progress the player always sees.
3. **Each finished leg uncovers a clue.** The raccoon walks to a spot in the alley, moves the prop there or blasts it with dynamite, and finds one clue symbol. The streak during the leg sets how clearly that clue shows.
4. **Every 3 legs (4 from heist 2), the clue hack.** At the vault keypad the player taps the clues in the order they were found. The hack gives the big jumps.
5. **A full stage pops or locks into place for good.**
6. **A cracked vault leads to the next heist.**

## Progress and regression

The balance casinos strike: progress feels earned, losses feel real, but nobody loses everything.

- **Getting busted** turns the current part backward, shown on the door: bolts screw back in, rings grind back. A bust takes back only part of the stage, never all of it.
- **Finished stages are kept.** A popped bolt stays popped. A bust only undoes progress on the stage in play.
- **Post bail** is offered while the door is grinding back, to stop the loss. This is the main paid moment. It must never feel required to progress, and any countdown on it must be real: the Dutch consumer authority fined Epic over fake timers in 2024.

## Clue hack

- **Replaces Plinko** (decided 2026-11-18). Plinko's random drop carried the "simulated gambling" age rating risk in `research-retention-and-pacing.md`; a hack won by remembering is skill, not chance.
- **Finding clues:** after each finished leg the raccoon walks to a spot in the alley, moves the prop there or blasts it with dynamite, and uncovers one clue symbol.
- **The hack:** every 3 legs (4 from heist 2) the raccoon goes to the vault keypad and the player taps the clues in the order they were found. Each correct tap climbs one step of the reward ladder. The first wrong tap ends the hack, and the player keeps every step already reached.
- **The streak rule:** a better streak during a leg shows that leg's clue more clearly. A weak leg makes the hack harder but never costs vault progress. This is how the short term bonuses in Royal Match and Candy Crush work in that research: a weak leg costs bonus, never main progress.
- **The ladder** (a tunable starting point, from the clue rules ticket):

| Clues in the hack | Step 1 | Step 2 | Step 3 | Step 4 | Perfect hack |
|---|---|---|---|---|---|
| 3 | $500 | Bribe | Drill | | Adds 4 vault turns |
| 4 | $500 | $1000 | Bribe | Drill | Adds 4 vault turns |

- **Earned only by playing**, never bought. No clue, retry or extra tap is ever sold.
- **Still open:** where the clues are kept for the player to see, a notebook in the corner of the screen or the sign over the vault. Not picked yet.

## Items in the sort

| Kind | Items | Effect |
|---|---|---|
| Core | Gems by color | Sort to the matching chest |
| Help | Gold bars, magnet, disguise | Bonus cash, pull matching gems, hide the next mistake |
| Hurt | Dynamite, fake gems, dye packs, cop badge | Swipe up to pocket it as gear (sorting it into a chest raises heat), painted glass that reveals itself on landing, stains a chest, spikes heat |
| Words | Letter tiles spelling HEIST, VAULT, ESCAPE | Swiped up into the raccoon's sack, never into a chest. Bonus cash today; may become clues for the story |

**Two chests, and the raccoon's sack takes the letters.** Decided 2026-11-18: there is no third chest. The sort has two chests, and letters are swiped up into the raccoon's sack, which acts as the third box. A third chest for letters was considered and rejected. How the sack gesture works in detail, and what else goes into the sack, is still open under the difficulty proposal (gem color as the only real decision in the sort), which has not been approved.

## Gear

Won in the clue hack, bought, or for dynamite, pocketed from the sort. Spent during the sort.

- **Dynamite:** clears a whole chute. Every falling stick swiped up goes in the player's pocket, so a hazard dodged becomes gear. The pocket has a cap, so free dynamite does not undercut selling it.
- **Smoke bomb:** slows falling items
- **Bribe:** knocks heat down
- **Drill:** turns the current vault part. Earned only, never sold.

## Money

- Three things players hold: **cash** (score and spending), **gear** (boosters) and **clues** (story progress, if we keep them).
- Whatever is sold for real money gets its own name. Not "gems", since gems are what players sort.
- Sell gear and post bail. Never sell a clue hack, a clue, or anything with a random outcome: Apple and Google require odds disclosure for paid random rewards, and Belgium treats them as gambling.
- Never sell a way around the hurt items. It reads as rigged.
- **Watch the EU Digital Fairness Act.** Proposed 2026-11-11, not law. It would ban selling "pay to progress" to minors. A bought drill would turn the vault directly, which is exactly that, so the drill is earned only: from the clue hack, never the store.

## Saving

Nothing is saved today. A multi day vault needs saved progress (stage, progress within it, gear, cash) before it can be tested on a phone. This is part of the next build.

## Ideas on hold

- **Sewer surge:** for one leg, loot rises up out of the manhole instead of falling, and swipe down trashes. A special event, never the default, because rising items fight the swipe up to trash. Build as a toggle and try it before deciding.
- **Crime board:** clue drops pin onto a board with photos and red string, telling the story of each heist. Waiting on the story work.
- **Leaderboards:** weekly races in small groups of similar players, scored on something every theme shares (cash banked or stages cleared that week), so new themes do not split the boards. Needs player accounts, through Game Center and Google Play Games or our own server. After the core loop is fun.
- **More bonus rounds:** a safe dial or a slot machine with tumblers, after the clue hack proves out.

## Next to build

The clue hack and the vault progress animations, together, since they are one feature: the player taps the clues at the keypad, the vault reacts. Plan first.

## TLDR

The sort has two chests, and letters go up into the raccoon's sack rather than a third chest. Each heist is cracking one vault, and the door shows progress: four bolts pop, two rings turn, the wheel spins, the door opens. The first vault opens in one sitting, growing to 3 to 5 days by the third heist, with 3 to 5 minute stages and saved progress. Sorting turns parts steadily. Each leg uncovers a clue, and every 3 legs (4 from heist 2) the player taps the clues back in order at the vault keypad, climbing a reward ladder; that clue hack gives the big jumps, is earned and never bought, and is skill rather than chance. Busts grind the current part back while finished stages are kept. Money comes from gear and post bail, never from random rewards. Next: plan the clue hack and vault animations.
