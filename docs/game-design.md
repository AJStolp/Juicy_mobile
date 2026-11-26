# Heist game design

Last updated 2026-11-25. How the heist fits together beyond the core sort. For what is built today, see `heist-alley-status.md`.

## The world map

Decided 2026-11-25, with the open questions tracked on the board as T-026. This is the shape the game is moving to. The sections below it that it changes say so.

- **Places on a map.** The game is a map of places: the city first, then places like a beach or a volcano. Each place is 6 levels and then a harder boss level, and a place's levels share one theme.
- **Every level builds up to the boss.** Winning a level earns one clue about that place's vault. How well the level was played sets how clearly the clue shows (the streak rule, under Clue hack).
- **The player reveals the clue at the end of the level** with a small hands on mechanic that changes from level to level: rubbing grime off a wall, shining a torch, a scratch off, tapping to break rocks. This replaces the clue trip after every leg.
- **Clues go on the crime board,** with photos and red string, telling the story of the heist.
- **An attempt takes 3 to 5 minutes.** Harder levels can take hours or days to beat, over many attempts, as in Candy Crush.
- **Failing a level costs only that attempt.** Never an earned clue, never map progress.
- **Why:** the alley carries every mechanic at once today. On a map, mechanics can arrive one level at a time, and the boss brings them together.

**The feel to aim for: winning all the time, but only just.**

- Constant small wins: every sort pays, with sound, cash and juice.
- Levels tuned so a good player wins with little to spare.
- Honest near misses: a fail shows how close it was ("2 gems short"). A near miss pushes hardest to try again when the player was in control (`research-retention-and-pacing.md`), and a skill level is that case.
- Never rig a level to land just short, and never sell the way out at that moment.

**Still open** (on T-026):

- What wins or fails a level: the goal and the limit.
- Which mechanic arrives at which level, and which stay out of the city.
- The boss. Assumed to be the vault, with the door stages and the clue hack as its finale.
- The pacing of a place, in place of the multi day vault. Assumed: the first city's boss cracks in one sitting.
- Whether the crime board is the map screen itself.
- Why the raccoon travels, and each place's theme.
- Whether attempts are limited (lives).

## The overall objective

**Under review since 2026-11-25:** with the world map, a vault becomes a place's boss level. The door stages below still show progress, but the pacing (a vault grown to days) is open on T-026.

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
3. **Each won level earns a clue.** At the end of the level the player reveals it (rub, torch, scratch off, break rocks), and how well the level went sets how clearly it shows. Until 2026-11-25 this was a clue trip after every leg; the world map replaced it.
4. **The clue hack.** At the vault keypad the player taps the clues in the order they were found. The hack gives the big jumps. Today it runs every 3 legs (4 from heist 2); with the world map it is assumed to become the boss's finale.
5. **A full stage pops or locks into place for good.**
6. **A cracked vault leads to the next heist.**

## Progress and regression

The balance casinos strike: progress feels earned, losses feel real, but nobody loses everything.

- **Getting busted** turns the current part backward, shown on the door: bolts screw back in, rings grind back. A bust takes back only part of the stage, never all of it.
- **Finished stages are kept.** A popped bolt stays popped. A bust only undoes progress on the stage in play.
- **Post bail** is offered while the door is grinding back, to stop the loss. This is the main paid moment. It must never feel required to progress, and any countdown on it must be real: the Dutch consumer authority fined Epic over fake timers in 2024.

## Clue hack

- **Replaces Plinko** (decided 2026-11-18). Plinko's random drop carried the "simulated gambling" age rating risk in `research-retention-and-pacing.md`; a hack won by remembering is skill, not chance.
- **Finding clues:** each won level earns one clue, which the player reveals at the end of the level (see The world map). This replaced the clue trip after every leg on 2026-11-25.
- **The hack:** every 3 legs (4 from heist 2) the raccoon goes to the vault keypad and the player taps the clues in the order they were found. Each correct tap climbs one step of the reward ladder. The first wrong tap ends the hack, and the player keeps every step already reached.
- **The streak rule:** a better streak during a leg shows that leg's clue more clearly. A weak leg makes the hack harder but never costs vault progress. This is how the short term bonuses in Royal Match and Candy Crush work in that research: a weak leg costs bonus, never main progress.
- **The ladder** (a tunable starting point, from the clue rules ticket):

| Clues in the hack | Step 1 | Step 2 | Step 3 | Step 4 | Perfect hack |
|---|---|---|---|---|---|
| 3 | $500 | Bribe | Drill | | Adds 4 vault turns |
| 4 | $500 | $1000 | Bribe | Drill | Adds 4 vault turns |

- **Earned only by playing**, never bought. No clue, retry or extra tap is ever sold.
- **Where clues are kept:** on the crime board (decided 2026-11-25). A notebook in the corner of the screen and the sign over the vault were both rejected on 2026-11-18.

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
- **Wager:** at times a player could stake something on an attempt for a bigger reward, so a fail costs more than the attempt (Anthony, 2026-11-25, "just a thought"; the default stays that a fail costs only the attempt). Staking on your own skill is not a random reward, but it sits close to the gambling line, so if it goes ahead: stake only what was earned in play, never anything bought with money, and never map progress or an earned clue; always optional; never offered as the way out of a fail.
- **Leaderboards:** weekly races in small groups of similar players, scored on something every theme shares (cash banked or stages cleared that week), so new themes do not split the boards. Needs player accounts, through Game Center and Google Play Games or our own server. After the core loop is fun.
- **More bonus rounds:** a safe dial or a slot machine with tumblers, after the clue hack proves out.

## Next to build

Settle T-026's open questions, starting with what wins or fails a level, then prototype one city level ending in a clue reveal. Building more on the clue trip after every leg (T-020) is on hold until then.

## TLDR

The game is moving to a world map: places like the city, a beach or a volcano, each 6 levels and then a harder boss level. Winning a level earns a clue about the vault, revealed by the player at the end of the level and kept on the crime board; failing costs only that attempt. Attempts take 3 to 5 minutes, and the aim is winning all the time but only just, with honest near misses and nothing rigged. What wins a level, the boss, and the pacing of a place are open on T-026. The sort has two chests, and letters go up into the raccoon's sack rather than a third chest. The vault door shows progress: four bolts pop, two rings turn, the wheel spins, the door opens; its multi day pacing is under review. In the clue hack the player taps the clues back in order at the vault keypad, climbing a reward ladder; it is earned and never bought, and is skill rather than chance. Busts grind the current part back while finished stages are kept. Money comes from gear and post bail, never from random rewards. A wager on an attempt is an idea on hold, with limits. Next: settle T-026's open questions, then prototype one city level ending in a clue reveal.
