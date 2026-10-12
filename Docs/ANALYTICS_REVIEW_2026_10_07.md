# Geo Sniper analytics review

Read directly from Google Analytics property GeoSniper (555760308) on 7 October 2026. Reporting period: 9 September–6 October 2026. Events report, secondary dimension App version, 50 rows. Report indicated 100% of available data.

| Version | First-open users | Users with level_start | Start events | Users with level_end | End events | App-remove users |
|---|---:|---:|---:|---:|---:|---:|
| 1.0.1 | 1 | 1 | 26 | 1 | 26 | No row |
| 1.0.2 | 86 | 18 | 212 | 18 | 189 | 44 |
| 1.0.3 | 8 | 3 | 19 | 3 | 18 | 2 |

Across versions: 95 first-open users, 20 distinct users with level_start, 257 starts and 233 ends. Version user counts overlap when users upgrade; do not sum them to infer distinct players.

## Interpretation

The most useful signal is the gap between first opens and users recording any level start. Separate aggregate counts do not establish that the same new-user cohort progressed in order. They are not first-mission conversion rates. Version 1.0.3 has only eight first opens, so no reliable improvement claim is possible.

Current source confirms level_start includes campaign missions and PvP duels. level_end includes success and failure. Event counts include repeats; 233/257 is not a success rate.

No map_load_started, map_load_completed, mission_started or mission_completed rows appeared among all 14 recorded event names. Current source does not emit map-loading events. Exact loading abandonment cannot be measured retrospectively from this report.

## Required measurement

For a precise first-mission funnel, capture location/map load start, completion and failure; capture first campaign mission start and successful completion separately. Keep stage index, contract type, load duration and a non-sensitive error category. Avoid sending coordinates or precise location. Retain analytics consent gating.

Use an ordered first_open → map load started → map load completed → first campaign mission started → first campaign mission completed exploration, broken down by App version. Register required custom dimensions for stage/contract/success if unavailable; registration does not backfill historical custom-dimension reports. Validate new events before drawing conclusions from the next release.

No app code, analytics collection settings, key-event settings, or shared reports were changed during this review. The event report was left open with App version as its secondary dimension.
