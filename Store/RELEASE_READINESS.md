# Geo Sniper — Play Store release audit

Reviewed 19 September 2026. **NOT READY TO PUBLISH.** Local fixes and editor tests are not certification of the final Android bundle. No production upload, new signed AAB, or physical-device validation was completed by this audit.

## Implemented in this release pass

- Central release configuration at `Assets/Resources/ReleaseConfiguration.json`; support email `bittruth1solutions@gmail.com`.
- Ads remain enabled, with official test IDs for development. UMP checks consent before requesting ads; settings expose privacy choices. Rewarded currency requires the SDK reward callback. Unavailable ads do not block mission transitions.
- Removed incentivized/five-star reviews and the victory-screen positive-review gate. Opening Google Play never grants currency or claims a review was submitted.
- Added privacy/support/credits screen, explicit location explanation and local map/location-cache deletion. Campaign and AI duel deploy offline; GPS is requested only through the explicit device-location action. Live-map deployment can be disabled in release configuration.
- Connected sensitivity to camera movement; persist volume and FPS settings. Removed invented initial currency/XP and recent-search displays, preserving existing saves.
- Android Back opens/closes the tactical pause map. Backgrounding an active mission opens the map instead of resuming combat immediately on return.
- Android API 36, IL2CPP and ARM64 settings; Android runtime MCP/debug compilation symbols removed. Cleartext HTTP and manifest backup flags disabled. Signing files excluded from Git.
- Added `Geo Sniper > Release` editor commands: readiness report, store-icon export, and signed AAB build. Non-development Android builds fail preflight when critical configuration/review items are incomplete. Development APKs remain buildable with test ads.
- Generated gold/charcoal logo, added it to the lobby and configured the default application icon. Android launcher-mask appearance still needs device verification.

## Publication blockers

| Priority | Issue | Required completion |
| --- | --- | --- |
| P0 | Production AdMob IDs missing | Supply Android app ID plus rewarded/interstitial unit IDs. Update Google Mobile Ads settings and release JSON; switch `useTestAds` off only for release. Publish and test the corresponding UMP consent messages. |
| P0 | No public privacy-policy URL or publisher name | Review the draft policy, supply the public developer identity, host it at an accessible HTTPS URL and configure that URL in game and Play Console. |
| P0 | Upload signing is not configured | Configure a private upload keystore and alias locally. Keep passwords out of chat/Git. Confirm `com.geosniper.game` is the permanent package name before first upload. |
| P0 | Third-party asset distribution rights unverified | Complete `ASSET_RIGHTS.md`. Technical import/orientation checks do not establish commercial licenses. Replace assets whose rights cannot be established. |
| P0 | Final artifact and device QA not performed | Build signed AAB, verify every native library for 16 KB compatibility, test install/startup, consent/ads, permissions, offline operation and performance on devices; run Play pre-launch testing. |
| P0 | Play declarations incomplete | Complete Data safety, contains-ads, target audience and violence/content rating, privacy and app-access declarations against the actual release. Do not simply flip the review booleans. |
| P1 | Live-map production provider review pending | Verify capacity, terms, attribution and fallback behavior for Overture, OSM/Overpass, Photon and any enabled map/elevation tiles. Public prototype services are not a guaranteed production backend. |
| P1 | Resource size/performance risk | Source `Resources` inventory is roughly 305 MB, not a measured download size. Heavy meshes and renderer counts remain; profile and use LODs, merge compatible meshes and move verified-unused source assets outside Resources before scale-up. |
| P1 | Store presentation incomplete | Icon available; capture genuine gameplay screenshots and supply a 1024×500 feature graphic. Review `LISTING.md`; do not advertise multiplayer, satellite imagery or guaranteed GPS accuracy. |

## Verification and evidence

Current results are recorded in `VALIDATION.md`. Existing geometry/difficulty evidence is in `GAMEPLAY_ASSET_REVIEW.md`, `CAMPAIGN_PROGRESSION.md` and isolated Unity logs. Automated checks cover model bounds/orientation, calibrated weapon geometry, hit accuracy/cover, campaign graphs and easy-to-hard profiles. They cannot establish visual quality on every device, player-perceived balance, SDK network ad delivery or crash-free Android startup.

Run `Tools/check_code.ps1`, `Tools/run_review_checks.ps1` and then `Tools/run_campaign_checks.ps1` (wait for each isolated Unity process to exit). Use the readiness menu in the real project to write `Logs/PlayStoreReadiness.txt`. The final signed AAB build is deliberately blocked until publisher configuration and required reviews are complete.

## Release sequence

1. Complete public identity, package confirmation, privacy hosting, asset licenses and provider review.
2. Configure production AdMob/UMP; test real IDs only on registered test devices. Keep developer builds on test IDs.
3. Configure upload signing locally, verify version code is greater than previous uploads, export store icon and build a signed AAB.
4. Complete `DEVICE_TEST_PLAN.md`, validate the exact bundle and upload to an internal/closed testing track.
5. Review console declarations and listing, inspect pre-launch results, then request production access and publish only after all blockers are resolved.

## Additional improvements

- Add device-tier quality presets, LODs and GPU/CPU/memory frame budgets; prioritize heavy character meshes and weapon renderer counts.
- Run structured playtests of all 14 campaign nodes, recording retries, completion time and accuracy before retuning difficulty. Preserve the introductory stationary/no-timer contracts.
- Improve low-memory map streaming and measure cache bounds, shader warmup and initial loading on budget Android devices.
- Replace remaining developer UI and placeholder art only after the underlying release reliability checks pass. Test color contrast, safe areas, large text and touch targets.
- Add a privacy-reviewed crash-reporting solution and operational monitoring only after deciding which data it collects; no new telemetry was added here.

## Policy references checked

- New apps/updates require API 36 from 31 August 2026: [Android target API requirements](https://developer.android.com/google/play/requirements/target-sdk).
- Final native dependencies require verification, not merely an ARM64 checkbox: [16 KB page sizes](https://developer.android.com/guide/practices/page-sizes).
- [AdMob Unity consent integration](https://developers.google.com/admob/unity/privacy) and [SDK data disclosures](https://developers.google.com/admob/android/privacy/play-data-disclosure).
- Incentivized ratings are prohibited: [Play ratings/reviews policy](https://support.google.com/googleplay/android-developer/answer/9898684).
- New personal accounts may require 12 opted-in testers for 14 continuous days: [production-access testing requirements](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en). Account eligibility is unknown.
- [Store graphic requirements](https://support.google.com/googleplay/android-developer/answer/9866151?hl=en-GB).
