using System;
using UnityEngine;

namespace SurvivalLog.LinkedCrafting;

public sealed class LinkedCraftingBehaviour : MonoBehaviour
{
    private ToolTableWebBridge webBridge;

    public LinkedCraftingBehaviour(IntPtr pointer)
        : base(pointer)
    {
    }

    private void Start()
    {
        webBridge = new ToolTableWebBridge();
    }

    private void Update()
    {
        HiddenStoragePool.ProcessQueuedWarehouseRecipe();
        webBridge?.Tick();
    }

    private void OnDestroy()
    {
        webBridge?.Dispose();
        webBridge = null;
    }
}
