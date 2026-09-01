using System;
using System.Text.Json;
using GameCore.HotUpdate;
using GameCore.HotUpdate.ReduxUI;
using Il2CppSystem.Threading.Tasks;
using UnityEngine;
using Vuplex.WebView;

namespace SurvivalLog.LinkedCrafting;

internal sealed class ToolTableWebBridge : IDisposable
{
    private const float SynchronizationIntervalSeconds = 0.25f;
    private const float ErrorLogIntervalSeconds = 5f;

    private Task<string> pendingExecution;
    private float nextSynchronizationTime;
    private float nextErrorLogTime;
    private int synchronizedRevision = -1;
    private int pendingRevision = -1;
    private bool cleanupRequired;
    private bool disposed;

    internal void Tick()
    {
        if (disposed)
        {
            return;
        }

        float now = Time.unscaledTime;
        ObservePending(now);
        if (pendingExecution != null || now < nextSynchronizationTime)
        {
            return;
        }

        HiddenStoragePool.GetUiSnapshot(
            out bool active,
            out bool hasPending,
            out string materialsJson,
            out int revision);

        nextSynchronizationTime = now + SynchronizationIntervalSeconds;
        if (active)
        {
            cleanupRequired = true;
            if (revision != synchronizedRevision)
            {
                TryExecute(BuildSynchronizationScript(hasPending, materialsJson), now, revision);
            }
        }
        else if (cleanupRequired)
        {
            TryExecute(BuildRemovalScript(), now, -1);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        TryExecuteFinalRemoval();
    }

    private void TryExecute(string script, float now, int revision)
    {
        try
        {
            if (!BaseSingleton<ReduxUISystem>.IsInstanceCreated)
            {
                return;
            }

            WebUILayer layer = BaseSingleton<ReduxUISystem>.Instance.GetWebUILayer();
            IWebView webView = layer?.canvasWebViewPrefab?.WebView;
            if (webView == null)
            {
                return;
            }

            pendingExecution = webView.ExecuteJavaScript(script);
            if (pendingExecution != null)
            {
                pendingRevision = revision;
            }
        }
        catch (Exception exception)
        {
            LogFailure(now, exception.ToString());
        }
    }

    private void ObservePending(float now)
    {
        if (pendingExecution == null || !((Task)pendingExecution).IsCompleted)
        {
            return;
        }

        Task<string> execution = pendingExecution;
        pendingExecution = null;
        if (((Task)execution).IsCanceled || ((Task)execution).IsFaulted)
        {
            string failure = ((Task)execution).IsCanceled
                ? "execution was canceled"
                : ((Task)execution).Exception?.ToString() ?? "execution faulted";
            LogFailure(now, failure);
            return;
        }

        string result = execution.Result;
        if (result == "synchronized")
        {
            synchronizedRevision = pendingRevision;
            pendingRevision = -1;
            return;
        }

        if (result == "removed" || result == "frame-unavailable")
        {
            cleanupRequired = false;
            synchronizedRevision = -1;
            pendingRevision = -1;
            return;
        }

        if (result == "document-loading" || result == "anchors-unavailable")
        {
            synchronizedRevision = -1;
            pendingRevision = -1;
            return;
        }

        LogFailure(now, $"script returned '{result}'");
    }

    private void TryExecuteFinalRemoval()
    {
        try
        {
            if (!BaseSingleton<ReduxUISystem>.IsInstanceCreated)
            {
                return;
            }

            WebUILayer layer = BaseSingleton<ReduxUISystem>.Instance.GetWebUILayer();
            layer?.canvasWebViewPrefab?.WebView?.ExecuteJavaScript(BuildRemovalScript());
        }
        catch (Exception exception)
        {
            Plugin.Instance.Log.LogError($"Linked crafting UI cleanup failed: {exception}");
        }
    }

    private void LogFailure(float now, string failure)
    {
        if (now < nextErrorLogTime)
        {
            return;
        }

        nextErrorLogTime = now + ErrorLogIntervalSeconds;
        Plugin.Instance.Log.LogError($"Linked crafting UI synchronization failed: {failure}");
    }

    private static string BuildSynchronizationScript(bool hasPending, string materialsJson)
    {
        string materialsLiteral = JsonSerializer.Serialize(materialsJson ?? string.Empty);
        string innerScript = @"(function(){
const doc=document;
const section=doc.querySelector('.workbench-section');
const controls=doc.querySelector('.craft-controls');
if(!section||!controls){return 'anchors-unavailable';}
let style=doc.getElementById('survival-log-linked-crafting-style');
if(!style){
style=doc.createElement('style');
style.id='survival-log-linked-crafting-style';
style.textContent=`
#survival-log-linked-crafting{width:100%;margin-top:12px;padding-top:10px;border-top:1px solid rgba(255,193,7,.18);box-sizing:border-box;}
#survival-log-linked-crafting .sl-linked-title{display:flex;align-items:center;justify-content:center;gap:8px;height:18px;margin-bottom:8px;color:#d7bd7a;font-size:12px;font-weight:600;letter-spacing:1px;}
#survival-log-linked-crafting .sl-linked-title:before,#survival-log-linked-crafting .sl-linked-title:after{content:'';height:1px;flex:1;background:linear-gradient(90deg,transparent,rgba(255,193,7,.22),transparent);}
#survival-log-linked-crafting .sl-linked-grid{display:grid;justify-content:center;align-items:center;gap:4px;min-height:58px;width:100%;}
#survival-log-linked-crafting .sl-linked-empty{grid-column:1/-1;align-self:center;color:rgba(255,255,255,.32);font-size:12px;letter-spacing:.5px;text-align:center;}
#survival-log-linked-crafting .sl-linked-material{display:flex;flex-direction:column;align-items:center;gap:4px;min-width:0;}
#survival-log-linked-crafting .sl-linked-slot{position:relative;display:flex;align-items:center;justify-content:center;background:linear-gradient(180deg,#131316 0%,#1d1d20 100%);border:1px solid rgba(255,193,7,.24);border-radius:4px;box-shadow:inset 0 1px 2px rgba(0,0,0,.55);overflow:hidden;}
#survival-log-linked-crafting .sl-linked-slot img{max-width:82%;max-height:82%;object-fit:contain;filter:drop-shadow(0 2px 4px rgba(0,0,0,.45));}
#survival-log-linked-crafting .sl-linked-count{position:absolute;right:3px;bottom:3px;min-width:18px;padding:0 5px;border-radius:8px;background:rgba(0,0,0,.7);color:#fff;font-size:11px;font-weight:700;line-height:15px;text-align:center;}
#survival-log-linked-crafting .sl-linked-slot.lack{border:3px solid #e44736;box-shadow:inset 0 0 0 1px rgba(0,0,0,.5);filter:saturate(.72);box-sizing:border-box;}
#survival-log-linked-crafting .sl-linked-missing{height:14px;color:#ff6557;font-size:11px;font-weight:700;line-height:14px;text-align:center;white-space:nowrap;}
.recipe-item .sl-linked-select{flex:0 0 auto;padding:3px 6px;border:1px solid rgba(255,193,7,.34);border-radius:4px;background:rgba(255,193,7,.08);color:#d7bd7a;font-family:inherit;font-size:10px;font-weight:600;line-height:16px;cursor:pointer;}
.recipe-item .sl-linked-select:hover{border-color:rgba(255,193,7,.72);background:rgba(255,193,7,.2);color:#fff1c8;}
.recipe-item.craft-locked .sl-linked-select{display:none;}
body.sl-linked-has-materials #fillToast{display:none!important;}
.workbench-section>.craft-controls{margin-top:14px;}
`;
doc.head.appendChild(style);
}
if(!window.__survivalLogLinkedCraftingRecipeButtonsInstalled){
const nativeBuildRecipeItem=buildRecipeItem;
buildRecipeItem=function(recipe){
const fragment=nativeBuildRecipeItem(recipe);
const item=fragment.querySelector('.recipe-item');
if(item&&recipe&&recipe.recipeKey&&!recipe.craftLocked){
const button=doc.createElement('button');
button.type='button';
button.className='sl-linked-select unity-interactive';
button.textContent='仓储';
button.title='使用全部储物中的材料';
button.addEventListener('mouseenter',function(event){event.stopPropagation();hideRecipeTooltip();});
button.addEventListener('click',function(event){
event.preventDefault();event.stopImmediatePropagation();hideRecipeTooltip();
const fillToast=doc.getElementById('fillToast');
if(fillToast){fillToast.classList.remove('show');}
core.UnitySendEvent('SurvivalLog.LinkedCrafting.StorageRecipe',{recipeKey:recipe.recipeKey});
});
item.appendChild(button);
}
return fragment;
};
window.__survivalLogLinkedCraftingRecipeButtonsInstalled=true;
if(lastRecipeData){renderRecipes(lastRecipeData);}
}
let panel=doc.getElementById('survival-log-linked-crafting');
if(!panel){
panel=doc.createElement('section');
panel.id='survival-log-linked-crafting';
const title=doc.createElement('div');
title.className='sl-linked-title';
title.textContent='仓储制作';
panel.appendChild(title);
const grid=doc.createElement('div');
grid.className='sl-linked-grid';
panel.appendChild(grid);
section.insertBefore(panel,controls);
}
const grid=panel.querySelector('.sl-linked-grid');
const enabled=__HAS_PENDING__;
let materials=[];
if(enabled){
try{materials=JSON.parse(__MATERIALS_JSON__);}catch(error){materials=[];}
}
grid.textContent='';
const count=Array.isArray(materials)?materials.length:0;
if(count===0){
grid.style.gridTemplateColumns='1fr';
const empty=doc.createElement('div');
empty.className='sl-linked-empty';
empty.textContent=enabled?'未取得配方材料':'选择可制造配方后显示';
grid.appendChild(empty);
doc.body.classList.remove('sl-linked-has-materials');
}else{
const cols=Math.max(1,Math.min(6,count));
const rows=Math.ceil(count/cols);
const slotSize=rows>2?44:(rows>1?50:56);
grid.style.gridTemplateColumns='repeat('+cols+','+slotSize+'px)';
materials.forEach(function(material){
const materialBox=doc.createElement('div');
materialBox.className='sl-linked-material';
const slot=doc.createElement('div');
slot.className='sl-linked-slot';
const missing=Math.max(0,(material.need||0)-(material.have||0));
if(!material.hasEnough&&missing>0){slot.classList.add('lack');}
slot.style.width=slotSize+'px';
slot.style.height=slotSize+'px';
slot.title=(material.name||'')+' ×'+(material.need||0);
if(material.icon){
const image=doc.createElement('img');
image.src=material.icon;
image.alt='';
image.onerror=function(){image.style.display='none';};
slot.appendChild(image);
}
const badge=doc.createElement('span');
badge.className='sl-linked-count';
badge.textContent='×'+(material.need||0);
slot.appendChild(badge);
materialBox.appendChild(slot);
if(missing>0){
const missingText=doc.createElement('div');
missingText.className='sl-linked-missing';
missingText.textContent='缺少 ×'+missing;
materialBox.appendChild(missingText);
}
grid.appendChild(materialBox);
});
doc.body.classList.add('sl-linked-has-materials');
}
if(typeof applyViewportFit==='function'){applyViewportFit();}
return 'synchronized';
})()"
            .Replace("__HAS_PENDING__", hasPending ? "true" : "false")
            .Replace("__MATERIALS_JSON__", materialsLiteral);

        return "(function(){" +
            "const frame=document.getElementById('ToolTable');" +
            "if(!frame||!frame.contentWindow){return 'frame-unavailable';}" +
            "if(!frame.contentDocument||frame.contentDocument.readyState!=='complete'){return 'document-loading';}" +
            "return frame.contentWindow.eval(" + JsonSerializer.Serialize(innerScript) + ");" +
            "})()";
    }

    private static string BuildRemovalScript()
    {
        return "(function(){" +
            "const frame=document.getElementById('ToolTable');" +
            "if(!frame||!frame.contentDocument){return 'frame-unavailable';}" +
            "const doc=frame.contentDocument;" +
            "doc.getElementById('survival-log-linked-crafting')?.remove();" +
            "doc.getElementById('survival-log-linked-crafting-style')?.remove();" +
            "doc.body?.classList.remove('sl-linked-has-materials');" +
            "const view=frame.contentWindow;" +
            "if(view&&typeof view.eval==='function'){view.eval(\"if(typeof applyViewportFit==='function'){applyViewportFit();}\");}" +
            "return 'removed';" +
            "})()";
    }
}
