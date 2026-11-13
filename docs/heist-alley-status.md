# Heist alley: where the game stands

Last updated 2026-11-12, branch `heist-vault-progress`. The design behind it is in `game-design.md`.

The game is now one 3D scene, `Assets/Scenes/DiegeticAlley.unity`. The old 2D SampleScene is retired; commit `fa26667e` holds everything it used, including art that was never committed before that.

## The vision

A diegetic color sort game with casino style juice: everything happens inside the 3D world, not on flat UI. Heist is the first theme. A hill climber theme in the spirit of a game show comes next, and later themes swap in for holidays, sports and so on, on the same sort core. The raccoon is the robber and the star; gameplay events drive his animations, the vault and the lights.

## How it plays today

- Items fall from invisible chutes above the street. One chute opens at the start and one more each leg, up to four. A leg is four vat fills.
- Swipe left or right to send an item into that side's deposit chest (crimson left, indigo right). Swipe up to trash it.
- Items are gems (one diamond model, tinted per color), dynamite bombs, cash stacks and letter tiles that spell HEIST, VAULT and ESCAPE.
- A correct sort pays 50 times the streak tier (x2 at 5, x3 at 10, x5 at 15). Gold pays 150. HEIST pays 25,000; the other words pay 2,500.
- Mistakes raise heat. At 100 heat you are busted: the stage loot is lost, the screen clears, and you stay on the same leg.
- The balance rolls up on the vault timer's screen.
- **The vault is the objective.** Every vat fill turns the vault door one step. It has seven stages: four corner bolts, the outer ring, the inner ring and the wheel. A full stage locks for good. A bust takes back half of the stage in play, never a locked one. Locking the wheel cracks the vault, and the next heist starts on a fresh door. Turns per stage are 9, 16, then 28 from the third vault on (`VaultProgress.TurnsByHeist`). Bot runs crack vault 1 in about 12 game minutes.
- **Plinko after every leg.** The sort pauses, a board swings out of the left wall beside the vault, and the camera moves onto it. Coins equal 1 plus the best streak tier reached that leg. Drag along the top rail to aim, let go to drop. Coins are real physics. Slots pay cash, a drill, a bribe, or the narrow middle vault slot, which turns the door 4 steps.
- **Swiping dynamite away pockets it** as gear, up to 3. Gear is only counted so far; nothing spends it yet.
- **Progress saves** between sittings: vault stage and turns, cash and gear (`HeistSave`, in PlayerPrefs). Legs, chutes, heat and streak start fresh each sitting.

## How the alley reacts

- The raccoon stands centered on the manhole in his torch idle. He does a light push on sorts and a heavy push on vat fills, vault stages and the crack.
- The vault door creeps forward as turns come in and grinds back slowly on a bust. Bolts back out and pop off as their stage locks; the rings and wheel turn; the keyhole glows greener as the vault fills. Locks, rollbacks and the crack push the camera toward the door. Cracking it spins the wheel and swings the door open on a flood of light.
- The chests glow brighter as their vat fills, flash orange on a mistake, and snap their lid open as an item drops in.
- The police strobe and the vault alarms run faster and brighter with heat. A bust shakes the camera. `AlleyCameraRig` owns every camera move: shakes, pushes, and the Plinko hold.
- Lamps flicker, the wall lamps hum, steam rises from the manhole, and clouds drift across the moon.

## Where things live

| What | Where |
|---|---|
| Sort rules, no presentation | `Assets/Scripts/Core/HeistSortRules.cs` |
| Vault stages, Plinko payouts, the save | `VaultProgress.cs`, `PlinkoPayout.cs`, `HeistSave.cs` in `Assets/Scripts/Core/` |
| Rule tests (31) | `Assets/Editor/Tests/` |
| Vault door, Plinko round, camera moves | `AlleyVaultDoor.cs`, `AlleyPlinko.cs`, `AlleyCameraRig.cs` in `Assets/Scripts/Alley/` |
| Falling items, swipes, chests, reactions, score | `Assets/Scripts/Alley/` |
| Keeping the torch upright and the raccoon on his spot | `TorchGrip.cs`, `StandOnSpot.cs` in `Assets/Scripts/Alley/` |
| Scene builder (layout, lighting, gameplay wiring) | `Assets/Editor/AlleySceneBuilder.cs`; the vault door and Plinko board in `AlleySceneBuilder.Heist.cs` |
| Art built from code | `ArtSource/Alley/`: `build_deposit_chests.py`, `build_alley_road.py`, `build_night_sky.py`, `build_vault_door.py`, `build_plinko_board.py` |
| Turning a Tripo or fal.ai GLB into an alley asset | `ArtSource/Alley/import_glb.py` |

