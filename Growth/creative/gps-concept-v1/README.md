# Your city. Your next mission. — concept promo

26-second animated concept film. **No gameplay recording appears anywhere.**

The fictional adult player is a newly generated image from the built-in image tool; its exact prompt is in `player-prompt.txt`. The character photograph uses camera motion, not generated live-action movement. The map, typing, camera travel, GPS rings, taps, cards, route and selection are deterministic Canvas motion graphics. The map is invented, labeled illustrative and makes no live GPS/network requests. Pune is an example city, not the user's detected location. Riverside District and Rooftop Recon are fictional story elements, not promises of exact released content.

## Story

| Time | Beat |
| --- | --- |
| 0–3.2s | Fictional player with a phone; “What if your city was the map?” |
| 3.2–8s | Type an example location and tap the simulated GPS option |
| 8–12s | Fly into a stylized isometric district with mission pins |
| 12–17.8s | Browse three fictional mission concepts; select Rooftop Recon |
| 17.8–21.8s | Confirm the mission with tap feedback and a success chime |
| 21.8–26s | Geo Sniper / coming to Android / follow for launch |

Persistent label: **CONCEPT DEMO · SIMULATED UI · NOT GAMEPLAY**. Keep this label and the caption disclosure when posting. The pre-launch CTA does not claim Play Store availability. No public account handle is invented.

Music is the project's original synthesized score. UI taps, sweeps and confirmation chimes are synthesized by the renderer; no third-party recordings or licensed songs are used. Review final rights, accuracy, accessibility, phone readability and sound before approving promotion.

## Re-render / edit

Edit `scene.mjs` for the motion design or `render.mjs` for the sound and timeline capture. No API key or paid video service is required for re-rendering with the existing player image.

```powershell
node Growth/creative/gps-concept-v1/render.mjs portrait new-concept-portrait.mp4
node Growth/creative/gps-concept-v1/render.mjs landscape new-concept-landscape.mp4
```

The renderer uses local Playwright/Chromium and FFmpeg already installed on this computer. For another computer, configure `GROWTH_PLAYWRIGHT_MODULE`, `GROWTH_CHROMIUM` and `PROMO_FFMPEG` locally as needed. Outputs are versioned and existing files are never overwritten. Validation keyframes and manifests are stored in `Growth/data/concept-qa`.

The HTML preview supports play/pause, seeking and story-beat buttons. The exported MP4 is a linear video, not a playable app. This simulated promo is for clearly labeled social concept content; do not substitute it for required authentic app screenshots or represent it as gameplay in a store listing.

## Account direction

Use a dedicated Geo Sniper account. `@underlyinglogictech` has not been used or changed. Create/authorize the dedicated account yourself, verify handle availability and connect that account before approving publishing. No social account is created, renamed or published to by these scripts.
