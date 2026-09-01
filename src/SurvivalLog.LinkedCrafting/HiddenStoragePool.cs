using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppLongList = Il2CppSystem.Collections.Generic.List<long>;

namespace SurvivalLog.LinkedCrafting;

internal static class HiddenStoragePool
{
    private static readonly object Gate = new object();
    private static long currentWorkbenchId;
    private static long[] storageOwnerIds = Array.Empty<long>();
    private static long[] pendingItemIds = Array.Empty<long>();
    private static string pendingRecipeKey = string.Empty;
    private static string pendingMaterialsJson = string.Empty;
    private static bool pendingConfirmed;
    private static string queuedRecipeKey = string.Empty;
    private static int uiRevision;
    private static State_Web_ToolTable currentToolTableState;
    private static State_Data_Item currentItemState;

    internal static void Refresh(
        long workbenchId,
        State_Data_Furniture furnitureState,
        State_Data_Item itemState)
    {
        if (furnitureState?.FurnitureCache == null || itemState?.Cache == null)
        {
            throw new InvalidOperationException("The Redux furniture/item state is unavailable.");
        }

        ConfigManager configManager = BaseSingleton<ConfigManager>.Instance;
        Il2CppStructArray<long> ownerIds = new Il2CppStructArray<long>(furnitureState.FurnitureCache.Count);
        furnitureState.FurnitureCache.Keys.CopyTo(ownerIds, 0);
        var result = new List<long>(ownerIds.Length);
        int occupiedStorageCount = 0;
        int storedItemCount = 0;

        for (int index = 0; index < ownerIds.Length; index++)
        {
            long ownerId = ownerIds[index];
            if (ownerId == workbenchId)
            {
                continue;
            }

            Data_Furniture furniture = null;
            if (!furnitureState.FurnitureCache.TryGetValue(ownerId, out furniture) || furniture == null)
            {
                continue;
            }

            Config_Furniture furnitureConfig = configManager.Get_Config_Furniture(furniture.ConfigId);
            if (furnitureConfig == null || furnitureConfig.ID != furniture.ConfigId || furnitureConfig.BagId <= 0)
            {
                continue;
            }

            Config_Bag bagConfig = configManager.Get_Config_Bag(furnitureConfig.BagId);
            if (bagConfig == null || bagConfig.ID != furnitureConfig.BagId || bagConfig.Size == null ||
                bagConfig.Size.Count != 2 || bagConfig.Size[0] <= 0 || bagConfig.Size[1] <= 0)
            {
                continue;
            }

            result.Add(ownerId);
            if (itemState.OwnerCache != null &&
                itemState.OwnerCache.TryGetValue(ownerId, out Il2CppSystem.Collections.Generic.Dictionary<long, Data_Item> ownedItems) &&
                ownedItems != null)
            {
                occupiedStorageCount++;
                storedItemCount += ownedItems.Count;
            }
        }

        lock (Gate)
        {
            currentWorkbenchId = workbenchId;
            storageOwnerIds = result.Distinct().OrderBy(value => value).ToArray();
            pendingItemIds = Array.Empty<long>();
            pendingRecipeKey = string.Empty;
            pendingMaterialsJson = string.Empty;
            pendingConfirmed = false;
            queuedRecipeKey = string.Empty;
            currentToolTableState = null;
            currentItemState = itemState;
            uiRevision++;
        }

        Plugin.Instance.Log.LogInfo(
            $"Hidden linked-crafting pool refreshed for workbench {workbenchId}: storageCount={result.Count}, " +
            $"occupiedStorageCount={occupiedStorageCount}, storedItemCount={storedItemCount}.");
    }

    internal static void AttachToolTableState(State_Web_ToolTable state, State_Data_Item itemState = null)
    {
        if (state == null)
        {
            return;
        }

        lock (Gate)
        {
            if (currentWorkbenchId != state.CurrentFurnitureId)
            {
                return;
            }

            currentToolTableState = state;
            if (itemState != null)
            {
                currentItemState = itemState;
            }
        }
    }

