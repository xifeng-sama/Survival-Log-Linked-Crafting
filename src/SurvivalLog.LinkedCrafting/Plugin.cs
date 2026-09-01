using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SurvivalLog.LinkedCrafting;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BasePlugin
{
    public const string PluginGuid = "com.survivallog.linkedcrafting";
    public const string PluginName = "Survival Log Linked Crafting";
    public const string PluginVersion = "1.2.4";

    internal static Plugin Instance { get; private set; }

    public override void Load()
    {
        Instance = this;
        var harmony = new Harmony(PluginGuid);

        try
        {
            NativeContract.Verify();
            harmony.PatchAll(typeof(Plugin).Assembly);
            AddComponent<LinkedCraftingBehaviour>();
            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            Log.LogError($"{PluginName} disabled because native API verification or patch installation failed: {exception}");
        }
    }
}
