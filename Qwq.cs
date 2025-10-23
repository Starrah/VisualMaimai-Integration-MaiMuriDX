using MelonLoader;

namespace VisualMaimai_Fix;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

public class EventInterceptorDiagnostic : MonoBehaviour
{
    [Header("Diagnostic Settings")]
    public bool enableDiagnosticOverlay = true;
    public bool logAllEvents = true;
    
    private GameObject diagnosticCanvas;
    private EventInterceptor interceptorScript;

    void Start()
    {
        if (enableDiagnosticOverlay)
        {
            CreateDiagnosticOverlay();
        }
    }

    void Update()
    {
        // 基础输入检测
        if (Input.GetMouseButtonDown(0)) MelonLogger.Msg("LEFT click detected via Input");
        if (Input.GetMouseButtonDown(1)) MelonLogger.Msg("RIGHT click detected via Input");
        if (Input.GetMouseButtonDown(2)) MelonLogger.Msg("MIDDLE click detected via Input");
        
        // 检测触摸
        if (Input.touchCount > 0)
        {
            foreach (Touch touch in Input.touches)
            {
                if (touch.phase == TouchPhase.Began)
                {
                    MelonLogger.Msg($"Touch BEGAN - fingerId: {touch.fingerId}, position: {touch.position}, tapCount: {touch.tapCount}");
                }
                else if (touch.phase == TouchPhase.Ended)
                {
                    MelonLogger.Msg($"Touch ENDED - fingerId: {touch.fingerId}, position: {touch.position}, tapCount: {touch.tapCount}");
                }
            }
        }
    }

    void CreateDiagnosticOverlay()
    {
        // 创建Canvas
        diagnosticCanvas = new GameObject("DiagnosticEventCanvas");
        Canvas canvas = diagnosticCanvas.AddComponent<Canvas>();
        CanvasScaler scaler = diagnosticCanvas.AddComponent<CanvasScaler>();
        GraphicRaycaster raycaster = diagnosticCanvas.AddComponent<GraphicRaycaster>();
        
        // 设置为覆盖式Canvas
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最高层级，确保在最前面
        
        // 配置Canvas Scaler
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        // 创建全屏透明Image用于拦截事件
        GameObject interceptorPanel = new GameObject("EventInterceptor");
        Image image = interceptorPanel.AddComponent<Image>();
        RectTransform rectTransform = interceptorPanel.GetComponent<RectTransform>();
        
        // 设置透明颜色
        image.color = new Color(0, 0, 0, 0.01f); // 几乎完全透明，但可以接收事件
        
        // 设置为全屏
        rectTransform.SetParent(diagnosticCanvas.transform);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        
        // 添加事件拦截脚本
        interceptorScript = interceptorPanel.AddComponent<EventInterceptor>();
        interceptorScript.logAllEvents = logAllEvents;
        
        MelonLogger.Msg("Diagnostic event overlay created. It will intercept and log ALL UI events.");
    }

    // 提供公共方法控制诊断覆盖层
    public void ToggleDiagnosticOverlay(bool enabled)
    {
        if (diagnosticCanvas != null)
        {
            diagnosticCanvas.SetActive(enabled);
        }
        enableDiagnosticOverlay = enabled;
    }
}

// 事件拦截器组件
public class EventInterceptor : MonoBehaviour, 
    IPointerClickHandler, 
    IPointerDownHandler, 
    IPointerUpHandler,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler
{
    [Header("Event Logging")]
    public bool logAllEvents = true;
    public bool logPointerClick = true;
    public bool logPointerDownUp = true;
    public bool logDragEvents = true;
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!logPointerClick && !logAllEvents) return;
        
        string pointerInfo = $"POINTER CLICK - Button: {eventData.button}";
        pointerInfo += $", Position: {eventData.position}";
        pointerInfo += $", PointerId: {eventData.pointerId}";
        pointerInfo += $", ClickCount: {eventData.clickCount}";
        pointerInfo += $", EligibleForClick: {eventData.eligibleForClick}";
        pointerInfo += $", Used: {eventData.used}";
        
        MelonLogger.Msg(pointerInfo);
        
        // 特别关注中键点击
        if (eventData.button == PointerEventData.InputButton.Middle)
        {
            MelonLogger.Warning("=== MIDDLE BUTTON CLICK INTERCEPTED ===");
            MelonLogger.Warning($"GameObject: {gameObject.name}");
            MelonLogger.Warning($"World Position: {eventData.pointerCurrentRaycast.worldPosition}");
            
            // 列出所有被射线击中的对象
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            
            MelonLogger.Warning("Raycast hits:");
            foreach (var result in results)
            {
                MelonLogger.Warning($"  - {result.gameObject.name} (Layer: {result.gameObject.layer})");
            }
        }
        
        // 不消费事件，让它继续传递
        // eventData.Use(); // 注释掉这行，让事件继续传递
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!logPointerDownUp && !logAllEvents) return;
        
        string pointerInfo = $"POINTER DOWN - Button: {eventData.button}";
        pointerInfo += $", Position: {eventData.position}";
        pointerInfo += $", PointerId: {eventData.pointerId}";
        
        MelonLogger.Msg(pointerInfo);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!logPointerDownUp && !logAllEvents) return;
        
        string pointerInfo = $"POINTER UP - Button: {eventData.button}";
        pointerInfo += $", Position: {eventData.position}";
        pointerInfo += $", PointerId: {eventData.pointerId}";
        
        MelonLogger.Msg(pointerInfo);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!logDragEvents && !logAllEvents) return;
        MelonLogger.Msg($"DRAG BEGIN - Button: {eventData.button}, Position: {eventData.position}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 拖拽事件太多，默认不记录，除非特别开启
        if (!logAllEvents) return;
        MelonLogger.Msg($"DRAGGING - Position: {eventData.position}");
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!logDragEvents && !logAllEvents) return;
        MelonLogger.Msg($"DRAG END - Button: {eventData.button}, Position: {eventData.position}");
    }
}

// 使用示例和测试代码
public class DiagnosticTest : MonoBehaviour
{
    private EventInterceptorDiagnostic diagnostic;
    
    void Start()
    {
        // 自动创建诊断器
        diagnostic = gameObject.AddComponent<EventInterceptorDiagnostic>();
        
        // 你也可以手动控制
        // diagnostic.ToggleDiagnosticOverlay(true);
    }
    
    void Update()
    {
        // 按F1切换诊断覆盖层
        if (Input.GetKeyDown(KeyCode.F1))
        {
            diagnostic.ToggleDiagnosticOverlay(!diagnostic.enableDiagnosticOverlay);
            MelonLogger.Msg($"Diagnostic overlay: {diagnostic.enableDiagnosticOverlay}");
        }
    }
}