    internal static bool TrySelectWarehouseRecipe(string recipeKey)
    {
        State_Web_ToolTable state;
        State_Data_Item itemState;
        lock (Gate)
        {
            state = currentToolTableState;
            itemState = currentItemState;
        }

        if (state == null || itemState == null || string.IsNullOrEmpty(recipeKey))
        {
            return false;
        }

        Il2CppSystem.Collections.Generic.List<int> materialIds =
            Reducer_Web_ToolTable.ParseMaterialKey(recipeKey);
        if (materialIds == null)
        {
            ClearPending(state.CurrentFurnitureId);
            return false;
        }

        Il2CppSystem.Collections.Generic.Dictionary<int, int> materialNeeded =
            Reducer_Web_ToolTable.CountMaterials(materialIds);
        if (materialNeeded == null)
        {
            ClearPending(state.CurrentFurnitureId);
            return false;
        }

        Il2CppLongList nativeOwners = Reducer_Web_ToolTable.GetAllLinkedOwnerIds(state);
        var nativeOwnerArray = new Il2CppStructArray<long>(nativeOwners?.Count ?? 0);
        if (nativeOwners != null)
        {
            for (int index = 0; index < nativeOwners.Count; index++)
            {
                nativeOwnerArray[index] = nativeOwners[index];
            }
        }

        Il2CppStructArray<long> allOwners = ExpandOwnerIds(nativeOwnerArray);
        string materialsJson = Reducer_Web_ToolTable.BuildMaterialsJson(
            materialNeeded,
            itemState,
            allOwners,
            new Il2CppSystem.Collections.Generic.Dictionary<int, Reducer_Web_ToolTable.PassedRecipe>());
        if (string.IsNullOrEmpty(materialsJson))
        {
            ClearPending(state.CurrentFurnitureId);
            return false;
        }

        Il2CppLongList selected = Reducer_Web_ToolTable.SelectItemsForRecipe(
            itemState,
            materialNeeded,
            allOwners);
        if (selected == null || selected.Count == 0)
        {
            SetUnavailableSelection(state.CurrentFurnitureId, recipeKey, materialsJson);
            if (state.CraftBtnLocked != null && !state.CraftBtnLocked.Value)
            {
                state.CraftBtnLocked.Value = true;
            }

            return false;
        }

        SetPending(state.CurrentFurnitureId, selected);
        ConfirmPending(state.CurrentFurnitureId, recipeKey, materialsJson);
        if (state.CraftBtnLocked != null)
        {
            state.CraftBtnLocked.Value = false;
        }

        Plugin.Instance.Log.LogInfo(
            $"Warehouse recipe selected without material transfer: workbench={state.CurrentFurnitureId}, itemCount={selected.Count}.");
        return true;
    }

    internal static void QueueWarehouseRecipe(string recipeKey)
    {
        if (string.IsNullOrEmpty(recipeKey))
        {
            throw new ArgumentException("The warehouse recipe key is empty.", nameof(recipeKey));
        }

        lock (Gate)
        {
            if (currentWorkbenchId == 0)
            {
                throw new InvalidOperationException("No workbench is active for warehouse crafting.");
            }

            queuedRecipeKey = recipeKey;
        }

        Plugin.Instance.Log.LogInfo($"Warehouse recipe request queued: recipeKey={recipeKey}.");
    }

    internal static void ProcessQueuedWarehouseRecipe()
    {
        string recipeKey;
        lock (Gate)
        {
            recipeKey = queuedRecipeKey;
            queuedRecipeKey = string.Empty;
        }

        if (string.IsNullOrEmpty(recipeKey))
        {
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            bool selected = TrySelectWarehouseRecipe(recipeKey);
            stopwatch.Stop();
            if (!selected)
            {
                Plugin.Instance.Log.LogWarning(
                    $"Warehouse recipe selection failed because the complete material set was unavailable: " +
                    $"recipeKey={recipeKey}, elapsedMs={stopwatch.ElapsedMilliseconds}.");
            }
            else
            {
                Plugin.Instance.Log.LogInfo(
                    $"Warehouse recipe request completed: recipeKey={recipeKey}, " +
                    $"elapsedMs={stopwatch.ElapsedMilliseconds}.");
            }
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Plugin.Instance.Log.LogError(
                $"Warehouse recipe selection failed unexpectedly: recipeKey={recipeKey}, " +
                $"elapsedMs={stopwatch.ElapsedMilliseconds}, error={exception}");
        }
    }

    private static void SetUnavailableSelection(long workbenchId, string recipeKey, string materialsJson)
    {
        lock (Gate)
        {
            if (currentWorkbenchId != workbenchId || string.IsNullOrEmpty(recipeKey) ||
                string.IsNullOrEmpty(materialsJson))
            {
                return;
            }

            pendingItemIds = Array.Empty<long>();
            pendingRecipeKey = recipeKey;
            pendingMaterialsJson = materialsJson;
            pendingConfirmed = true;
            uiRevision++;
        }
    }

    internal static Il2CppStructArray<long> ExpandOwnerIds(Il2CppStructArray<long> originalOwnerIds)
    {
        var merged = new List<long>();
        if (originalOwnerIds != null)
        {
            for (int index = 0; index < originalOwnerIds.Length; index++)
            {
                merged.Add(originalOwnerIds[index]);
            }
        }

        lock (Gate)
        {
            merged.AddRange(storageOwnerIds);
        }

        long[] distinct = merged.Distinct().ToArray();
        var result = new Il2CppStructArray<long>(distinct.Length);
        for (int index = 0; index < distinct.Length; index++)
        {
            result[index] = distinct[index];
        }

        return result;
    }