The art scripts run headless in Blender, for example `blender -b -P ArtSource/Alley/build_alley_road.py`. `import_glb.py` takes `--cut` to strip stray geometry and `--untextured` for models colored in game.

## Running checks with Unity open

The editor watches for flag files, so tools can drive it without a second Unity instance:

| Touch this file | It does | Result in |
|---|---|---|
| `/tmp/juicy-run-tests` | runs the EditMode tests | `/tmp/juicy-tests.txt` |
| `/tmp/juicy-alley-layout` | rebuilds the layout, lighting and gameplay, then renders | `/tmp/juicy-alley-light.png` |
| `/tmp/juicy-alley-wire` | rewires gameplay only | `/tmp/juicy-alley-wire.txt` |
| `/tmp/juicy-alley-bot-run` | plays on auto play at 4x until vault 1 cracks, with a sloppy bot so busts happen; logs stage times, rollbacks and Plinko rounds | `/tmp/juicy-bot.txt`, `/tmp/juicy-bot-progress.txt`, `/tmp/juicy-bot-{street,bolt,plinko,crack}.png` |
| `/tmp/juicy-save-check` | plants a save, enters Play, checks it came back, restores the real save | `/tmp/juicy-save-check.txt` |

Unity only picks up script changes when its window has focus and refreshes (Cmd+R). With the screen locked, Play mode in the open editor barely advances, so a bot run stalls.

## Running checks with Unity closed

`AlleyBatch` runs the same tools headless; batch mode still renders.

```
U=/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -projectPath . -executeMethod AlleyBatch.Wire -quit
$U -batchmode -projectPath . -executeMethod AlleyBatch.Previews -quit   # both door sizes, door close up, Plinko open
$U -batchmode -projectPath . -executeMethod AlleyBatch.Bot              # no -quit; exits when the report is written
$U -batchmode -projectPath . -executeMethod AlleyBatch.SaveCheck        # no -quit
$U -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <file>
```

## Open for next session

- **Gameplay:** spending gear during the sort (dynamite clears a chute, smoke slows items, bribe cuts heat, drill turns the vault), post bail during the rollback grind, and the store. Towers, missiles and the lockdown escape from the 2D version are not in the design.
- **Vault door size:** two sizes are switchable under Juicy > Alley > Vault: set into the painted doorway (the default) or a large door mounted over the facade, which reads better on a phone but covers graffiti. Not decided.
- **Plinko placement:** a rusty bracket on the left wall overlaps the board's bottom left corner in the Plinko view.
- **Story:** who the raccoon is, why this vault, and how the heist unfolds across legs.
- **Character animation:** trigger points for the raccoon. He only has torch idle, push light, push heavy, climb and climb down. The vault crack needs a celebration clip; it uses the heavy push for now. Walking down the alley needs a walk clip. In some poses his tail tip dips into the manhole cover.
- **Themes:** the old theme system was 2D sprites and is retired. Swapping heist for the hill climber needs a 3D theme design.
- **Phone check:** nothing has been run on a device yet. Bloom, fog, transparent clouds and steam all cost frame time on mobile.

## TLDR

The alley is the game: items fall from chutes that open one per leg, swipes sort them into two deposit chests, and every vat fill turns the vault door, which is the progress meter. Bolts pop, rings and wheel turn, busts grind the current stage back, and cracking it opens the door. Every leg ends in a physics Plinko round on a board that swings out of the wall beside the vault. Dynamite swiped away is pocketed, and progress saves between sittings. Rules live in a tested core (31 tests), and the alley art is built from code in `ArtSource/Alley/`. Checks run through flag files with Unity open or `AlleyBatch` with it closed. Next: spending gear, post bail, picking a door size, and a celebration clip for the raccoon.
