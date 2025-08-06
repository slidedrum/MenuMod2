using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Pigeon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static PlayerAnimation;

namespace MenuMod2
{
    [MycoMod(null, ModFlags.IsClientSide | ModFlags.IsSandbox)]
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class MenuMod2 : BaseUnityPlugin
    {
        public static ManualLogSource Logger;
        public InputActionMap _actionmap;
        private InputAction _openMenu;
        public GameObject menuCanvas;
        private bool enabled = false;
        public List<MenuMod2Menu> allMenues = new List<MenuMod2Menu>();
        public MenuMod2Menu mainMenu;

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Logger.LogDebug($"Scene loaded: {scene.name}");
            if (PlayerData.ProfileConfig.Instance == null)
                return;
            FieldInfo field = typeof(PlayerData.ProfileConfig).GetField("profileIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Logger.LogError("Could not find profileIndex field in PlayerData.");
                return;
            }
            int profileIndex = (int)field.GetValue(PlayerData.ProfileConfig.Instance);
            if (EnemyManager.Instance != null)
            {
                if (profileIndex == 0)
                {
                    Logger.LogWarning($"Using default profile is not supported.  Please switch to a different profile on the main menu.");
                    _actionmap.Disable();
                    return;
                }
                createMainMenu();
            }
        }

