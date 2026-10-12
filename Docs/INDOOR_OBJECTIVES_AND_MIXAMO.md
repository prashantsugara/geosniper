# Indoor objectives and character animations

Evidence recovery uses printed documents on folders, placed on reachable indoor desks. Tap PICK UP DOCUMENTS or hold E while nearby and facing the item. Successful collection removes the papers and plays a pickup sound.

Sabotage uses indoor electrical relay enclosures with metallic surfaces, vents, screws, power cables, identification labels, a red disconnect lever and a green power lamp. Tap SWITCH OFF RELAY or hold E for three seconds. The enclosure remains after shutdown; the lever flips and the lamp goes dark. Patrols respond to the outage. Each unfinished relay/document appears on the full map with a direction line, distance and INSIDE label, including a clamped edge marker when off-screen. Deployment chooses a street point with a complete navigation route to the indoor objectives.

The Leak requires both guarded archives, four seconds per collection, bounded patrol responses (at most two responses and four added enemies), and a twelve-second extraction hold that resets when leaving the zone. Damage interrupts collection. Briefing narration was regenerated.

Five motions downloaded from the signed-in official Mixamo website are retained under Assets/ThirdParty/MixamoSource, outside Resources. Only 13 retargeted animation clips ship in Resources: enemy walk/hit for SWAT and CH35; civilian walk/run/fear for Carla, Eric and Doctor. Authored civilian animation takes priority over procedural fallback. Enemy fire/hit retains the existing upper-body mixing policy. Source provenance is in Store/MixamoAnimationSources.json.


Checks cover all 50 playable stage setups in the training sector, indoor pickup/shutdown across every evidence/sabotage mission, The Leak response/hold boundaries, all animation curve bindings and silhouette height, and civilian walking/fear state playback. These checks do not replace a complete playthrough or testing varied downloaded geographic maps and Android frame time/memory.

The visual-quality target and all 50 level audit entries are in CAMPAIGN_VISUAL_STANDARD.md. Current interiors, lighting and other mission props still need further art passes before describing the game as AAA.
