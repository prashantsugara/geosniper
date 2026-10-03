# Campaign audit — 29 September 2026

The current graph contains 50 nodes, not the 14 documented in the earlier campaign report. The table below is extracted from the authored graph. Mission names describe objectives; bespoke environments or named multi-wave mechanics are not guaranteed by those names.

## Changes

- Preserve the failure reason separately from temporary HUD notices.
- Show objective-specific next-attempt guidance and make Retry the primary action.
- Failed setup reports a retryable error instead of silently substituting a generic patrol.
- Failure cancels delayed victory; a dead player cannot receive a pending victory.
- Extraction requires the entire hold timer; eliminating the first group no longer skips it.
- Late tiers remove bonus time/VIP health, shorten enemy reaction and sniper lock allowances, and increase damage. Starter training remains forgiving.

## All authored levels

| Level | Operation | Tier | Enemy budget | Required objective |
|---|---|---|---|---|
| 1 | FIRST CONTACT | 0 | 2 | Eliminate the marked commander and any counter-sniper. |
| 2 | VIP OVERWATCH | 0 | 2 | Keep the VIP alive until extraction. |
| 3 | FUGITIVE SPRINT | 0 | 2 | Eliminate the courier before escape or timeout. |
| 4 | SILENT INFILTRATION | 0 | 2 | Eliminate every sentry below the alert limit. |
| 5 | HOT EXTRACTION | 0 | 2 | Survive the full hold timer, then board the helicopter. |
| 6 | RIVAL DUEL: GHOST | 1 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 7 | PLAZA WARLORD | 1 | 2 | Eliminate the marked commander and any counter-sniper. |
| 8 | CONVOY GUARDIAN | 1 | 2 | Keep the VIP alive until extraction. |
| 9 | SHADOW COURIER | 1 | 3 | Eliminate the courier before escape or timeout. |
| 10 | SYNDICATE CELL | 1 | 3 | Eliminate every sentry below the alert limit. |
| 11 | ROOFTOP RESCUE | 2 | 3 | Survive the full hold timer, then board the helicopter. |
| 12 | DISTRICT PURGE | 2 | 3 | Eliminate the marked commander and any counter-sniper. |
| 13 | RIVAL DUEL: STALKER | 2 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 14 | AMBUSH CORRIDOR | 2 | 3 | Eliminate the marked commander and any counter-sniper. |
| 15 | HIGHWAY RUNNER | 2 | 3 | Eliminate the courier before escape or timeout. |
| 16 | NIGHT WATCH | 3 | 4 | Keep the VIP alive until extraction. |
| 17 | COVERT EXTRACT | 3 | 4 | Eliminate every sentry below the alert limit. |
| 18 | ARMORED TARGET | 3 | 4 | Eliminate the marked commander and any counter-sniper. |
| 19 | STREET SWEEP | 3 | 4 | Survive the full hold timer, then board the helicopter. |
| 20 | RIVAL DUEL: VIPER | 3 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 21 | DOCKS OVERWATCH | 4 | 4 | Keep the VIP alive until extraction. |
| 22 | CARGO RUNNER | 4 | 4 | Eliminate the courier before escape or timeout. |
| 23 | SILENT SILO | 4 | 5 | Eliminate every sentry below the alert limit. |
| 24 | SECTOR CUTOFF | 4 | 5 | Eliminate the marked commander and any counter-sniper. |
| 25 | CRANE PERCH | 4 | 5 | Survive the full hold timer, then board the helicopter. |
| 26 | CROSSFIRE BAY | 5 | 5 | Eliminate the marked commander and any counter-sniper. |
| 27 | RIVAL DUEL: COBRA | 5 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 28 | DOWNTOWN SIEGE | 5 | 5 | Eliminate the marked commander and any counter-sniper. |
| 29 | RAPID PURSUIT | 5 | 5 | Eliminate the courier before escape or timeout. |
| 30 | MIDNIGHT ESCORT | 5 | 6 | Keep the VIP alive until extraction. |
| 31 | SAFE-HOUSE RUN | 6 | 6 | Eliminate every sentry below the alert limit. |
| 32 | BARRICADE ASSAULT | 6 | 6 | Eliminate the marked commander and any counter-sniper. |
| 33 | CHOPPER DEFENSE | 6 | 6 | Survive the full hold timer, then board the helicopter. |
| 34 | RIVAL DUEL: TITAN | 6 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 35 | PERIMETER SHIELD | 6 | 6 | Keep the VIP alive until extraction. |
| 36 | SPRINT CROSSROADS | 7 | 6 | Eliminate the courier before escape or timeout. |
| 37 | GHOST RECON | 7 | 7 | Eliminate every sentry below the alert limit. |
| 38 | ALLEY PURGE | 7 | 7 | Eliminate the marked commander and any counter-sniper. |
| 39 | BUNKER DEFENSE | 7 | 7 | Survive the full hold timer, then board the helicopter. |
| 40 | COMMAND POST | 7 | 7 | Eliminate the marked commander and any counter-sniper. |
| 41 | RIVAL DUEL: BLACKOUT | 8 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 42 | EMBASSY UNDER FIRE | 8 | 7 | Keep the VIP alive until extraction. |
| 43 | EXPRESSWAY INTERCEPT | 8 | 7 | Eliminate the courier before escape or timeout. |
| 44 | PHANTOM STRIKE | 8 | 8 | Eliminate every sentry below the alert limit. |
| 45 | LZ EXTRACTION | 8 | 8 | Survive the full hold timer, then board the helicopter. |
| 46 | HEAVY BARRAGE | 9 | 8 | Eliminate the marked commander and any counter-sniper. |
| 47 | SKYLINE WATCH | 9 | 8 | Keep the VIP alive until extraction. |
| 48 | RIVAL DUEL: APEX SPIRE | 9 | 1 | Defeat the rival sniper; break laser locks using cover. |
| 49 | FORTRESS COLLAPSE | 10 | 8 | Eliminate the marked commander and any counter-sniper. |
| 50 | FINAL EXTINCTION | 10 | 8 | Eliminate the marked commander and any counter-sniper. |

## Verification

Extraction missions now use three assault waves, with an active-enemy cap of 4–8. Their total enemies across all waves can exceed the table's original threat budget. See `SQUAD_AND_WAVE_PASS.md` for the updated behavior and the seven extraction-node checks.

Compilation, isolated graph/profile/route/reward regression checks, and the full 50-node Play-mode setup/failure smoke test passed. The test includes a dry, supported extraction check for VIP and courier routes. This verifies mission setup and failure guidance, but not that every objective is completable by a human player or that the balance feels fair on Android touch controls. Device playtesting remains necessary. No APK built.
