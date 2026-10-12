# Window traversal and enemy rifle turns

Jump near an open window while moving or facing through it triggers a short vault. Generated enterable buildings register each opening's direction, width and sill height. The player checks the capsule path and a dry, supported landing before starting. The controller remains enabled throughout; a new obstruction, death or blocked input cancels the movement. Windows with a large drop do not receive automatic vault assistance; normal jumping remains available. Reload the sector to generate the opening metadata.

Enemy rifles previously aimed directly at the player even during a large body turn, pulling both arms across or behind the torso. Aim now stays within 35 degrees of the body in yaw and pitch until the body catches up. Enemy arm IK uses a stable elbow direction instead of feeding the preceding solved elbow position into the next pose. Shared first-person and lobby IK retain their existing behaviour.

Visual review also found an upright imported rifle despite valid hand contacts. The enemy rifle now aligns its stock-to-barrel direction to the weapon's forward axis and uses the magazine to determine the upright direction. Unknown models preserve the FBX root rotation when applying the fallback correction. A geometry check catches a rifle whose barrel disagrees with its aim axis.

Source compilation and the Unity review checks cover generated window entry and exit, obstructed-opening rejection, both weapon hand contacts during half turns and yaw wraparound, opening sightlines, and all 50 mission setups. These are desktop checks; touch control feel and device frame times still require Android playtesting. The previously reverted Mixamo death change is not reintroduced.
