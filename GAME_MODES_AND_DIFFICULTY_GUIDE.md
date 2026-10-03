# GeoSniper: Game Modes, Mission Architecture & Difficulty Tuning Guide

Current campaign implementation (2026-09-19): see [CAMPAIGN_PROGRESSION.md](CAMPAIGN_PROGRESSION.md). There are 14 authored nodes, explicit contract types, per-node threat budgets and tier-based modifiers composed with `DifficultyProfile`. That document supersedes the older tuning examples and line numbers below; do not tune the current game by changing those historical hardcoded values. The opening mission is now untimed two-target training, and all sniper duels are against AI, not online players.

The remainder of this document is an earlier technical/design reference for **GeoSniper**. Some sections describe prototype behavior or planned content rather than the current implementation.

---

## 1. Project & Gameplay Architecture Overview

GeoSniper operates on a modular, event-driven urban combat framework:

```
                                  [GeoSniperGame]
                               (Lobby & Vault System)
                           (Play Store Rate ⭐ +2K Cash)
                                         │
                   ┌─────────────────────┴─────────────────────┐
                   ▼                                           ▼
          [CampaignNodeGraph]                         [SectorWorld (GIS)]
        (14-Node Tactical Tree)                   (Real-World OpenStreetMap)
                   │                                           │
                   └─────────────────────┬─────────────────────┘
                                         ▼
                             [UrbanCombatMission]
                         (Mission Orchestration Engine)
                   (Satellite Recon Briefing & GIS Telemetry)
                                         │
        ┌──────────────────┬─────────────┼────────────┬──────────────────┐
        ▼                  ▼             ▼            ▼                  ▼
  [Combat Modes]      [EnemyBot AI]  [Tactical Map] [BallisticsSystem] [AdManager]
  • HVT Strike        • Roles & FSM  • Live OSM GIS • Scope & Sway     • Rewarded 2x
  • VIP Overwatch     • Alert System • Waypoints    • Bullet Cam       • Revive
  • Timed Courier     • Weapons      • Air Strikes  • Telemetry Score  • Interstitials
  • Stealth Ghost     • Cover/Flank                                    • Rate 5★ Bonus
  • Rooftop Siege
  • 1v1 PvP Duel
```

- **Core Loop**: Select Mission Node $\to$ **Satellite Recon Briefing** $\to$ Rooftop Infiltration $\to$ Scope Target Recognition $\to$ Ballistics Lead & Elimination $\to$ 2x Rewarded Ad Payout / **Play Store Rating** $\to$ Vault Deposit $\to$ Armory Upgrades.
- **Key State Machine**: `MissionState.Briefing` (Satellite Recon Modal) $\to$ `MissionState.InProgress` (Real-World Telemetry HUD) $\to$ `MissionState.Extraction` $\to$ `MissionState.Complete` (3-Star Rating Prompt) / `MissionState.Failed` (Emergency Ad Revive).

---

## 2. Detailed Game Modes Breakdown

GeoSniper features **7 distinct combat modes**, each presenting unique pacing, objectives, and failure conditions.

### Mode 1: Target Identification & HVT Strike (`CampaignContractType.TargetIdentification`)
- **Premise**: High-Value Target (Commander) surrounded by guards and innocent pedestrian crowds.
- **Objectives**:
  1. Scan ground district through sniper optics to identify the marked Commander.
  2. Eliminate the Commander and supporting sentries without civilian collateral damage.
  3. Neutralize the opposing rooftop Counter-Sniper before your position is suppressed.
- **Fail Conditions**: Player health hits 0, or innocent civilian is killed.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `SetupModeTargetElimination()`.

### Mode 2: VIP Overwatch (`CampaignContractType.Overwatch`)
- **Premise**: An allied operative is navigating a perilous urban street corridor to an extraction zone.
- **Objectives**:
  1. Oversee the VIP as they move between street waypoints toward the extraction police cruiser.
  2. Eliminate Syndicate ambushers that intercept the VIP at road intersections.
  3. Neutralize a rooftop sniper overlooking the extraction corridor.
