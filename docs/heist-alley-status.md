# Heist alley: where the game stands

Last updated 2026-11-26, branch `T-028-level-goals`. The design behind it is in `game-design.md`.

The game is now one 3D scene, `Assets/Scenes/DiegeticAlley.unity`. The old 2D SampleScene is retired; commit `fa26667e` holds everything it used, including art that was never committed before that.

## The vision

A diegetic color sort game with casino style juice: everything happens inside the 3D world, not on flat UI. Heist is the first theme. A hill climber theme in the spirit of a game show comes next, and later themes swap in for holidays, sports and so on, on the same sort core. The raccoon is the robber and the star; gameplay events drive his animations, the vault and the lights.

## How it plays today

- **The alley plays the city's six levels** (T-028, goal C picked by Anthony 2026-11-26). Each level's numbers live in `Assets/Levels/City.asset` (`PlaceData`, a list of `LevelSpec`, T-032): the target, the misses to spare, the seed, how many dynamite and cash stacks, how often something drops, how fast it falls, the most chutes that open, and a one line hint. A level drops enough gems for its target plus its misses, mixed with its dynamite and cash stacks, all in the same order (the first 4 drops are always gems, so the hint is read first) on every attempt (`LevelRules`, seeded), so a fail is the player's doing.
- **New things arrive one level at a time** (T-033): City 1 and 2 are gems only, City 2 faster. City 3 brings dynamite, City 4 cash stacks, and City 5 and 6 mix both at higher speed with fewer misses to spare. A level that brings something new names it in its hint.
- Items fall from invisible chutes above the street. One chute opens at the start and one more each leg, up to four. A leg is four vat fills.
- Swipe left or right to send an item into that side's deposit chest (crimson left, indigo right), or up to flick it away.
  - **Gems** go in their color's chest. A wrong chest, a flick up or a miss loses the gem.
  - **Dynamite** must be flicked up, which pockets it. Letting it land, or sorting it into a chest, costs one miss, and it goes off with a shake.
  - **Cash stacks** count as two gems in either chest, or flicked up. A missed cash stack costs nothing. (In code the item is still `ItemKind.Gold`.)
- **Misses left** is how many more gems can be lost with the target still in reach. A lost gem or a dynamite mistake takes one; a cash stack gives two back. The level is won at the target and failed the moment misses left would go below zero. Cash still to fall is a bonus and is not counted ahead, so the counter on screen is what decides a fail.
- The top of the screen shows gems sorted against the target and the misses left, which turn red at 3. Each attempt opens on the level's name and hint for 3 seconds. When an attempt ends, a panel shows LEVEL WON with the misses to spare, or TOO MANY MISSES with how many gems short. Tap anywhere to try again. A fail costs only the attempt: cash and gear carry over.
- A correct sort pays 50 times the streak tier (x2 at 5, x3 at 10, x5 at 15). A cash stack pays 150. The balance rolls up on the vault timer's screen.
- **Held back for the boss (T-026, T-037):** letter tiles do not fall; heat is off, so there are no busts; the vault door does not turn; the HEIST word sign is hidden; and the clue hack never comes due. Their code is still in place.
- **Progress saves** between sittings: cash and gear (`HeistSave`, in PlayerPrefs).

## How the alley reacts

- The raccoon stands centered on the manhole in his torch idle. He does a light push on sorts and a heavy push on vat fills, vault stages and the crack.
- The vault door creeps forward as turns come in and grinds back slowly on a bust. The bars and handle slide as their stage fills, the gear lock turns, and the wheel turns a quarter per stage. The HEIST word sign sits centered above the door frame, clear of the door. Locks, rollbacks and the crack push the camera toward the door. Cracking it spins the wheel and swings the door open on a flood of light.
- The chests glow brighter as their vat fills, flash orange on a mistake, and snap their lid open as an item drops in.
- The police strobe and the vault alarms run faster and brighter with heat. A bust shakes the camera. `AlleyCameraRig` owns every camera move: shakes, pushes, and holds on a pose.
- Lamps flicker, the wall lamps hum, steam rises from the manhole, and clouds drift across the moon.

## Where things live