    internal static void AddOwnerIds(Il2CppLongList result)
    {
        if (result == null)
        {
            return;
        }

        var existing = new HashSet<long>();
        for (int index = 0; index < result.Count; index++)
        {
            existing.Add(result[index]);
        }

        lock (Gate)
        {
            foreach (long ownerId in storageOwnerIds)
            {
                if (existing.Add(ownerId))
                {
                    result.Add(ownerId);
                }
            }
        }
    }

    internal static void SetPending(long workbenchId, Il2CppLongList itemIds)
    {
        if (itemIds == null || itemIds.Count == 0)
        {
            ClearPending(workbenchId);
            return;
        }

        var copied = new long[itemIds.Count];
        for (int index = 0; index < itemIds.Count; index++)
        {
            copied[index] = itemIds[index];
        }

        lock (Gate)
        {
            if (currentWorkbenchId == workbenchId)
            {
                pendingItemIds = copied;
                pendingRecipeKey = string.Empty;
                pendingMaterialsJson = string.Empty;
                pendingConfirmed = false;
                uiRevision++;
            }
        }
    }

    internal static void ConfirmPending(long workbenchId, string recipeKey, string materialsJson)
    {
        lock (Gate)
        {
            if (currentWorkbenchId != workbenchId || pendingItemIds.Length == 0 ||
                string.IsNullOrEmpty(recipeKey) || string.IsNullOrEmpty(materialsJson))
            {
                return;
            }

            pendingRecipeKey = recipeKey;
            pendingMaterialsJson = materialsJson;
            pendingConfirmed = true;
            uiRevision++;
        }
    }

    internal static bool HasPending(long workbenchId)
    {
        lock (Gate)
        {
            return currentWorkbenchId == workbenchId && pendingConfirmed && pendingItemIds.Length > 0;
        }
    }

    internal static bool TryTakePending(
        long workbenchId,
        out Il2CppLongList itemIds,
        out string recipeKey,
        out string materialsJson)
    {
        itemIds = null;
        recipeKey = string.Empty;
        materialsJson = string.Empty;
        long[] copied;
        lock (Gate)
        {
            if (currentWorkbenchId != workbenchId || !pendingConfirmed || pendingItemIds.Length == 0)
            {
                return false;
            }

            copied = pendingItemIds;
            recipeKey = pendingRecipeKey;
            materialsJson = pendingMaterialsJson;
            pendingItemIds = Array.Empty<long>();
            pendingRecipeKey = string.Empty;
            pendingMaterialsJson = string.Empty;
            pendingConfirmed = false;
            currentToolTableState = null;
            currentItemState = null;
            uiRevision++;
        }

        itemIds = new Il2CppLongList(copied.Length);
        foreach (long itemId in copied)
        {
            itemIds.Add(itemId);
        }

        return true;
    }

    internal static void RestorePending(
        long workbenchId,
        Il2CppLongList itemIds,
        string recipeKey,
        string materialsJson)
    {
        SetPending(workbenchId, itemIds);
        ConfirmPending(workbenchId, recipeKey, materialsJson);
    }

    internal static void GetUiSnapshot(
        out bool active,
        out bool hasPending,
        out string materialsJson,
        out int revision)
    {
        lock (Gate)
        {
            active = currentWorkbenchId != 0;
            hasPending = pendingConfirmed;
            materialsJson = hasPending ? pendingMaterialsJson : string.Empty;
            revision = uiRevision;
        }
    }

    internal static void ClearPending(long workbenchId)
    {
        lock (Gate)
        {
            if (currentWorkbenchId == workbenchId)
            {
                bool changed = pendingItemIds.Length > 0 || pendingConfirmed ||
                    !string.IsNullOrEmpty(pendingRecipeKey) || !string.IsNullOrEmpty(pendingMaterialsJson);
                pendingItemIds = Array.Empty<long>();
                pendingRecipeKey = string.Empty;
                pendingMaterialsJson = string.Empty;
                pendingConfirmed = false;
                queuedRecipeKey = string.Empty;
                if (changed)
                {
                    uiRevision++;
                }
            }
        }
    }

    internal static void Clear(long workbenchId)
    {
        lock (Gate)
        {
            if (currentWorkbenchId != workbenchId)
            {
                return;
            }

            currentWorkbenchId = 0;
            storageOwnerIds = Array.Empty<long>();
            pendingItemIds = Array.Empty<long>();
            pendingRecipeKey = string.Empty;
            pendingMaterialsJson = string.Empty;
            pendingConfirmed = false;
            queuedRecipeKey = string.Empty;
            uiRevision++;
        }
    }
}