- **Fail Conditions**: Allied VIP health reaches 0, or player is eliminated.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `SetupModeOverwatch()`, `CivilianBot.cs`.

### Mode 3: Timed Interception (`CampaignContractType.TimedInterception`)
- **Premise**: A high-speed courier carrying sensitive military data is sprinting toward an extraction point.
- **Objectives**:
  1. Acquire and track the fast-moving target (`speed = 5.2m/s`) weaving through streets.
  2. Lead your shots to compensate for target velocity and bullet travel time.
  3. Eliminate flanking suppressors laying down covering fire.
- **Timer**: Default **45 seconds** before courier escapes.
- **Fail Conditions**: Timer reaches 0, courier reaches getaway vehicle, or player dies.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `SetupModeTimedInterception()`.

### Mode 4: Ghost Protocol / Stealth Contract (`CampaignContractType.Stealth`)
- **Premise**: Infiltrate a hostile syndicate district with zero detection allowed.
- **Objectives**:
  1. Silently eliminate all patrolling sentries across the sector.
  2. Use suppressed weaponry (MK12 SPR recommended).
  3. Coordinate kills when sentries are isolated; missing shots or letting a sentry fire raises the radio alarm.
- **Alert Tolerance**: Global Alert Level must stay **below 2** (`GlobalAlertLevel < 2`).
- **Fail Conditions**: Alert level reaches 2 (alarm triggered), or player dies.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `SetupModeStealthSyndicate()`, `EnemyBot.cs`.

### Mode 5: Rooftop Siege & Hot Extraction (`CampaignContractType.Escape`)
- **Premise**: Your nest position has been compromised; waves of assault rushers are converging on your rooftop.
- **Objectives**:
  1. Hold your ground against sprinting assault rushers (`speed = 3.2m/s`).
  2. Survive for **45 seconds** until the allied extraction bird arrives.
  3. Reach the extraction landing zone once the timer expires.
- **Fail Conditions**: Overwhelmed by assault rushers, or player health reaches 0.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `SetupModeHotExtraction()`.

### Mode 6: 1v1 PvP Rooftop Sniper Duel
- **Premise**: A lethal showdown against an AI rival master sniper on an opposing skyscraper.
- **Objectives**:
  1. Acquire the rival sniper across distances of 80m–150m.
  2. The rival traces your position with an active red targeting laser (`duelLockTimer`).
  3. Land a lethal headshot before the rival reaches 100% lock (3.2 seconds).
- **Fail Conditions**: Rival completes laser lock and executes instantaneous high-caliber strike.
- **Primary Script**: `UrbanCombatMission.cs` $\to$ `BeginPvPDuel()`.

### Mode 7: Free Roam & Real-World Sector Mode
- **Premise**: Open exploration in procedural or cached GPS real-world street sectors.
- **Objectives**: Practice long-range marksmanship, dynamic day/night/rain weather adaptation, and thermal FLIR optics against ambient patrols.
- **Primary Script**: `SectorWorld.cs`, `LiveWeatherController.cs`.

---

## 3. Difficulty Parameters & Tuning Guide

Difficulty in GeoSniper is governed by five interconnected layers:
1. **Enemy AI Attributes** (`EnemyBot.cs`)
2. **Mission Objectives & Constraints** (`UrbanCombatMission.cs`)
3. **Weapon & Ballistics Dynamics** (`WeaponConfig`, `BallisticsSystem.cs`)
4. **Campaign Node Scaling** (`CampaignNodeGraph.cs`)
5. **Player Health & Survivability** (`UrbanPlayer.cs`)

---

### Layer 1: Enemy AI Tuning (`Assets/Scripts/EnemyBot.cs`)

File path: `Assets/Scripts/EnemyBot.cs`

