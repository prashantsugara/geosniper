using System;
using System.Collections.Generic;
using UnityEngine;

namespace GeoSniper
{
    public enum NodeState
    {
        Locked,
        Available,
        Completed,
        Current
    }

    [System.Serializable]
    public class LevelNode
    {
        public int id;
        public string title;
        public string codeName;
        public string modeTag;
        public Color modeColor;
        public bool isPvPDuel;
        public int stageIndex;
        public int difficultyTier;
        public CampaignContractType contractType;
        public Vector2 gridPos;
        public int[] nextNodeIds;
        public string briefing;
        public int enemyCount;
        public int rewardCash;
        public int rewardXP;
    }

    public static class CampaignNodeGraph
    {
        public static readonly LevelNode[] Nodes = BuildAllNodes();

        static LevelNode[] BuildAllNodes()
        {
            var list = new List<LevelNode>(50);

            // Palette definitions
            Color colTarget = new Color(0.35f, 0.95f, 0.45f);  // Emerald
            Color colOverwatch = new Color(0.00f, 0.88f, 1.00f); // Cyan
            Color colRunner = new Color(1.00f, 0.55f, 0.15f);   // Amber
            Color colStealth = new Color(0.72f, 0.42f, 1.00f);  // Purple
            Color colEscape = new Color(0.95f, 0.25f, 0.55f);   // Rose
            Color colDuel = new Color(1.00f, 0.72f, 0.10f);     // Gold
            Color colApex = new Color(1.00f, 0.25f, 0.25f);     // Red

            // Authored specs for all 50 levels (id: 0..49)
            // (title, codename, modeTag, color, isDuel, type, gridX, gridY, nextIds, briefing)
            var specs = new (string title, string codename, string tag, Color color, bool isDuel, CampaignContractType type, float gx, float gy, int[] next, string briefing)[]
            {
                // Chapter 1: Recon & Basic Training (Levels 1 - 6, Tier 0 - 1)
                ("FIRST CONTACT", "OP: DAWN SWEEP", "FIRST CONTACT", colTarget, false, CampaignContractType.TargetIdentification, 0f, 0f, new int[] { 1, 2 }, "Initial field deployment: direct kill authorized on primary target. No wait required."),
                ("VIP OVERWATCH", "OP: GUARDIAN SHIELD", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 1f, new int[] { 3 }, "Provide overwatch for a walking informant through low-threat city streets. Eliminate hostiles before they intercept."),
                ("FUGITIVE SPRINT", "OP: FAST TRACK", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 1f, new int[] { 4 }, "Marked syndicate courier is sprinting toward extraction. Lead your target and eliminate before time expires."),
                ("SILENT INFILTRATION", "OP: SHADOW VEIL", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, -1.2f, 2f, new int[] { 5 }, "Eliminate the marked squad without raising maximum base alert. Time your shots when guards are isolated."),
                ("HOT EXTRACTION", "OP: RAPID EVAC", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, 1.2f, 2f, new int[] { 5 }, "Hold down the extraction zone against approaching search teams until the transport arrives."),
                ("RIVAL DUEL: GHOST", "OP: GHOST APEX", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 3f, new int[] { 6, 7 }, "Milestone confrontation: defeat rival sniper codenamed GHOST. Dodge incoming laser locks, take cover, and fire back."),

                // Chapter 2: Urban Syndicate Operations (Levels 7 - 13, Tier 2 - 3)
                ("PLAZA WARLORD", "OP: IRON HAMMER", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 4f, new int[] { 8 }, "Syndicate lieutenant is inspecting a central plaza. Heavily guarded by bodyguards. Neutralize the primary HVT."),
                ("CONVOY GUARDIAN", "OP: SENTINEL PATH", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, 1.2f, 4f, new int[] { 9 }, "Armored vehicle VIP is proceeding on foot across contested intersections. Clear rooftops and side alleys."),
                ("SHADOW COURIER", "OP: VELOCITY", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, -1.2f, 5f, new int[] { 10 }, "High-ranking messenger sprinting through alleyways. Faster route with tight firing windows between apartment blocks."),
                ("SYNDICATE CELL", "OP: BLACKOUT", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, 1.2f, 5f, new int[] { 11 }, "Infiltrate the supply depot. Multiple guards patrolling in pairs. Suppressed rifle recommended."),
                ("ROOFTOP RESCUE", "OP: ROOFTOP HAVEN", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, -1.2f, 6f, new int[] { 12 }, "Survive heavy hostile fire on the helipad rooftop while rescue transport approaches under pressure."),
                ("DISTRICT PURGE", "OP: SWEEPING FURY", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, 1.2f, 6f, new int[] { 12 }, "Syndicate quartermaster located. Rooftop counter-snipers spotted in the surrounding perimeter."),
                ("RIVAL DUEL: STALKER", "OP: SHADOW EYE", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 7f, new int[] { 13, 14 }, "Milestone confrontation: Veteran sniper STALKER defends this district with high-caliber armor-piercing rounds."),

                // Chapter 3: Industrial Stronghold (Levels 14 - 20, Tier 3 - 4)
                ("AMBUSH CORRIDOR", "OP: STEEL GRIP", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 8f, new int[] { 15 }, "Neutralize commander entrenched behind sandbag parapets. Multiple spotters covering sightlines."),
                ("HIGHWAY RUNNER", "OP: INTERCEPTOR", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 8f, new int[] { 16 }, "Fast courier dashing across highway overpasses. Long distance engagement requiring bullet drop calculation."),
                ("NIGHT WATCH", "OP: MOONLIT SHIELD", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 9f, new int[] { 17 }, "Escort undercover agent through factory district under industrial searchlights."),
                ("COVERT EXTRACT", "OP: GHOST RUN", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, 1.2f, 9f, new int[] { 18 }, "Eliminate guards in manufacturing zone without triggering alarm klaxons."),
                ("ARMORED TARGET", "OP: HEAVY SHELL", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 10f, new int[] { 19 }, "Elite syndicate enforcer wearing body armor. Headshots or multiple center-mass hits required."),
                ("STREET SWEEP", "OP: CLEAR PATH", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, 1.2f, 10f, new int[] { 19 }, "Syndicate reinforcements converging on your position. Repel hostiles until extraction arrives."),
                ("RIVAL DUEL: VIPER", "OP: VIPER NEST", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 11f, new int[] { 20, 21 }, "Milestone confrontation: VIPER repositions between shots. Extremely rapid laser-lock window."),

                // Chapter 4: Port & Docks Contested Zone (Levels 21 - 27, Tier 5 - 6)
                ("DOCKS OVERWATCH", "OP: HARBOR GUARDIAN", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 12f, new int[] { 22 }, "Protect informant navigating cargo container maze. Enemies flank from multiple elevated walkways."),
                ("CARGO RUNNER", "OP: DOCK SPRINT", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 12f, new int[] { 23 }, "Smuggler racing toward getaway speedboat. Intercept before he rounds the warehouse corner."),
                ("SILENT SILO", "OP: WHISPER DEPTH", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, -1.2f, 13f, new int[] { 24 }, "Infiltrate port silos. Rooftop guards maintain overlapping vision cones. Extreme precision required."),
                ("SECTOR CUTOFF", "OP: ANCHOR BREAK", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, 1.2f, 13f, new int[] { 25 }, "Eliminate arms dealer orchestrating contraband shipment. Bodyguard fire team armed with automatic rifles."),
                ("CRANE PERCH", "OP: SKYFALL", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, -1.2f, 14f, new int[] { 26 }, "Defend the crane staging deck. Hostiles scaling ladders and firing from neighboring container stacks."),
                ("CROSSFIRE BAY", "OP: SEA BREEZE", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, 1.2f, 14f, new int[] { 26 }, "Engage syndicate captains meeting on pier. High crosswind conditions testing your trajectory adjustments."),
                ("RIVAL DUEL: COBRA", "OP: COBRA FANG", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 15f, new int[] { 27, 28 }, "Milestone confrontation: Master marksman COBRA. Fires suppressed high-damage rounds with minimal warning."),

                // Chapter 5: Financial District High-Rise (Levels 28 - 34, Tier 6 - 7)
                ("DOWNTOWN SIEGE", "OP: GLASS CANYON", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 16f, new int[] { 29 }, "Syndicate financier barricaded on banking plaza. Multiple sharpshooters on opposite balcony."),
                ("RAPID PURSUIT", "OP: METRO RUSH", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 16f, new int[] { 30 }, "High-speed courier dashing through subway station plaza. High pedestrian density requiring clean identification."),
                ("MIDNIGHT ESCORT", "OP: BLACK SHIELD", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 17f, new int[] { 31 }, "Defend ambassador traversing central avenue. Hostile squads attacking from side vehicles."),
                ("SAFE-HOUSE RUN", "OP: PHANTOM CORRIDOR", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, 1.2f, 17f, new int[] { 32 }, "Neutralize security ring around corporate tower without triggering sector-wide lockdown."),
                ("BARRICADE ASSAULT", "OP: BREAKTHROUGH", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 18f, new int[] { 33 }, "Target entrenched inside armored outpost. Pick off perimeter guards before taking the leader down."),
                ("CHOPPER DEFENSE", "OP: IRON RESCUE", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, 1.2f, 18f, new int[] { 33 }, "Protect stranded reconnaissance team while gunship provides suppressing air cover."),
                ("RIVAL DUEL: TITAN", "OP: TITAN LOCK", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 19f, new int[] { 34, 35 }, "Milestone confrontation: Heavy-caliber sniper TITAN. Hits inflict massive shock damage; stay in cover."),

                // Chapter 6: Citadel Infiltration (Levels 35 - 41, Tier 7 - 8)
                ("PERIMETER SHIELD", "OP: CITADEL GATE", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 20f, new int[] { 36 }, "Cover friendly assault team breaching perimeter gates. Heavy hostile resistance from fortified bunkers."),
                ("SPRINT CROSSROADS", "OP: BULLET TRAIN", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 20f, new int[] { 37 }, "High-ranking warlord aide racing to armored convoy. Firing window under 14 seconds."),
                ("GHOST RECON", "OP: SILENT SHADOW", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, -1.2f, 21f, new int[] { 38 }, "Infiltrate inner courtyard. Highly trained guards with rapid alert response and tight patrol routes."),
                ("ALLEY PURGE", "OP: RAZOR EDGE", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, 1.2f, 21f, new int[] { 39 }, "Eliminate weapon supplier distributing rocket munitions. Multiple counter-snipers on high ground."),
                ("BUNKER DEFENSE", "OP: LAST STAND", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, -1.2f, 22f, new int[] { 40 }, "Survive three-wave assault from elite syndicate shock-troopers armed with tactical AK rifles."),
                ("COMMAND POST", "OP: CROWN STRIKE", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, 1.2f, 22f, new int[] { 40 }, "Neutralize regional syndicate commander and his personal security detail."),
                ("RIVAL DUEL: BLACKOUT", "OP: BLACKOUT STORM", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 23f, new int[] { 41, 42 }, "Milestone confrontation: Elite assassin BLACKOUT. Uses smoke and brief window peaks. Requires sharp reflexes."),

                // Chapter 7: Apex Operations & Black Ops (Levels 42 - 47, Tier 8 - 9)
                ("EMBASSY UNDER FIRE", "OP: EMBASSY RESCUE", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, -1.2f, 24f, new int[] { 43 }, "Diplomatic compound overrun by insurgent commandos. Cover the diplomatic staff across open grounds."),
                ("EXPRESSWAY INTERCEPT", "OP: SONIC DASH", "TIMED RUNNER", colRunner, false, CampaignContractType.TimedInterception, 1.2f, 24f, new int[] { 44 }, "Fugitive escaping via multi-level highway. Extreme lead required on distant moving target."),
                ("PHANTOM STRIKE", "OP: ZERO TRACE", "STEALTH SYNDICATE", colStealth, false, CampaignContractType.Stealth, -1.2f, 25f, new int[] { 45 }, "Silent strike against apex warlord communications hub. Single mistake triggers instant sector alarm."),
                ("LZ EXTRACTION", "OP: HELO RUN", "HOT EXTRACTION", colEscape, false, CampaignContractType.Escape, 1.2f, 25f, new int[] { 46 }, "Defend the extraction point against overwhelming waves of tactical soldiers and counter-snipers."),
                ("HEAVY BARRAGE", "OP: WAR STORM", "HVT IDENTIFY", colTarget, false, CampaignContractType.TargetIdentification, -1.2f, 26f, new int[] { 47 }, "Defeat the warlord's elite vanguard force. High armor, tactical cover usage, and aggressive return fire."),
                ("SKYLINE WATCH", "OP: CLOUD PINNACLE", "VIP OVERWATCH", colOverwatch, false, CampaignContractType.Overwatch, 1.2f, 26f, new int[] { 47 }, "Long-distance overwatch across city rooftops. Extreme distance bullet drop and crosswind mastery."),
                ("RIVAL DUEL: APEX SPIRE", "OP: APEX HORIZON", "AI SNIPER DUEL", colDuel, true, CampaignContractType.TargetIdentification, 0f, 27f, new int[] { 48 }, "Supreme rival duel: The syndicate's master sniper at maximum lethal precision. Sub-second laser lock."),

                // Chapter 8: Grand Finale (Levels 49 - 50, Tier 10 - Apex End Game)
                ("FORTRESS COLLAPSE", "OP: CITADEL FALL", "HVT IDENTIFY", colApex, false, CampaignContractType.TargetIdentification, 0f, 28f, new int[] { 49 }, "Penultimate strike: Infiltrate the warlord's central stronghold. Clear fortified perimeter defenses and outer guards."),
                ("FINAL EXTINCTION", "OP: GRAND ENDGAME", "HVT IDENTIFY", colApex, false, CampaignContractType.TargetIdentification, 0f, 29f, new int[0], "The ultimate campaign operation: eliminate the Supreme Syndicate Warlord and his elite cadre of bodyguard snipers. Maximum difficulty, maximum hostiles.")
            };

            for (int i = 0; i < specs.Length; i++)
            {
                var s = specs[i];
                int tier = Mathf.Clamp(i / 5, 0, 10);
                if (i >= 48) tier = 10;

                int enemies;
                if (s.isDuel)
                {
                    enemies = 1;
                }
                else
                {
                    // Scales smoothly from 2 enemies at Level 1 up to 8 enemies at Level 50
                    enemies = Mathf.Clamp(2 + (int)(i * 0.14f), 2, 8);
                }

                int cash = 200 + i * 95;
                int xp = 100 + i * 48;
                if (s.isDuel) { cash = (int)(cash * 1.35f); xp = (int)(xp * 1.3f); }
                if (i == specs.Length - 1) { cash = 5000; xp = 2500; }

                list.Add(new LevelNode
                {
                    id = i,
                    difficultyTier = tier,
                    contractType = s.type,
                    title = s.title,
                    codeName = s.codename,
                    modeTag = s.tag,
                    modeColor = s.color,
                    isPvPDuel = s.isDuel,
                    stageIndex = s.isDuel ? -1 : i,
                    gridPos = new Vector2(s.gx, s.gy),
                    nextNodeIds = s.next,
                    briefing = s.briefing,
                    enemyCount = enemies,
                    rewardCash = cash,
                    rewardXP = xp
                });
            }

            return list.ToArray();
        }

