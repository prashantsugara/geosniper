# Existing rifle progression

The armory opens with four weapon categories: Sniper Rifles, Assault Rifles, SMGs and Sidearms. Selecting a category opens a weapon showcase with a filtered gun strip, current stats, ownership/rank status and buy/equip actions. The Upgrades button opens the five upgrade paths in a separate scrolling panel. Categories returns to the first page; campaign recommendations and rank-up notices open the relevant weapon directly. Preview images are rendered from the in-game models.

Missions deploy with the selected owned sniper (weapon IDs 0–4), falling back to the free M24 when a pistol, SMG or assault rifle is selected. The Eagle remains available as a backup through the weapon dock. Gun surfaces share the same textured PBR finish in the lobby, first-person view and rival models: metal receives specular highlights while wood, rubber and stocks remain matte.

Each rifle has six ranks for damage, magazine capacity, optics, reload and stability. Existing upgrade preference keys are retained, including previously purchased ranks. Upgrades apply when deploying or selecting a weapon in campaign. Online and practice duel loadouts use base stats.

| Upgrade | Currency | First cost | Cost growth | Effect |
| --- | --- | --- | --- | --- |
| Damage | Credits | 1,500 | 1.45 per rank | Linear damage increase |
| Magazine | Credits | 1,000 | 1.65 per rank | +2 Barrett rounds or +5 M24/MK12 rounds per rank |
| Optics | Gold | 40 | 1.45 per rank | Improved maximum zoom |
| Reload | Gold | 25 | 1.45 per rank | Diminishing reduction, capped at 35% |
| Stability | Gold | 25 | 1.45 per rank | Diminishing sway/recoil reduction, capped at 40% |

Cost is rounded upward. Rifle damage at base/max: Barrett 120/264, M24 20/122, MK12 45/105. Standard 100-HP torso targets need five base M24 hits or one rank-five M24 hit, six base pistol hits or one maxed pistol hit, and three base MK12 hits or one maxed MK12 hit; limbs and stronger targets can require more. Precision headshots retain execution behavior in training and standalone modes; armored campaign enemies take actual headshot damage.

Rewards: +50 gold or +500 credits per completed rewarded currency ad, with a shared three-per-UTC-day limit. No currency is issued on unavailable or unfinished ads. Daily supplies remain +100 gold. Completed duel wins award 50 credits to the bounty vault and 200 XP; already earned balances are retained. Barrett requires the full 300 gold to unlock; MK12 remains 7,500 credits. New players start with the M24 primary and Eagle pistol. All other weapons require purchase in campaign and duels, including development builds. Existing purchases and owned selections are preserved.

Validation: Tools/run_duel_visual_checks.ps1 -ExecuteMethod MultiplayerPolishChecks.RunAll runs currency purchase/cap checks, the actual campaign DamageSystem hit pipeline, normalized duel loadouts, and multiplayer/HUD/roof-access regressions. Ad SDK delivery and reward completion require Android device verification after rebuilding; these automated checks verify local reward accounting rather than live ad delivery.

Attachments, crafting resources, prestige and recalibration are deferred. Campaign replay payouts and overall progression pacing still need live player telemetry before further balancing.

## Expanded weapon catalog

The catalog now has eight stable weapon IDs. Original save IDs 0–2 are unchanged.

| New weapon | Role | Base / max damage | Base magazine | Unlock credits |
| --- | --- | --- | --- | --- |
| M40 Scout | Lighter bolt-action | 90 / 162 | 5 | 5,500 |
| SVD Marksman | Semi-auto marksman | 65 / 125 | 10 | 9,000 |
| M4A1 Assault | Automatic rifle | 28 / 52 | 30 | 4,500 |
| MP5 Compact | Close-range automatic SMG | 22 / 40 | 30 | 3,500 |
| Eagle Sidearm | Starter semi-auto pistol | 18 / 102 | 7 | Free |

All five use the same six-rank vertical upgrades with independent saved levels. Horizontal choice comes from distinct weapon roles: damage, cadence, reload, magazine size, recoil, zoom, noise, projectile speed and maximum range. Automatic weapons retain multi-hit torso damage to preserve their role; precision rifles and the upgraded heavy pistol can deliver one-hit standard torso kills.

New automatic/pistol headshots deal double damage; precision rifle headshots deal four-times damage against armored campaign enemies and retain execution behavior against unarmored targets. Automatic kills skip cinematic bullet-camera interruptions. The initial mobile dock shows the M24 and pistol without a duplicate third slot. After additional purchases, the expanded mobile weapon dock uses previous/current/next owned weapon selection; keyboard keys 1–8 select weapons directly. Armory pages scroll on shorter screens.