        public void Awake()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
            _actionmap = new InputActionMap("MenuMod2");
            _openMenu = _actionmap.AddAction("openMenu", binding: "<Keyboard>/backquote");
            _openMenu.performed += ctx => toggleMenu();
            _actionmap.Enable();
            var harmony = new Harmony("com.SlideDrum.menumod2");
            harmony.PatchAll();
        }

        public void createMainMenu()
        {
            if (mainMenu != null)
            {
                Logger.LogWarning("Main menu already exists, This should not happen.");
                return;
            }
            Logger.LogInfo("Creating MenuMod2!");
            mainMenu = new MenuMod2Menu("Main Menu");

            MenuMod2Menu spawnMenu = new MenuMod2Menu("Spawn Menu", mainMenu); //TODO add host check for network objects
            MenuMod2Menu vehicleSpawnMenu = new MenuMod2Menu("Vehicle", spawnMenu);
            vehicleSpawnMenu.addButton("Spawn Dart", () => Cheats.spawnObject("Kart"));
            vehicleSpawnMenu.addButton("Spawn WheelBox", () => Cheats.spawnObject("WheelBox"));
            MenuMod2Menu enemySpawnMenu = new MenuMod2Menu("Enemy", spawnMenu);
            MenuMod2Menu spawnBossMenu = new MenuMod2Menu("BOSSES", enemySpawnMenu);
            spawnBossMenu.addButton("Spawn Amalgamation", () => Cheats.spawnEnemy(Global.Instance.EnemyClasses.FirstOrDefault(x => x.APIName == "amalgamation")));
            spawnBossMenu.addButton("Spawn Cranius", () => Cheats.spawnEnemy(Global.Instance.EnemyClasses.FirstOrDefault(x => x.APIName == "cranius")));

            var enemyGroups = GetItemsFromWeightedArray<EnemyClassGroup>(EnemyManager.Instance.EnemyClassGroups);
            foreach (var group in enemyGroups)
            {
                var groupName = group.name;
                MenuMod2Menu thisMenu = new MenuMod2Menu(groupName, enemySpawnMenu);
                thisMenu.addButton("Random", () => Cheats.spawnEnemy(group));
                var groupClasses = GetItemsFromWeightedArray<EnemyClass>(group.enemyClasses);
                foreach (var enemyClass in groupClasses)
                {
                    thisMenu.addButton(enemyClass.name, () => Cheats.spawnEnemy(enemyClass));
                }
            }
            MenuMod2Menu objectSpawnMenu = new MenuMod2Menu("Object", spawnMenu);
            objectSpawnMenu.addButton("Saxitos", () => Cheats.spawnObject("SaxitosBag"));
            objectSpawnMenu.addButton("Radio", () => Cheats.spawnObject("Jukebox"));
            objectSpawnMenu.addButton("Barrel", () => Cheats.spawnObject("HoldableBarrel"));
            objectSpawnMenu.addButton("Dummy", () => Cheats.spawnObject("TrainingDummy"));
            objectSpawnMenu.addButton("Box", () => Cheats.spawnObject("NoteBlock"));
            objectSpawnMenu.addButton("Bomb", () => Cheats.spawnObject("ExplosiveHotPotato"));
            objectSpawnMenu.addButton("Bear", () => Cheats.spawnObject("BearPhys"));
            objectSpawnMenu.addButton("Toilet Paper", () => Cheats.spawnObject("TP"));
            objectSpawnMenu.addButton("Milk", () => Cheats.spawnObject("MilkJug"));

            MenuMod2Menu enemyMenu = new MenuMod2Menu("Enemys", mainMenu);
            {
                MM2Button button = null;
                button = enemyMenu.addButton("Toggle enemy spawning", () => Cheats.toggleSpawning(button)).changeColour(Color.green);
            }
            enemyMenu.addButton("Kill all enemies", () => Cheats.killAllEnemies());
            enemyMenu.addButton("Spawn swarm", () => Cheats.spawnSwarm(10));
            enemyMenu.addButton("Clean up parts", () => Cheats.cleanUpParts());
            enemyMenu.addButton("Clean up collectables", () => Cheats.cleanUpCollectables());

            MenuMod2Menu playerMenu = new MenuMod2Menu("Player", mainMenu);
            //TODO make this code look prettier
            {
                MM2Button button = null;
                button = playerMenu.addButton("Godmode", () => Cheats.toggleGod(button)).changeColour(Color.red);
            }
            {
                MM2Button button = null;
                button = playerMenu.addButton("Super sprint", () => Cheats.toggleSprintFast(button)).changeColour(Color.red);
            }
            {
                MM2Button button = null;
                button = playerMenu.addButton("Super jump", () => Cheats.toggleSuperJump(button)).changeColour(Color.red);
            }
            //playerMenu.addButton("Air jump on", () => Cheats.airJump(true)); //TODO needs fixing
            //playerMenu.addButton("Air jump off", () => Cheats.airJump(false));

            MenuMod2Menu missionMenu = new MenuMod2Menu("Missions", mainMenu);
            MenuMod2Menu forceModifierMenu = new MenuMod2Menu("Force Modifier", missionMenu);
            FieldInfo itemsField = typeof(WeightedArray<MissionModifier>).GetField("items", BindingFlags.NonPublic | BindingFlags.Instance);
            var items = itemsField.GetValue(Global.Instance.MissionModifiers) as Array;
            foreach (var node in items)
            {
                var valueField = node.GetType().GetField("value");
                var modifier = (MissionModifier)valueField.GetValue(node);
                {
                    MM2Button button = null;
                    button = forceModifierMenu.addButton($"{modifier.ModifierName}", () => Cheats.toggleForceModifier(modifier, button));
                }
            }
            MenuMod2Menu loadMissionMenu = new MenuMod2Menu("Load mission", missionMenu);
            List<MenuMod2Menu> missionMenus = new List<MenuMod2Menu>();
            foreach (var mission in Global.Instance.Missions)
            {
                if (mission.MissionFlags.HasFlag(MissionFlags.NormalMission) && mission.CanBeSelected())
                {
                    var thisMenu = new MenuMod2Menu(mission.MissionName, loadMissionMenu);
                    missionMenus.Add(thisMenu);
                    var regions = Global.Instance.Regions;
                    foreach (var region in regions)
                    {
                        thisMenu.addButton($"{region.RegionName}", () => Cheats.loadMission(mission, region));
                    }
                }
            }

            //MenuMod2Menu debugPrintMenu = new MenuMod2Menu("Debug Print Menu", mainMenu);
            //debugPrintMenu.addButton("Print All Upgrade Stats", () => Cheats.printAllUpgradeStats());
            //debugPrintMenu.addButton("Test message in chat", () => Cheats.SendDebugTextChatMessage());

            MenuMod2Menu inventory = new MenuMod2Menu("Inventory", mainMenu);
            MenuMod2Menu Skills = new MenuMod2Menu("Skills", inventory);
            MenuMod2Menu Upgrades = new MenuMod2Menu("Upgrades", inventory);
            MenuMod2Menu unlockWeapon = new MenuMod2Menu("Unlock weapon", inventory);
            MenuMod2Menu levelup = new MenuMod2Menu("Levels", inventory);
            inventory.addButton("Give max resources", () => Cheats.giveAllResoruces());
            Upgrades.addButton("Give one of each upgrade", () => Cheats.giveAllUpgrades());
            Upgrades.addButton("Give one of each cosmetic", () => Cheats.giveAllCosmetics());
            Upgrades.addButton("Give missing upgrades", () => Cheats.giveMissingUpgrades());
            Skills.addButton("Give all skills", () => Cheats.giveAllSkills());
            Skills.addButton("Unlock locked skills", () => Cheats.unlockLockedSkills());
            levelup.addButton("Level 30 all weapons", () => Cheats.levelUpAllWeapons(30));
            levelup.addButton("Level 10 all employees", () => Cheats.setAllCharictersToLevel(10));

            MenuMod2Menu spesificUpgradeType = new MenuMod2Menu("Give spesific upgrade", Upgrades);
            var allGear = Global.Instance.AllGear;
            List<MenuMod2Menu> upgradeMenus = new List<MenuMod2Menu>();
            foreach (var gear in allGear)
            {
                var gearInfo = gear.Info;
                if (gearInfo.Upgrades.Length > 0)
                {
                    var upgradeMenu = new MenuMod2Menu(gearInfo.Name, spesificUpgradeType);
                    Color color = Color.red;
                    if (PlayerData.GetGearData(gear).IsUnlocked)
                        color = Color.green;
                    else if (PlayerData.GetGearData(gear).IsCollected)
                        color = Color.yellow;
                    {
                        MM2Button button = null;
                        button = unlockWeapon.addButton(gearInfo.Name, () => Cheats.unlockGear(gear, button)).changeColour(color);
                    }
                    upgradeMenus.Add(upgradeMenu);
                    foreach (var upgrade in gearInfo.Upgrades)
                    {
                        if (upgrade.UpgradeType == Upgrade.Type.Normal)
                        {
                            upgradeMenu.addButton(MenuMod2.CleanRichText(upgrade.Name), () => Cheats.giveUpgrade(upgrade, gear)).changeColour(upgrade.Color);
                        }
                    }
                    upgradeMenu.thisButton.changeColour(color);
                }
            }
        }
        public static List<T> GetItemsFromWeightedArray<T>(object weightedArray)
        {
            if (weightedArray == null) return null;

            Type type = weightedArray.GetType();

            if (!type.IsGenericType || type.GetGenericTypeDefinition().Name != "WeightedArray`1")
                throw new ArgumentException("Expected instance of WeightedArray<T>");

            FieldInfo itemsField = type.GetField("items", BindingFlags.NonPublic | BindingFlags.Instance);
            if (itemsField == null)
                throw new MissingFieldException("Could not find 'items' field in WeightedArray");

            Array items = itemsField.GetValue(weightedArray) as Array;
            if (items == null) return null;

            List<T> result = new List<T>();

            foreach (var node in items)
            {
                if (node == null) continue;

                var nodeType = node.GetType();
                FieldInfo weightField = nodeType.GetField("weight");
                FieldInfo valueField = nodeType.GetField("value");

                if (weightField == null || valueField == null) continue;

                int weight = (int)weightField.GetValue(node);
                if (weight > 0)
                {
                    T value = (T)valueField.GetValue(node);
                    result.Add(value);
                }
            }

            return result;
        }

        public void toggleMenu()
        {
            if (MenuMod2Manager.currentMenu != null)
            {
                MenuMod2Manager.currentMenu.Close();
            }
            else if (mainMenu != null)
            {
                mainMenu.Open();
            }
        }
        public static GameObject findObjectByName(string name)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name == name)
                {
                    if (!obj.scene.IsValid())
                        return obj;
                    else
                        Logger.LogWarning($"Object {name} found but not in hide and don't save");
                }
            }
            Logger.LogWarning($"Could not find {name}");
            return null;
        }
        public static string CleanRichText(string input)
        {
            if (input != null)
            {
                var ret = Regex.Replace(input, "<.*?>", string.Empty);
                return ret;
            }
            else
            {
                return input;
            }
        }

    }

    [HarmonyPatch(typeof(MissionContainer), nameof(MissionContainer.GetAdditionalModifiers))]
    class MissionContainerPatch
    {
        static void Prefix(ref Span<int> modifiers, int seed, int startIndex, Mission mission)
        {
            var logger = MenuMod2.Logger;
            var forcedModifiers = Cheats.forcedModifiers;

            logger.LogDebug($"GetAdditionalModifiers called with seed: {seed}, startIndex: {startIndex}, mission: {mission}");

            if (forcedModifiers == null || forcedModifiers.Count == 0)
                return;

            for (int i = 0; i < forcedModifiers.Count; i++)
            {
                MissionModifier missionModifier = forcedModifiers[i];
                //TODO stop duplicates

                if (missionModifier != null)
                {
                    modifiers[startIndex + i] = Global.Instance.MissionModifiers.IndexOf(missionModifier);
                }
            }

            logger.LogDebug($"Injected {forcedModifiers.Count} forced modifiers at index {startIndex}");
        }
    }
    [HarmonyPatch(typeof(MissionContainer), nameof(MissionContainer.GetAdditionalModifierCount))]
    class AdditionalModifierCountPatch
    {
        static void Postfix(ref int __result)
        {
            var forced = Cheats.forcedModifiers;
            if (forced != null && forced.Count > 0)
            {
                __result += forced.Count;
                MenuMod2.Logger.LogDebug($"GetAdditionalModifierCount patched: increased by {forced.Count}, new count = {__result}");
            }
        }
    }
}