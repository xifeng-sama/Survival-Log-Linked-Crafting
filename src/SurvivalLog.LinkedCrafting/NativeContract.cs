using System;
using System.Reflection;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using GameCore.HotUpdate.ReduxUI;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Collections.Generic;
using ObservableCollections;
using R3;
using Vuplex.WebView;
using Il2CppTask = Il2CppSystem.Threading.Tasks.Task<string>;

namespace SurvivalLog.LinkedCrafting;

internal static class NativeContract
{
    internal static void Verify()
    {
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_Open",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_Open),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Player),
            typeof(State_Data_Furniture),
            typeof(State_Data_Item),
            typeof(State_Data_PromptTrigger));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "GetAllLinkedOwnerIds",
            typeof(List<long>),
            typeof(State_Web_ToolTable));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "SelectItemsForRecipe",
            typeof(List<long>),
            typeof(State_Data_Item),
            typeof(Dictionary<int, int>),
            typeof(Il2CppStructArray<long>));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "SyncWorkbenchTo",
            typeof(void),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Item),
            typeof(List<long>),
            typeof(long));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_RefreshBag",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_RefreshBag),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Furniture),
            typeof(State_Data_Item),
            typeof(State_Data_PromptTrigger));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_Make",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_Make),
            typeof(State_Web_ToolTable));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_Close",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_Close),
            typeof(State_Web_ToolTable));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "ApplyFillRecipe",
            typeof(bool),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Item),
            typeof(string));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_RecipeClick",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_RecipeClick),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Item));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "ParseMaterialKey",
            typeof(List<int>),
            typeof(string));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "CountMaterials",
            typeof(Dictionary<int, int>),
            typeof(List<int>));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "BuildMaterialsJson",
            typeof(string),
            typeof(Dictionary<int, int>),
            typeof(State_Data_Item),
            typeof(Il2CppStructArray<long>),
            typeof(Dictionary<int, Reducer_Web_ToolTable.PassedRecipe>));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_ItemMove",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_ItemMove),
            typeof(State_Web_ToolTable),
            typeof(State_Data_Item));
        RequireMethod(
            typeof(Reducer_Web_ToolTable),
            "RA_ClearWorkbench",
            typeof(State_Web_ToolTable),
            typeof(Ac_ToolTable_ClearWorkbench),
            typeof(State_Web_ToolTable));
        RequireMethod(
            typeof(ToolTableManager),
            "StartProduction",
            typeof(void),
            typeof(long),
            typeof(List<long>));
        RequireMethod(
            typeof(ReduxUISystem),
            "GetWebUILayer",
            typeof(WebUILayer));
        RequireMethod(
            typeof(IWebView),
            "ExecuteJavaScript",
            typeof(Il2CppTask),
            typeof(string));
        RequireMethod(
            typeof(WebUILayer),
            "OnMessageFromJS",
            typeof(void),
            typeof(Il2CppSystem.Object),
            typeof(EventArgs<string>));

        RequireProperty(typeof(Ac_ToolTable_Open), "TargetId", typeof(long));
        RequireProperty(typeof(State_Data_Furniture), "FurnitureCache", typeof(Dictionary<long, Data_Furniture>));
        RequireProperty(typeof(State_Data_Item), "Cache", typeof(Dictionary<long, Data_Item>));
        RequireProperty(
            typeof(State_Data_Item),
            "OwnerCache",
            typeof(Dictionary<long, Dictionary<long, Data_Item>>));
        RequireProperty(typeof(Data_Furniture), "ConfigId", typeof(int));
        RequireProperty(typeof(State_Web_ToolTable), "CurrentFurnitureId", typeof(long));
        RequireProperty(typeof(State_Web_ToolTable), "CraftBtnLocked", typeof(ReactiveProperty<bool>));
        RequireProperty(
            typeof(State_Web_ToolTable),
            "RecipeList",
            typeof(ObservableList<Data_Web_ToolTable_Recipe>));
        RequireProperty(
            typeof(Data_Web_ToolTable_Recipe),
            "RecipeKey",
            typeof(ReactiveProperty<string>));
        RequireProperty(
            typeof(Data_Web_ToolTable_Recipe),
            "MaterialsJson",
            typeof(ReactiveProperty<string>));
        RequireProperty(typeof(BattleLogicWorld), "_ToolTableManager", typeof(ToolTableManager));
        RequireProperty(typeof(Config_Furniture), "ID", typeof(int));
        RequireProperty(typeof(Config_Furniture), "BagId", typeof(int));
        RequireProperty(typeof(Config_Bag), "ID", typeof(int));
        RequireProperty(typeof(Config_Bag), "Size", typeof(List<int>));
        RequireProperty(typeof(WebUILayer), "canvasWebViewPrefab", typeof(CanvasWebViewPrefab));
        RequireProperty(typeof(BaseWebViewPrefab), "WebView", typeof(IWebView));
        RequireProperty(typeof(EventArgs<string>), "Value", typeof(string));
    }

    private static void RequireMethod(Type type, string name, Type returnType, params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static,
            null,
            parameterTypes,
            null);
        if (method == null || method.ReturnType != returnType)
        {
            throw new MissingMethodException(type.FullName, name);
        }
    }

    private static void RequireProperty(Type type, string name, Type propertyType)
    {
        PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        if (property == null || property.PropertyType != propertyType || property.GetMethod == null)
        {
            throw new MissingMemberException(type.FullName, name);
        }
    }
}
