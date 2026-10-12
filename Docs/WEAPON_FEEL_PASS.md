# Weapon feel pass

Campaign fire no longer calls presentation recoil before BallisticsSystem calls it again. Chamber cooldown no longer masquerades as magazine reload time in the presentation.

M24 and M40 bolt handling follows each rifle's firing cadence, with lift, backward pull and return phases. Brass ejects during the pull; semi-auto weapons eject immediately. Mechanical clicks accompany bolt movement and magazine withdrawal, seating and latch completion. Reload cues use the actual reload duration, including durations above three seconds. Casing material and clink audio are reused instead of created for every shot.

Confirmed hits appear both scoped and unscoped. The short four-part marker leaves the center visible; confirmed kills use gold. Existing rifle report, impacts and recoil tuning remain in use.

Validation: runtime and editor source compilation passed. Unity handling checks passed all eight weapon poses, recoil recovery at 30/60/120 fps, bolt sequencing and reload audio phase checks, including a 4.5-second reload. Saved report: output/weapon-feel/Checks.txt. The final HUD addition passed compilation. Android playtesting and subjective audio/animation review remain necessary; no device performance or retention gain is claimed.
