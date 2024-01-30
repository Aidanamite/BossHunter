using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Reflection;
using System.Collections.Generic;
using AssetsLib;

namespace BossHunter
{
    [BepInPlugin("Aidanamite.BossHunter", "BossHunter", "1.1.0")]
    public class Main : BaseUnityPlugin
    {
        internal static Assembly modAssembly = Assembly.GetExecutingAssembly();
        internal static string modName = $"{modAssembly.GetName().Name}";
        internal static string modDir = $"{Environment.CurrentDirectory}\\BepInEx\\{modName}";
        public static string callingAmulet;

        void Awake()
        {
            var item = new ItemMaster();
            item.SetupBasicItem(
                Id: "CallingCharm",
                NameLocalizationKey: AssetsLibTools.RegisterLocalization("CallingCharm.Name", new Dictionary<string, string> { ["default"] = "Calling Charm" }),
                DescriptionLocalizationKey: AssetsLibTools.RegisterLocalization("CallingCharm.Desc", new Dictionary<string, string> { ["default"] = "Charm that will revive a defeated boss. Entering a boss floor will cause the charm to shatter" }),
                SpriteAssetName: AssetsLibTools.RegisterAsset("CallingCharm", AssetsLibTools.LoadImage("calling_ring.png", 32, 32).CreateSprite()),
                MaxStackSize: 5, GoldValue: 0, Culture: ItemMaster.Culture.Merchant,
                CanAppearInChest: true,
                MinPlusLevel: 0, Tier: ItemMaster.Tier.Tier1);
            item.RegisterItem();
            item.OverrideChestSpawns(new SpawnWeight(new Dictionary<WeightContext, float>() {
                [new WeightContext(-1,-1,ItemMaster.Tier.Tier1)] = 0.5f
            }));
            callingAmulet = item.name;

            new Harmony($"com.Aidanamite.{modName}").PatchAll(modAssembly);
            Logger.LogInfo($"{modName} has loaded");
        }

        public static bool IsBossDefeated(ItemMaster.Culture Culture) => Culture != ItemMaster.Culture.Wanderer && ((GameManager.Instance.currentGameSlot.culturesDefeated & (int)Mathf.Pow(2f, (float)Culture)) > 0 | GameManager.Instance.bossDefeatedCulture == Culture);
        
    }

    /*static class ExtentionMethods
    {
        public static void PrintAllFeilds<T>(this T value)
        {
            if (value == null)
                Debug.Log($"Type: {typeof(T).Namespace}.{typeof(T).Name} | Cannot read fields of NULL");
            string s = $"To String: {value}";
            var t = value.GetType();
            while (t != typeof(object))
            {
                s += $"\n --------- [{t.Namespace}.{t.Name}] Fields:";
                foreach (var f in t.GetFields((BindingFlags)(-1)))
                    if (!f.IsStatic)
                        s += $"\n[{f.FieldType.Namespace}.{f.FieldType.Name}] {f} = {f.GetValue(value)}";
                t = t.BaseType;
            }
            Debug.Log(s);
        }
    }*/

    [HarmonyPatch(typeof(DungeonBossRoom), "IsBossDefeated", MethodType.Getter)]
    class Patch_ForceBossSpawn
    {
        public static bool force = false;
        public static bool Prefix(ref bool __result) => force ? __result = false : true;
    }

    [HarmonyPatch(typeof(DungeonGenerator), "IsGeneratingDungeon", MethodType.Setter)]
    class Patch_StartGeneratingDungeon
    {
        public static void Postfix(DungeonGenerator __instance, bool value)
        {
            if (value)
            {
                if (__instance.level == 2 && Main.IsBossDefeated(__instance.culture))
                {
                    Patch_ForceBossSpawn.force = HeroMerchant.Instance.heroMerchantInventory.GetBagItemCount(ItemDatabase.GetItemByName(Main.callingAmulet, GameManager.Instance.GetCurrentGamePlusLevel())) > 0;
                    if (Patch_ForceBossSpawn.force)
                        Patch_UseDash.removeItem++;
                }
                else
                    Patch_ForceBossSpawn.force = false;
            }
        }
    }

    [HarmonyPatch(typeof(GameManager), "LoadTownSceneFromDungeon")]
    class Patch_PlayUnlockAnimation
    {
        static void Prefix(ref bool fromEmblem)
        {
            if (Patch_ForceBossSpawn.force)
            {
                GameManager.Instance.bossDefeatedCulture = ItemMaster.Culture.Developer;
                fromEmblem = true;
            }
        }
    }

    /*[HarmonyPatch(typeof(HeroMerchant), "Update")]
    class PatchGiveItem
    {
        public static void Postfix(HeroMerchant __instance)
        {
            if (Input.GetKeyDown(KeyCode.Slash))
            {
                var item = ItemDatabase.GetItemByName(Main.callingAmulet);
                CultureManager.Instance.DiscoverItem(item);
                __instance.heroMerchantInventory.TryAddItem(ItemStack.Create(item));
            }
            //if (Input.GetKeyDown(KeyCode.Semicolon) && InventoryPanel.currentSelectedSlot && InventoryPanel.currentSelectedSlot.GetComponent<InventorySlotGUI>() && InventoryPanel.currentSelectedSlot.GetComponent<InventorySlotGUI>().itemStack)
            //InventoryPanel.currentSelectedSlot.GetComponent<InventorySlotGUI>().itemStack.master.PrintAllFeilds();

        }
    }*/

    [HarmonyPatch(typeof(HeroMerchantController), "UseAbility")]
    class Patch_UseDash
    {
        public static int removeItem = 0;
        public static void Postfix(HeroMerchantController __instance)
        {
            if (removeItem != 0)
            {
                removeItem -= HeroMerchant.Instance.heroMerchantInventory.RemoveItem(ItemDatabase.GetItemByName(Main.callingAmulet, GameManager.Instance.GetCurrentGamePlusLevel()), removeItem);
                HUDManager.Instance.UpdateBagLabel();
                HUDManager.Instance.PlayBagRumble();
            }
        }
    }
}