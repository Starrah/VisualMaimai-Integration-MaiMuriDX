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
        if ((GetAsyncKeyState(VK_MBUTTON) & 0x0001) != 0 && Application.isFocused)
        {
            // 执行射线检测
            GameObject clickedObject = GetClickedObject();
            if (clickedObject != null)
            {
                MelonLogger.Msg($"Middle click on {clickedObject.name} : {clickedObject}");
                // TODO 这里可以处理点击对象
            }
            else
            {
                MelonLogger.Msg($"Middle click on null!");
            }
        }
    }
    
    private GameObject GetClickedObject()
    {
        // 首先检查是否点击了UI元素
        if (EventSystem.current.IsPointerOverGameObject())
        {
            // 获取UI点击对象
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            if (results.Count > 0)
            {
                foreach (RaycastResult raycastResult in results)
                {
                    var obj = raycastResult.gameObject;
                    MelonLogger.Msg($"qwq {obj.name} {obj.GetType().FullName}");
                    foreach (var component in obj.GetComponents<Component>())
                    {
                        MelonLogger.Msg($"comp {component}");
                    }
                    
                    for (int i = 0; i < obj.transform.childCount; i++)
                    {
                        MelonLogger.Msg($"child {obj.transform.GetChild(i).name}");
                    }
                    
                    MelonLogger.Msg($"parent {obj.transform.parent.name}");
                    MelonLogger.Msg($"pp {obj.transform.parent.parent.name}");
                    MelonLogger.Msg($"ppp {obj.transform.parent.parent.parent.name}");
                    var editMonos = obj.GetComponentsInChildren<EditMono>();
                    foreach (var edit in editMonos)
                    {
                        MelonLogger.Msg($"editMonos {edit} in {edit.gameObject.name}");
                    }
                    var editMonos2 = obj.transform.parent.gameObject.GetComponentsInParent<EditMono>();
                    foreach (var edit in editMonos2)
                    {
                        MelonLogger.Msg($"editMonos2 {edit} in {edit.gameObject.name}");
                    }
                    MelonLogger.Msg($"END THIS");
                }
                // return results[0].gameObject;
            }
        }
        else
        {
            // 非UI对象，使用物理射线检测
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                MelonLogger.Msg($"3D OBJ {hit.collider.gameObject.name} : {hit.collider.gameObject}");
                // return hit.collider.gameObject;
            }

            // 检测2D物体
            RaycastHit2D hit2D = Physics2D.Raycast(ray.origin, ray.direction);
            if (hit2D.collider != null)
            {
                MelonLogger.Msg($"2D OBJ {hit2D.collider.gameObject.name} : {hit2D.collider.gameObject}");
                // return hit2D.collider.gameObject;
            }
        }

        return null;
    }

    [HarmonyPatch(typeof(EditMono), "OnPointerClick")]
    [HarmonyPrefix]
    public static void qwq2(EditMono __instance)
    {
        MelonLogger.Msg($"qwq2 11{__instance} 22{__instance.gameObject.name} 33{__instance.transform.parent} 44{__instance.transform.parent.gameObject.name} " +
                        $"55{__instance.transform.parent.parent.gameObject.name} 66{__instance.transform.parent.parent.parent.gameObject.name}");
    }
}