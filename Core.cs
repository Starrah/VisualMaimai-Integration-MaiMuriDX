using System.Runtime.InteropServices;
using EditorScene.Edit;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;

[assembly: MelonInfo(typeof(VisualMaimai_Fix.Core), "VM-TouchPad-MidButton-Fix", "1.0.0", "Starrah", null)]
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
                EditMono editMono = clickedObject.GetComponentInParent<EditMono>();
                if (editMono != null)
                {
                    // 模拟以中键手工调用editMono.OnPointerClick
                    PointerEventData eventData = new PointerEventData(EventSystem.current);
                    eventData.position = Input.mousePosition;
                    eventData.button = PointerEventData.InputButton.Middle;
                    editMono.OnPointerClick(eventData);
                }
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
}