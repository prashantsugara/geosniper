# Geo Sniper analytics and revenue review — 10 October 2026

Read directly from signed-in Google Analytics and AdMob. No game code, campaign, account setting, or monetization setting changed. Windsor has Google Ads and Facebook connected, but no GA4/AdMob connection; browser reports supplied the game data.

## Income so far

AdMob Ads Activity report, App filter **Geo Sniper: GPS Shooting Game**, date **All time**, observed 10 October:

| Metric | Value |
|---|---:|
| Estimated earnings | US$0.18 |
| Impressions | 54 |
| Requests | 622 |
| Match rate | 63.34% |
| Observed eCPM | US$3.40 |

App overview: September US$0.08; October so far US$0.10; yesterday and today so far each US$0.00. Figures are rounded estimates, not verified payments or profit. Account-wide earnings include other apps and must not be attributed to Geo Sniper. Payment history and advertising acquisition costs were not audited.

| All-time format | Earnings | Impressions | Requests | Match rate | eCPM |
|---|---:|---:|---:|---:|---:|
| Rewarded | US$0.12 | 17 | 326 | 48.47% | US$6.77 |
| Interstitial | US$0.06 | 27 | 208 | 82.69% | US$2.31 |
| Native advanced | US$0.01 | 10 | 88 | 72.73% | US$0.60 |

Independently rounded rows do not sum exactly to the total. The sample is too small to establish stable format yields.

3–9 October vs 26 September–2 October: estimated earnings US$0.10 vs US$0.06 (+54.63% using unrounded values), impressions 24 vs 12, requests 225 vs 167, match rate 68.00% vs 56.89%, eCPM US$4.03 vs US$5.22. AdMob's GA-powered user card shows active users 85 vs 49 and average session duration 3m19s vs 3m2s. These are different metrics from GA4 session engagement below.

## Gameplay analytics

GA4 property GeoSniper 555760308, **1 January–9 October 2026**, excluding the incomplete current day. The extended range has the same totals as 12 September–9 October. No claim is made about data before January.

| Metric | Count |
|---|---:|
| Total users / new users | 133 / 133 |
| Returning users | 13 |
| Sessions recorded | 202 from 132 users |
| Level starts | 339 from 36 users |
| Level ends | 309 from 31 users |
| App removals | 71 from 71 users |
| Deployment started | 61 from 12 users |
| Deployment completed | 49 from 11 users |
| Deployment failed | 10 from 2 users |
| First mission started | 14 from 11 users |
| First mission completed | 10 from 7 users |
| Mission first shot | 29 from 4 users |
| Next mission selected | 20 from 1 user |
| GA4 ad impressions | 34 from 15 users |
| GA4 ad revenue / purchase revenue | ₹15.49 / ₹0.00 |

Firebase overview: average online active-user engagement 4m47s; average online session engagement 3m15s; engaged online sessions per active user 1.5. Latest release card shows 1.0.5, Successful; reported crash-free users 100%. This does not prove freedom from loading failures, freezes, runtime exceptions or poor frame rates, or that the locally tested 1.0.6 fixes reached Store players.

### Measurement limits

- Recorded level reach is 36/133 (27.1%), a warning signal, not a measured 72.9% abandonment rate. Event coverage differs across versions; aggregates are not an ordered cohort funnel.
- 71 app removals divided by 133 new users is not a cohort uninstall rate. Returning users are not D1/D7 retention. Numerical D1/D7 values were not established from the chart.
- Level ends include failures and wins; 309/339 is not a win rate.
- New custom events cover only some versions/users. Twenty next-mission events from one user do not show broad repeat engagement.
- GA4 and AdMob impression counts differ. Do not add their revenues: they describe overlapping earnings, with different coverage/currency/reporting. Use AdMob for earnings and investigate event coverage separately.
- Developer/testing traffic has not been excluded in this audit. Reinstalls and device testing can influence these small counts.

## Recommended order

1. **Ship and verify loading reliability.** Ten recorded deployment failures from two users justify checking release-version failure reasons and time to first playable frame. Measure median and p95 startup/load times on lower-end Android; keep a fast bundled first mission and useful recovery for map/network failure. A longer timeout alone does not make loading good.
2. **Make the first minute immediately playable and readable.** A short scoped shot, clear target/objective, visible reward and clear next mission should precede optional location/loadout complexity. Treat improving recorded level reach as a hypothesis to test with a proper funnel.
3. **Repair measurement before judging content or buying more traffic.** Track actual lobby visibility, deploy tap, first playable frame, first shot, mission outcome and mission-two start with app version and anonymous session/attempt identifiers. Inspect Firebase startup readiness, queue eligible events with consent respected, distinguish retries from mission stages, and separate developer devices. Compare D1/D7 cohorts only when mature.
4. **Investigate ad availability and presentation, not just request volume.** Only 54 impressions from 622 requests is 8.7%, but this is not show rate: requests include unmatched/preloaded ads that may never have a placement opportunity. Rewarded match rate is 48.47%. Add load/match/show/fail/expiry/opt-in measurements by format and version; verify foreground safety and rewards on device. Test a clear optional reward offer at a natural mission break. Keep the first mission free of forced interruptions.
5. **Polish the representative mission, then expand variety.** Improve target visibility, aiming, material/light quality and clean touch HUD while measuring frame time. Distinct buildings and props should improve readable routes and mission goals. Validate mission-two reach and return cohorts before expanding every sector.

Recent weekly activity grew, but the player base and ad sample remain too small for revenue forecasts or monetization experiments with many simultaneous changes. Initial goals should be a reliable playable start, accurate funnel coverage, more first wins and more players reaching mission two.

## Sources

- [AdMob Geo Sniper app overview](https://admob.google.com/v2/apps/8915056238/overview) — app-scoped monthly and weekly cards; all-time figures from its View report with All time selected.
- [GA4 GeoSniper Events](https://analytics.google.com/analytics/web/?authuser=0&hl=en-GB#/a179088421p555760308/reports/explorer?r=top-events) — custom range set to 1 January–9 October.
- [GA4 Firebase overview](https://analytics.google.com/analytics/web/?authuser=0&hl=en-GB#/a179088421p555760308/reports/dashboard?r=firebase-overview) — same custom range.
- [GA4 retention](https://analytics.google.com/analytics/web/?authuser=0&hl=en-GB#/a179088421p555760308/reports/dashboard?r=games-retention) — new/returning users.

All reports require account access. Current source inspection and the historical 8 October opening-flow audit supplement hypotheses; neither establishes the exact behavior of every shipped version.
