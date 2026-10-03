# Campaign progression and fixes

Historical progression notes from 2026-09-19. The campaign has since expanded to **50 authored nodes**; see `CAMPAIGN_LEVEL_AUDIT.md` for the current level list and validation. The table below describes the earlier 14-node graph and is retained for background.

## Easy to hard

| Map node | Operation | Tier / label | Maximum hostiles |
|---|---|---|---|
| 1 | First Contact | 0 / Training | 2 |
| 2 | VIP Overwatch | 1 / Easy | 3 |
| 3 | Fugitive Sprint | 1 / Easy | 4 |
| 4 | Rooftop Sniper Showdown | 2 / Easy | 1 rival |
| 5 | Silent Infiltration | 3 / Standard | 5 |
| 6 | Syndicate Lieutenant | 3 / Standard | 5 |
| 7 | Hot Extraction | 4 / Standard | 6 |
| 8 | Dockyard Intercept | 5 / Hard | 6 |
| 9 | Subterra Ghost | 5 / Hard | 6 |
| 10 | Highrise VIP Escort | 5 / Hard | 6 |
| 11 | Citadel Command | 6 / Hard | 7 |
| 12 | Razor Wire Infil | 7 / Expert | 8 |
| 13 | Apex Duel Spire | 7 / Expert | 1 rival |
| 14 | Final Extinction | 8 / Expert | 10 |

Names identify contracts, not bespoke environments: missions use the available sector geometry. Safe spacing and roof availability can reduce the actual count. Briefings show the spawned count; the campaign map shows the budget.

Training requires eliminating both stationary enemies. It has no time limit, counter-sniper or nearby civilian distractions. Later HVT contracts require the commander and any counter-sniper; bodyguards are supporting threats. Courier missions require the courier, stealth requires all sentries, and VIP missions allow either clearing threats or reaching extraction.

## Difficulty composition

`DifficultyProfile.Selected` (Casual / Standard / Hardcore) is the baseline. `CampaignProgression.CreateProfile` clones it and applies tier multipliers. No profile is compounded on retry. Standalone AI duels retain their baseline, without campaign modifiers.

The following factors are interpolated linearly across tiers 0–8, then multiplied by the selected baseline:

| Parameter | Training | Expert |
|---|---:|---:|
| Enemy health | 0.70x | 1.18x |
| Enemy damage | 0.45x | 1.30x |
| Movement speed | 0.78x | 1.10x |
| Detection speed | 0.50x | 1.25x |
| Reaction delay | 1.80x | 0.85x |
| Interception time allowance | 1.55x | 1.00x |
| VIP health | 1.60x | 1.00x |
| Duel laser-lock time | 1.70x | 0.90x |
| Counter-sniper laser-lock time | 1.60x | 0.95x |

Counter-snipers begin at tier 3. Early stealth adds one alarm allowance; later stealth uses the selected baseline. Survival divides its tier-dependent hold duration by the time allowance, so Casual means a **shorter** hold, not a longer one.

Courier speed is also capped using route length, waypoint rounding and extraction radius. Increasing the displayed countdown alone would not help a player whose courier escapes early. Routes must have connected, unobstructed legs without jumps between floors; invalid layouts return a retryable setup error.

## Other fixes

- Removed the six-node test unlock: new saves begin at First Contact. Existing completed nodes remain accessible.
- Authored mission type no longer depends on `stageIndex % 5`. Later VIP, stealth and finale nodes now launch the advertised objective.
- Node completion saves progress only. Mission rewards pay once per victory, with replay rewards still supported.
- Campaign identity travels with the launched mission. Standalone duels cannot complete or pay out a stale selected campaign node.
- Duel restart preserves the mission type and campaign tier. Offline practice clears pending campaign/duel launch state.
- Enemy counts use authored budgets. Early missions no longer receive the same elite threats as later ones.
- VIPs and enemies pause during briefing and map inspection. Campaign countdowns and scored duration pause on the map as well.
- Missing HVT/rival references no longer count as kills. Failed setup no longer silently replaces a contract with free-roam enemies.
- Vantage selection requires a supported, unobstructed position and a line of sight. No invented floating rooftop fallback; invalid duel rooftops fail safely.
- Objective failures and invalid layouts do not offer an unusable death-revive ad.

## Validation and remaining work

`Tools/check_code.ps1` compiles runtime and editor code. `Tools/run_review_checks.ps1` runs progression/unlock, route, reward and existing geometry/combat regression checks in `.utmp/review-unity`. After it exits, `Tools/run_campaign_checks.ps1` exercises all 50 current authored missions against a generated offline world in Play mode, including briefing/map suspension, failure guidance, and duel restart. Reports are written to that isolated project's `Logs` folder.

These are implementation and smoke tests, not a complete human balance study. Before release:

- Play every branch with the starter M24 on an Android device in all three settings; tune first-attempt success rates and time-to-kill.
- Test varied live-map geometry, long routes, narrow streets, cover, rooftop edges and extraction accessibility. Offline fixtures do not prove every downloaded map is playable.
- Add short, interactive scope/hold-breath/reload lessons, objective-specific failure hints and checkpointed multi-part missions.
- Author recognizable combat spaces and cover arrangements; increase tactical complexity rather than relying only on health/damage multipliers.
- Profile low-end Android frame time, memory, heat and crashes. Add LODs and reduce heavy imported asset draw calls as tracked in `GAMEPLAY_ASSET_REVIEW.md`.
