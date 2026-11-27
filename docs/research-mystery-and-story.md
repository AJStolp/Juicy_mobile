# Research: mystery and story, sneaking, and a 3D world map

Researched 2026-11-26 for the mystery and story layer (Anthony: "research game mystery, see what people truly love"), the raccoon sneaking along paths, and the idea of a 3D island to island world map. Every claim has a link. Each finding is labeled by strength:

- **Hard data**: figures a company, platform or regulator published about itself, or counts anyone can check (store ratings, Steam review counts).
- **Vendor data**: estimates from analytics companies (Sensor Tower, AppMagic, AppGoblin, GameRefinery). Usually store purchases only.
- **Developer statement**: what the people who made the game said, in an interview, talk or post. Honest about intent, but rarely backed by numbers.
- **Secondary**: analysts and press (Naavik, Deconstructor of Fun, PocketGamer.biz, Game Developer columns).
- **Player opinion**: store reviews and critic reviews, quoted directly. Shows what people notice, not how many people.
- **Unverified**: anything I could not trace to a source.

Our game, for reference: a real time sort. Gems fall in a 3D alley and the player swipes each one into the matching chest. Levels sit on a crime board map in a case file look (T-048 kept it as the map's only look). Each won level gives a clue, revealed by a small hands on mechanic, pinned to the board with red string, and the clues crack the vault boss of each place (`game-design.md`). The player never controls the raccoon directly; the swipe is on the gems.

## 1. Mobile casual games with a mystery or story wrapper

### Who makes money with it

| Game | Publisher | What you do | Scale | Source and strength |
|---|---|---|---|---|
| June's Journey | Wooga (Playtika) | Hidden object scenes in a 1920s detective story, plus decorating an island | $1B lifetime revenue on 2023-11-15, the first German mobile game to reach it | [PocketGamer.biz, 2023-11-16](http://www.pocketgamer.biz/junes-journeys-1billion-revenue-makes-german-gaming-history/) (company figure, reported). Over 60M downloads by October 2022 ([mobilegamer.biz](https://mobilegamer.biz/junes-journey-hits-60m-downloads-over-500m-gross-lifetime-spend/), source not stated). US weekly revenue peaked near $2M in late May 2025 ([Sensor Tower](https://sensortower.com/blog/2025-q2-unified-top-5-hidden%20objects%20games-revenue-us-6017d22b241bc16eb8f7b5dc), vendor data) |
| Lily's Garden | Tactile Games | Match 3 that pays for renovation tasks, each task an animated story scene | "$500 million success" in the headline | [PocketGamer.biz interview](https://www.pocketgamer.biz/interview/81434/how-tactile-games-made-marketing-and-diversity-core-to-lilys-gardens-500-million-success/) (figure in headline only, not checked in the body) |
| Criminal Case | Pretty Simple | Hidden object crime scenes, lab analysis, suspects, arrest the killer | Facebook Game of the Year 2013; over 100M users by December 2013; 369 cases, the last released 2021-01-21 | [Wikipedia](https://en.wikipedia.org/wiki/Criminal_Case_(video_game)) and [fan wiki](https://criminalcase-facebookgame.fandom.com/wiki/Criminal_Case) (secondary). App Store 4.7 from 36K ratings (hard data) |
| Seekers Notes | Mytona | Hidden object in a cursed Victorian town, with match 3 and mini puzzles | 43M downloads in 195 countries; about $8.7M IAP in one quarter of 2024 | [PocketGamer.biz, 2024-08-07](http://www.pocketgamer.biz/nine-years-of-seekers-notes-and-43-million-downloads-across-195-countries/) (developer statement); revenue is an AppMagic estimate quoted in a [2024 industry report](https://wnhub.io/news/other/item-44329) (vendor data) |
| Mystery Matters | Playrix | Match 3 and hidden object to solve a murder in an inherited manor | About 2M iOS installs, under $10K a month on iOS (estimate); App Store 4.8 from 34K ratings | [AppGoblin](https://appgoblin.info/apps/6450447751) (vendor data, rough); [App Store](https://apps.apple.com/us/app/mystery-matters-adventure-game/id6450447751) (hard data for the rating) |
| Murder by Choice | Nordcurrent | Hidden object crime scenes on a private island, 10 suspects, talk to guests to narrow them down | App Store 4.3 from 25K ratings | [App Store](https://apps.apple.com/us/app/-/id1483785220) (hard data) |
| Royal Match (comparison) | Dream Games | Match 3 with a thin story (a king and his castle) and an area of renovation tasks | $5.8B lifetime revenue | [PocketGamer.biz, November 2025](https://www.pocketgamer.biz/royal-kingdom-surpasses-300m-in-first-year-beating-royal-match) (vendor data via press) |

Two things stand out:

- **The mystery hits are hidden object games for older women.** June's Journey: "Almost half of June's Journey's players are over 60 years old, and 85% of the playerbase is female" ([Naavik, 2022/2023](https://naavik.co/deep-dives/junes-journey-hidden-object-game/), secondary). Criminal Case: "80% of the active players are women aged between 30 and 55" (a 2017 study cited by [Wikipedia](https://en.wikipedia.org/wiki/Criminal_Case_(video_game)), secondary). Seekers Notes says its main audience is "older players" (developer statement, link above). A reflex sort may not draw that audience, so their results do not transfer automatically.
- **The newer mystery match 3 games are small.** Playrix's own Mystery Matters is estimated at under $10K a month on iOS. A murder mystery theme did not lift it the way the garden and home themes lifted Gardenscapes and Homescapes. That is one game and an estimate, so it is a warning, not proof.

### Does story actually help retention?

What I found, strongest first:

1. **Royal Match tested its meta and kept the version with tasks and rewards.** Dream Games ran an A/B/C test against "No Meta" (a level button with the castle as a fixed background), "Episode Meta" (the background changes after set level numbers, no rewards) and "Episode Meta with Rewards." The control, an area with tasks, "turned out to be the right balance" ([Naavik, 2025-02-23](https://naavik.co/digest/royal-match-finding-success-through-iteration/), secondary; no per variant numbers published). The lesson is that changing scenery alone was not enough; spending earned stars on visible tasks was.
2. **King admits players do not remember its story.** King's lead narrative designer at GDC 2022: "I admit player retention of story is not as high as I would like." In King's surveys, two thirds of players found narrative events more engaging and 70% of active and lapsed players wanted a Candy Crush backstory ([PocketGamer.biz, 2022-03-21](http://www.pocketgamer.biz/gdc-how-king-tells-a-story-with-ux/), developer statement with survey figures). So players like having story around, but the plot itself does not stick.
3. **Exposition at the start loses players.** "In multiple games across different genres, we see a big drop in the FTUE (first time user experience) funnel at the point when the story video/or comics strip is displayed" ([Xing Wang, Game Developer, 2014](https://gamedeveloper.com/design/story-in-games-what-we-can-learn-from-other-story-telling-media), practitioner statement, no numbers). Mytona reworked the opening of Seekers Notes, saw retention fall, and "reverted to the old version, which helped restore metrics" (developer statement, link above).
4. **June's Journey has a very flat long tail.** About 16.5% of players remain on Day 30, 15% on Day 60, and the curve drops only 0.75 points more by Day 180 ([Naavik](https://naavik.co/deep-dives/junes-journey-hidden-object-game/), secondary, source of the figures not given). That fits a weekly story chapter that gives players a reason to come back, but it is not proof the story causes it; the game also has decorating, collections and monthly events.
5. **Analytics firms say narrative goes with revenue, without showing the numbers.** GameRefinery: storytelling and dialogue features grew 7% among the top 20% of casual games; two merge games without a running story "have proven unprofitable"; "For storytelling elements to have their maximum impact, they need to be a main event instead of a side feature" ([GameRefinery, 2023-11-30](https://www.gamerefinery.com/game-changing-storytelling-how-to-integrate-narrative-elements-in-mobile-games/), vendor analysis). 42% of the top 20% grossing match 3 games use renovation or construction meta ([GameRefinery via Mobidictum, 2022-10-31](https://mobidictum.com/case-study-why-renovation-favorite-in-casual-games/), vendor data, a correlation).
6. **Developers believe in it.** Wooga: "we strategically prioritised the development of story-driven casual games" and engagement depends on "getting attached to characters" (PocketGamer.biz, link above). Playrix calls narrative "a very important part of our game" ([PocketGamer.biz, 2021-10-28](https://www.pocketgamer.biz/how-gardenscapes-changed-match-three/)). MAG Interactive's Alice Bowman: "If you recreate the feeling of a TV series, books, or soaps people will return because they now feel invested" ([PocketGamer.biz, 2023-07-12](http://www.pocketgamer.biz/mag-interactives-alice-bowman-adding-a-story-narrative-to-your-game-can-add-long-term-player-loyalty/)). All developer statements, none with before and after numbers.

**Proven:** a meta where earned progress buys something visible beats scenery alone (Royal Match test). Long exposition up front costs players. **Not proven anywhere I looked:** that a story wrapper by itself raises retention in a puzzle game. Nobody published an A/B test of story against no story.

### How long story beats are and how often they come

| Game | Cadence | Size of a beat | Source |
|---|---|---|---|
| June's Journey | One new chapter a week since launch (October 2017), each chapter about 5 hidden object scenes and 1 puzzle scene | 6,050 dialogues across 275 chapters, about 22 per chapter (my division) | [Naavik](https://naavik.co/deep-dives/junes-journey-hidden-object-game/) (secondary) |
| Lily's Garden | "one story day per week, normally on Wednesday," like "a weekly TV series" | "generally around 18 tasks," each a fully animated scene. About 260 story days, "five years of content." Needs "a team of about 30 people working on the game full-time" | Tactile game director, [PocketGamer.biz, 2024-10-31](https://www.pocketgamer.biz/narrative-design-in-mobile-games/) (developer statement) |
| Homescapes | Tasks grouped into "days" with a theme; each task costs stars, one star per won level | Players report 1 to 6 stars per task, so a short scene every 1 to 6 levels. One player banks stars "until I have 100 stars and can do a whole day at a time as I find the story easier to follow that way" | [AppGamer guide](https://www.appgamer.com/homescapes/strategy-guide/completing-a-day) and store reviews (secondary, player opinion) |
| Candy Crush Saga Adventures | 60 levels with "four key milestones," each unlocking a new part of the story; "The story unfolds as players complete levels" | Not stated | King engineer, [PocketGamer.biz, 2025-11-13](http://www.pocketgamer.biz/how-saga-adventures-make-candy-crush-a-living-breathing-universe/) (developer statement) |
| Writing guidance | "frequent narrative payoffs for shorter session times and cliffhangers," plus "overarching mysteries" | Bowman: a small character limit; the player should follow the story "whilst being out ordering coffee" | Jam City's VP of narrative, [GDC 2019 talk listing](https://gdconf.com/news/master-nuances-make-mobile-f2p-game-story-great-gdc-2019); Bowman, link above (developer statements) |

So the pattern in the big story games: **a short beat after almost every task or scene (a few lines), a bigger milestone every 15 or so levels, and a weekly chapter**. The weekly chapter is the expensive part (Tactile's 30 people).

### What players love (player opinion, quoted)

- Story wrapped around simple play: "I like that there is a story with the hidden object scenes" (June's Journey, App Store, 2021). "Story, puzzles, decorate, hidden objects and great graphics" (2024). ([reviews](https://apps.apple.com/us/app/junes-journey-hidden-objects/id1200391796?see-all=reviews))
- Characters and travel: "the characters and stories are fun and engaging," "you get to travel all around the world with june as she goes on adventures and solves cases" (June's Journey, 2025).
- The moment of solving: "The feeling you get when you solve a case is so special and fun." "Each case is unique and it's fairly realistic as to what really happens as far as motives" (Criminal Case, 2017, [reviews](https://apps.apple.com/us/app/criminal-case/id767473889?see-all=reviews)).
- A light mystery is enough: "I like the cute little mystery and the decorating part of the game"; "fast paced but cool if you like a simple story that's just there" (Mystery Matters, 2024, [reviews](https://apps.apple.com/us/app/mystery-matters-adventure-game/id6450447751?see-all=reviews)).
- Choice: "I find it awesome that our choices matter in this game" (Murder by Choice, 2023, [reviews](https://apps.apple.com/us/app/murder-by-choice-mystery-game/id1483785220?see-all=reviews)).

### What players complain about (player opinion, quoted)

- **Being locked out of the ending.** "[I] will probably never get to see who done it in the end" (Murder by Choice, 2023, about energy prices). This is the sharpest complaint in the genre: hold the reveal hostage and players resent it.
- **Energy and waiting.** "the 175 energy lasted me about 3-5 minutes of gameplay" (Murder by Choice). "it takes 40 minutes just to give you only one life" and the wait for "lab specimens to come back" (Criminal Case). "I don't like being forced to wait a full week to play the next chapter" (June's Journey, 2022).
- **Story that has nothing to do with the play.** "they have you do some unneeded tasks that have nothing to do with the story"; "You forget where you are in the stoy, what you are trying to earn stars for" (Mystery Matters, 2023 and 2024).
- **Difficulty walls that stall the story.** "I've been stuck on the same level for over a week"; "it becomes nearly impossible to win without you having to spend real money" (Mystery Matters, 2024).
- **Ads that promise a different game.** The UK Advertising Standards Authority ruled Homescapes and Gardenscapes ads misleading on 2020-09-30: they showed rescue puzzles ("Help her escape!") while the games "in large part consisted of gameplay" of renovation and match 3, and the "Not all images represent actual gameplay" note did not fix it ([ASA ruling](https://asa.org.uk/rulings/plr-worldwide-sales-ltd-g20-1061644-plr-worldwide-sales-ltd.html), hard data). Royal Match's "save the king" ads draw the same complaint ([Wikipedia](https://www.Wikipedia.org/wiki/Royal_Match), secondary).

### Collections and case boards

- **June's Journey "Memoirs"** are collectibles in five rarities, sold in card packs; Naavik calls it "the game's gacha mechanic." Duplicates convert to missing pieces; finishing a set adds permanent sights to the island (whales, a sailing boat). A **Photo Album** of character cards unlocks a character backstory ([Naavik](https://naavik.co/deep-dives/junes-journey-hidden-object-game/), secondary). Our own rule bars selling random rewards (`game-design.md`), so the paid pack part is out; the "complete a set, unlock a backstory or a permanent sight" part is not random and fits.
- **Criminal Case** gives stars per case that "can't be used on others," which players dislike (review above).
- **Case boards:** I found no mobile hit whose case board is the main screen. Criminal Case and Murder by Choice show suspects and evidence inside each case; June's Journey shows story through dialogue. A crime board as the level map would be unusual, which is a point of difference, not a known risk.

### What it means for us

- Story helps most as **a reason to spend progress on something you can see**, not as text to read. Our clue reveal and crime board already do this: each won level adds a visible piece.
- Keep beats **short and frequent**: a line or two per won level, a bigger moment at the boss. No story before the first level.
- **Never gate the reveal behind payment or waiting.** The culprit and the cracked vault are what players are owed for playing.
- Tie every beat to the level just played. "Tasks that have nothing to do with the story" is a top complaint.

## 2. Detective and mystery games on PC and console

### How they did

| Game | Sales | Steam English reviews, read 2026-11-26 (hard data, player opinion) |
|---|---|---|
| Return of the Obra Dinn (Lucas Pope, solo) | Estimates range from about 600K to 1.9M on Steam ([Raijin](https://raijin.gg/app/653530/Return_of_the_Obra_Dinn), [Levvvel](https://levvvel.com/return-of-the-obra-dinn-statistics/), vendor data; no official figure) | 96% of 19,009 positive |
| The Case of the Golden Idol (Color Gray Games) | No figure found | 98% of 6,655 |
| Her Story (Sam Barlow) | Over 100,000 App Store downloads, per an interview ([Digitally Downloaded](https://www.digitallydownloaded.net/?p=46601), secondary) | 88% of 5,118 |
| Disco Elysium, The Final Cut | "more than 5 million copies" ([Wikipedia](https://wikipedia.com/wiki/ZA/UM), secondary) | 92% of 58,660 |
| Phoenix Wright: Ace Attorney Trilogy | Over 5 million copies; the series about 15 million by 2026-06-30 ([wnhub](https://wnhub.io/news/finance/item-52132), citing Capcom) | 97% of 10,419 |
| Danganronpa: Trigger Happy Havoc | No figure found | 95% of 18,196 |
| Professor Layton series | Over 20 million units by April 2026 ([Wikipedia](https://en.wikipedia.org/wiki/Professor_Layton), secondary); 15 million in 2013 per Level-5's CEO ([Engadget](https://www.engadget.com/2013/08/27/level-5-layton-series-sells-15-million-ni-no-kuni-1-4-million/)) | Not on Steam |

### What players and critics single out

1. **Doing the deduction yourself.** Kotaku on Obra Dinn: "There is enough information to figure everything out without guessing, but it's _just_ enough information," and the reviewer was "glued to it until I was done" ([Kotaku](https://kotaku.com/return-of-the-obra-dinn-the-kotaku-review-1829797772), critic opinion). Golden Idol was built because of a "lack of deduction games that make the player feel like a detective through gameplay and not just through narrative flavor" ([Game Developer, 2022-11-01](https://www.gamedeveloper.com/design/case-of-the-golden-idol), developer statement).
2. **The "Aha!" moment, and being told you are close.** Golden Idol's designer: "Those precious 'Aha!' moments are something that we strived for." Marking a whole answer only right or wrong "frustrated the players," so they added "two or fewer slots are incorrect," which rewards "the player for being close to the solution." Also: "Given the opportunity, players will optimize the fun out of a game" (same link, developer statement).
3. **Confirmation in batches, so guessing does not pay.** Obra Dinn confirms fates only three at a time (two at a time for the last six) "to deter guesswork" ([Wikipedia](https://en.wikipedia.org/wiki/Return_of_the_Obra_Dinn), secondary). Pope later said many players solved crew identities by guessing and that he made it too hard ([Automaton interview, Japanese](https://automaton-media.com/articles/interviewsjp/20190719-97599/2/), developer statement via summary).
4. **A trial or confrontation as the payoff.** Reviewers treat Danganronpa's class trials as the series' best part: the player fires evidence ("truth bullets") at lies in testimony, with Ace Attorney as the model. Hardcore Gamer: the murders are "expertly designed as always, giving you just enough info to figure out most of the puzzle for yourself before the final pieces come out in trial" ([Hardcore Gamer](https://hardcoregamer.com/reviews/review-danganronpa-v3-killing-harmony/272490/), critic opinion). The common criticism is that trials can drag when you have already worked out the answer. Golden Idol's designer warns against the opposite: "if a player understands the main mystery immediately and then is stuck for thirty minutes on some technicality, that's bad" ([Game Developer, 2023-03-02](https://www.gamedeveloper.com/road-to-igf-2023/-the-case-of-the-golden-idol-i-used-frequent-testing-to-improve-its-mystery-solving)).
5. **Short pieces, assembled out of order.** Her Story is short interview clips found by searching a database; Barlow tested it by cutting real interview transcripts into chunks ([PC Gamer](https://www.pcgamer.com/the-story-behind-her-story), secondary summary).
6. **Puzzles that have nothing to do with the mystery still sell, if the mystery is the reward.** This is the closest model to our game. Layton reviews: "the puzzles usually have nothing to do with the mystery at hand; the story is the reward for solving puzzles"; still scored 8 ([GameRevolution](https://www.gamerevolution.com/review/53159-professor-layton-and-the-last-spectre-review), critic opinion). A Gamasutra column argued the thin link "hurts both portions of the game" ([Game Developer, 2010](https://gamedeveloper.com/game-platforms/opinion-professor-layton-and-the-unrelated-story-), opinion). The series still sold over 20 million.

**What about the red string board?** I found nothing showing players love the board itself. They love the deduction it holds. Golden Idol's "thinking" screen is a fill in the blanks page, not string; its sequel even dropped that screen for pop up windows ([Thinky Games](https://thinkygames.com/features/how-the-case-of-the-golden-idol-developers-made-one-of-the-decades-best-detective-games-twice/), secondary). The red string board works as a recognizable picture of "this is a case," which is how we use it.

### What it means for us

- Our core is a reflex sort, so we cannot be Obra Dinn. **We can be Layton:** the sort is the puzzle, and the mystery is the reward that plays out on the crime board.
- One small deduction moment per place would borrow the best part of these games: at the boss, the player uses the place's clues to point at the culprit. Keep it light, give "you are close" feedback rather than a flat wrong, and never let a wrong pick cost a clue or map progress (our rule). Batch the confirmation, as Obra Dinn does, only if guessing turns out to be a problem.
- A twist works best when it reframes clues the player already pinned up. King reports the same: "twists that reinforce the player's actions" ([Game Developer, 2022-03-21](https://www.gamedeveloper.com/marketing/how-king-used-a-player-centric-approach-to-add-meaningful-narrative-to-candy-crush-saga), developer statement).

## 3. Sneaking and hiding: what makes it charming

### The games

| Game | Results | What makes the sneaking work | Source |
|---|---|---|---|
| Untitled Goose Game (House House, published by Panic) | Over 1 million sold in about 3 months (late 2019), not counting Game Pass ([Game Developer](https://gamedeveloper.com/business/-i-untitled-goose-game-i-has-topped-1-million-sales-in-three-months), company figure reported). Steam 96% of 10,442 | Comedy in the animation: "The humor of Untitled Goose Game is built into the mechanics and animations" ([GameSpot](https://www.gamespot.com/reviews/untitled-goose-game-review-in-a-flap/1900-6417308/), critic). Stealth "not particularly taxing and no fail states" ([Prima](https://primagames.com/gaming/untitled-goose-game-review-pc), critic). The team added "idle loops where people would use their items, reaction animations for state transitions," and the music moves "between playing the high or low energy versions" with the action; the aim was "avoiding artifice" ([Game Developer Q&A, 2019-10-09](https://gamedeveloper.com/design/behind-the-honk-an-i-untitled-goose-game-i-q-a), developer statement). The score is short Debussy clips in a low and a normal energy version ([Game Developer](https://gamedeveloper.com/audio/getting-goosey-with-debussy-creating-an-adaptive-score-for-i-untitled-goose-game-i-)) |
| Hitman GO (Square Enix Montreal) | Debuted in the App Store top 10 paid ([Wikipedia](https://en.wikipedia.org/wiki/Hitman_Go), secondary). Steam 85% of 1,001 | Turn based board game pieces on a diorama: "playing it as a board game makes a lot of sense; it gives you a lot of time to think and plan"; "We created all the nodes and the lines first, and only later did we dress them up" ([Game Developer, 2014-04-28](https://www.gamedeveloper.com/design/for-square-enix-designing-i-hitman-i-as-a-board-game-just-made-sense), developer statement). GDC postmortem: [Design by Constraints](https://gdcvault.com/play/1021908/Design-by-Constraints-Hitman-GO) |
| Lara Croft GO (same studio) | Steam 92% of 1,401 | "She had to be animated. No more board game aesthetic." They "spent a lot of time to make sure the player doesn't need a tutorial" ([GamesBeat, 2015-09-02](https://gamesbeat.com/lara-croft-go-developer-on-turning-an-action-blockbuster-into-a-turn-based-mobile-game/), developer statement) |
| Sneaky Sasquatch (RAC7, two people) | Apple bought the studio in May 2025 "to help Sneaky Sasquatch continue to grow on Apple Arcade" ([MacRumors](https://www.macrumors.com/2025/05/27/apple-acquires-gaming-studio-rac7/), hard data) | Began at a jam themed "You Are the Monster" as "basic stealth mechanics, level-based gameplay, and a list of food the Sasquatch needed to obtain"; they added "anything that made us laugh during development" ([PocketGamer.biz, 2020-04-01](http://www.pocketgamer.biz/indie-spotlight-rac7-games-jesse-ringrose/), developer statement). Critics call it "very chill, very relaxed" ([Destructoid](https://www.destructoid.com/reviews/review-sneaky-sasquatch/), critic) |

### What the charm comes from (developer statements and critic opinion)

- **A small rascal doing something mildly naughty, with low stakes.** Goose: "you're not destroying their lives, just casually ruining a single day" ([Tom's Guide](https://tomsguide.com/reviews/untitled-goose-game)). Sasquatch: stealing campers' food. A raccoon stealing gems fits the same shape exactly.
- **Getting caught is funny, not a fail.** Goose has no fail states; being chased is part of the fun.
- **Reactions.** The people around the goose react; the music reacts. Charm comes from the world noticing the character.
- **Readable at a glance.** Hitman GO and Lara Croft GO show the paths as nodes and lines and need no tutorial.

### What a casual player can watch rather than control

Nothing I found measures whether watching a character raises retention. These are design points drawn from the games above, so treat them as opinion:

- **Travel between levels.** In Hitman GO the piece moves node to node along visible lines. On our crime board, the raccoon could scurry along the red string to the next photo, duck behind a pinned photo when a flashlight sweeps past, then pop out. It plays for a second or two after a win and the player only watches.
- **Reactions during a level.** The raccoon peeks out from behind the chests, flinches when heat rises, hugs a big gem, freezes when a guard's light passes. These are the "reaction animations for state transitions" House House describes.
- **Music that shifts between a quiet sneaking layer and a busier one**, as Goose does, driven by the level's state (heat, streak).
- **Ads must match the game.** If our ads show the raccoon sneaking past guards, the game has to contain it in a real way, or we repeat the Playrix ASA ruling.

## 4. 3D world maps that link levels

### Evidence on retention

- **I found no evidence that a 3D overworld retains better than a flat map** in mobile puzzle games, and no published test of one against the other.
- **The closest test points the other way:** in Royal Match, changing scenery after set level numbers was a tested variant and did not win; the version where earned stars buy visible tasks did ([Naavik](https://naavik.co/digest/royal-match-finding-success-through-iteration/), secondary). What a map looks like mattered less than what progress buys.
- **The biggest map game updated its map for readability, not depth.** King gave the Candy Crush Saga map a visual update in 2022 "to make it clearer to read and easier to navigate," with features showing on the map only when relevant ([PocketGamer.biz, 2026-01-08](http://www.pocketgamer.biz/competitive-or-casual-designing-candy-crush-with-a-spectrum-of-flavours/), developer statement). King's limited time Saga Adventures change the map's look to match the story: "As players progress, the Saga map evolves, new visuals appear, environments shift" (developer statement, linked in section 1).
- **Most mobile maps are 2D paintings.** "most of the games adopt vertical scroll configuration for the map"; "nowadays most game maps are cartoon style digital paintings" and "a few games make maps by 3D render." "in Cookie JAM each area is an independent island" ([Game Developer, 2015](https://www.gamedeveloper.com/design/characteristics-of-a-few-mobile-game-maps-part-i), practitioner statement).
- **Most sort hits have no map at all** and rely on events and albums (`research-color-sort-market.md`).
- Platformer overworlds (Mario, Donkey Kong Country) are loved, but I found no data tying them to retention, and they serve games where each level is a long, distinct place.

### What a 3D overworld costs

- **Art.** "The single most important factor which influent budget, is art style/detail level" and "how dense you would pack the level nodes in the map" (Game Developer, 2015, above). A 3D island per place means modeling, lighting and animating a new environment for every 7 levels, on top of the level's own alley look.
- **Download size.** Google: "For every 6 MB increase to an APK's size, we see a decrease in the install conversion rate of 1%," and in emerging markets cutting 10 MB lifted conversion about 2.5% ([Google Play, 2017](https://medium.com/googleplaydev/shrinking-apks-growing-installs-5d3fcba23ce2), platform data, a correlation from 2017; one developer's own test [did not reproduce it](https://medium.com/@martinmazanec/shrinked-app-size-by-42mb-was-it-worth-it-ac34f76b14c7)).
- **Load time and low end phones.** "New players are especially susceptible to long loading times as they aren't yet invested in the game" ([Google Play, first ten minutes](https://medium.com/googleplaydev/why-the-first-ten-minutes-is-crucial-if-you-want-to-keep-players-coming-back-to-your-mobile-game-4a89031b6308), platform statement, no figure). A second 3D scene between every level adds a load and GPU work on cheap Android phones.
- **Time not spent elsewhere.** For a solo developer, weeks on an overworld are weeks not spent on levels, the fail offer, events and analytics, which the market research shows drive results.
- **Readability.** A 3D camera that orbits islands is harder to read at a glance than a board where the next level is obvious; King's 2022 change went toward clarity.

### What it means for us

- **Not for soft launch.** No evidence of a retention gain, clear costs in art, size and loading, and the crime board is already the chosen map look (T-048).
- **A cheaper middle ground keeps the feel Anthony likes:** a Hitman GO style diorama. Each place is one small 3D board (a rooftop, a pier, a museum wing) seen from a fixed angle, with the levels as nodes and the raccoon sneaking along the path between them. That is one scene per place, not a free roaming world, and it can sit behind the crime board as the picture pinned to it.
- **Revisit after soft launch** if Day 7 lands in the puzzle range (11 to 14%, per `research-color-sort-market.md`) and players finish the first place. That is when a richer world rewards players who already stay.

## 5. Recommendations for the soft launch

### Build first

1. **One mystery per place, ending in a reveal at the boss.** Each place has a small cast on the crime board (three or four suspect photos) and a question (who tipped off the guards, who has the vault code). Each won level's clue rules a suspect in or out on the board. The boss is the reveal: the strings connect, a photo flips, the vault opens. Why: "the feeling you get when you solve a case" is the most praised moment in the mobile mystery reviews, and a reveal paid for by earned clues is the Layton model.
2. **A light accusation at the boss, as a test.** Before the vault, the player taps the suspect the clues point to. A wrong pick shows "close, look at the clue from level 4" and lets them try again, costing nothing. Why: the deduction moment is what players single out in every detective hit, and Golden Idol shows "you are close" feedback beats a flat wrong. Keep it small; this is a sort game, and the clue hack is still the vault finale.
3. **Short, frequent story beats tied to the level just played.** One or two lines with the clue reveal after each win (under about 10 seconds, always skippable), and a slightly longer beat at the start of a place and at the boss. No story before the first level. Why: the big story games put a short beat after almost every task; King says the plot does not stick anyway; exposition before play loses players.
4. **Two or three recurring characters with faces.** The raccoon, a rival or partner, and whoever is chasing them (a detective works with the case file look). Why: attachment to characters is what Wooga and Tactile credit, and it gives ads a face.
5. **The raccoon sneaking as something players watch.** After a win, the raccoon scurries along the red string to the next photo, hiding behind pinned photos as a flashlight sweeps; during levels it reacts to heat, streaks and big gems. Why: the charm in Goose and Sasquatch is low stakes mischief and reactions, and none of it needs player input.
6. **Completed boards as the collection.** Each place's finished crime board is kept in a case file album; finishing one unlocks a small permanent extra (a backstory card, a trophy on the board). Why: June's Journey's set completion unlocks are loved, and this version is earned, not random, so it stays inside our no random paid rewards rule.

### Skip for the soft launch, and why

- **The 3D island overworld.** No retention evidence, real cost in art, size and loading. Consider the one diorama per place version later.
- **Weekly story chapters.** Tactile needs about 30 people for its weekly cadence. Ship a fixed set of places instead.
- **Branching choices and multiple endings.** Expensive to write and test. One Murder by Choice reviewer liked that "our choices matter," but most of its reviews are about energy, not branches.
- **Energy or waiting in front of the story, and any paid reveal.** "Never get to see who done it" and "wait a full week" are the loudest complaints.
- **Card packs or random collectibles.** June's Journey's Memoirs are gacha; our rules bar paid random rewards.
- **Long cutscenes or voice acting.** Cost without evidence of return, and King found players do not retain plot.
- **Ads that show sneaking or rescue play the game does not have.** The ASA has already ruled against this.

### What to measure in soft launch

- Share of players who skip the story beats, and whether skippers retain differently.
- Share of players who finish the first place's boss in their first session (the design assumes the first boss cracks in one sitting).
- If there are enough players, an A/B test of the story beats on and off, to learn what nobody in the industry has published.

## 6. What I could not find

- **Any published A/B test of story against no story** in a puzzle game. Royal Match's meta test is the closest, and its numbers are not public.
- **The source of the Homescapes 68% and Wildscapes 38% Day 1 retention figures** quoted in some summaries. Not traced, so not used.
- **Retention data for a 3D overworld against a 2D map** in any mobile game.
- **Sales for Golden Idol and Danganronpa**, and an official Obra Dinn figure.
- **Snippet length in seconds or words** for any mobile story game. Only counts of dialogues and tasks are published.
- **Several pages refused automated reading** (Steam review text, Thinky Games' Golden Idol feature, inkle's detective design post, the Sneaky Sasquatch App Store reviews), so they are cited only from summaries or not at all.
- **Reddit threads**: searches did not return usable threads, so player voice here comes from store reviews and critics.

## TLDR

- **Mobile mystery money is real but specific:** June's Journey reached $1B (Wooga), Criminal Case won Facebook's 2013 Game of the Year, Seekers Notes has 43M downloads. They are hidden object games for older women (85% female and half over 60 for June's Journey). Playrix's newer murder mystery match 3 is estimated under $10K a month on iOS.
- **Story's proven effect is narrow:** Royal Match's test favored spending earned stars on visible tasks over scenery alone; story exposition before play loses players; King admits players do not retain its plot, though two thirds find story events more engaging. No one has published a story versus no story test.
- **Cadence in the big story games:** a few lines after almost every task or level, a milestone every 15 or so levels, a weekly chapter. The weekly chapter takes Tactile about 30 people.
- **Players love** solving the case, characters, and a light story "that's just there." **They hate** energy walls in front of the story, a paid or delayed reveal ("never get to see who done it"), story unrelated to play, and ads that show a different game (ASA ruled against Playrix in 2020).
- **Detective hits on PC and console** are loved for doing the deduction yourself, the "Aha!" moment, feedback that you are close, and a trial style payoff. Nobody loves the red string board for itself. Professor Layton (over 20M sold) proves unrelated puzzles work when the mystery is the reward, which is our model.
- **Sneaking charm** comes from low stakes mischief, funny reactions, no harsh fail, and music that shifts with the action (Untitled Goose Game, 1M sold in 3 months). All of it can be watched, so the raccoon can sneak along the red string between levels and react during them without any input.
- **3D island overworld:** no evidence it retains better than a flat map, real costs in art, download size (Google: about 1% fewer installs per 6 MB, a 2017 correlation) and loading. Skip for soft launch; a one diorama per place version is the cheaper later option.
- **Build first:** one mystery per place with a reveal at the boss, a light no cost accusation, one or two line beats per win, two or three recurring characters, the raccoon sneaking on the board, and finished boards as an earned collection. **Skip:** the 3D overworld, weekly chapters, branching, energy or paid reveals, card packs, long cutscenes.
