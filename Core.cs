using EditorScene.Chart;
using Gameplay.Manager;
using Global.Chart;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(Starrah.VM_MaiMuri.Core), "VisualMaimai-Integration-MaiMuriDX", "2.0.0", "Starrah")]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace Starrah.VM_MaiMuri;

[HarmonyPatch]
public class Core : MelonMod
{
    private const float DebounceSeconds = 1.0f;

    private static bool _dirty;
    private static float _dirtyAt;

    public override void OnInitializeMelon()
    {
        OperationManager.OnChartUpdate += MarkDirty;
        HarmonyInstance.PatchAll();
        MelonLogger.Msg("Initialized.");
    }

    public override void OnDeinitializeMelon()
    {
        OperationManager.OnChartUpdate -= MarkDirty;
    }
    
    [HarmonyPatch(typeof(InitManager), nameof(InitManager.Init))]
    [HarmonyPostfix]
    private static void InitManagerPostfix() => MarkDirty();

    private static void MarkDirty()
    {
        _dirty = true;
        _dirtyAt = Time.unscaledTime;
    }

    public override void OnUpdate()
    {
        if (!_dirty) return;
        if (Time.unscaledTime - _dirtyAt < DebounceSeconds) return;

        var chartData = InitManager.ChartData;
        if (chartData == null)
        {
            _dirty = false;
            return;
        }

        string simaiNotes;
        try
        {
            simaiNotes = ChartExporter.ExportNote(chartData, false);
        }
        catch (Exception e)
        {
            _dirty = false;
            MelonLogger.Warning($"Failed to export simai notes: {e}");
            return;
        }

        if (MuriChecker.TryCheckAsync(simaiNotes))
        {
            _dirty = false;
        }
    }
}