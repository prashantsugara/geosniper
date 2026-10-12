# Retention flow update

## Opening campaign

Players without an explicit GPS or saved/selected location can play the first three story missions in a labeled local training sector. The terrain uses the existing deterministic offline street grid with fewer central buildings and lower surrounding buildings. A chosen real-world location still overrides training. Local world generation is still required.

The three lessons retain the existing story: identify the scout, protect Mina, intercept the courier. They add short gameplay prompts and face the initial objective. The scout stays still for aiming practice; the courier has at least 18 seconds before moving. Opening sightline selection ignores the player's own collider and searches supported positions when needed. The escort still requires protecting a moving ally; these are not automatic wins.

## Results and progression

One results screen contains story aftermath with existing narration, earned credits/XP, accuracy/headshots/time, level progress, and the next weapon goal or a specific weapon newly available to buy. Rank-up no longer blocks lobby navigation. NEXT MISSION goes directly to the next story briefing so the player can hear/read the mission before deployment. The final mission returns to campaign. Replay and Lobby remain available; View Armory appears for a rank unlock.

The separate aftermath → rewards and sponsored debriefing screens have been removed from the active completion path. Rewards are already deposited, so the primary action no longer says Claim. Optional rewarded doubling remains and cannot pay twice from repeated callbacks.

Currency-ad confirmation text now derives amounts from reward constants: 5 gold or 200 credits, matching payout.

## Ad pacing

No forced campaign transition ad on the first three stages or within the first five minutes. Later transitions additionally require three completed campaign nodes, ad availability and cooldown. Defaults are three completed stages between interstitials and a 180-second cooldown. Existing serialized inspector values may override these defaults, but the opening protection remains enforced. Forced transitions use ordinary interstitials, not rewarded interstitials. Optional rewarded ads and duel-specific pacing remain separate.

## Starter rifle

M24 base damage increases from 20 to 40; new magazine capacity changes from 20 to 5 with +1-round magazine upgrades, and reload changes from 1.45 to 1.8 seconds. Headshots remain lethal for sniper rifles. Purchased damage tiers keep at least their previous damage. Previously purchased magazine capacity is migrated and retained; subsequent upgrades add another round. Duel base balance also changes, so both peers must use the same updated release.

## Validation and limits

Runtime/editor C# compile and isolated Play-mode checks cover navigation, the opening missions, starting sightlines, starter stats, rank-up blocking, ad-policy boundaries and magazine migration. Wide and compact results screenshots are captured for visual inspection.

Actual Android load times, sustained frame rate, thermal behavior, Firebase delivery and retention improvement require an Android device and release data. No APK/AAB or store release was produced. Downloading more scenery was not part of this change.
