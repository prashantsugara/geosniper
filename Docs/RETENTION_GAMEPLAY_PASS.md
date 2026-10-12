# Opening gameplay and mission continuation pass

This pass targets the first three contracts and the transition after a win. It preserves landscape play and the earlier Mixamo death-animation revert.

## Changes

- Next Mission prepares and starts the next unlocked contract in the loaded sector. Its story briefing plays over gameplay instead of requiring another deployment screen.
- The first three mission result screens omit the optional reward-doubling ad offer. Existing early interstitial protection remains.
- Result screens show the mission's existing replay challenge, with a compact layout for shorter displays.
- Opening scope prompts give mission-specific advice and stop after the first shot. The first contract prefers a supported, visible elevated starting position.
- Campaign surface impacts now use four pooled spatial audio voices for concrete, metal and water. Rifle reports receive a subtle synthesized reflection tail, reduced indoors and with suppressors. These are supplemental effects, not new recorded weapon assets.
- Generated enterable buildings have lower window sills and taller open bays so the standing player capsule can pass at jump height. This changes newly generated sectors; an already loaded sector must be reloaded. Decorative windows on non-enterable scenery are not openings.
- Custom analytics events include app_version. mission_first_shot records time to the first shot; opening_scope_used records time to scope use in the opening contracts; next_mission_selected records the transition between contracts. Existing consent rules apply.

## Evaluation

Source compilation passed. The Unity review run passed direct next-mission advancement, four pooled impact voices, the three opening sightlines, all 50 mission setups, reachable objective security, and The Leak's extraction reset checks. In the generated test sector, 708 window openings cleared a standing capsule at jump height. This verifies opening collision clearance, not a recorded player-controlled jump through every window. Results were captured and visually inspected.

Compare loading completion, first-shot delay, first-mission completion, next-mission selection and later return rates by released app version. These changes are hypotheses for improving retention; they do not demonstrate an improvement before release data is available. Unity Analytics custom event schemas may need the new events and parameters configured in the dashboard.

The automated campaign checks cover valid mission setup, initial sightlines, indoor objective placement and nearby reachable security. They do not replace full mission playthroughs, testing arbitrary downloaded map locations, or Android frame-time and memory profiling.

This pass does not constitute a complete overhaul of AI, all interiors, daily contracts or weapon animation. Those existing systems remain and need further focused playtesting and authored polish.
