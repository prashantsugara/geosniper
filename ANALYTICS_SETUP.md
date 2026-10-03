# Google Analytics 4 & Realtime Telemetry Guide for GeoSniper

GeoSniper includes a lightweight, built-in Google Analytics 4 (GA4) & Firebase Measurement Protocol engine ([`GameAnalyticsManager.cs`](Assets/Scripts/GameAnalyticsManager.cs)).

It requires **zero external SDKs or native JAR plugins**, which prevents Android Gradle build breakages while giving you full Google Analytics web & mobile reporting.

---

## 1. What Is Automatically Tracked

| Metric | What You See in GA4 Dashboard | How It Works |
|---|---|---|
| **Realtime Downloads / Installs** | Event `first_open` / New Users | Fires once upon initial app launch on a device with device model, OS, screen resolution. |
| **Realtime Active Players** | "Users in last 30 minutes" on live World Map | Sends `session_start` and continuous 30s `user_engagement` heartbeats. |
| **Average Time Spent on Game** | "Average engagement time per active user" & "Average session duration" | Continuous engagement time calculation reported per session, day, and week. |
| **Mission Progression & Drop-offs** | `level_start` and `level_end` | Tracks stage number, duration, stars, accuracy, headshots, and exact failure reason (`fail_reason`). |
| **Armory Upgrades** | `spend_virtual_currency` | Tracks weapon index, stat upgraded (Damage, Scope, Mag), new level, and cash spent. |
| **Rewarded & Interstitial Ads** | `ad_reward_view` | Tracks placement tag (`emergency_revive`, `armory_shortfall`, `transition`) and view completion. |

---

## 2. Quick 2-Minute Google Analytics Setup

### Step 1: Create a Free GA4 Property
1. Go to [analytics.google.com](https://analytics.google.com/) and sign in with your Google account.
2. Click **Admin** (gear icon bottom left) > **Create Property**.
3. Property Name: `GeoSniper` (set your timezone and currency) > Click **Next**.
4. Business Objectives: Select **Examine user behavior** or **Baseline reports** > Click **Create**.

### Step 2: Create a Data Stream & Get Measurement ID
1. In the "Data Streams" screen, choose **Web** (recommended for standard Measurement Protocol) or **Android app**.
2. If choosing **Web**:
   - Stream URL: `https://geosniper.game` (or your GitHub repo / any domain)
   - Stream Name: `GeoSniper Android`
3. Click **Create Stream**.
4. Copy your **MEASUREMENT ID** (it starts with `G-`, e.g., `G-1A2B3C4D5E`).

### Step 3: Create Measurement Protocol API Secret
1. In the same Data Stream details page, scroll down to **Measurement Protocol API secrets**.
2. Click **Create** > Nickname: `GeoSniper Client` > Click **Create**.
3. Copy the generated **Secret Value**.

---

## 3. Connect to GeoSniper

Open [`Assets/Resources/ReleaseConfiguration.json`](Assets/Resources/ReleaseConfiguration.json) and paste your values:

```json
{
  "analyticsEnabled": true,
  "gaMeasurementId": "G-XXXXXXXXXX",
  "gaApiSecret": "your_api_secret_here"
}
```

That's it! When you run the game in Unity Editor or install the APK:
1. In GA4, go to **Reports** > **Realtime**.
2. Launch the game.
3. You will immediately see:
   - **Users in last 30 minutes: 1**
   - Blue pin on your city on the world map.
   - Event `first_open` (new install/download).
   - Event `session_start` and `user_engagement`.

---

## 4. In-Game Telemetry Dashboard

Even before configuring GA4 or when offline, GeoSniper tracks your local lifetime playtime and session metrics:
- Open **Settings** from the Lobby.
- Under **REALTIME TELEMETRY & ENGAGEMENT**, you can see:
  - `SESSIONS: <count>`
  - `AVG PLAYTIME: <mins:secs>`
  - `TOTAL: <hrs:mins>`
  - `CONNECTION STATUS: ONLINE / LOCAL`