| What | Where |
|---|---|
| Sort rules, no presentation | `Assets/Scripts/Core/HeistSortRules.cs` |
| Vault stages, clue hack rules, gear, the save | `VaultProgress.cs`, `ClueRules.cs`, `GearKind.cs`, `HeistSave.cs` in `Assets/Scripts/Core/` |
| Rule tests | `Assets/Editor/Tests/` |
| Vault door, camera moves | `AlleyVaultDoor.cs`, `AlleyCameraRig.cs` in `Assets/Scripts/Alley/` |
| Falling items, swipes, chests, reactions, score | `Assets/Scripts/Alley/` |
| Keeping the torch upright and the raccoon on his spot | `TorchGrip.cs`, `StandOnSpot.cs` in `Assets/Scripts/Alley/` |
| Scene builder (layout, lighting, gameplay wiring) | `Assets/Editor/AlleySceneBuilder.cs`; the vault door, word sign and hack keypad in `AlleySceneBuilder.Heist.cs` |
| Vault door parts | `ArtSource/Alley/import_glb.py --vault-parts` (with `facade_vault_parts.py`) splits the facade model's own door into moving parts |
| Art built from code | `ArtSource/Alley/`: `build_deposit_chests.py`, `build_alley_road.py`, `build_night_sky.py` |
| Turning a Tripo or fal.ai GLB into an alley asset | `ArtSource/Alley/import_glb.py` |

The art scripts run headless in Blender, for example `blender -b -P ArtSource/Alley/build_alley_road.py`. `import_glb.py` takes `--cut` to strip stray geometry and `--untextured` for models colored in game.

## Running checks with Unity open

The editor watches for flag files, so tools can drive it without a second Unity instance:

| Touch this file | It does | Result in |
|---|---|---|
| `/tmp/juicy-run-tests` | runs the EditMode tests | `/tmp/juicy-tests.txt` |
| `/tmp/juicy-alley-layout` | rebuilds the layout, lighting and gameplay, then renders | `/tmp/juicy-alley-light.png` |
| `/tmp/juicy-alley-wire` | rewires gameplay only | `/tmp/juicy-alley-wire.txt` |
| `/tmp/juicy-alley-bot-run` | plays every level of the city on auto play, paced like a person (a 0.45 s reaction and 0.3 s between swipes): 4 attempts per level by a careful bot (10% mistakes) and 4 by a sloppy one (30%) at 4x, logging each result, gems short or misses to spare, cash caught, and length; then films one won and one failed attempt of City 4 at 1x, with stills of its opening banner, the first dynamite and the first cash | `/tmp/juicy-bot.txt`, `/tmp/juicy-bot-progress.txt`, `/tmp/juicy-level-film/{won,failed}/frame_*.jpg`, `/tmp/juicy-level-{home,banner,dynamite,cash}.png` |
| `/tmp/juicy-save-check` | plants a save, enters Play, checks it came back, restores the real save | `/tmp/juicy-save-check.txt` |

Unity only picks up script changes when its window has focus and refreshes (Cmd+R). With the screen locked, Play mode in the open editor barely advances, so a bot run stalls.

## Running checks with Unity closed

`AlleyBatch` runs the same tools headless; batch mode still renders.

```
U=/Applications/Unity/Hub/Editor/6000.3.10f1/Unity.app/Contents/MacOS/Unity
$U -batchmode -projectPath . -executeMethod AlleyBatch.Wire -quit
$U -batchmode -projectPath . -executeMethod AlleyBatch.Previews -quit   # street with the vault door, door close up
$U -batchmode -projectPath . -executeMethod AlleyBatch.Bot              # no -quit; exits when the report is written
$U -batchmode -projectPath . -executeMethod AlleyBatch.BotFilm          # no -quit; only the two films, no numbers
$U -batchmode -projectPath . -executeMethod AlleyBatch.SaveCheck        # no -quit
$U -batchmode -projectPath . -runTests -testPlatform EditMode -testResults <file>
```

## Open for next session

Last session ended 2026-11-13.

Open work lives on the board, not here: project `juicysortgame` at `http://100.76.63.94:8000` (the `headspace-board` machine on the tailnet, no login). The API is described at `/openapi.json`. The board says what is up next.

Running tools in the open editor: write an `AlleyBatch` method name to `/tmp/juicy-batch` (for example `Wire`, `MeasurePlayCpu`, `SaveCheck`, `WordSignPreviews`). The runner refreshes scripts first and waits out a compile; the result is in `/tmp/juicy-batch.txt`.

## TLDR

The alley plays the city's six levels, each a set of numbers in one asset, with dynamite to flick up arriving in City 3 and cash stacks worth two in City 4. Everything falls in the same order every attempt, and sorting the target into the two deposit chests wins it. Run out of misses and the panel says how many gems short, with a tap to try again that costs only the attempt. Every correct sort pays cash on the streak. The other items, heat, the vault door and the clue hack are held back for the levels and the boss that bring them back (T-026). The launch plan is on the board under T-030. Rules live in a tested core, and the alley art is built from code in `ArtSource/Alley/`. Checks run through flag files with Unity open or `AlleyBatch` with it closed. Open work is on the board (project `juicysortgame`).
