# Objective floors and mission endings

Document recovery preserves the file retrieval and withdrawal flow. Relay sabotage has visible breaker movement during the three-second power isolation, alerts surviving guards, and requires six uninterrupted seconds at extraction. Leaving extraction or taking damage resets this hold. The Leak retains its separate two-archive counterattack and twelve-second extraction.

Accessible multi-floor objective buildings select a varying upper floor where possible. On entering the building, Command names the target floor in an eight-second HUD message, a waypoint label, and bundled commander audio. Ground floor is explicitly named; upper storeys use floor 2 onward. Guidance is issued once per objective, including another objective in the same building.

Initial objective security occupies every non-objective floor, with supported NavMesh placements and reachable approaches. Single-floor objectives retain nearby security. Alerted guards can naturally pursue the player between floors.

Victory immediately exits scope and binoculars, restores the 65-degree camera, and animates the blocked weapon in its lowered pose. Results show earned stars, a brief gold burst, reward count-up and the existing victory fanfare, with direct Next Mission navigation. Failure and legacy First Contact completion also close aiming.

Validation: C# runtime compile; disposable Unity campaign checks across 50 mission setups, floor security, one-shot HQ messages, indoor interactions, relay extraction hold reset, scope reset and wide/compact result captures. Six floor radio clips use the established local Michael/Fenrir commander voice, mono 24kHz source, imported with Vorbis compression. Android device playtesting is still required.