| Parameter | Default Value | Easy / Casual | Hardcore / Realistic | Effect on Difficulty |
|---|---|---|---|---|
| **Health** (L11) | `60f` | `40f` (1 body shot) | `100f`–`150f` (requires headshot) | Enemy damage sponge threshold |
| **Speed** (L11) | `2.5f` | `1.8f` | `3.5f`–`4.2f` | Movement velocity while patrolling/combat |
| **Detection Range** (L11) | `65m` | `40m` | `90m`–`120m` | Distance at which bot notices sniper |
| **Engagement Range** (L11) | `50m` | `30m` | `80m` | Distance at which bot opens fire |
| **Fire Cooldown** (L50) | `0.8f + (i%4)*0.25f` | `1.5f + (i%4)*0.5f` | `0.4f + (i%4)*0.15f` | Delay between enemy bursts |
| **Combat Pause** (L51) | `0.8f + (i%3)*0.4f` | `1.5f + (i%3)*0.5f` | `0.3f + (i%3)*0.2f` | Time spent pausing between repositioning |
| **Radio Alert Radius** (L89) | `80m` / `120m` | `45m` / `70m` | `150m` / `200m` | How far gunshots alert nearby guards |

#### Code Location in `EnemyBot.cs`:
```csharp
// Line 11: Base AI Physical Attributes
public float speed = 2.5f, detectionRange = 65, engagementRange = 50, health = 60;

// Lines 50-51: Firing Delay & Combat Reaction
fireCooldown = .8f + (index % 4) * .25f;  // Increase for slower enemy fire
combatPause = .8f + (index % 3) * .4f;    // Increase to give player more time to aim
```

---

### Layer 2: Mission-Specific Mode Tuning (`Assets/Scripts/UrbanCombatMission.cs`)

File path: `Assets/Scripts/UrbanCombatMission.cs`

#### A. Timed Interception (Courier Sprint)
- **Courier Speed** (L1459): Default is `5.2f`.
  - *Casual*: Set to `3.6f` (easy to track).
  - *Hardcore*: Set to `6.5f` (requires precise predictive lead).
- **Courier Health** (L1460): Default is `75f`.
- **Mission Timer** (L1416): Default is `45f`.
  - *Casual*: Set to `60f`.
  - *Hardcore*: Set to `30f`.

#### B. Ghost Protocol (Stealth Alarm)
- **Alert Limit** (L2478): Default is `EnemyBot.GlobalAlertLevel / 2`.
  - *Casual*: Allow `3` or `4` alerts before failure.
  - *Hardcore*: Change condition to `GlobalAlertLevel >= 1` (instant fail on any alert).
- **Sentry Detection Range** (L1530): Default is `50f`.
- **Sentry Count** (L1507): `Mathf.Clamp(3 + stageIndex / 2, 4, 7)`.

#### C. VIP Overwatch (Escort Survival)
- **VIP Health** (L1358): Default is `500f`.
  - *Casual*: Set to `800f`–`1000f` (VIP can absorb multiple bursts).
  - *Hardcore*: Set to `200f` (VIP dies in 3-4 hits).
- **Ambusher Reaction Time** (L1376): Default `8.5f + i * 3.5f` seconds.
  - Decreasing this causes ambushers to attack sooner after VIP spawn.

#### D. Rooftop Siege (Survival Rush)
- **Siege Timer** (L1560): Default `45f` seconds.
- **Assault Enemy Count** (L1566): `Mathf.Clamp(4 + stageIndex / 2, 4, 8)`.
- **Rusher Speed** (L1588): Default `3.2f` m/s.

#### E. 1v1 PvP Sniper Duel
- **Rival Lock Timer Threshold** (L2521): Default is `3.2f` seconds.
  ```csharp
  // Line 2521 in UrbanCombatMission.cs:
  float lockPct = Mathf.Clamp01(duelLockTimer / 3.2f);
  ```
  - *Casual*: Change `3.2f` to `5.0f` (gives player 5 seconds to spot and eliminate rival).
  - *Hardcore*: Change `3.2f` to `1.8f` (requires lightning reflexes).

---

### Layer 3: Weapon Balance & Ballistics (`Assets/Scripts/SniperPresentation.cs` & `BallisticsSystem.cs`)