Online duels use base catalog stats for every gun, host-validated firing cadence, ammunition, reloads and range, and synchronized opponent weapon geometry. Protocol version 9 includes all eight ammunition counts during reconnect and the equipped weapon in state updates. Both devices must use the new build.

Assets: all five added guns now use free CGTrader models under the Royalty Free License (no AI): M4A1 by mminhnghii02, Desert Eagle by AmanDeep0707, MP5 by Fallxhore, M40 and SVD by Yahia-DdZ. Source and modification records are in Store/CGTraderGunLicenses.json and in-game credits. Blender port 9876 was used to normalize scale, optimize meshes and fit grip markers. Textures are 1024px with Android ASTC 6x6 compression and Unity metallic/smoothness maps. The SVD download lacked textures, so licensed M40 surface maps were adapted with planar UVs. Card previews are rendered from Unity models. The assault rifle retains ID 5 and its M16Assault resource path to preserve saved upgrades. Store/SketchfabGunLicense.json records the superseded initial assets.


## Campaign weapon readiness

The existing 50 authored levels now scale standard enemy durability smoothly from 60 HP at level 1 to 210 HP at level 50, multiplied by the selected difficulty profile. Runner targets keep their lighter 75% base health. From level 7, campaign enemies resist automatic headshot executions: damage and upgrades determine whether a headshot kills. Online and standalone practice duels retain their existing health and normalized stats.

Before deployment, a weak loadout (more than three estimated torso hits, or an armored target surviving a headshot) opens a readiness warning. It shows the target HP, current hit estimate, a damage-rank recommendation and a suitable replacement. Buttons open the current or recommended weapon directly in the armory. A player may still attempt the mission. Recommendations target two close-range torso hits; range and limb hits can require more. Existing completion saves are preserved; no additional levels were needed for this progression curve.

Failed weapon or upgrade purchases with insufficient funds open an optional rewarded-ad prompt for the exact currency needed. It shows price, balance, shortfall, reward and remaining daily allowance. Completion grants +500 credits or +50 gold under the existing shared three-per-day cap; the player retries the purchase after earning sufficient funds. No automatic ad or automatic purchase occurs. Tests cover all 50 health steps, actual armored hit damage, upgrade/replacement eligibility, armory navigation, correct-currency prompts and existing multiplayer regressions. Live Android ad delivery remains a device check.

## Player ranks and weapon purchase guidance

Player rank rises from 1 to 50 using the existing lifetime XP balance, with 1,000 XP per rank. Campaign completions, gameplay XP awards and duel victory XP use the same progression service. Rank is preserved when a civilian penalty reduces XP. Existing XP and owned weapons remain intact, even when an already owned gun has a higher new rank requirement. Currency ads do not directly grant player XP.

| Player rank | Weapon available to buy | Role |
| --- | --- | --- |
| 1 | M24 and Eagle pistol, free starters | Precision training and backup |
| 2 | MP5, 3,500 credits | Close-range defense |
| 3 | M4A1, 4,500 credits | Automatic close/mid-range support |
| 4 | M40, 5,500 credits | First high-damage sniper purchase |
| 6 | MK12, 7,500 credits | Suppressed stealth and rapid follow-ups |
| 9 | SVD, 9,000 credits | Semi-auto moving-target engagements |
| 15 | Barrett, 300 gold | High damage against heavy targets |

Rank eligibility enables purchase; it does not grant the weapon for free. The purchase service enforces both rank and funds, including in development builds. Armory cards show the required rank, practical role, damage relative to the equipped gun, effective range, estimated torso hits and next rank unlock. Campaign recommendations only suggest owned or rank-eligible weapons; a low-rank player can improve the starter instead. A persistent rank-up notice lists newly available purchases and opens the relevant gun in the armory. Mission results and the lobby show current player rank and XP progress. Automated checks cover rank boundaries, multiple-rank notifications, rank-gated purchases, old ownership, XP penalties, rank cap and overflow.

## Campaign replay rewards

First completion pays 100% of the authored credits and XP. Completed-node replays pay 25%, rounded down with a one-unit minimum for positive rewards. Completion status is read before marking the node complete, so the first win is not mistaken for a replay. Repeated payout callbacks within one run remain blocked. The campaign deck and lobby preview show the actual reward and whether it is a first-clear or replay payout.

Replay accolade/kill XP also pays 25%, with the on-screen accolade showing the reduced amount. Optional 2x completion ads double the actual payout (a replay reaches 50% of its original payout), rather than restoring full first-clear rewards. Daily rewards and standalone/multiplayer rewards retain their separate rules. Existing balances are preserved. Validation: Tools/run_duel_visual_checks.ps1 -ExecuteMethod CampaignProgressionChecks.Rewards verifies first clear, replay preview, actual replay payout, duplicate prevention, reduced kill XP and standalone-mode isolation.
