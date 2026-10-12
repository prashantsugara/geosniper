# AdMob delivery review

The report screenshot shows 72 requests, 16.67% match rate, and 2 impressions. This implies approximately 12 matched requests and a 16.7% show rate, assuming identical filters. It does not establish the reason for no fill.

Code corrections:
- Native ads now wait for MobileAds.Initialize completion.
- All native load entry points honor the exponential retry deadline, including display-triggered requests.
- Rewarded, interstitial, rewarded interstitial, and native successful loads log response information. Full-screen formats now log SDK impression callbacks separately from load success.

Production configuration uses live IDs; Editor and development builds intentionally use Google test IDs. Banners are intentionally disabled. Native ads appear on the mission debrief; ordinary interstitials appear at eligible completed-stage and duel exits. Loaded ads can remain unused if players do not reach these placements.

For a release-device diagnosis, collect Android logs filtered by [AdManager], along with the AdMob report by ad unit including matched requests, impressions, and show rate. Check app readiness and any serving restrictions in the AdMob account. Local code cannot verify these account states or guarantee fill.

References:
- https://support.google.com/admob/table/9462111?hl=en
- https://developers.google.com/admob/unity/quick-start
