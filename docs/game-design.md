# Heist game design

Last updated 2026-11-12. How the heist fits together beyond the core sort. For what is built today, see `heist-alley-status.md`.

## The overall objective

Every heist is one vault, and the goal is to crack it. The vault door is the progress meter: no bars or numbers, the door itself changes.

| Stage | What happens on the door |
|---|---|
| 1 to 4 | A corner bolt slowly unscrews, then pops out |
| 5 | The outer ring turns and clunks into place |
| 6 | The inner ring turns the other way |
| 7 | The wheel spins and the keyhole flashes green |
| Payoff | The door swings open, light floods the alley, the raccoon celebrates |

The vault is a payoff, not a puzzle. Players never work out a code.

**Pacing** (backed by `research-retention-and-pacing.md`):

- **The first vault cracks in one sitting.** Most players who leave do it on day one, so they need to see the door open before then.
- **Vaults grow from there,** reaching 3 to 5 days of casual play by about the third heist.
- **A stage is 3 to 5 minutes,** the length of a typical mobile session, and progress inside a stage is saved, so closing the app never costs anything.

## The loop

1. **Second to second, sorting.** Swipe items into chests, keep heat down. Unchanged from today.
2. **Each vat fill turns the current vault part a little.** Steady, earned progress the player always sees.
3. **Each finished leg earns a Plinko round.** The streak during the leg sets how many coins the player drops. Plinko gives the big jumps.
4. **A full stage pops or locks into place for good.**
5. **A cracked vault leads to the next heist.**

## Progress and regression

The balance casinos strike: progress feels earned, losses feel real, but nobody loses everything.

- **Getting busted** turns the current part backward, shown on the door: bolts screw back in, rings grind back. A bust takes back only part of the stage, never all of it.
- **Finished stages are kept.** A popped bolt stays popped. A bust only undoes progress on the stage in play.
- **Post bail** is offered while the door is grinding back, to stop the loss. This is the main paid moment. It must never feel required to progress, and any countdown on it must be real: the Dutch consumer authority fined Epic over fake timers in 2024.

## Plinko

- **Earned only by playing**, never bought. One round per finished leg; coins equal to how well the player sorted that leg.
- **Slots:** cash in most, gear in a few, and one narrow vault slot in the middle that turns the current part a big step. The pins beside the vault slot are where near misses happen.
- **Honest physics.** Near misses come from real bounces, never scripted. Letting the player choose where each coin drops makes near misses feel like "almost had it" rather than "rigged".
- **Never sell coins, re drops or anything that buys entry**, even indirectly. That is the line that keeps Plinko outside every loot box rule. Apple may still rate it 13+ as simulated gambling.
- **Where:** a worn pachinko style board slides out of the alley wall next to the vault, and the raccoon flicks coins into it. It stays in the world, not on a separate screen.

## Items in the sort

| Kind | Items | Effect |
|---|---|---|
| Core | Gems by color | Sort to the matching chest |
| Help | Gold bars, magnet, disguise | Bonus cash, pull matching gems, hide the next mistake |
| Hurt | Dynamite, fake gems, dye packs, cop badge | Swipe up to pocket it as gear (sorting it into a chest raises heat), painted glass that reveals itself on landing, stains a chest, spikes heat |
| Words | Letter tiles spelling HEIST, VAULT, ESCAPE | Bonus cash today; may become clues for the story |

## Gear

Won in Plinko, bought, or for dynamite, pocketed from the sort. Spent during the sort.

- **Dynamite:** clears a whole chute. Every falling stick swiped up goes in the player's pocket, so a hazard dodged becomes gear. The pocket has a cap, so free dynamite does not undercut selling it.
- **Smoke bomb:** slows falling items
- **Bribe:** knocks heat down
- **Drill:** turns the current vault part. Earned only, never sold.

## Money

- Three things players hold: **cash** (score and spending), **gear** (boosters) and **clues** (story progress, if we keep them).
- Whatever is sold for real money gets its own name. Not "gems", since gems are what players sort.
- Sell gear and post bail. Never sell Plinko rounds or anything with a random outcome: Apple and Google require odds disclosure for paid random rewards, and Belgium treats them as gambling.
- Never sell a way around the hurt items. It reads as rigged.
- **Watch the EU Digital Fairness Act.** Proposed 2026-11-11, not law. It would ban selling "pay to progress" to minors. A bought drill would turn the vault directly, which is exactly that, so the drill is earned only: from Plinko, never the store.

## Saving

Nothing is saved today. A multi day vault needs saved progress (stage, progress within it, gear, cash) before it can be tested on a phone. This is part of the next build.

## Ideas on hold

- **Sewer surge:** for one leg, loot rises up out of the manhole instead of falling, and swipe down trashes. A special event, never the default, because rising items fight the swipe up to trash. Build as a toggle and try it before deciding.
- **Crime board:** clue drops pin onto a board with photos and red string, telling the story of each heist. Waiting on the story work.
- **Leaderboards:** weekly races in small groups of similar players, scored on something every theme shares (cash banked or stages cleared that week), so new themes do not split the boards. Needs player accounts, through Game Center and Google Play Games or our own server. After the core loop is fun.
- **More bonus rounds:** a safe dial or a slot machine with tumblers, after Plinko proves out.

## Next to build

The 3D Plinko and the vault progress animations, together, since they are one feature: coins land, the vault reacts. Plan first.

## TLDR

Each heist is cracking one vault, and the door shows progress: four bolts pop, two rings turn, the wheel spins, the door opens. The first vault opens in one sitting, growing to 3 to 5 days by the third heist, with 3 to 5 minute stages and saved progress. Sorting turns parts steadily, Plinko (earned per leg, never bought) gives big jumps through a narrow vault slot, and busts grind the current part back while finished stages are kept. Money comes from gear and post bail, never from random rewards. Next: plan the 3D Plinko and vault animations.