1. **Rifle Profiles**:
   - **Barrett .50 Cal**: Ultra high damage (one-shot kill anywhere), high recoil/shake (`shakeIntensity = 0.8f`), slow cycle rate.
   - **M24 Tactical**: Balanced marksman rifle, moderate recoil (`shakeIntensity = 0.5f`), medium zoom.
   - **MK12 SPR**: Semi-automatic suppressed sniper, silent acoustic signature (vital for Stealth contracts), lower per-shot damage.
2. **Scope Sway**: Configured via `WeaponSway` in `SniperPresentation.cs`. Increasing sway requires player to time shots with breathing pauses.
3. **Bullet Travel & Time**: Governed in `BallisticsSystem.cs` line 227:
   ```csharp
   float flightDuration = Mathf.Clamp(totalDist / 42f, 1.3f, 2.1f);
   ```

---

### Layer 4: Campaign Node Progression & Scaling (`Assets/Scripts/CampaignNodeGraph.cs`)

File path: `Assets/Scripts/CampaignNodeGraph.cs`

Every campaign stage node specifies:
- `enemyCount`: Base enemy count for that stage.
- `rewardCash` & `rewardXP`: Progression economy payouts.
- `isPvPDuel`: Whether the stage triggers the high-tension 1v1 rooftop duel.

To adjust difficulty of specific campaign levels, modify the node definitions in `CampaignNodeGraph.Nodes`:

```csharp
new LevelNode
{
    id = 4,
    title = "ROOFTOP SHOWDOWN",
    codeName = "OP: RED LASER",
    modeTag = "PvP DUEL",
    isPvPDuel = true,
    enemyCount = 1,
    rewardCash = 500,     // Adjust payout
    rewardXP = 250
}
```

---

### Layer 5: Star Rating Difficulty Formula

The 1 to 3-star victory rating is determined in `UrbanCombatMission.cs` (lines 1779–1784):

```csharp
rewardStars = 1; // Base 1-Star for completing contract
float hpPct = (player != null && player.Health != null) ? (player.Health.Health / 100f) : 1f;

// Star 2 Requirement: At least 1 headshot OR survived with >= 50% HP
if (totalHeadshotsScored > 0 || hpPct >= 0.5f) rewardStars++;

// Star 3 Requirement: At least 2 headshots OR (>= 80% HP AND cleared under 90 seconds)
if (totalHeadshotsScored >= 2 || (hpPct >= 0.8f && missionDuration < 90f)) rewardStars++;

rewardStars = Mathf.Clamp(rewardStars, 1, 3);
```

#### How to Make Star Requirements More Demanding:
- Require $\ge 3$ headshots for Star 3.
- Lower the time limit from `90f` to `60f` seconds.
- Require player HP to remain at $\ge 95\%$.

---

## 4. Ready-to-Use Difficulty Presets

### Preset A: "Casual Mobile Marksman" (Accessible, High Engagement)
- Enemy Health: `45f` (clean 1-hit body kills with all weapons).
- Enemy Reaction / Combat Pause: `1.8s`.
- Timed Courier Speed: `3.8m/s`, Timer: `60s`.
- PvP Duel Laser Lock: `4.8s`.
- VIP Health: `800f`.
- Stealth Alert Tolerance: `3 alerts`.

### Preset B: "Standard Tactical" (Default Project Profile)
- Enemy Health: `60f` (1-hit kill on Barrett, 2-hit body on MK12, 1-hit headshot on all).
- Enemy Reaction / Combat Pause: `0.8s–1.2s`.
- Timed Courier Speed: `5.2m/s`, Timer: `45s`.
- PvP Duel Laser Lock: `3.2s`.
- VIP Health: `500f`.
- Stealth Alert Tolerance: `2 alerts`.

