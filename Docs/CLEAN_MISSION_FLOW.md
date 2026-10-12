# Clean mission flow

Continue Story → mission preparation → Play Mission → loading → gameplay.

Preparation combines mission selection, the full story briefing, objective, location and current loadout. Changing the location or opening the armory is optional. Weapon power recommendations appear inline rather than interrupting deployment. Difficulty remains in Settings.

The lobby has three main choices: Continue Story, Multiplayer and Practice Range. Armory, location, rewards and settings remain available. Duplicate multiplayer entry buttons, the separate story modal, false co-op label, decorative telemetry and extra pre-play confirmation have been removed.

If no location exists yet, Play Mission opens the location picker directly. A searched place preserves the selected campaign mission. The location picker explains optional GPS use. A saved GPS choice goes straight to loading; the operating system still requests location permission when needed. After the briefing has been reviewed, successful loading begins the mission immediately. Direct mission setup without a reviewed briefing retains its safety screen.

## Narration

All 50 mission briefings and 50 aftermaths have bundled neural commander narration generated with Kokoro. The preparation screen plays its story automatically. Narration can be turned off; the choice persists. Leaving preparation stops speech, and lobby music lowers while it plays. The aftermath plays its matching narration and stops when opening rewards or leaving the mission. Written story remains visible. See COMMANDER_VOICE_AND_OPENING.md for the generation workflow and voice sources.

Clips are mono 22.05kHz, imported with Vorbis compression and loaded as needed. The source scripts are recorded by text hash in Assets/Resources/Audio/Story/manifest.json. Regenerate changed lines on Windows with Tools/generate_story_audio.ps1; use -Force to regenerate everything.

This flow does not change Relay hosting, joining or gameplay. Android device playback and touch performance still require a device check.
