# Objective security audit — 8 October 2026

Cause: indoor objectives were chosen after the ordinary street enemies had been placed. Only The Leak moved enemies towards document sites, leaving other evidence and relay levels with distant patrols.

Evidence, sabotage and rescue objectives now share objective guard placement. Candidates are sampled around the objective on its own floor, with a connected NavMesh approach; nearby street points are a fallback. Guards must remain at least 2.5m from the interaction position and 10m from the player's initial position. Each site requires nearby security within 24m; unusable sites produce the existing sector setup error rather than quietly starting unguarded.

Early missions request one guard per site. From level 11, and throughout The Leak, sites request two where safe positions permit. Existing ordinary enemies are reassigned first, with additional guards only when the current roster is insufficient. Marked targets and counter-snipers retain their roles. The Leak retains its bounded response waves and extraction hold. Documents can still be recovered stealthily; there is no forced kill requirement.

Validation checks every campaign setup in the training sector and checks reachable nearby guards for every document, relay and rescue objective. Combat, overwatch, interception, escape and rival missions retain their existing enemy roles and are included in the setup sweep. This is not a full 50-level playthrough or validation of every downloaded geographic sector.

Report: `output/objective-security/campaign-checks.txt`.
