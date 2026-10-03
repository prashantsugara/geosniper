# Geo Sniper promotional assets

## Deliverables

- `geosniper-feature-1024x500.png`: exact Google Play feature graphic, RGB PNG with no alpha.
- `geosniper-gps-trailer-1080p.mp4`: 30 seconds, 1920 x 1080, 16:9, 30 fps, H.264 High / AAC stereo, BT.709, fast-start MP4.
- `geosniper-original-score.wav`: original 30-second synthesized instrumental, "Local Coordinates", 96 BPM, stereo 48 kHz. No third-party music, loops or samples.
- `trailer-storyboard.jpg`: six-frame overview for review.
- `geosniper-youtube-thumbnail.jpg`: matching 1280 x 720 YouTube thumbnail.
- `geosniper-feature-master.png`: original higher-resolution artwork used in the video closing card.
- `geosniper-feature-refined-master.png`: higher-resolution feature artwork with a clear center for the Play Store video play button.
- `feature-art-prompt.txt`: exact image-generation prompt, using the built-in image tool and `Assets/Textures/GameLogo.jpg` as a brand reference.
- `feature-art-refinement-prompt.txt`: typography adjustment used to keep the Play Store video play-button area clear.
- `edit-manifest.json`: clip timecodes, source details and encode settings.
- `geosniper-trailer-en.srt`: optional English descriptive captions; there is no spoken narration.
- `render-promo.mjs`: repeatable edit and soundtrack generation. Requires Node.js and FFmpeg; set `PROMO_FFMPEG` if using another FFmpeg location.

## Footage and review

The moving footage comes from this project's `issues/issues.mp4`, dated September 16, 2026. It is recorded Android gameplay, not generated gameplay. The source is 1280 x 576; the export scales the complete frame proportionally into a designed 1920 x 1080 layout. This is an upscale, not a native 1080p capture. The top and bottom panels carry branding and readable feature captions.

The connected phone was unavailable during production. Compare the trailer with the current release before publishing, especially the scope/HUD and map visuals. The older recording includes the earlier weapon selector and other earlier presentation details. Replace source clips in `render-promo.mjs` with fresh recordings if those changes materially affect what players will see. The existing film is a completed promotional edit, but it is not verification of the latest APK.

Only the final five-second closing card uses generated promotional artwork. Around 83% of the video shows actual gameplay/map interaction, with brief crossfades. The artwork is an illustration of the premise, not a screenshot or a promise of photorealistic graphics. No competitor or inspiration footage was used. The phone recording's original audio is not included.

## YouTube upload

1. Upload `geosniper-gps-trailer-1080p.mp4` to your YouTube channel.
2. Use the title and description below. YouTube requires you to answer its audience/content settings accurately for your game.
3. Set visibility to **Public** or **Unlisted**; do not use Private.
4. Ensure embedding is allowed. The Play Store preview must not be age restricted.
5. Turn off monetization for this video if that option is available. Check that it has no third-party Content ID claims that would cause ads to appear. These settings concern the YouTube video, not the game's AdMob ads.
6. Wait until YouTube finishes its 1080p processing.
7. In Play Console, open your main store listing. Upload `geosniper-feature-1024x500.png` as the feature graphic and paste the video's normal YouTube watch URL into **Preview video**. Use a video URL, not a channel/playlist link or a link with a start-time parameter.
8. Preview the listing on a phone with sound muted as well as enabled.

No YouTube upload or publication has been performed by this task.

Suggested title:

Geo Sniper | GPS Sniper Missions in Real-World Locations

Suggested description:

Your city. Your next mission.

Explore map-based streets, track moving targets and line up your shot in Geo Sniper, a 3D sniper shooting game built around real-world locations. Use optional GPS to load a nearby sector, or search for a place by name.

This trailer features captured Android gameplay. Map coverage and detail vary by location, and environments are adapted for gameplay. All missions and characters are fictional. Internet is required to download new map areas.

Support: bittruth1solutions@gmail.com

Suggested feature-graphic alt text (under 140 characters):

A sniper overlooks a city linked to a glowing map pin. Geo Sniper: Your city. Your mission.

## Official requirements checked

Google Play preview asset guidance:
https://support.google.com/googleplay/android-developer/answer/9866151?hl=en

This specifies the 1024 x 500 feature graphic in JPEG or 24-bit PNG without alpha; public/unlisted, embeddable YouTube preview videos without ads or age restrictions; and recommends actual gameplay early, representative experience for at least 80% of the video, readable copy, and concise pacing.
