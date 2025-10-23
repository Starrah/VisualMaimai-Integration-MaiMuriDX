using System.Runtime.InteropServices;
using EditorScene.Edit;
using EditorScene.Select;
using HarmonyLib;
using MelonLoader;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(VisualMaimai_Fix.Core), "VisualMaimai-Fix", "1.0.0", "Starrah", null)]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace VisualMaimai_Fix;

[HarmonyPatch]
public class Core : MelonMod
{
    public override void OnInitializeMelon()
    {
        MelonLogger.Msg("Initialized.");
    }

    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    // 鼠标中键的虚拟键码是0x04
    const int VK_MBUTTON = 0x04;

    public override void OnUpdate()
    {
        // 检测中键是否按下，并且窗口聚焦
        if (Application.isFocused && (GetAsyncKeyState(VK_MBUTTON) & 0x0001) != 0)
        {
            // 执行射线检测
            GameObject clickedObject = GetClickedObject();
            if (clickedObject != null)
            {
                MelonLogger.Msg($"Middle click on {clickedObject.name}");
                // TODO 这里可以处理点击对象
                EditMono editMono = clickedObject.GetComponentInParent<EditMono>();
                if (editMono != null)
                {
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = Input.mousePosition;
                    eventData.button = PointerEventData.InputButton.Middle;
                    editMono.OnPointerClick(eventData);
                    MelonLogger.Msg($"call editMono.OnPointerClick on {editMono}");
                }
                else
                {
                    MelonLogger.Msg("No editMono found");
                }
            }
            else
            {
                MelonLogger.Msg($"Middle click on null!");
            }
        }
    }

    private GameObject GetClickedObject()
    {
        // 获取UI点击对象
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Input.mousePosition;
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        if (results.Count > 0)
        {
            return results[0].gameObject;
        }

        return null;
    }

    [HarmonyPatch(typeof(EditMono), "OnPointerClick")]
    [HarmonyPrefix]
    public static void qwq2(EditMono __instance)
    {
        MelonLogger.Msg(
            $"qwq2 11{__instance} 22{__instance.gameObject.name} 33{__instance.transform.parent} 44{__instance.transform.parent.gameObject.name} " +
            $"55{__instance.transform.parent.parent.gameObject.name} 66{__instance.transform.parent.parent.parent.gameObject.name}");
    }
}