using HarmonyLib;
using MelonLoader;

[assembly: MelonInfo(typeof(Starrah.VM_MaiMuri.Core), "VisualMaimai-Integration-MaiMuriDX", "2.0.0", "Starrah")]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace Starrah.VM_MaiMuri;

[HarmonyPatch]
public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Initialized.");
    }
}