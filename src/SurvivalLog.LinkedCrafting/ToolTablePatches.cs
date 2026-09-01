using System;
using System.Text.Json;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Vuplex.WebView;
using Il2CppLongList = Il2CppSystem.Collections.Generic.List<long>;

namespace SurvivalLog.LinkedCrafting;

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_Open",
    new Type[]
    {
        typeof(Ac_ToolTable_Open),
        typeof(State_Web_ToolTable),
        typeof(State_Data_Player),
        typeof(State_Data_Furniture),
        typeof(State_Data_Item),
        typeof(State_Data_PromptTrigger)
    })]
internal static class ToolTableOpenPatch
{
    private static void Prefix(
        Ac_ToolTable_Open ac,
        State_Data_Furniture furnitureState,
        State_Data_Item itemState)
    {
        try
        {
            if (ac != null)
            {
                HiddenStoragePool.Refresh(ac.TargetId, furnitureState, itemState);
            }
        }
        catch (Exception exception)
        {
            Plugin.Instance.Log.LogError($"Hidden storage discovery failed: {exception}");
        }
    }

    private static void Postfix(State_Web_ToolTable __result, State_Data_Item itemState)
    {
        HiddenStoragePool.AttachToolTableState(__result, itemState);
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_RecipeClick",
    new Type[]
    {
        typeof(Ac_ToolTable_RecipeClick),
        typeof(State_Web_ToolTable),
        typeof(State_Data_Item)
    })]
internal static class NormalRecipeClickPatch
{
    private static void Prefix(State_Web_ToolTable state)
    {
        if (state != null)
        {
            HiddenStoragePool.ClearPending(state.CurrentFurnitureId);
        }
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_RefreshBag",
    new Type[]
    {
        typeof(Ac_ToolTable_RefreshBag),
        typeof(State_Web_ToolTable),
        typeof(State_Data_Furniture),
        typeof(State_Data_Item),
        typeof(State_Data_PromptTrigger)
    })]
internal static class ToolTableRefreshPatch
{
    private static void Postfix(State_Web_ToolTable __result, State_Data_Item itemState)
    {
        HiddenStoragePool.AttachToolTableState(__result, itemState);
        if (__result != null && HiddenStoragePool.HasPending(__result.CurrentFurnitureId) &&
            __result.CraftBtnLocked != null)
        {
            __result.CraftBtnLocked.Value = false;
        }
    }
}

[HarmonyPatch(
    typeof(WebUILayer),
    "OnMessageFromJS",
    new Type[] { typeof(Il2CppSystem.Object), typeof(EventArgs<string>) })]
internal static class WarehouseRecipeWebMessagePatch
{
    private const char ProtocolSeparator = '\u001e';
    private const string ProtocolType = "3";
    private const string ProtocolPage = "ToolTable";
    private const string MessageType = "SurvivalLog.LinkedCrafting.StorageRecipe";

    private static bool Prefix(EventArgs<string> eventArgs)
    {
        string message = eventArgs?.Value;
        if (message == null)
        {
            return true;
        }

        string[] parts = message.Split(ProtocolSeparator, 4);
        if (parts.Length != 4 ||
            !string.Equals(parts[0], ProtocolType, StringComparison.Ordinal) ||
            !string.Equals(parts[1], ProtocolPage, StringComparison.Ordinal) ||
            !string.Equals(parts[2], MessageType, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            using JsonDocument payload = JsonDocument.Parse(parts[3]);
            if (!payload.RootElement.TryGetProperty("recipeKey", out JsonElement recipeKeyElement) ||
                recipeKeyElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("The warehouse recipe message did not contain a string recipeKey.");
            }

            HiddenStoragePool.QueueWarehouseRecipe(recipeKeyElement.GetString());
        }
        catch (Exception exception)
        {
            Plugin.Instance.Log.LogError(
                $"Warehouse recipe selection failed unexpectedly: payload={parts[3]}, error={exception}");
        }

        return false;
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_Make",
    new Type[] { typeof(Ac_ToolTable_Make), typeof(State_Web_ToolTable) })]
internal static class ToolTableMakePatch
{
    private static bool Prefix(State_Web_ToolTable state, ref State_Web_ToolTable __result)
    {
        if (state == null || !HiddenStoragePool.TryTakePending(
                state.CurrentFurnitureId,
                out Il2CppLongList itemIds,
                out string recipeKey,
                out string materialsJson))
        {
            return true;
        }

        __result = state;
        try
        {
            if (!BaseSingleton<BattleLogicWorld>.IsInstanceCreated)
            {
                throw new InvalidOperationException("The live battle world is unavailable.");
            }

            ToolTableManager manager = BaseSingleton<BattleLogicWorld>.Instance._ToolTableManager;
            if (manager == null)
            {
                throw new InvalidOperationException("The native ToolTableManager is unavailable.");
            }

            manager.StartProduction(state.CurrentFurnitureId, itemIds);
            if (state.CraftBtnLocked != null)
            {
                state.CraftBtnLocked.Value = true;
            }

            Plugin.Instance.Log.LogInfo(
                $"Native production started directly from linked storage: workbench={state.CurrentFurnitureId}, itemCount={itemIds.Count}.");
        }
        catch (Exception exception)
        {
            HiddenStoragePool.RestorePending(
                state.CurrentFurnitureId,
                itemIds,
                recipeKey,
                materialsJson);
            Plugin.Instance.Log.LogError($"Direct linked-storage production failed: {exception}");
        }

        return false;
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_ItemMove",
    new Type[]
    {
        typeof(Ac_ToolTable_ItemMove),
        typeof(State_Web_ToolTable),
        typeof(State_Data_Item)
    })]
internal static class ManualWorkbenchMovePatch
{
    private static void Prefix(State_Web_ToolTable state)
    {
        if (state != null)
        {
            HiddenStoragePool.ClearPending(state.CurrentFurnitureId);
        }
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_ClearWorkbench",
    new Type[] { typeof(Ac_ToolTable_ClearWorkbench), typeof(State_Web_ToolTable) })]
internal static class ClearWorkbenchPatch
{
    private static void Prefix(State_Web_ToolTable state)
    {
        if (state != null)
        {
            HiddenStoragePool.ClearPending(state.CurrentFurnitureId);
        }
    }
}

[HarmonyPatch(
    typeof(Reducer_Web_ToolTable),
    "RA_Close",
    new Type[] { typeof(Ac_ToolTable_Close), typeof(State_Web_ToolTable) })]
internal static class ToolTableClosePatch
{
    private static void Postfix(State_Web_ToolTable __result)
    {
        if (__result != null)
        {
            HiddenStoragePool.Clear(__result.CurrentFurnitureId);
        }
    }
}
