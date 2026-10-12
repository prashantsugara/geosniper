# Deployment recovery

9 October 2026: GA4 for 1.0.4 showed 10 deployment failures from two users. Historical failure categories did not identify their cause; these changes must not be described as reproducing those specific failures.

- The real-map loading driver now steps nested map/elevation enumerators under exception handling. An exception returns to recovery immediately instead of leaving loading active until the watchdog expires.
- The recovery screen offers PLAY BUNDLED SECTOR for solo deployments. It preserves campaign node and stage, including briefing approval, rather than changing to unrelated free practice. Multiplayer cannot use this local fallback.
- Failure reasons now separate map/location/world/player errors and timeouts, missing cached sectors, missing shaders, unsafe spawns and exhausted memory. Raw exception text and coordinates are not sent to analytics.
- Existing GPS failure handling remains explicit; the fallback requires the player's selection and does not silently substitute a real-world location.

Runtime and editor compilation passed. The injected timeout regression passed: recovery preserves the contract and reaches InProgress. The saved-GPS deployment check also passed. The broader flow run subsequently failed its victory scope/zoom check; that issue remains unresolved and is recorded in output/deployment-recovery/Checks.txt. Android timing and the cause of the historical failures still require device/release verification. Register reason and load_phase in GA4 to inspect new failures.
