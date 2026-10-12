# Data safety review worksheet — not a completed declaration

Publisher must confirm against the final SDKs, network requests and service contracts. The game contains ads; do not select “no data collected” solely because progress is stored locally.

| Flow | Current implementation | Review required |
| --- | --- | --- |
| Progress/settings | PlayerPrefs on device | Local-only data is distinct from network collection; verify platform backup behavior on final APK. |
| Optional device location | Requested explicitly to select nearby gameplay sector | Review precise/approximate location collection and sharing, purposes, optionality and retention. |
| Map requests | Selected area/coordinates and request IP reach map services | A searched area is not necessarily the player's location; still disclose applicable transmitted data accurately. |
| Place search | Search text sent to Photon | Review query data, IP, retention and provider role. |
| Map/location cache | Local persistent files and saved recent coordinates | In-app deletion clears local data, not provider-held logs. |
| Advertising/consent | Google Mobile Ads and UMP | Review identifiers, approximate location, interactions, diagnostics and other SDK-disclosed data; configure regional consent messages. |
| Optional gameplay analytics | Firebase Analytics and Unity Analytics SDKs after separate in-game opt-in | Review app/device identifiers, usage events, SDK diagnostics, sharing, deletion and regional consent; verify the final AAB and both providers' current disclosures. |
| Support | User-initiated email | Define handling, access, deletion and retention for support correspondence. |
| Online duels | Unity anonymous Authentication and DTLS Relay; callsigns, weapon/game state and host-selected map coordinates are transmitted | Review player identifiers, IP/connection metadata, gameplay and location data, Unity's service-provider role and retention. Choosing a live-location map shares that location with the opponent. |

Use Google's [Mobile Ads SDK disclosure guide](https://developers.google.com/admob/android/privacy/play-data-disclosure) and actual integration configuration. Consent is not equivalent to declaring that the SDK collects nothing. Check data encryption, deletion-request handling, service-provider exceptions, purposes and any audience-specific requirements in Play Console.

Unity anonymous Authentication was added for Relay multiplayer; there is no player-facing email/password login. Review final packaged dependencies and traffic. Re-review Play Console Data safety before release for analytics and multiplayer services.
