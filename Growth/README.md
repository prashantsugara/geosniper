# Geo Sniper Growth Studio

A free, local organic-promotion workflow for **YouTube Shorts + Instagram Reels**. Built for a game awaiting Google Play approval. No subscription, paid AI calls, cloud hosting, follower bots, paid ads or new game telemetry are required. Your PC's internet, storage and electricity still apply; platform APIs have quotas and eligibility requirements.

## Start here

1. Install/use Node.js 24+ (already installed on this computer).
2. Run `Growth/Start-GrowthStudio.ps1`, or run `node Growth/server.mjs` from the repository root.
3. Open **http://127.0.0.1:4318**. The launcher starts Node hidden; the terminal command stays in the foreground.
4. Open **Content queue**. A first plan starting September 21, 2026 has been prepared: 12 concepts, 24 channel drafts. Generate a later plan when needed; overlapping dates are not overwritten.
5. Open **Video studio**. Preview the three starter renders, or put fresh 1080×1920 H.264/AAC MP4s in `Growth/media` and register their filenames. The current starter footage is from September 16, not a verified recording of the latest release.
6. Watch the WHOLE clip. Verify current-build accuracy, rights, audio, phone framing, no private coordinates, no account details, no reference footage from another game. Only then select **Confirm video review**.
7. Review a queue item, attach the appropriate video, edit its caption and time, **Save as draft**, then **Approve saved draft**. Match footage to the storyboard. Do not reuse one starter video for every concept.
8. In default **upload-pack** mode, approved due posts are copied to `Growth/exports/<post-id>-r<revision>/` with video, caption, metadata and upload instructions. Upload through YouTube Studio / Instagram or schedule with their native tools where available. Paste the actual published URL back into the post.
9. Record cumulative results at 24h and 7d. Download the report in **Results & learning**.

Nothing was posted to a social account during implementation. No account has been connected for you. The optional Windsor.ai plugin suggested during setup is **not required** for this system.

## What runs automatically

- Local template-based four-week campaign generation: GPS/map stories, precision moments and build diaries. This is not a remote AI generator.
- One-click vertical video rendering with the project's original synthesized music. No licensed song subscription or watermark from a paid service.
- A durable SQLite queue. The running server checks due approved posts every minute.
- In export mode: creates local upload packs. In API mode, with credentials and the enable switch: delivers to the selected official platform API.
- One delivery per channel per rolling 24h, atomic claims, media SHA-256 verification, approval invalidation after edits, and restart recovery.
- Missed slots older than 12h need a new schedule. Ambiguous failures are never blindly retried; check the platform first, record the published URL if present, or explicitly confirm there was no upload before resetting the draft.
- Results use the latest cumulative observation per post, not the sum of repeated snapshots. Unknown values stay unknown.

**Not automated:** capturing new gameplay, deciding whether content is truthful, your account consent, community replies, native analytics collection, store-approval detection, or making the computer stay awake. These are intentionally not faked. No public posting occurs merely because credentials exist.

The local scheduler runs only while Node and this PC are awake. It is not a cloud service or a Codex recurring task. Closing the dashboard tab does not stop Node. To stop a foreground launch, press Ctrl+C in its terminal. For the hidden launcher, identify the Node process whose command line is this Growth server in Task Manager; do not kill unrelated Node processes. Only one server should run on port 4318. Back up `Growth/data` with the server stopped; SQLite WAL files belong with the database.

## Free account setup

The fastest free path is export packs plus each platform's native upload tools. Direct API automation is implemented but **has only been tested with mocked platform responses**, not your real accounts. Do a private YouTube upload and a deliberately approved Instagram test before enabling routine publishing.

Never send passwords or API secrets in chat. Copy `Growth/env.example` to `Growth/.env` locally, fill values there, and restart the server. `.env`, SQLite data, exports and generated media are gitignored. Local secrets are plaintext on disk: protect your Windows account and backups; never share `.env` or host this app publicly. The UI reports only whether credentials are present, not their contents.

### YouTube Shorts

1. Create/use your Geo Sniper YouTube channel. Create a Google Cloud project and enable **YouTube Data API v3**.
2. Configure the OAuth consent screen for your own use and add your Google account as a test user if the app is in testing. Create an OAuth client of type **Desktop app**.
3. Put its client ID and secret in `Growth/.env` as `YOUTUBE_CLIENT_ID` and `YOUTUBE_CLIENT_SECRET`.
4. Run `node Growth/connect-youtube.mjs`. Open the printed Google consent link yourself and choose the intended channel. The helper uses a loopback callback, PKCE and a single-use state token. It saves the refresh token in `.env`; it never uploads a video.
5. Restart Growth Studio. Use **Launch & channels → Official platform APIs**, keep YouTube visibility **private**, and enable delivery only when you are ready to test an individually approved post.
6. Verify the uploaded video in YouTube Studio. Only then switch future posts to public if your API project is eligible. Unverified API projects can have uploads forced to private; the app displays `uploaded / verify` for private, unlisted or unconfirmed uploads and marks published only when the API actually reports public. Changing visibility resets approvals.
7. Test-mode OAuth tokens can expire or be revoked; reconnect when required. Public/distributed use may require OAuth verification and a YouTube API compliance audit. Do not assume a new API project can publish publicly unattended.

The videos are 9:16 and 12 seconds; YouTube determines Shorts classification. The upload declares `selfDeclaredMadeForKids=false`, reflecting this realistic sniper game's intended audience. Review all platform audience/disclosure settings yourself.

### Instagram Reels

