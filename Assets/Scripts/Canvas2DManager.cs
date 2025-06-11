using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Canvas2DManager.cs - 2D畫布管理器
public class Canvas2DManager : MonoBehaviour
{
    [Header("2D Canvas References")]
    public GameObject Canvas2D;  // 2D畫布的GameObject
    public RawImage canvasImage; // 顯示畫布的RawImage
    public Camera canvas2DCamera; // 用於2D畫布的相機

    [Header("Drawing Settings")]
    public int canvasWidth = 1024;
    public int canvasHeight = 1024;
    public Color backgroundColor = Color.white;

    private Texture2D drawingTexture;
    private bool isDrawing = false;
    private Vector2 lastDrawPosition;
    private Color currentBrushColor = Color.black;
    private float currentBrushSize = 5f;

    [Header("Performance Settings")]
    public bool enableDebugLogs = false; // 控制debug輸出
    public float textureUpdateInterval = 0.02f; // 貼圖更新間隔（秒）
    private float lastTextureUpdate = 0f;
    private bool needsTextureUpdate = false;

    private void Start()
    {
        // 如果Canvas2D未設置，自動創建
        if (Canvas2D == null)
        {
            CreateCanvas2D();
        }

        InitializeCanvas();
    }

    private void Update()
    {
        // 批次更新貼圖以提升性能
        if (needsTextureUpdate && Time.time - lastTextureUpdate > textureUpdateInterval)
        {
            drawingTexture.Apply();
            needsTextureUpdate = false;
            lastTextureUpdate = Time.time;
        }
    }

    void CreateCanvas2D()
    {
        // 找到主Canvas
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("找不到主Canvas！");
            return;
        }

        // 創建Canvas2D容器
        GameObject canvas2DObj = new GameObject("Canvas2D");
        canvas2DObj.transform.SetParent(mainCanvas.transform, false);

        RectTransform canvas2DRect = canvas2DObj.AddComponent<RectTransform>();
        canvas2DRect.anchorMin = Vector2.zero;
        canvas2DRect.anchorMax = Vector2.one;
        canvas2DRect.offsetMin = Vector2.zero;
        canvas2DRect.offsetMax = Vector2.zero;

        Canvas2D = canvas2DObj;

        // 創建背景
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(canvas2DObj.transform, false);

