# Disarmed guards and enemy gunfire

Disarm cancels aim, active bursts and warning effects immediately. Dropped rifles remain physical objects for recovery. The guard takes cover during incoming fire, suppression, a close visible opponent or severe injury. After at least four seconds, a healthy guard may approach its gun if the destination is supported, reachable and within twelve metres. A stationary 1.4-second crouched pickup restores the original weapon attachment, removes drop physics, reenables the weapon anchor, and imposes a settling/firing delay. Incoming pressure resets pickup progress. Unrecoverable dropped rifles expire after thirty active seconds; mission cleanup also removes them. Disarmed couriers retain their escape movement.

The final character pose overrides armed hand poses after authored and fallback animations. Defensive hands stay near the torso; pickup hands reach down. This does not replace locomotion, death or civilian animation.

Enemy bursts, VIP attackers and counter-snipers use the bundled HeavyRifleShot recording, with the existing heavy-rifle generator as fallback. Report playback uses consistent logarithmic attenuation, zero Doppler and bounded pitch. The counter-sniper pitch is held for the report instead of reset immediately. Radio sources remain separate.

Validation: C# compile, disarm/no-fire and interrupted pickup checks, restoration of attachment and firing delay, recorded clip selection and spatial settings, actual SWAT and ch35 arm/leg pose checks, and all fifty campaign setup regressions. Android sound balance and live recovery under combat still need device playtesting.
