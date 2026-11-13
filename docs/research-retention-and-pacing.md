# Research: retention, pacing, near misses and loot box rules

Researched 2026-11-12 for the heist vault design. Every claim has a link. Where a source is an analytics company rather than a platform, regulator or court, it is labeled **vendor data**. Where only fan guides or press covered a point, it is labeled **secondary** or **unverified**.

The design being checked: one vault per heist, cracked in stages (4 bolts, two rings, wheel, door). Sorting gives steady progress, a free Plinko bonus round earned by finishing a leg gives big jumps, a bust turns the current stage backward but keeps finished stages, and one vault should take 3 to 5 days at about one 5 to 10 minute sitting per stage. Monetization is boosters, gear, and a paid "post bail" continue after a bust.

## 1. Retention and session benchmarks

### All mobile games, 2025 calendar year (vendor data, GameAnalytics)

Sample: 16,262 live games with at least 1,000 monthly active users, iOS and Android, all genres, January 1 to December 31, 2025. Source: [GameAnalytics 2026 Mobile and PC Gaming Benchmarks (PDF)](https://investgame.net/wp-content/uploads/2026/01/2026-01-27-2026-mobile-pc-benchmarks_compressed.pdf).

| Metric | Median game | Top 25% | Top 10% | Top 1% |
|---|---|---|---|---|
| Day 1 retention | about 22% | just above 30% | about 40% | 64 to 68% |
| Day 7 retention | just under 4% | 6 to 7% | 11 to 12% | above 25% |
| Day 30 retention | 0.68 to 0.79% | 1.6 to 1.8% | not stated | 13 to 15% |
| Session length | 3.1 to 3.5 min | about 5.2 min | just over 8 min | above 22 min |
| Sessions per day | 3.8 to 3.9 | 5.3 to 5.7 | up to about 9.6 | above 12 |
| Playtime per day | about 12 min | 22 to 24 min | | |

All three retention numbers fell again from 2024. The same report advises building three nested loops: one that fits inside a single session, one that spans several sessions in a day, and one that spans days or weeks.

### All mobile games, 2024 calendar year (vendor data, GameAnalytics)

Sample: 11,600 games, January 1 to December 31, 2024. Source: [GameAnalytics 2025 Mobile Gaming Benchmarks (PDF)](https://investgame.net/wp-content/uploads/2025/02/2025-GameAnalytics-Mobile-Gaming-Benchmarks.pdf).

- Day 1: top 25% at 26.5 to 27.7% (iOS 31 to 33%, Android 25 to 27%). Day 7: median 3.4 to 3.9%, top 25% at 7 to 8%. Day 28: 75% of games stayed under 3%.
- Session length: median 5 to 6 min, top 25% at 8 to 9 min. Casual games "often feature shorter sessions (5 to 6 minutes)."
- Sessions per day: median 4. North America 3.75.
- Board, Card, Puzzle and Casino games "show consistently strong retention rates across D1, D7, and D28" and rank high on session count. The genre numbers are only in charts, so I could not read exact puzzle figures.

### Casual puzzle specifically (vendor data, Appodeal)

Android, United States, June 2024 to January 2025: Day 1 retention about 32% for Match games and 30% for Puzzle games. The longest average session of any casual subgenre was Merge at 656 seconds (about 11 min). Day 7 and Day 30 for puzzle were behind the paywall. Source: [Appodeal Mobile Casual Benchmarks Report 2025](https://gameindustrylibrary.com/documents/mobile-casual-benchmarks-report-2025).

**Unverified:** blog posts commonly quote casual puzzle at 30 to 40% Day 1, 10 to 20% Day 7 and 3 to 7% Day 30, or attribute 28 to 32% / 9 to 12% / 3.5 to 5% to AppsFlyer 2022 to 2023 data. I could not trace either back to the vendor's own report, so do not rely on them.

### What the numbers mean for us

- The GameAnalytics medians include many small titles. A polished puzzle game should aim for the top quartile or better: roughly 30% Day 1, 7% Day 7, 2% Day 30 as a floor, and the top 10% figures (40% / 12%) as a target.
- A typical sitting is short: 3 to 6 minutes, about 4 times a day. Our planned 5 to 10 minute stage is at the top of that range or longer than it.
- On Day 7, even good games keep fewer than 1 in 8 players. Most players will never see Day 3 to 5, so the first vault cannot be the one that takes 3 to 5 days.

## 2. How top games pace the main goal, and how they handle failure

Note on sources: none of these companies publish how long an area, episode, village or board is designed to take. Platform and investor documents do not cover it either. Everything below on pacing comes from fan guides and analyst write ups and is marked secondary.

| Game | Main goal | Size (secondary) | What failure costs |
|---|---|---|---|
| Royal Match (Dream Games) | Restore an "area" by spending stars on tasks; one star per level won | 10 to more than 100 levels per area, over 100 areas ([esports.net](https://www.esports.net/news/mobile-games/royal-match-areas/)) | A life (max 5) and the win streak bonus resets |
| Candy Crush Saga (King) | Clear an "episode" on the map | Historically 15 levels per episode, new levels added weekly ([Windows Central](https://www.windowscentral.com/candy-crush-saga-updated-15-new-candy-keep-levels), [Pocket Gamer](https://www.pocketgamer.com/candy-crush-saga/how-many-levels-are-there)) | A life, lives refill one per 30 min ([King community, Jelly Saga](https://community.king.com/en/candy-crush-jelly-saga/kb/articles/38-how-do-lives-work); [fan wiki](https://candycrush.fandom.com/wiki/Lives)) |
| Coin Master (Moon Active) | Build a "village": 5 buildings x 5 upgrades | 25 upgrades per village, costs climb to billions of coins by village 300 ([Pocket Gamer](https://www.pocketgamer.com/coin-master/village-cost-list)) | Other players' attacks knock a building down one level; shields block ([levvvel](https://levvvel.com/coin-master/gameplay/attacks/)) |
| Monopoly GO (Scopely) | Build every landmark on a "board", then move to the next | Not documented | Other players' shutdowns destroy a landmark that must be repaired; bank heists take cash; shields block shutdowns only ([Sportskeeda](https://sportskeeda.com/esports/how-defend-monopoly-go-landmarks), [AFK Gaming](https://afkgaming.com/gaming/general/buildings-landmarks-in-monopoly-go-all-you-need-to-know)) |

Patterns worth noting:

- **Puzzle games never take away the main goal.** In Royal Match and Candy Crush, a failed level costs a life and time, never stars, tasks or map progress. What you lose is a short term bonus: Royal Match's Butler's Gift and Candy Crush's win streak track give more boosters for each consecutive win and reset when you fail ([Deconstructor of Fun, 2024](https://www.deconstructoroffun.com/blog/2024/8/5/win-streak-the-gift-that-keeps-on-giving); Royal Match reset rule from [Twinfinite](https://twinfinite.net/guides/how-to-get-boosters-royal-match/), secondary). That streak is exactly what the paid "out of moves" offer protects. Deconstructor of Fun calls this the "End Game Offer" and argues higher streak stakes raise its conversion (analysis, no data).
- **Real regression exists only in the casino style games** (Coin Master, Monopoly GO), and there it comes from other players, softened by shields, and only knocks back one building or landmark, never the whole village or board. That is close to our rule (bust turns back the current stage, finished stages are kept).
- **Royal Match tested its meta.** Naavik reports Dream Games tested an episode map against an area with tasks before picking areas ([Naavik](https://naavik.co/digest/royal-match-finding-success-through-iteration/), secondary).
- **Unverified:** I found no reliable figure for how many days an area, episode, village or board takes a typical player. If you want one, the honest route is playing them and timing it.

## 3. Near misses, and earned versus paid random rewards

### Near misses

- **The core study:** Clark and colleagues, *Neuron*, 2009. Near misses on a simulated slot machine felt *less* pleasant than clear misses but *raised* the urge to keep playing, and lit up the same reward areas as real wins. The effect showed up **only when the player had personal control** (chose the play themselves); when the computer chose, near misses lowered the urge to play. Source: [Clark et al. 2009, PubMed Central](https://pmc.ncbi.nlm.nih.gov/articles/PMC2658737).
- **In a mobile puzzle game:** Larche, Musielak and Dixon, *Journal of Gambling Studies* 33(2), 2017 (online 2016). Sixty Candy Crush players, 30 minutes each, with heart rate and skin response measured. Near misses (failing a level just short of the goal) were more arousing than losses, rated the *most frustrating* outcome, and produced the strongest urge to keep playing of any outcome. The authors say this may lead players to play longer than intended. Source: [Springer](https://link.springer.com/article/10.1007/s10899-016-9633-7); [thesis at University of Waterloo](https://uwspace.uwaterloo.ca/handle/10012/12522).
- **I found no GDC talk** that covers near miss design directly. Unverified.

For us: a bust at 95 heat, or a Plinko ball that bounces off the jackpot slot, is a near miss. Plinko where the player picks the drop point is the "personal control" case that strengthens the effect. The "post bail" offer appearing right after a narrow bust is the same moment the Candy Crush study measured as most frustrating and most motivating.

### Earned versus paid random rewards

- **Paying is what links loot boxes to problem gambling in most studies.** Zendle and Cairns (*PLOS ONE*, 2018) found a link between how much gamers spent on loot boxes and problem gambling severity ([White Rose repository](https://eprints.whiterose.ac.uk/139116/)).
- **Earned does not mean harmless.** Larche, Chini, Lee and Dixon, *Journal of Gambling Studies* 39(2), 2023, compared Overwatch players who *bought* loot boxes with those who only *earned* them. Contrary to their own expectation, the earners showed *greater* gambling related harms, with no difference in problem gaming. This comes from abstract fragments ([index record](https://ouci.dntb.gov.ua/works/42oAa06l), [DOI](https://doi.org/10.1007/s10899-022-10127-5)); I could not open the full paper, so the sample size and effect size are unverified, and the study cannot show cause and effect.
- **Regulators are starting to say the same.** The Dutch consumer authority ACM (October 2025 position paper) recommends an EU wide ban on variable reward systems that **also covers unpaid ones**, because "while financial risks in unpaid variable reward systems may be limited, the health risks remain unaltered" ([ACM position paper (PDF)](https://www.acm.nl/system/files/documents/acm-position-paper-video-games.pdf)). That is a recommendation, not law.

## 4. Rules on paid random rewards

### Apple App Store Review Guidelines (page last updated October 16, 2026)

- **3.1.1:** "Apps offering 'loot boxes' or other mechanisms that provide randomized virtual items for purchase must disclose the odds of receiving each type of item to customers prior to purchase."
- **5.3** covers real money gaming, lotteries, sweepstakes and contests (licensing, geo restriction, no in app purchase of real money credit). It does not mention simulated gambling.
- Source: [App Store Review Guidelines](https://developer.apple.com/app-store/review/guidelines/).
- **Age rating:** Apple defines "Simulated Gambling" as "betting or wagering without using real money or in-game currency that can be exchanged for real money." Infrequent simulated gambling rates 13+, frequent rates 18+. Paid loot boxes rate 9+ (16+ in Australia, A18 in Brazil). Source: [App Store Connect age ratings](https://developer.apple.com/help/app-store-connect/reference/app-information/age-ratings-values-and-definitions).

### Google Play

- Payments policy: games offering randomized virtual items from a purchase "must clearly disclose the odds of receiving those items in advance of, and in close and timely proximity to, that purchase." Nothing on free or earned items. Source: [Google Play Payments policy](https://support.google.com/googleplay/android-developer/answer/9858738).
- The Real Money Gambling policy bans real money wagering without approval and, for apps showing gambling ads, simulated gambling content. Nothing on free games without prizes. Source: [Real Money Gambling, Games, and Contests](https://support.google.com/googleplay/android-developer/answer/9877032).

### Belgium

- The Belgian Gaming Commission's April 2018 *Research Report on Loot Boxes* found paid loot boxes in Overwatch, FIFA 18 and CS:GO to be games of chance needing a license. A game of chance needs a bet, a win or loss, and chance. "A wager (bet) of any type is sufficient... Use of money is not necessary," but **"we do not consider the game-play currency as a wager... if this cannot be purchased."** Star Wars Battlefront II, whose boxes could no longer be bought with money at the time, was found **not** to be gambling "because there is no wager present that can be valued in terms of money." FIFA packs bought with a currency that is only *indirectly* buyable still counted as a wager. Source: [English translation hosted by Skadden (PDF)](https://www.skadden.com/-/media/files/publications/2019/08/video-gaming-egaming-law-update/fn12_onderzoeksrapportlootboxenengelspublicatie.pdf?la=en) (the official version is Dutch; I could not find it on the Commission's own site).
- Enforcement since then has been weak: a peer reviewed study found paid loot boxes still widespread in the Belgian App Store ([Peer Community In Registered Reports](https://rr.peercommunityin.org/articles/rec?id=264)).

### Netherlands

- The Netherlands Gaming Authority (Kansspelautoriteit) 2018 study found 4 of 10 games broke the law, but only because their prizes could be traded and so had market value ([KSA study (PDF)](https://kansspelautoriteit.nl/publish/library/17/study_into_loot_boxes_-_a_treasure_or_a_burden_-_eng.pdf)).
- On March 9, 2022 the Council of State (Raad van State, ECLI:NL:RVS:2022:690) ruled that FIFA Ultimate Team packs are **not** a game of chance and cancelled the authority's penalty on Electronic Arts. No further appeal was possible ([Raad van State](https://www.raadvanstate.nl/@130206/dwangsom-onterecht-opgelegd-loot-boxes/)). So paid loot boxes are currently not treated as gambling in the Netherlands.
- The Dutch consumer authority ACM instead acts under consumer law. In May 2024 it fined Epic Games 1,125,000 euros over Fortnite, partly for countdown timers that created false urgency for children: "ACM finds these types of deceptive timers to be misleading" ([ACM](https://acm.nl/en/node/25515)).

### European Union (new, proposed yesterday)

The European Commission proposed the **Digital Fairness Act** on November 11, 2026 (COM(2026) 1100, procedure 2026/0356(COD)). It is a proposal only: Parliament and Council must agree it, and it would apply 12 months after it enters into force. Source: [proposal text (PDF)](https://commission.europa.eu/document/download/0575d59f-554f-4a9e-b54b-e6b717175809_en); [Commission news](https://commission.europa.eu/news-and-media/news/making-your-online-experience-fairer-2026-11-11_en).

- **Article 15:** traders offering **paid** variable reward systems (defined as random rewards "acquired through payment transactions... or in exchange for paid-for virtual currencies"; recital 63 names "loot boxes, wheels of fortune, or collectible card packs") must show contents and odds, must give everyone a tool to switch them off, and may not offer them to minors.
- **Article 16:** no "pay-to-progress and pay-to-win mechanisms" for minors, defined as purchases "necessary or presented as necessary for in-game or in-app progress or success." Recital 65 names "progression speeds that render the unpaid path illusory."
- **Article 17:** games designed in a way that can foreseeably encourage compulsive use must let players switch off, among other things, features rewarding play "at regular times or with greater frequency, including through penalties or loss of benefits for failing to engage regularly."

### Does a free, earned Plinko round fall under these rules?

As the design stands (earned only by finishing a leg, never bought, no real money stake, prizes usable only in game and not tradeable):

- **Apple 3.1.1 and Google Play odds disclosure:** no. Both apply only to random items "for purchase" or "from a purchase."
- **Belgium:** very likely no, by the Commission's own 2018 reasoning (gameplay currency that cannot be bought is not a wager; Battlefront II cleared on that basis). This is my reading of a regulator report, not legal advice.
- **Netherlands:** no under current law (no stake and no tradeable prize; and even paid FIFA packs were cleared in 2022).
- **EU Digital Fairness Act as proposed:** no for Article 15, which covers only *paid* systems. The ACM is lobbying to include unpaid ones, so watch the final text.
- **Apple age rating:** a gray area. If Apple treats Plinko as "simulated gambling" the game rates 13+ (or 18+ if frequent). There is no wager, which argues against it, but I found nothing official on how reviewers treat a free Plinko board. Unverified.

**What would pull it under the rules:**

1. Selling a Plinko drop, a re-drop, or an extra ball, or offering one inside a paid bundle.
2. Letting a purchasable currency or a paid booster be the entry fee, even indirectly. FIFA's indirect currency still counted as a wager in Belgium.
3. Selling anything random elsewhere (random gear chests). Then Apple, Google and the proposed EU Act all require odds, and the EU proposal bans it for minors.

Also note the "post bail" continue and paid boosters. They are not random, so loot box rules do not apply. But under the proposed Article 16 they must not be "presented as necessary" to progress if minors play, and a countdown on the bail offer must be honest (the ACM fined Epic over fake timers).

## 5. Does 3 to 5 days per vault hold up?

**Mostly yes for the long term goal, no for the first one.**

What holds up:

- The structure matches the three loop advice in the GameAnalytics 2026 report: a sort round per session, stages across a day, a vault across days.
- Keeping finished stages and only turning back the current one is gentler than Coin Master or Monopoly GO and harsher than Royal Match or Candy Crush. That is a reasonable middle for a heist theme.
- Keeping the bonus round earned only keeps Plinko outside every current loot box rule.

What should change:

1. **Make the first vault crackable in the first sitting** (about 10 to 15 minutes, all five stages short). Fewer than a third of players come back on Day 1 even in good games, and fewer than 1 in 8 by Day 7. A 3 to 5 day first vault means most players never see a door open. Ramp vault length up over the first few heists: for example the first vault in one sitting, the second in a day, and 3 to 5 days from the third or fourth.
2. **Shorten the stage, or save progress inside a stage.** Typical sessions in 2025 were 3 to 5 minutes, and casual sessions 5 to 6 minutes in 2024. A 5 to 10 minute stage will often end mid stage. Either target 3 to 5 minutes per stage, or make sure partial stage progress is saved between sittings. (I am assuming it currently is not; please confirm.)
3. **Make sure a bust never undoes a whole sitting.** A bust that turns back more than about one session of work will feel like losing the goal, which top puzzle games avoid. Limit the setback to part of the current stage, not all of it.
4. **Treat post bail as protecting a bonus, not the goal.** Royal Match and Candy Crush sell continues to protect a streak bonus, not core progress. A bail offer that is the only way to avoid losing real progress drifts toward "pay to progress", which the EU proposal would ban for minors.
5. **Don't rig Plinko near misses.** Use honest physics or real odds, show the odds anyway, and avoid tuning the ball to graze the jackpot slot. Player chosen drop points make near misses hit harder (Clark 2009), and the ACM wants even unpaid random rewards banned. Never sell drops.
6. **No penalty for days away.** Don't decay a vault or stage when someone skips a day. The proposed EU Act would require a switch for "loss of benefits for failing to engage regularly."

## TLDR

- **Benchmarks (vendor data, GameAnalytics 2025 calendar year):** median mobile game keeps about 22% of players on Day 1, under 4% on Day 7 and under 1% on Day 30. The top 10% keep about 40% and 12%. Casual puzzle Day 1 is about 30 to 32% (Appodeal, mid 2024 to early 2025). Sessions are 3 to 5 minutes, about 4 a day.
- **Pacing in top games:** no primary source says how long an area, episode, village or board takes. Puzzle games never take away main progress on failure (you lose a life and a streak bonus). Coin Master and Monopoly GO do knock back one building, and the damage comes from other players, softened by shields.
- **Near misses:** they raise the urge to keep playing, most strongly when the player feels in control (Clark 2009), and in Candy Crush they are the most frustrating outcome and the strongest push to continue (Larche 2017). Surprise: one 2023 study found players who *earned* loot boxes showed more gambling harm than buyers.
- **Rules:** Apple and Google require odds only for paid random items. Belgium treats paid loot boxes as gambling but not unbuyable earned ones. The Netherlands' top court cleared paid FIFA packs in 2022. The EU proposed the Digital Fairness Act on November 11, 2026: odds, an off switch and a ban for minors on *paid* random rewards, plus no "pay to progress" for minors. A free, earned, unbuyable Plinko round falls outside all of these. Only Apple's "simulated gambling" age rating is a gray area.
- **Verdict:** 3 to 5 days per vault is fine from about the third heist on. Make the first vault one sitting, keep stages to 3 to 5 minutes or save progress inside a stage, cap how much a bust takes back, keep Plinko unbuyable and honest, and make sure post bail never feels required to progress.
