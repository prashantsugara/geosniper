# Commander voice and opening operation

The prior Windows Zira narration is replaced with offline neural speech generated using Kokoro 1.0. The commander blends Michael (65%) and Fenrir (35%), with a slightly measured delivery and natural sentence pauses. The first three spoken briefings are concise radio orders. All 50 story briefings and aftermaths use the same voice; the written story remains available. A separate line announces the opening extraction. No voice model, Python runtime or network speech service is included in the game.

Generation: install `kokoro-onnx` into `.utmp/kokoro-runtime`, place `kokoro-v1.0.onnx` and `voices-v1.0.bin` in `.utmp/voice-models`, and run `Tools/generate_story_audio.py` with Python. `--preview` writes a sample under `output/commander-voice`. The manifest records the exact spoken text, hash, voice and clip duration. Existing Unity audio metadata retains mono Vorbis compression.

Voice model: [Kokoro 82M](https://huggingface.co/hexgrad/Kokoro-82M), Apache 2.0. Development inference wrapper: [kokoro-onnx](https://github.com/thewh1teagle/kokoro-onnx), MIT. The earlier Piper candidates were not used for the shipped narration.

The opening scout contract seeks a supported rooftop and interior window in the same accessible building, both with a clear target view. Its surviving guards patrol and respond after the scout is eliminated. The green extraction beacon requires a four-second hold that resets outside the zone or on damage. Unsupported downloaded sectors retain their safe existing opening position rather than spawning an unsafe interior route.

Retention remains an evaluation goal. Android aim feel, loading, frame time and return rates need device and release testing.

Validation: the final Unity desktop check passed all 100 narration imports and playback/mute/release checks, opening rooftop/window selection, extraction hold and reset, window traversal, enemy weapon turns, and all 50 mission setups. All 101 generated WAV files passed mono PCM format, manifest text-hash and clipping checks. These checks do not replace a full campaign playthrough or Android device testing. The saved report is output/opening-operation/Checks.txt.
