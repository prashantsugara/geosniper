# Geo Sniper privacy policy — DRAFT, NOT PUBLISHED

Publisher: **TO BE PROVIDED**. Effective date: **TO BE SET AFTER REVIEW**.
Support and privacy requests: bittruth1solutions@gmail.com.

This draft describes the implementation reviewed on 19 September 2026. The publisher must verify provider practices, retention, applicable regional obligations and audience before adopting and hosting it. Do not publish this placeholder version.

## Game data on your device

The game stores progress, rewards, settings and selected locations locally. Downloaded map data and recent locations may be cached to improve loading. The privacy/settings screen provides an action to clear saved locations and map caches without clearing gameplay progress. This action does not delete logs held by independent service providers. Clearing Android app storage or uninstalling may remove other local game data, subject to device backup behavior.

## Optional location and map search

Offline campaign and practice do not require location permission. If you explicitly choose device location, the game requests foreground location access to select a nearby map sector. You can instead search for a place or play offline. Selected coordinates/areas are sent to configured map services, and search text is sent to the search provider. Those services also receive network information such as your IP address. A selected map area may differ from your physical location.

The current integration uses Overture map data, OpenStreetMap/Overpass services and Photon place search. Map/elevation tile services may apply where enabled. Maps are approximate geographic data used for fictional gameplay, not navigation or a statement about real people or places.

## Ads and privacy choices

The game uses Google Mobile Ads and Google's consent platform. Depending on the configuration, region and applicable choices, the SDK may process device/advertising identifiers, approximate location, ad interactions and diagnostic information for advertising, measurement, fraud prevention and service operation. See [Google's privacy policy](https://policies.google.com/privacy) and [advertising technology information](https://policies.google.com/technologies/ads). Available privacy controls are accessible through game settings. Rewards require completion reported by the rewarded-ad SDK; reviews are never rewarded.

## Support and requests

If you email support, your email address and any information you send are used to respond to your request. Do not send passwords, private signing keys or unnecessary personal information. Contact bittruth1solutions@gmail.com for privacy questions or deletion requests regarding information the publisher controls.

## Items the publisher must complete

- Legal/public developer identity and correspondence details as applicable.
- Verified retention/deletion practices for support messages and any publisher-operated services.
- Provider roles, regional transfer safeguards and relevant rights/request processes.
- Target audience, children's-data treatment and any age-specific advertising restrictions.
- Policy-update process and effective date.

After review, host the completed policy at a public accessible HTTPS URL and set `privacyPolicyUrl` in `Assets/Resources/ReleaseConfiguration.json` and Play Console.