### Preset C: "MilSim Operative" (Hardcore, High Skill Ceiling)
- Enemy Health: `90f` (body armor absorbs non-magnum calibers; headshots mandatory).
- Enemy Reaction / Combat Pause: `0.4s`.
- Timed Courier Speed: `6.0m/s`, Timer: `32s`.
- PvP Duel Laser Lock: `1.9s`.
- VIP Health: `220f`.
- Stealth Alert Tolerance: `1 alert` (any gunshot without suppressor fails mission).

---

## 5. Monetization & Ad Integration Architecture

AdMob monetization is managed by `AdManager.cs`:

1. **Rewarded 2x Payout (Post-Mission Victory)**:
   - Location: `UrbanCombatMission.cs` $\to$ `DrawVictoryRewardScreen`.
   - Players can tap `[ 🎬 2X REWARD ]` to double their mission Cash and XP.
2. **Emergency Second-Chance Revive (Mission Failed)**:
   - Location: `UrbanCombatMission.cs` $\to$ `DrawMissionFailedScreen`.
   - Players tap `[ 🎬 EMERGENCY REVIVE ]` to heal to 100 HP and resume combat immediately.
3. **Sponsored Tactical Crate (Lobby Vault)**:
   - Location: `GeoSniperGame.cs` $\to$ `DrawRewardsModal`.
   - Free +$1,500 cash crate on watching sponsor transmission.
4. **Paced Interstitials**:
   - Fires automatically between missions every 2 completed stages with a 90-second safety cooldown.
5. **Editor Setup Tool**:
   - Accessible in Unity via **Tools $\to$ GeoSniper $\to$ AdMob Configuration**.

---

## 6. Real-World Map GIS Engine (Game USP)

The central Unique Selling Proposition (USP) of GeoSniper is that **every battlefield is a physical, 1:1 real-world urban district** generated directly from OpenStreetMap GIS vector and geospatial data rather than fictitious, hand-crafted video game levels. 

To ensure players immediately understand and appreciate this USP, real-world cartography is surfaced at every layer of the user experience:

```
                                  [Player Journey]
                                         │
 ┌───────────────────────────────────────┼───────────────────────────────────────┐
 ▼                                       ▼                                       ▼
[1. Lobby Real 3D Badge]    [2. Satellite Recon Modal]             [3. Combat Telemetry HUD]
• Real-World 3D Sector Tag  • Real District / Landmark Name        • Real District Name & GPS
• Live Location Switcher    • Physical Building Count Readout      • Dynamic Building Count
• OpenStreetMap Attribution • Direct [🗺️ INSPECT REAL MAP] Button • [M] Key / Tap Sat-Map
                                         │
                                         ▼
                            [4. Tactical GIS Cartography]
                            • Full-screen OpenStreetMap Raster
                            • Real-World Place & Street Search
                            • Street-Level POI Labels & Waypoints
                            • Live Bearing & Metre Distance Scale
```

### 1. Pre-Mission Satellite Reconnaissance Modal
- **File**: `Assets/Scripts/UrbanCombatMission.cs` (lines 2604–2655)
- **Trigger**: Displayed automatically when entering `MissionState.Briefing` before gameplay begins.
- **Features**:
  - **Live District Attribution**: Dynamically extracts the real-world neighborhood or landmark name via `GetTargetBuildingLocationName()` from loaded `SectorWorld` features.
  - **Physical Structural Count**: Calculates the exact number of physical architectural structures loaded in the sector (`world.Buildings.Count`).
  - **OpenStreetMap Authentication**: Explains that roads, alleys, and building footprints correspond to real Earth GPS coordinates.
  - **Interactive Preview**: Provides `[ 🗺️ INSPECT REAL MAP [M] ]` button, allowing the player to survey the real GIS map before initiating the operation.

### 2. In-Combat HUD Telemetry Banner
- **File**: `Assets/Scripts/UrbanCombatMission.cs` (line 2529)
- **Visual**: Prominent cyan telemetry line across the bottom of the active combat HUD.
- **Content**:
  ```
  🌐 REAL-WORLD SECTOR: {district} ({bldCount} PHYSICAL BUILDINGS) | 📍 GPS 3D GIS TERRAIN • [M] TAP TO OPEN SAT-MAP
  ```
