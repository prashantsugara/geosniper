# Squad tactics and extraction waves

Implemented 2026-09-30 for the Android-first build.

- Nearby alerted infantry now share suppressor, flanker and assault roles. Role assignment applies to ordinary detection and radio alerts, as well as damage. VIP attackers, couriers and counter-snipers keep their objective behavior.
- Flankers attempt a supported 8–12 metre side approach while a visible, engaged teammate provides covering fire. They reserve destinations, stop if support is lost or the route blocks, and use a cooldown between attempts. Movement follows the last observed position; walls still block shots. Existing suppressing audio and on-screen barks communicate the maneuver.
- All seven extraction missions now deliver three waves. Warnings precede reinforcements, later waves prefer opposite approaches, and contact notices report the actual compass direction. Available map geometry determines the safe approach.
- Active hostile limits scale from four to eight. Reinforcements spawn one at a time, at least 0.65 seconds apart, on dry supported ground away from the player and outside direct camera view or behind scenery. A full active roster delays further arrivals until enemies are eliminated.
- The map and briefing pause the wave schedule. Extraction requires all three waves to arrive, the minimum hold to expire, and at least six seconds after the final arrival. Briefing, HUD and failure guidance explain these requirements.
- If the map offers no safe reinforcement entry for a sustained period, the mission reports the sector limitation instead of silently waiting forever.

## Validation

- Runtime/editor compilation passed (existing Unity deprecation warnings remain).
- Isolated regression suite passed, including supported flank selection, support-loss cancellation, active-cap enforcement, pending-wave blocking, recovery intervals and final-arrival hold.
- All 50 campaign Play-mode checks passed. All seven extraction nodes exercised actual reinforcement spawns and reached extraction; the check accelerates scheduling and simulates successful defense, so it is not a human balance or frame-time benchmark.
- Unity logged an editor SearchDatabase indexing exception during the campaign run; the stack is confined to UnityEditor.Search and the campaign checks completed successfully.
- Android touch-control balance, sustained device performance and visual review remain unverified. No APK was built.