This adapter uses the **Facebook Login** variant of Meta's official API, not the separate Instagram Login flow.

1. Use an Instagram **Business** account linked to a Facebook Page you manage. Create a Meta developer app with the applicable Instagram/Facebook Login setup.
2. Authorize your account using Meta's official tools. The official sample uses `instagram_basic`, `instagram_content_publish`, `pages_show_list`, `pages_read_engagement` and `business_management`; applicable access and app review depend on your account/app setup. Use the permissions required by your chosen current configuration.
3. Obtain the Instagram business-account ID and an appropriately authorized access token. Add `INSTAGRAM_USER_ID`, `META_ACCESS_TOKEN` and the supported `META_API_VERSION` shown in your app dashboard to `.env`. Do not paste credentials into the dashboard.
4. Restart. Review a single intended Reel, select API delivery, enable it and approve that post. **Instagram API publishing is public; there is no private test mode here.** If not ready for a public post, keep using export packs.
5. The connector creates a resumable container, uploads local bytes to Meta, checks processing, then publishes once. No public video hosting is required. Tokens expire; you remain responsible for renewing them and maintaining app permissions. App review/advanced access may be required outside your app's authorized test roles.

If the platform rejects a request, the item becomes `needs review`. Raw token-bearing responses and upload URLs are not exposed in the UI. Always check whether a post already exists before rescheduling.

## When Google approves the game

1. Verify the public Play Store listing works for your intended countries/device eligibility.
2. Enable **The game is publicly live** in Launch & channels. New plans now use store CTAs and channel-specific Play referrer links become visible.
3. Existing drafts are not silently rewritten. Update captions, replace pre-launch end cards if needed, and reapprove them. The three built-in video presets deliberately say “Coming to Android”; use newly captured/rendered launch videos for launch posts.
4. Put the YouTube campaign link in your channel profile and Instagram link in your bio. Shorts descriptions/comments do not make external URLs clickable. Use truthful “link in profile/bio” wording, not “tap below.”
5. Links do not automatically record clicks or installs. Use verified Play Console/acquisition data or add properly disclosed attribution later. A single bio link is channel/campaign-level attribution, **not reliable per-post attribution**. Do not enter the same aggregate install count against several posts.

## The first four weeks

- **Week 1 — establish the USP:** map → mission; one clear shot; recognizable streets.
- **Week 2 — show credibility:** current scope feel; place search without GPS permission; an aiming moment.
- **Week 3 — invite participation:** broad city suggestions; how mapped environments work; an honestly labeled AI duel.
- **Week 4 — repeat and refine:** compare two recorded districts; one shipped improvement; GPS recap.

Spend roughly one short session a week recording 3 genuinely different clips, another reviewing and scheduling, and a few minutes replying to real comments manually. Treat this as a starting workflow, not an evidence-backed optimal cadence. The 7 PM India timing is a test, not a guarantee. Compare within channel at similar post age. After at least six measured posts, test one opening or pacing change at a time; low-sample differences are directional.

Prefer public landmarks and broad city names. Never solicit a follower's home address, precise GPS coordinate or movement route. Keep missions fictional. Preserve required map attribution in the video/caption; verify provider and asset terms. Do not claim live satellite video, online PvP (duels are AI), exact real-world buildings, universal map availability, rankings or guaranteed earnings. Do not buy reviews, spam comments/DMs, fabricate testimonials or use reference footage as your gameplay.

Future paid promotion should be a separate opt-in project with a confirmed budget, attribution and privacy review. Add it only after the store page and player retention are measured; this implementation cannot spend money.

## Commands and files

```powershell
node Growth/server.mjs
node Growth/cli.mjs plan 2026-10-19
node Growth/cli.mjs render gps
node Growth/cli.mjs render
node Growth/cli.mjs report
node --test --test-isolation=none Growth/tests/*.test.mjs
```

`Growth/lib` holds the planner, SQLite store, approval rules, worker, renderer and official API adapters. `Growth/public` is the responsive dashboard. `Growth/data/growth.sqlite` is local state. `Growth/media` contains videos. `Growth/exports` contains approved upload packs and reports. No Unity runtime files are changed.

FFmpeg is auto-detected at `.utmp/promo-tools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe`, already present here. Set `PROMO_FFMPEG` to a different local executable if needed. The renderer uses Windows Arial fonts and the existing `issues/issues.mp4` plus `Store/Promo/geosniper-original-score.wav`. Removing those files breaks preset rendering, but you can still register new videos. Preview/probe validation requires FFmpeg too. The optional browser QA script uses an already-installed Playwright; no production npm dependencies are needed.

## Official references checked September 20, 2026

- [YouTube resumable uploads](https://developers.google.com/youtube/v3/guides/using_resumable_upload_protocol)
- [YouTube videos.insert and private-only restrictions](https://developers.google.com/youtube/v3/docs/videos/insert)
- [Google desktop OAuth and PKCE](https://developers.google.com/identity/protocols/oauth2/native-app)
- [Which YouTube links are clickable](https://support.google.com/youtube/answer/13748639?hl=en)
- [Meta's official Instagram publishing sample and local-file upload protocol](https://github.com/fbsamples/reels_publishing_apis/tree/main/insta_reels_publishing_api_sample)
- [Instagram Content Publishing](https://developers.facebook.com/docs/instagram-platform/instagram-api-with-facebook-login/content-publishing/)
- [Google Play acquisition / install referrer](https://developer.android.com/games/playgames/user-acquisition)

APIs, eligibility, quotas and policies change. These adapters need real-account acceptance testing before relying on unattended publishing.
