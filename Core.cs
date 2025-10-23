using System.Runtime.InteropServices;
using EditorScene.Edit;
using EditorScene.Select;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(VisualMaimai_Fix.Core), "VisualMaimai-Fix", "1.0.0", "Starrah", null)]
[assembly: MelonGame("CH3COOOH", "Visual Maimai")]

namespace VisualMaimai_Fix
{
    [HarmonyPatch]
    public class Core : MelonMod
    {
        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(int vKey);
        
        private const int VK_MBUTTON = 0x04; // 中键虚拟键码

        private static bool wuwuji = false;
        
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("Initialized.");
            
            Input.ResetInputAxes();
        
            // 检查输入设备状态
            MelonLogger.Msg($"Mouse present: {Input.mousePresent}");
            MelonLogger.Msg($"Touch supported: {Input.touchSupported}");
            MelonLogger.Msg($"Touch count: {Input.touchCount}");
        
            // 确保触控模拟开启
            Input.simulateMouseWithTouches = true;
            
            
        }

        public override void OnGUI()
        {
            // 在 OnGUI 中检测，这是最底层的输入检测
            if (Event.current != null && Event.current.isMouse)
            {
                MelonLogger.Msg($"OnGUI detected click {Event.current.button} at: {Event.current.mousePosition}");
            }
            
            // 显示当前输入状态
            GUILayout.Label($"Mouse Position: {Input.mousePosition}");
            GUILayout.Label($"Mouse Delta: {Input.mouseScrollDelta}");
            GUILayout.Label($"Any Key: {Input.anyKey}");
            GUILayout.Label($"Any Key Down: {Input.anyKeyDown}");
        
            // 检查所有鼠标按钮状态
            for (int i = 0; i < 3; i++)
            {
                if (Input.GetMouseButton(i))
                {
                    GUILayout.Label($"Mouse Button {i} is pressed");
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NoteEdit), "OnSelect")]
        public static void qwq()
        {
            MelonLogger.Msg("qwq");
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(EditMono), "OnPointerClick")]
        public static void qwq2(PointerEventData eventData)
        {
            MelonLogger.Msg($"qwq2 {eventData} {eventData.button}");
            if (eventData.button == PointerEventData.InputButton.Left && !wuwuji) {}
            {
                var gameObject = SceneManager.GetActiveScene().GetRootGameObjects()[0];
                gameObject.AddComponent<EventInterceptorDiagnostic>();
                MelonLogger.Msg("wuwuji to true");
                wuwuji = true;
            }
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NoteEdit), "OnClick")]
        public static void qwq3()
        {
            MelonLogger.Msg("qwq3");
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NoteEdit), "OnRemove")]
        public static void qwq4()
        {
            MelonLogger.Msg("qwq4");
        }
        
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SelectArea), "OnBeginDrag")]
        public static void qwq5()
        {
            MelonLogger.Msg("qwq5");
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SelectArea), "OnEndDrag")]
        public static void qwq6()
        {
            MelonLogger.Msg("qwq6");
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SelectArea), "OnDrag")]
        public static void qwq7()
        {
            MelonLogger.Msg("qwq7");
        }

        public override void OnUpdate()
        {
            // 使用 Windows API 直接检查中键状态
            short state = GetAsyncKeyState(VK_MBUTTON);
            bool isPressed = (state & 0x8000) != 0;
            bool isPressed2 = (state & 0x0001) != 0;
        
            if (isPressed)
            {
                MelonLogger.Msg("Windows API detected middle button press");
            }
            if (isPressed2)
            {
                MelonLogger.Msg("Windows API detected middle button press2");
            }
            
            state = GetAsyncKeyState(0x01);
            isPressed = (state & 0x8000) != 0;
            isPressed2 = (state & 0x0001) != 0;
        
            if (isPressed)
            {
                MelonLogger.Msg("Windows API detected LEFT button press");
            }
            if (isPressed2)
            {
                MelonLogger.Msg("Windows API detected LEFT button press2");
            }
            
            if (Input.touchCount > 0)
            {
                Debug.Log($"Detected {Input.touchCount} touches");
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    Debug.Log($"Touch {i}: phase={touch.phase}, fingerId={touch.fingerId}, pos={touch.position}");
                }
            }
            
            
            if (Input.GetKeyDown(KeyCode.Mouse0))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse0)");
            if (Input.GetKeyDown(KeyCode.Mouse1))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse1)");
            if (Input.GetKeyDown(KeyCode.Mouse2))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse2)");
            if (Input.GetKeyDown(KeyCode.Mouse3))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse3)");
            if (Input.GetKeyDown(KeyCode.Mouse4))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse4)");
            if (Input.GetKeyDown(KeyCode.Mouse5))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse5)");
            if (Input.GetKeyDown(KeyCode.Mouse6))
                MelonLogger.Msg("Input.GetKeyDown(KeyCode.Mouse6)");
            
            if (Input.GetMouseButtonDown(2))
            {
                MelonLogger.Msg("OnMouseDown2");
            }

            if (Input.GetMouseButton(2))
            {
                MelonLogger.Msg("OnMouse2");
            }

            if (Input.GetMouseButtonUp(2))
            {
                MelonLogger.Msg("OnMouseUp2");
            }

            if (Input.GetMouseButtonDown(0))
            {
                MelonLogger.Msg("OnMouseDown0");
            }

            if (Input.GetMouseButtonDown(1))
            {
                MelonLogger.Msg("OnMouseDown1");
            }

            // Input.GetMouseButton(2)
            // 如果检测到鼠标中键按下
            if (Input.GetMouseButtonDown(2))
            {
                MelonLogger.Msg("OnMiddleMouseDown");
                // 检查鼠标位置是否在当前UI元素上
                if (IsPointerOverUIElement())
                {
                    HandleMiddleClick();
                }
            }
        }

        private bool IsPointerOverUIElement()
        {
            // 创建一个PointerEventData，用于EventSystem检查
            var eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;

            // 创建一个列表来接收结果
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // 检查是否点击到了当前UI元素
            foreach (var result in results)
            {
                MelonLogger.Msg($"re {result} {result.gameObject} {result.gameObject.name}");
                // if (result.gameObject == gameObject)
                // {
                //     return true;
                // }
            }
            return results.Count > 0;
        }

        private void HandleMiddleClick()
        {
            // 处理中键点击的逻辑
            MelonLogger.Msg("Middle click handled");
        }
    }
    
    public class TopLevelEventListener : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log($"Top level received: {eventData.button}, pointerId: {eventData.pointerId}");
            // 不要调用 eventData.Use()，让事件继续传递
        }
    }
}