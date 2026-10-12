# First-play retention changes

The Analytics review found 95 first opens but only 20 distinct users recording any level start. These aggregate counts are not an ordered cohort funnel and do not prove a cause. First deployment previously required location selection followed by GPS/map/elevation work.

## Faster first mission

The first incomplete story mission uses an explicitly labeled training sector when the player has neither a selected/saved location nor an explicit GPS choice. The existing offline world generator runs locally; the reviewed story starts directly after generation. It still requires local asset loading and world generation, so instant startup is not promised. Choosing a real location overrides this route. Later missions keep the existing real-map flow.

The lobby says START STORY for the first incomplete mission. The location label explains that the training sector needs no GPS or download. Loading shows a preparation stage and elapsed seconds; the progress strip represents steps, not network download percentage.

## Measurement

All new events use the existing analytics-enabled and consent checks. They include no coordinates, place names, raw exception messages or device identifiers.

- campaign_deploy_clicked: stage_index, map_mode.
- deployment_started/completed/failed/abandoned/backgrounded: stage_index, map_mode, load_phase, duration_sec, reason.
- map_load_started/completed: same deployment parameters. Offline training bypasses download events. Reusing a prepared sector records map completion with cached_sector.
- first_mission_started/completed/failed: stage one activity, repeated attempts included. Successful completion is separate from level_end, which also includes failure. PvP stage -1 is excluded.

Backgrounding is a diagnostic signal, not proof of abandonment. A killed app may not deliver an app_quit event; absence of deployment_completed in an ordered user funnel is the more useful signal. Attempts are not assigned persistent identifiers, so do not join repeated attempts solely by counts.

After deploying a new app version, validate events in Firebase DebugView on an Android device. Register custom dimensions for map_mode, load_phase and reason, and use App version to compare releases. Compare first_open → campaign_deploy_clicked → deployment_started → first_mission_started → first_mission_completed; separately inspect real-map loading and failures. The new data does not backfill previous releases.

Validation: C# runtime and editor compile; isolated Unity first-play checks. Android startup timing and Firebase delivery require a device build. No Play Store release was performed.
