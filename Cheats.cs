using BepInEx.Logging;
using Pigeon.Movement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MenuMod2
{
    public static class Cheats
    {
        public static ManualLogSource Logger = MenuMod2.Logger;
        public static int previousAirJumps = 0;
        public static float previousAirJumpSpeed = 0;
        public static bool god = false;
        public static bool sprintFast = false;
        public static bool superJump = false;
        public static bool airJump = false;
        public static List<MissionModifier> forcedModifiers = new List<MissionModifier>();
        public static void giveAllUpgrades(MM2Button b = null)
        {
            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Invalid && upgrade.UpgradeType != Upgrade.Type.Cosmetic)
                    {
                        var iUpgrade = new UpgradeInstance(upgrade, gear);
                        PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                        iUpgrade.Unlock(true);
                    }
                }
            }
            SendTextChatMessageToClient("All upgrades for weapons are added silently.");
        }

        public static void giveAllCosmetics(MM2Button b = null)
        {
            const string debugPattern = @"(_test_|_dev_|_wip|debug|temp|placeholder|todo|_old|_backup|_copy|\.skinasset$|^test_)";
            
            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Cosmetic ||
                        upgrade.ExcludeFromWorldPool != false ||
                        Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase)) 
                        continue;
                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }

            foreach (var gear in Global.Instance.Characters)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    if (upgrade.UpgradeType != Upgrade.Type.Cosmetic ||
                        Regex.IsMatch(upgrade.Name, debugPattern, RegexOptions.IgnoreCase))
                        continue;
                    
                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }
            
            SendTextChatMessageToClient("All cosmetics for characters and weapons are added silently.");
        }

        public static void giveMissingUpgrades(MM2Button b = null)
        {

            foreach (var gear in Global.Instance.AllGear)
            {
                var gearInfo = gear.Info;
                foreach (var upgrade in gearInfo.Upgrades)
                {
                    var iUpgrade = new UpgradeInstance(upgrade, gear);
                    if ((upgrade.UpgradeType == Upgrade.Type.Invalid || upgrade.UpgradeType == Upgrade.Type.Cosmetic) || (PlayerData.GetUnlockedInstances(upgrade) != null && PlayerData.GetUnlockedInstances(upgrade).Instances != null && PlayerData.GetUnlockedInstances(upgrade).Instances.Count > 0))
                    {
                        continue;
                    }
                    Logger.LogInfo($"Unlocking upgrade {upgrade.Name} for gear {gearInfo.Name}");
                    PlayerData.CollectInstance(iUpgrade, PlayerData.UnlockFlags.Hidden);
                    iUpgrade.Unlock(true);
                }
            }
            SendTextChatMessageToClient("All missing upgrades are added silently.");
        }
        public static void giveUpgrade(Upgrade upgrade, IUpgradable gear, MM2Button b = null)
        {
            var iUpgrade = new UpgradeInstance(upgrade, gear);
            PlayerData.CollectInstance(iUpgrade);
            iUpgrade.Unlock(true);
        }
        public static void printAllUpgradeStats(MM2Button b = null)
        {
            List<String> spawnPoolWhitelist = new List<String>();
            spawnPoolWhitelist.Add("Broke1");
            spawnPoolWhitelist.Add("Broke2");
            spawnPoolWhitelist.Add("Broke3");
            spawnPoolWhitelist.Add("Broke4");
            spawnPoolWhitelist.Add("PrismEp");
            spawnPoolWhitelist.Add("PrismEx");
            spawnPoolWhitelist.Add("PrismR");
            spawnPoolWhitelist.Add("PrismS");

            List<Type> upgradePropertyBlacklist = new List<Type>();
            upgradePropertyBlacklist.Add(typeof(UpgradeProperty_UpgradeFlag));

            foreach (var gear in Global.Instance.AllGear)
            {
                GearInfo gearInfo = gear.Info;
                if (gearInfo == null || !gearInfo.HasUpgradeGrid || gearInfo.Upgrades.Count() == 0) continue;
                var gearName = gearInfo.Name;
                MenuMod2.Logger.LogInfo($"Gear: {gearName}");
                var allUpgrades = gearInfo.Upgrades;
                if (allUpgrades == null || allUpgrades.Length == 0) continue;
                foreach (var upgrade in allUpgrades)
                {
                    if (upgrade.UpgradeType == Upgrade.Type.Invalid || upgrade.UpgradeType == Upgrade.Type.Cosmetic) continue;
                    if (upgrade.ExcludeFromWorldPool && !spawnPoolWhitelist.Contains(upgrade.APIName)) continue;
                    var cleanName = MenuMod2.CleanRichText(upgrade.Name);
                    var cleanDesc = MenuMod2.CleanRichText(upgrade.Description);
                    MenuMod2.Logger.LogInfo($"-\tUpgrade: {cleanName} [{upgrade.RarityName}] - {cleanDesc}");
                    var properties = upgrade.Properties;
                    if (properties.HasProperties == false) continue;
                    foreach (var property in properties)
                    {
                        if (upgradePropertyBlacklist.Contains(property.GetType())) continue;
                        MenuMod2.Logger.LogInfo($"-\t-\tProperty: {property}");
                        var propertyFields = property.GetType().GetFields(
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.DeclaredOnly);
                        LogFieldsRecursive(property);
                    }
                }
            }
            void LogFieldsRecursive(object obj, string indent = "-\t-\t-\t", int depth = 0, int maxDepth = 10)
            {
                if (obj == null || depth > maxDepth)
                    return;

                Type type = obj.GetType();

                if (type.IsPrimitive || type == typeof(string) || type == typeof(Vector2) || type == typeof(Vector3) || type.IsEnum)
                {
                    MenuMod2.Logger.LogInfo($"{indent}{type.Name} = {obj}");
                    return;
                }

                var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);

                if (fields.Length == 0)
                {
                    MenuMod2.Logger.LogInfo($"{indent}{type.Name} has no fields to log.");
                    return;
                }

                //Logger.LogInfo($"{indent}Found {fields.Length} fields in {type.Name}");

                foreach (var field in fields)
                {
                    object value = field.GetValue(obj);

                    if (value == null)
                    {
                        MenuMod2.Logger.LogInfo($"{indent}{field.Name} = null");
                    }
                    else
                    {
                        Type valueType = value.GetType();

                        if (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(Vector2) || valueType == typeof(Vector3) || valueType.IsEnum)
                        {
                            MenuMod2.Logger.LogInfo($"{indent}{field.Name} = {value} ({valueType.Name})");
                        }
                        else
                        {
                            MenuMod2.Logger.LogInfo($"{indent}{field.Name} = {valueType.Name}");
                            LogFieldsRecursive(value, indent + "-\t", depth + 1, maxDepth);
                        }
                    }
                }
            }
        }
        public static void spawnObject(string name, MM2Button b = null)
        {
            Vector3 position = Player.LocalPlayer.gameObject.transform.position;
            Vector3 lookDirection = Player.LocalPlayer.PlayerLook.Camera.transform.forward;
            if (Physics.Raycast(position, lookDirection, out RaycastHit hit, 100000))
            {
                //SendTextChatMessageToClient($"Hit pos: {hit.point}");
                GameObject gobject = MenuMod2.findObjectByName(name);
                if (gobject == null)
                {
                    return;
                }
                GameObject spawned = UnityEngine.Object.Instantiate(gobject);
                spawned.transform.position = hit.point;
                if (!FindValidSpawnPos(ref spawned))
                {
                    SendTextChatMessageToClient("Could not find valid spawn position in look direction.");
                    return;
                }
                spawned.GetComponent<Unity.Netcode.NetworkObject>()?.Spawn(true);
            }
            else
            {
                SendTextChatMessageToClient("Invalid look position.");
            }
        }
        public static void SendDebugTextChatMessage(MM2Button b = null)
        {
            MenuMod2.Logger.LogInfo("Sending test message to chat");
            SendTextChatMessageToClient("Debug Message this message is very long, longer than the 128 char limit. does it show everything, or what happens. " +
                                        "I dont' know untill I test it.  But you'll know when you're reading this! \nDebug Message this message is very long, longer than the 128 " +
                                        "char limit. does it show everything, or what happens.  I dont' know untill I test it.  But you'll know when you're reading this!");
        }
        public static void SendTextChatMessageToClient(string Message, MM2Button b = null)
        {
            Player.LocalPlayer.PlayerLook.AddTextChatMessage(Message, Player.LocalPlayer);
        }

        public static bool FindValidSpawnPos(ref GameObject obj, MM2Button b = null) //AI GENERATED
        {
            Vector3 startPos = obj.transform.position;
            if (obj == null)
                return false;

            obj.SetActive(false); // Prevent interactions before placing

            // Get combined bounds
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogWarning("No renderers found, cannot calculate bounds.");
                UnityEngine.Object.Destroy(obj);
                return false;
            }

            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            Vector3 halfExtents = bounds.extents;
            Quaternion rotation = obj.transform.rotation;

            Vector3 offset = bounds.center - obj.transform.position;
            Vector3 checkPosition = startPos;

            const int maxAttempts = 100;
            const float step = 1f;

            for (int i = 0; i < maxAttempts; i++)
            {
                Vector3 testCenter = checkPosition + offset;
                if (!Physics.CheckBox(testCenter, halfExtents, rotation))
                {
                    obj.transform.position = checkPosition;
                    obj.SetActive(true);
                    return true;
                }

                checkPosition += Vector3.up * step;
            }

            Debug.LogWarning("No valid spawn position found. Destroying object.");
            UnityEngine.Object.Destroy(obj);
            return false;
        }
        public static void toggleGod(MM2Button button = null)
        {
            if (god)
            {
                Player.LocalPlayer.SetMaxHealth(37.5f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
                god = false;
            }
            else
            {
                Player.LocalPlayer.SetMaxHealth(999999f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                god = true;
            }
            if (button != null)
            {
                if (god)
                {
                    button.changeColour(Color.green);
                }
                else
                {
                    button.changeColour(Color.red);
                }
            }
        }
        public static void toggleSprintFast(MM2Button b = null)
        {
            if (sprintFast)
            {
                Player.LocalPlayer.DefaultMoveSpeed = 10;
                sprintFast = false;
            }
            else
            {
                Player.LocalPlayer.DefaultMoveSpeed = 100;
                sprintFast = true;
            }
            if (b != null)
            {
                if (sprintFast)
                {
                    b.changeColour(Color.green);
                }
                else
                {
                    b.changeColour(Color.red);
                }
            }
        }
        public static void toggleSuperJump(MM2Button b = null)
        {
            if (superJump)
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 14f);
                superJump = false;
            }
            else
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 100f);
                superJump = true;
            }
            if (b != null)
            {
                if (superJump)
                {
                    b.changeColour(Color.green);
                }
                else
                {
                    b.changeColour(Color.red);
                }
            }
        }
        public static void setGod(bool enabled, MM2Button b = null)
        {
            if (enabled)
            {
                Player.LocalPlayer.SetMaxHealth(999999f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 999999f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 999999f });
            }
            else
            {
                Player.LocalPlayer.SetMaxHealth(37.5f);
                MethodInfo setHealthClient = typeof(Player).GetMethod("SetHealth_Client", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthClient?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
                MethodInfo setHealthOwner = typeof(Player).GetMethod("SetHealth_Owner", BindingFlags.NonPublic | BindingFlags.Instance);
                setHealthOwner?.Invoke(Player.LocalPlayer, new object[] { 37.5f });
            }
        }
        public static void setSprintFast(bool enabled, MM2Button b = null)
        {
            if (enabled)
            {
                Player.LocalPlayer.DefaultMoveSpeed = 100;
            }
            else
            {
                Player.LocalPlayer.DefaultMoveSpeed = 10;
            }
        }
        public static void setSuperJump(bool enabled, MM2Button b = null)
        {
            if (enabled)
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 100f);
            }
            else
            {
                FieldInfo field = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, 14f);
            }
        }
        public static void setAirJump(bool enabled, MM2Button b = null) // TODO BROKEN
        {
            if (enabled)
            {
                FieldInfo airJumps = typeof(Player).GetField("airJumps", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo airJumpSpeed = typeof(Player).GetField("airJumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                if ((int)airJumps.GetValue(Player.LocalPlayer) != 100)
                    previousAirJumps = (int)airJumps.GetValue(Player.LocalPlayer);
                if ((int)airJumpSpeed.GetValue(Player.LocalPlayer) != 100)
                    previousAirJumpSpeed = (float)airJumpSpeed.GetValue(Player.LocalPlayer);
                airJumps.SetValue(Player.LocalPlayer, 100);

                FieldInfo jumpSpeed = typeof(Player).GetField("jumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                airJumpSpeed.SetValue(Player.LocalPlayer, jumpSpeed.GetValue(Player.LocalPlayer));
            }
            else
            {
                FieldInfo field = typeof(Player).GetField("airJumps", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, previousAirJumps);
                FieldInfo airJumpSpeed = typeof(Player).GetField("airJumpSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(Player.LocalPlayer, previousAirJumpSpeed);
            }
        }
        public static void enemySpawning(bool enabled, MM2Button b = null)
        {
            if (enabled)
                EnemyManager.Instance.EnableSpawning();
            else
                EnemyManager.Instance.DisableSpawning();
        }
        public static MM2Button toggleSpawning(MM2Button b = null)
        {
            FieldInfo field = typeof(EnemyManager).GetField("enableAmbientWave", BindingFlags.NonPublic | BindingFlags.Instance);
            if ((bool)field.GetValue(EnemyManager.Instance))
            {
                EnemyManager.Instance.DisableSpawning();
                if (b != null)
                {
                    b.changeColour(Color.red);
                }
            }
            else
            {
                EnemyManager.Instance.EnableSpawning();
                if (b != null)
                {
                    b.changeColour(Color.green);
                }
            }
            return b;
        }
        public static void killAllEnemies(MM2Button b = null)
        {
            EnemyManager.Instance.KillAllEnemies_Server();
        }
        public static void spawnSwarm(int size, MM2Button b = null)
        {
            EnemyManager.Instance.SpawnSwarm_ServerRpc(size);
        }
        public static void toggleForceModifier(MissionModifier modifier, MM2Button b = null)
        {
            if (forcedModifiers.Contains(modifier))
            {
                forcedModifiers.Remove(modifier);
                if (b != null)
                {
                    b.changePrefix("");
                    b.changeColour(Color.white);
                }
                Logger.LogDebug($"Removed forced modifier: {modifier.ModifierName}");
            }
            else
            {
                forcedModifiers.Add(modifier);
                if (b != null)
                {
                    b.changePrefix("* ");
                    b.changeColour(Color.green);
                }
                Logger.LogDebug($"Added forced modifier: {modifier.ModifierName}");
            }
        }
        public static void cleanUpParts(MM2Button b = null)
        {
            List<EnemyPart> enemyParts = GameObject.FindObjectsOfType<EnemyPart>().ToList();
            foreach (var part in enemyParts)
            {
                part.Kill(DamageFlags.Despawn);
            }
        }
        public static void cleanUpCollectables(MM2Button b = null)
        {
            List<ClientCollectable> collectables = GameObject.FindObjectsOfType<ClientCollectable>().ToList();
            foreach (var part in collectables)
            {
                part.DespawnTrackedObject();
            }
        }

        public static void collectGear(IUpgradable gear, MM2Button b = null)
        {
            PlayerData.CollectGear(gear);
            PlayerData.GetGearData(gear).Collect();
            if (b != null)
            {
                b.changeColour(Color.yellow);
            }
        }
        public static void unlockGear(IUpgradable gear, MM2Button b = null)
        {
            PlayerData.UnlockGear(gear);
            PlayerData.GetGearData(gear).Unlock();
            if (b != null)
            {
                b.changeColour(Color.green);
            }
        }
        public static void loadMission(Mission mission, WorldRegion region, MM2Button b = null)
        {
            //public MissionData(int seed, Mission mission, WorldRegion region, string scene, MissionContainer container)
            MissionData missionData = new MissionData(MissionManager.MissionSeed, mission, region, region.SceneNames[0], Global.Instance.DefaultMissionContainer); // TODO fix random seed
            DropPod.SetMission(missionData);
        }
        public static void giveAllResoruces(MM2Button b = null)
        {
            var allResources = Global.Instance.PlayerResources;
            foreach (var resource in allResources)
            {
                PlayerData.Instance.AddResource(resource, resource.Max);
            }
        }
        public static void levelUpAllWeapons(int level, MM2Button b = null)
        {
            var allGear = Global.Instance.AllGear;
            MenuMod2.Logger.LogDebug($"found {allGear.Length} gear items to level up.");
            foreach (var gear in allGear)
            {
                levelUpWeapon(gear, level);
            }
        }
        public static void levelUpWeapon(IUpgradable gear, int level, MM2Button b = null)
        {
            MenuMod2.Logger.LogInfo($"Leveling up {gear.Info.Name} to level {level}");
            var gearData = PlayerData.GetGearData(gear);
            if (gearData.IsUnlocked)
            {
                var levelField = gearData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                int currentLevel = (int)levelField.GetValue(gearData);
                levelField.SetValue(gearData, Math.Max(level, currentLevel));
            }
        }
        public static void setAllCharictersToLevel(int level, MM2Button b = null)
        {
            var allCharicters = Global.Instance.Characters;
            MenuMod2.Logger.LogDebug($"found {allCharicters.Length} characters to set level for.");
            foreach (var employee in allCharicters)
            {
                MenuMod2.Logger.LogInfo($"Setting level for {employee.name} to {level}");
                var employeeData = PlayerData.GetGearData(employee);
                if (employeeData.IsUnlocked)
                {
                    var levelField = employeeData.GetType().GetField("level", BindingFlags.NonPublic | BindingFlags.Instance);
                    int currentLevel = (int)levelField.GetValue(employeeData);
                    levelField.SetValue(employeeData, Math.Max(level, currentLevel));
                }
            }
        }
        public static void giveAllSkills(MM2Button b = null)
        {
            var allCharicters = Global.Instance.Characters;
            MenuMod2.Logger.LogDebug($"found {allCharicters.Length} characters to unlock skills for.");
            //var allSkillTrees = allCharicters.Select(c => c.SkillTree).ToList();
            foreach (var employee in allCharicters)
            {
                MenuMod2.Logger.LogDebug($"Unlocking all skills for {employee.name}");
                var skillTree = employee.SkillTree;
                SkillTreeUpgradeUI[] upgrades = skillTree.GetComponentsInChildren<SkillTreeUpgradeUI>();
                foreach (var upgrade in upgrades)
                {
                    MenuMod2.Logger.LogInfo($"Unlocking skill {upgrade.Upgrade.Name} for {employee.name}");
                    UpgradeInstance upgradeInstance = PlayerData.CollectInstance(employee, upgrade.Upgrade, PlayerData.UnlockFlags.None);
                    upgradeInstance.Seed = upgrade.Upgrade.ID;
                    upgradeInstance.Unlock(false);
                    PlayerData instance = PlayerData.Instance;
                    int totalSkillPointsSpent = instance.TotalSkillPointsSpent;
                    instance.TotalSkillPointsSpent = totalSkillPointsSpent + 1;
                    skillTree.Refresh();
                }

            }

        }
        public static void unlockLockedSkills(MM2Button b = null)
        {
            var allCharicters = Global.Instance.Characters;
            MenuMod2.Logger.LogDebug($"found {allCharicters.Length} characters to unlock skills for.");
            //var allSkillTrees = allCharicters.Select(c => c.SkillTree).ToList();
            foreach (var employee in allCharicters)
            {
                MenuMod2.Logger.LogDebug($"Unlocking all skills for {employee.name}");
                var skillTree = employee.SkillTree;
                SkillTreeUpgradeUI[] upgrades = skillTree.GetComponentsInChildren<SkillTreeUpgradeUI>();
                foreach (var upgrade in upgrades)
                {
                    if (PlayerData.GetUpgradeInfo(employee, upgrade.Upgrade).TotalInstancesCollected > 0)
                    {
                        MenuMod2.Logger.LogInfo($"Skill {upgrade.Upgrade.Name} is already unlocked for {employee.name}");
                        continue;
                    }
                    MenuMod2.Logger.LogInfo($"Unlocking skill {upgrade.Upgrade.Name} for {employee.name}");
                    UpgradeInstance upgradeInstance = PlayerData.CollectInstance(employee, upgrade.Upgrade, PlayerData.UnlockFlags.None);
                    upgradeInstance.Seed = upgrade.Upgrade.ID;
                    upgradeInstance.Unlock(false);
                    PlayerData instance = PlayerData.Instance;
                    int totalSkillPointsSpent = instance.TotalSkillPointsSpent;
                    instance.TotalSkillPointsSpent = totalSkillPointsSpent + 1;
                    skillTree.Refresh();
                }

            }
        }

        public static void spawnEnemy(EnemyClass enemyClass, Vector3 pos = default, MM2Button b = null)
        {
            if (pos == default)
            {
                EnemyManager.Instance.SpawnEnemy_Server(enemyClass);
                return;
            }
            EnemyManager.Instance.SpawnEnemy_Server(pos, enemyClass);
        }
        public static void spawnEnemy(EnemyClassGroup enemyClassGroup, Vector3 pos = default, MM2Button b = null)
        {
            if (pos == default)
            {
                EnemyManager.Instance.SpawnEnemy_Server(enemyClassGroup);
                return;
            }
            EnemyManager.Instance.SpawnEnemy_Server(pos, enemyClassGroup);
        }
    }
}