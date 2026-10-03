# Opening encounter prototype

Campaign stage 0, when configured as TargetIdentification, now uses the existing validated target/nest setup with a paced introduction. It is presented as The Rooftop Meeting; a rooftop player vantage depends on the real sector and is not guaranteed.

- Eight-second observation lead-in; centre the stationary contact in the scope with line of sight for three accumulated seconds. Progress decays slowly when looking away. Map/bullet camera pause observation.
- Escorts patrol during observation without attacking; contact turns toward an escort. No bespoke handshake/arrival animation has been authored.
- Fire before identification gives guidance without consuming ammo. First accepted shot releases normal enemy combat. Misses do not immediately fail the mission; the contact remains stationary and can be reacquired.
- Only the contact (and counter-sniper if one exists) is required, not every guard. Completion delay extended to 2.8 seconds and still waits for bullet camera.
- Existing setup validation is retained. New selectable alternate-vantage routes, automatic encounter regeneration, and full GPS-map playability testing remain outstanding.

Weapon handling: per-rifle recoil and chamber intervals, exponential frame-rate-stable recoil recovery, no magazine reload sound on each shot, simulated bullets no longer replay the shot/flash at impact. Existing rifle meshes and materials retained. No new polished weapon mesh or textured art asset is claimed: Blender inspection timed out. Need a current in-game rifle screenshot and working asset-review connection for the visual redesign.

Validation: Tools/test_meeting.ps1 tests pure encounter state transitions; Tools/check_code.ps1 compiles runtime and editor sources. Unity playback and Android frame rate/visuals are unverified. Existing Unity preview licensing/MCP blockers remain.