        RectTransform bgRect = backgroundObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = backgroundObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f); // 半透明黑色

        // 創建畫布區域
        GameObject drawingAreaObj = new GameObject("DrawingArea");
        drawingAreaObj.transform.SetParent(canvas2DObj.transform, false);

        RectTransform drawingRect = drawingAreaObj.AddComponent<RectTransform>();
        drawingRect.anchorMin = new Vector2(0.5f, 0.5f);
        drawingRect.anchorMax = new Vector2(0.5f, 0.5f);
        drawingRect.sizeDelta = new Vector2(800, 600);
        drawingRect.anchoredPosition = Vector2.zero;

        canvasImage = drawingAreaObj.AddComponent<RawImage>();
        canvasImage.color = Color.white;

        Debug.Log("自動創建了2D Canvas UI");
    }

    void InitializeCanvas()
    {
        // 創建畫布材質
        drawingTexture = new Texture2D(canvasWidth, canvasHeight);

        // 填充白色背景
        Color[] fillColors = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < fillColors.Length; i++)
        {
            fillColors[i] = backgroundColor;
        }
        drawingTexture.SetPixels(fillColors);
        drawingTexture.Apply();

        // 設置到RawImage
        if (canvasImage != null)
        {
            canvasImage.texture = drawingTexture;
        }

        // 確保畫布開始時是隱藏的
        if (Canvas2D != null)
        {
            Canvas2D.SetActive(false);
        }
    }

    public void Show2DCanvas()
    {
        if (Canvas2D != null)
        {
            Canvas2D.SetActive(true);
            Debug.Log("2D Canvas已顯示");
        }
        else
        {
            Debug.LogError("Canvas2D GameObject未設置！");
        }
    }

    public void Hide2DCanvas()
    {
        if (Canvas2D != null)
        {
            Canvas2D.SetActive(false);
            Debug.Log("2D Canvas已隱藏");
        }
    }

    public void SetBrushColor(Color color)
    {
        currentBrushColor = color;
        if (enableDebugLogs)
            Debug.Log($"設置筆刷顏色: {color}");
    }

    public void SetBrushSize(float size)
    {
        currentBrushSize = Mathf.Max(1f, size); // 確保最小筆刷大小為1
        if (enableDebugLogs)
            Debug.Log($"設置筆刷大小: {currentBrushSize}");
    }

    public void StartDrawing(Vector2 screenPosition)
    {
        // 先檢查是否在畫布範圍內
        if (!IsPositionInCanvas(screenPosition))
        {
            return; // 不在畫布範圍內，不開始繪圖
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition);
        if (enableDebugLogs)
            Debug.Log($"開始繪圖 - 螢幕座標: {screenPosition}, 畫布座標: {canvasPos}");

        if (IsValidCanvasPosition(canvasPos))
        {
            isDrawing = true;
            lastDrawPosition = canvasPos;
            DrawPoint(canvasPos);
            if (enableDebugLogs)
                Debug.Log("成功開始繪圖");
        }
        else if (enableDebugLogs)
        {
            Debug.Log($"座標無效: {canvasPos}, 畫布大小: {canvasWidth}x{canvasHeight}");
        }
    }

    public void UpdateDrawing(Vector2 screenPosition)
    {
        if (!isDrawing) return;

        // 檢查是否還在畫布範圍內
        if (!IsPositionInCanvas(screenPosition))
        {
            StopDrawing(); // 離開畫布範圍，停止繪圖
            return;
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition);
        if (IsValidCanvasPosition(canvasPos))
        {
            DrawLine(lastDrawPosition, canvasPos);
            lastDrawPosition = canvasPos;
        }
    }

    public void StopDrawing()
    {
        if (isDrawing)
        {
            isDrawing = false;
            // 立即更新貼圖當繪圖結束
            ForceTextureUpdate();
            if (enableDebugLogs)
                Debug.Log("停止繪圖");
        }
    }

    // 檢查螢幕座標是否在畫布RawImage範圍內
    private bool IsPositionInCanvas(Vector2 screenPosition)
    {
        if (canvasImage == null) return false;

        Canvas canvas = canvasImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransform rectTransform = canvasImage.rectTransform;
        Vector2 localPoint;
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPosition, uiCamera, out localPoint);

        return isInside;
    }

    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition)
    {
        if (canvasImage == null)
        {
            if (enableDebugLogs)
                Debug.LogError("canvasImage為null！");
            return Vector2.zero;
        }

        // 獲取畫布的Canvas組件
        Canvas canvas = canvasImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        // 將螢幕座標轉換為畫布本地座標
        RectTransform rectTransform = canvasImage.rectTransform;
        Vector2 localPoint;
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPosition, uiCamera, out localPoint);

        if (!isInside)
        {
            if (enableDebugLogs)
                Debug.Log("點擊位置不在畫布範圍內");
            return new Vector2(-1, -1); // 返回無效座標
        }

        // 轉換為貼圖座標 (0到canvasWidth/canvasHeight)
        Rect rect = rectTransform.rect;
        Vector2 normalizedPos = new Vector2(
            (localPoint.x - rect.x) / rect.width,
            (localPoint.y - rect.y) / rect.height
        );

        // 確保座標在0-1範圍內
        normalizedPos.x = Mathf.Clamp01(normalizedPos.x);
        normalizedPos.y = Mathf.Clamp01(normalizedPos.y);

        Vector2 texturePos = new Vector2(
            normalizedPos.x * canvasWidth,
            normalizedPos.y * canvasHeight
        );

        return texturePos;
    }

    private bool IsValidCanvasPosition(Vector2 canvasPos)
    {
        bool valid = canvasPos.x >= 0 && canvasPos.x < canvasWidth &&
                    canvasPos.y >= 0 && canvasPos.y < canvasHeight;
        return valid;
    }

    private void DrawPoint(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x);
        int y = Mathf.RoundToInt(position.y);
        int radius = Mathf.Max(1, Mathf.RoundToInt(currentBrushSize * 0.5f));

        for (int px = x - radius; px <= x + radius; px++)
        {
            for (int py = y - radius; py <= y + radius; py++)
            {
                if (px >= 0 && px < canvasWidth && py >= 0 && py < canvasHeight)
                {
                    float distance = Vector2.Distance(new Vector2(px, py), position);
                    if (distance <= radius)
                    {
                        drawingTexture.SetPixel(px, py, currentBrushColor);
                    }
                }
            }
        }

        // 標記需要更新，但不立即應用
        needsTextureUpdate = true;
    }

    private void DrawLine(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.RoundToInt(distance);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            DrawPoint(point);
        }
    }

    // 強制立即更新貼圖（用於繪圖結束時）
    public void ForceTextureUpdate()
    {
        if (needsTextureUpdate)
        {
            drawingTexture.Apply();
            needsTextureUpdate = false;
            lastTextureUpdate = Time.time;
        }
    }

    public void ClearCanvas()
    {
        Color[] fillColors = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < fillColors.Length; i++)
        {
            fillColors[i] = backgroundColor;
        }
        drawingTexture.SetPixels(fillColors);
        drawingTexture.Apply();
    }
}