- **Function**: Constantly reinforces that the sniper nest and targets are situated in an authentic physical city sector.

### 3. Tactical Radar & Full Satellite Cartography
- **Files**: `UrbanCombatMission.cs` (line 2748) & `UrbanCombatMission.Map.cs` (lines 166–215)
- **Minimap Radar**: Branded as `🌐 REAL MAP RADAR` with a direct `[ 🛰️ REAL-WORLD MAP ]` launcher.
- **Full Map View**:
  - Branded header: `🌐 REAL-WORLD SECTOR // Live OpenStreetMap 3D GIS`.
  - Searchable real places, streets, and commercial POIs from OpenStreetMap tags (`amenity`, `shop`, `tourism`, `highway`).
  - Interactive tactile panning, pinch-to-zoom, and waypoint beacon placement with real metric distance calculations.
  - Tactical air strikes callable directly onto pinned GPS coordinates.

### 4. Lobby Real-World 3D Badge
- **File**: `Assets/Scripts/GeoSniperGame.cs` (line 861)
- **Header Placement**: Displays `🌐 REAL-WORLD 3D` badge alongside current city name in the lobby navigation bar. Tapping it opens the global city/sector search modal (`DrawLocationPicker`).

---

## 7. Google Play Store Rating & Player Review System

Player reviews and 5-star ratings are essential for store algorithm visibility and organic discovery. GeoSniper incorporates a frictionless, rewarded Google Play Store rating pipeline.

### Core Architecture (`Assets/Scripts/AdManager.cs`)

```csharp
// Rating Method in AdManager.cs
public void OpenPlayStoreRating(int rewardCashBonus = 2000)
{
    string appId = Application.identifier;
    if (string.IsNullOrEmpty(appId) || appId == "com.DefaultCompany.sniper2")
        appId = "com.geosniper.urbancombat";

    // 1. Credit Player with In-Game Bounty
    PlayerPrefs.SetInt("GeoSniper.HasRatedPlayStore", 1);
    int curCredits = PlayerPrefs.GetInt("GeoSniper.Credits", 0);
    PlayerPrefs.SetInt("GeoSniper.Credits", curCredits + rewardCashBonus);
    PlayerPrefs.Save();

    // 2. Launch Android Market Intent with Web Fallback
    try
    {
        Application.OpenURL("market://details?id=" + appId);
    }
    catch (System.Exception)
    {
        Application.OpenURL("https://play.google.com/store/apps/details?id=" + appId);
    }
}
```

### Player Incentives & Tracking
- **Cash Bonus**: Awards `+$2,000` in-game currency upon opening the rating prompt.
- **Persistent State**: Tracked in `PlayerPrefs` key `"GeoSniper.HasRatedPlayStore"` (`1` = rated, `0` = unrated).
- **Graceful Fallback**: Attempts Android native intent `market://details?id=...`; if the Google Play client is unavailable or running in editor/web, seamlessly opens the browser URL.

### UI Integration Points

1. **Lobby Top Bar (`GeoSniperGame.cs` L874)**:
   - Gold highlighted `[ ⭐ RATE 5★ | +$2,000 BONUS ]` button in the top navigation bar.
   - Automatically switches to a subtle green `[ ⭐ RATED 5★ | PLAY STORE ✓ ]` badge once claimed.
2. **Tactical Rewards & Bounty Vault (`GeoSniperGame.cs` L1140)**:
   - Dedicated **Google Play Review** card alongside the Daily Supply Drop and Sponsored Airdrop.
   - Shows description, reward amount (`+$2,000 BONUS 💵`), and one-tap claim action.
3. **3-Star Mission Victory Flow (`UrbanCombatMission.cs` L2835)**:
   - Triggered when the player achieves a perfect 3-star mission victory.
   - Prompts the player at the peak of positive engagement:
     `[ ⭐ ENJOYING REAL MAPS? RATE 5★ ON PLAY STORE (+$2,000 BONUS) ]`.