        public static LevelNode GetNode(int id)
        {
            if (id < 0 || id >= Nodes.Length) return null;
            return Nodes[id];
        }

        public static bool IsNodeCompleted(int id)
        {
            return PlayerPrefs.GetInt("GeoSniper.NodeCompleted_" + id, 0) == 1;
        }

        public static bool IsNodeUnlocked(int id)
        {
            if (id < 0 || id >= Nodes.Length) return false;
            if (id == 0) return true;
            if (IsNodeCompleted(id)) return true;

            // Check if any parent node is completed
            for (int i = 0; i < Nodes.Length; i++)
            {
                var n = Nodes[i];
                if (n.nextNodeIds != null)
                {
                    for (int k = 0; k < n.nextNodeIds.Length; k++)
                    {
                        if (n.nextNodeIds[k] == id && IsNodeCompleted(n.id))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public static NodeState GetNodeState(int id, int selectedId)
        {
            bool completed = IsNodeCompleted(id);
            bool unlocked = IsNodeUnlocked(id);

            if (!unlocked) return NodeState.Locked;
            if (id == selectedId) return NodeState.Current;
            if (completed) return NodeState.Completed;
            if (unlocked) return NodeState.Available;
            return NodeState.Locked;
        }

        // Progress only. UrbanCombatMission is the single authority for reward payment.
        public static void CompleteNode(int id)
        {
            var node = GetNode(id);
            if (node == null || !IsNodeUnlocked(id)) return;
            PlayerPrefs.SetInt("GeoSniper.NodeCompleted_" + id, 1);
            if (node.stageIndex >= 0)
            {
                PlayerPrefs.SetInt("GeoSniper.StageUnlocked", Mathf.Max(PlayerPrefs.GetInt("GeoSniper.StageUnlocked", 0), node.stageIndex + 1));
            }
            PlayerPrefs.Save();
        }

        public static LevelNode ForStage(int index)
        {
            foreach (var node in Nodes)
            {
                if (!node.isPvPDuel && node.stageIndex == index) return node;
            }
            if (index >= 0 && index < Nodes.Length) return Nodes[index];
            return null;
        }

        public static int GetCompletedCount()
        {
            int count = 0;
            for (int i = 0; i < Nodes.Length; i++)
            {
                if (IsNodeCompleted(Nodes[i].id)) count++;
            }
            return count;
        }

        public static int TotalNodeCount => Nodes.Length;
    }
}
