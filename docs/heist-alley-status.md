# Heist alley: where the game stands

Last updated 2026-11-11, branch `heist-vault-door`.

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

## How the alley reacts

- The raccoon stands centered on the manhole in his torch idle. He does a light push on sorts and a heavy push on vat fills.
- The chests glow brighter as their vat fills, flash orange on a mistake, and snap their lid open as an item drops in.
- The police strobe and the vault alarms run faster and brighter with heat. A bust shakes the camera.
- Lamps flicker, the wall lamps hum, steam rises from the manhole, and clouds drift across the moon.

## Where things live

| What | Where |
|---|---|
| Sort rules, no presentation | `Assets/Scripts/Core/HeistSortRules.cs` |
| Rule tests (15) | `Assets/Editor/Tests/HeistSortRulesTests.cs` |
| Falling items, swipes, chests, reactions, score | `Assets/Scripts/Alley/` |
| Keeping the torch upright and the raccoon on his spot | `TorchGrip.cs`, `StandOnSpot.cs` in `Assets/Scripts/Alley/` |
| Scene builder (layout, lighting, gameplay wiring) | `Assets/Editor/AlleySceneBuilder.cs` |
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
| `/tmp/juicy-alley-bot-run` | plays 60 seconds on auto play, captures frames at 8, 25 and 50 seconds | `/tmp/juicy-bot.txt`, `/tmp/juicy-bot-0..2.png` |

Unity only picks up script changes when its window has focus and refreshes (Cmd+R).

## Open for next session

- **Gameplay:** how the loop builds beyond the core slice. Towers, missiles, the Plinko bonus round and the lockdown escape existed in the 2D version and need 3D designs. The win condition and level pacing are not defined in the alley yet.
- **Story:** who the raccoon is, why this vault, and how the heist unfolds across legs.
- **Character animation:** trigger points for the raccoon. He only has torch idle, push light, push heavy, climb and climb down. Walking down the alley needs a walk clip. In some poses his tail tip dips into the manhole cover.
- **Themes:** the old theme system was 2D sprites and is retired. Swapping heist for the hill climber needs a 3D theme design.
- **Phone check:** nothing has been run on a device yet. Bloom, fog, transparent clouds and steam all cost frame time on mobile.

## TLDR

The alley is the game: items fall from chutes that open one per leg, swipes sort them into two deposit chests, cash rolls up on the vault timer, and the raccoon, lights and chests react. Rules live in a tested core with no UI, the alley front end sits on top, and the road, sky and chests are built from code in `ArtSource/Alley/`. Next session: gameplay beyond the core loop, the story, and the raccoon's animations and trigger points.
