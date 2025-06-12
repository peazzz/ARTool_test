using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Canvas2DManager : MonoBehaviour
{
    [Header("2D Canvas References")]
    public GameObject Canvas2D;
    public RawImage canvasImage;
    public Camera canvas2DCamera;

    [Header("Drawing Icon")]
    public GameObject Pen;
    public GameObject Eraser;
    public GameObject PaintBucket;
    public GameObject Eyedropper;

    private bool usePen;
    private bool useEraser;
    private bool usePaintBucket;
    private bool useEyedropper;

    [Header("2D Canvas UI Input")]
    public Slider Canvas2DWidthSlider;
    public InputField Canvas2DWidthInputField;
    private float canvas2DLineWidth = 1f;

    [Header("2D Canvas System")]
    public Button Finish;
    public UIManager uiManager;
    public DrawFunction drawFunction;
    public FlexibleColorPicker fcp;
    public int canvasWidth = 1024;
    public int canvasHeight = 1024;
    public Color backgroundColor = Color.white;

    private Texture2D drawingTexture;
    private bool isDrawing = false;
    private Vector2 lastDrawPosition;
    private Color currentBrushColor = Color.black;
    private float currentBrushSize = 1f;
    private Color eraserColor;

    [Header("Performance Settings")]
    public bool enableDebugLogs = false;
    public float textureUpdateInterval = 0.016f;
    public int maxBrushSize = 50;
    public float drawingDistanceThreshold = 3f;
    public int maxFloodFillPixels = 50000;
    public int pixelsPerFrame = 2000;
    public bool useDiffusionFill = true;
    public float diffusionRadius = 3f;
    private float lastTextureUpdate = 0f;
    private bool needsTextureUpdate = false;
    private bool isFloodFilling = false;

    private void Start()
    {
        if (Canvas2D == null)
        {
            CreateCanvas2D();
        }

        InitializeCanvas();
        SetupAllButtonEvents();
        WidthSetting();
        SetupColorPicker();

        usePen = true;
        useEraser = false;
        usePaintBucket = false;
        useEyedropper = false;
        SelectTool(1, 0, 0, 0);
    }

    private void Update()
    {
        if (needsTextureUpdate && Time.time - lastTextureUpdate > textureUpdateInterval)
        {
            drawingTexture.Apply();
            needsTextureUpdate = false;
            lastTextureUpdate = Time.time;
        }
    }

    void WidthSetting()
    {
        Canvas2DWidthSlider.minValue = 0.1f;
        Canvas2DWidthSlider.maxValue = 5f;
        Canvas2DWidthSlider.value = canvas2DLineWidth;

        Canvas2DWidthInputField.text = FloatToPercentageString(canvas2DLineWidth);

        if (Canvas2DWidthSlider != null)
            Canvas2DWidthSlider.onValueChanged.AddListener(OnWidthSliderChanged);

        if (Canvas2DWidthInputField != null)
            Canvas2DWidthInputField.onEndEdit.AddListener(OnWidthInputChanged);
    }

    public void OnWidthSliderChanged(float value)
    {
        canvas2DLineWidth = value;
        Canvas2DWidthInputField.text = FloatToPercentageString(value);
        SetBrushSize(value);
    }

    public void OnWidthInputChanged(string inputText)
    {
        float percentageValue = PercentageStringToFloat(inputText);
        canvas2DLineWidth = percentageValue;
        Canvas2DWidthSlider.value = percentageValue;
        SetBrushSize(percentageValue);
    }

    private string FloatToPercentageString(float value)
    {
        int percentage = Mathf.RoundToInt(value * 100f);
        return percentage.ToString() + "%";
    }

    private float PercentageStringToFloat(string percentageText)
    {
        string cleanText = percentageText.Replace("%", "");

        if (float.TryParse(cleanText, out float percentage))
        {
            float floatValue = percentage / 100f;
            return Mathf.Clamp(floatValue, 0.1f, 5f);
        }

        return canvas2DLineWidth;
    }

    void SetupAllButtonEvents()
    {
        Pen.GetComponent<Button>().onClick.AddListener(() => { usePen = true; useEraser = false; usePaintBucket = false; useEyedropper = false; SelectTool(1, 0, 0, 0); });
        Eraser.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = true; usePaintBucket = false; useEyedropper = false; SelectTool(0, 1, 0, 0); });
        PaintBucket.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = true; useEyedropper = false; SelectTool(0, 0, 1, 0); });
        Eyedropper.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = false; useEyedropper = true; SelectTool(0, 0, 0, 1); });
        Finish.onClick.AddListener(() => Complete());
    }

    private void SelectTool(float _pen, float _eraser, float _paintBucket, float _eyedropper)
    {
        Pen.GetComponent<Image>().color = new Color(1, 1, 1, _pen);
        Eraser.GetComponent<Image>().color = new Color(1, 1, 1, _eraser);
        PaintBucket.GetComponent<Image>().color = new Color(1, 1, 1, _paintBucket);
        Eyedropper.GetComponent<Image>().color = new Color(1, 1, 1, _eyedropper);
    }

    void CreateCanvas2D()
    {
        Canvas mainCanvas = FindObjectOfType<Canvas>();

        GameObject canvas2DObj = new GameObject("Canvas2D");
        canvas2DObj.transform.SetParent(mainCanvas.transform, false);

        RectTransform canvas2DRect = canvas2DObj.AddComponent<RectTransform>();
        canvas2DRect.anchorMin = Vector2.zero;
        canvas2DRect.anchorMax = Vector2.one;
        canvas2DRect.offsetMin = Vector2.zero;
        canvas2DRect.offsetMax = Vector2.zero;

        Canvas2D = canvas2DObj;

        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(canvas2DObj.transform, false);

        RectTransform bgRect = backgroundObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = backgroundObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);

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
        drawingTexture = new Texture2D(canvasWidth, canvasHeight);
        eraserColor = backgroundColor;

        Color[] fillColors = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < fillColors.Length; i++)
        {
            fillColors[i] = backgroundColor;
        }
        drawingTexture.SetPixels(fillColors);
        drawingTexture.Apply();

        if (canvasImage != null)
        {
            canvasImage.texture = drawingTexture;
        }

        SetBrushSize(canvas2DLineWidth);

        if (Canvas2D != null)
        {
            Canvas2D.SetActive(false);
        }
    }

    public void Show2DCanvas()
    {
        if (Canvas2D != null)
        {
            uiManager.DrawPanel1.SetActive(false);
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
        UpdateUIColorDisplay();
        if (enableDebugLogs)
            Debug.Log($"設置筆刷顏色: {color}");
    }

    public void SetBrushSize(float size)
    {
        currentBrushSize = Mathf.Max(0.1f, size);
    }

    public void StartDrawing(Vector2 screenPosition)
    {
        if (!IsPositionInCanvas(screenPosition))
        {
            return;
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition);
        if (enableDebugLogs)
            Debug.Log($"開始繪圖 - 螢幕座標: {screenPosition}, 畫布座標: {canvasPos}");

        if (IsValidCanvasPosition(canvasPos))
        {
            if (useEyedropper)
            {
                PickColor(canvasPos);
                return;
            }

            if (usePaintBucket)
            {
                FloodFill(canvasPos, currentBrushColor);
                return;
            }

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

        if (useEyedropper || usePaintBucket) return;

        if (!IsPositionInCanvas(screenPosition))
        {
            StopDrawing();
            return;
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition);
        if (IsValidCanvasPosition(canvasPos))
        {
            float distance = Vector2.Distance(canvasPos, lastDrawPosition);
            if (distance >= drawingDistanceThreshold)
            {
                DrawLine(lastDrawPosition, canvasPos);
                lastDrawPosition = canvasPos;
            }
        }
    }

    public void StopDrawing()
    {
        if (isDrawing)
        {
            isDrawing = false;
            ForceTextureUpdate();
            if (enableDebugLogs)
                Debug.Log("停止繪圖");
        }
    }

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

        Canvas canvas = canvasImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransform rectTransform = canvasImage.rectTransform;
        Vector2 localPoint;
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPosition, uiCamera, out localPoint);

        if (!isInside)
        {
            if (enableDebugLogs)
                Debug.Log("點擊位置不在畫布範圍內");
            return new Vector2(-1, -1);
        }

        Rect rect = rectTransform.rect;
        Vector2 normalizedPos = new Vector2(
            (localPoint.x - rect.x) / rect.width,
            (localPoint.y - rect.y) / rect.height
        );

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
        int radius = Mathf.Max(1, Mathf.Min(Mathf.RoundToInt(currentBrushSize * 10f), maxBrushSize));

        int startX = Mathf.Max(0, x - radius);
        int endX = Mathf.Min(canvasWidth - 1, x + radius);
        int startY = Mathf.Max(0, y - radius);
        int endY = Mathf.Min(canvasHeight - 1, y + radius);

        float radiusSquared = radius * radius;

        Color colorToUse = useEraser ? eraserColor : currentBrushColor;

        if (!useEraser)
        {
            colorToUse.a = 1f;
        }

        for (int px = startX; px <= endX; px++)
        {
            for (int py = startY; py <= endY; py++)
            {
                float dx = px - x;
                float dy = py - y;
                float distanceSquared = dx * dx + dy * dy;

                if (distanceSquared <= radiusSquared)
                {
                    drawingTexture.SetPixel(px, py, colorToUse);
                }
            }
        }

        needsTextureUpdate = true;
    }

    private void DrawLine(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);

        if (distance < drawingDistanceThreshold)
        {
            DrawPoint(to);
            return;
        }

        int steps = Mathf.Min(Mathf.RoundToInt(distance * 0.5f), 20);
        steps = Mathf.Max(steps, 1);

        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = Vector2.Lerp(from, to, t);
            DrawPoint(point);
        }
    }

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

    private void PickColor(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x);
        int y = Mathf.RoundToInt(position.y);

        if (x >= 0 && x < canvasWidth && y >= 0 && y < canvasHeight)
        {
            Color pickedColor = drawingTexture.GetPixel(x, y);
            SetBrushColor(pickedColor);

            if (fcp != null)
            {
                fcp.color = pickedColor;
            }

            if (enableDebugLogs)
                Debug.Log($"吸取顏色: {pickedColor}");
        }
    }

    private void FloodFill(Vector2 startPosition, Color newColor)
    {
        if (isFloodFilling) return;

        int startX = Mathf.RoundToInt(startPosition.x);
        int startY = Mathf.RoundToInt(startPosition.y);

        if (startX < 0 || startX >= canvasWidth || startY < 0 || startY >= canvasHeight)
            return;

        Color targetColor = drawingTexture.GetPixel(startX, startY);
        newColor.a = 1f;

        if (ColorsEqual(targetColor, newColor))
            return;

        // 特殊處理：如果是填充背景色且是大畫布，直接使用高效方法
        if (ColorsEqual(targetColor, backgroundColor) && (canvasWidth * canvasHeight) > 500000)
        {
            FillEntireCanvas(newColor);
            return;
        }

        // 選擇填充方式
        if (useDiffusionFill)
        {
            StartCoroutine(DiffusionFillCoroutine(startX, startY, targetColor, newColor));
        }
        else
        {
            StartCoroutine(ScanLineFillCoroutine(startX, startY, targetColor, newColor));
        }
    }

    private IEnumerator DiffusionFillCoroutine(int startX, int startY, Color targetColor, Color newColor)
    {
        isFloodFilling = true;

        HashSet<Vector2Int> visited = new HashSet<Vector2Int>();
        HashSet<Vector2Int> toFill = new HashSet<Vector2Int>();

        // 先找出所有需要填充的像素
        Queue<Vector2Int> scanQueue = new Queue<Vector2Int>();
        scanQueue.Enqueue(new Vector2Int(startX, startY));
        visited.Add(new Vector2Int(startX, startY));

        while (scanQueue.Count > 0)
        {
            Vector2Int current = scanQueue.Dequeue();
            int x = current.x;
            int y = current.y;

            if (x >= 0 && x < canvasWidth && y >= 0 && y < canvasHeight)
            {
                Color currentColor = drawingTexture.GetPixel(x, y);
                if (ColorsEqual(currentColor, targetColor))
                {
                    toFill.Add(current);

                    // 檢查四個方向的鄰居
                    Vector2Int[] neighbors = {
                        new Vector2Int(x + 1, y),
                        new Vector2Int(x - 1, y),
                        new Vector2Int(x, y + 1),
                        new Vector2Int(x, y - 1)
                    };

                    foreach (Vector2Int neighbor in neighbors)
                    {
                        if (!visited.Contains(neighbor) &&
                            neighbor.x >= 0 && neighbor.x < canvasWidth &&
                            neighbor.y >= 0 && neighbor.y < canvasHeight)
                        {
                            visited.Add(neighbor);
                            scanQueue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }

        if (enableDebugLogs)
            Debug.Log($"準備擴散填充 {toFill.Count} 個像素");

        // 按距離排序，創造圓形擴散效果
        List<Vector2Int> sortedPixels = new List<Vector2Int>(toFill);
        sortedPixels.Sort((a, b) => {
            float distA = Vector2.Distance(new Vector2(a.x, a.y), new Vector2(startX, startY));
            float distB = Vector2.Distance(new Vector2(b.x, b.y), new Vector2(startX, startY));
            return distA.CompareTo(distB);
        });

        // 分組擴散填充
        int totalProcessed = 0;
        float currentRadius = 0;

        while (totalProcessed < sortedPixels.Count)
        {
            currentRadius += diffusionRadius;
            int pixelsThisFrame = 0;

            // 填充當前半徑範圍內的所有像素
            for (int i = totalProcessed; i < sortedPixels.Count && pixelsThisFrame < pixelsPerFrame; i++)
            {
                Vector2Int pixel = sortedPixels[i];
                float distance = Vector2.Distance(new Vector2(pixel.x, pixel.y), new Vector2(startX, startY));

                if (distance <= currentRadius)
                {
                    drawingTexture.SetPixel(pixel.x, pixel.y, newColor);
                    totalProcessed++;
                    pixelsThisFrame++;
                }
                else
                {
                    break; // 超出當前半徑，等下一幀
                }
            }

            // 更新紋理並等待下一幀
            if (pixelsThisFrame > 0)
            {
                drawingTexture.Apply();

                if (enableDebugLogs && totalProcessed % 1000 == 0)
                    Debug.Log($"圓形擴散進度: {totalProcessed}/{sortedPixels.Count} 像素，半徑: {currentRadius:F1}");

                yield return null;
            }

            // 如果這一幀沒有處理任何像素，說明當前半徑範圍內沒有像素，直接跳到下一個像素的位置
            if (pixelsThisFrame == 0 && totalProcessed < sortedPixels.Count)
            {
                Vector2Int nextPixel = sortedPixels[totalProcessed];
                currentRadius = Vector2.Distance(new Vector2(nextPixel.x, nextPixel.y), new Vector2(startX, startY));
            }
        }

        // 最終更新
        drawingTexture.Apply();
        isFloodFilling = false;

        if (enableDebugLogs)
            Debug.Log($"圓形擴散填充完成，處理了 {totalProcessed} 像素");
    }

    private IEnumerator ScanLineFillCoroutine(int startX, int startY, Color targetColor, Color newColor)
    {
        isFloodFilling = true;

        Stack<Vector2Int> pixels = new Stack<Vector2Int>();
        pixels.Push(new Vector2Int(startX, startY));

        int processedLines = 0;
        int maxLines = 2000;
        int linesThisFrame = 0;
        int linesPerFrame = 5; // 本地變數

        while (pixels.Count > 0 && processedLines < maxLines)
        {
            Vector2Int point = pixels.Pop();
            int x = point.x;
            int y = point.y;

            if (y < 0 || y >= canvasHeight) continue;

            // 向左掃描到邊界
            int leftX = x;
            while (leftX >= 0 && ColorsEqual(drawingTexture.GetPixel(leftX, y), targetColor))
            {
                leftX--;
            }
            leftX++;

            // 向右掃描到邊界
            int rightX = x;
            while (rightX < canvasWidth && ColorsEqual(drawingTexture.GetPixel(rightX, y), targetColor))
            {
                rightX++;
            }
            rightX--;

            // 填充這一行
            for (int fillX = leftX; fillX <= rightX; fillX++)
            {
                drawingTexture.SetPixel(fillX, y, newColor);
            }

            // 檢查上下兩行
            bool spanAbove = false;
            bool spanBelow = false;

            for (int scanX = leftX; scanX <= rightX; scanX++)
            {
                // 檢查上方
                if (y > 0)
                {
                    bool needsFillAbove = ColorsEqual(drawingTexture.GetPixel(scanX, y - 1), targetColor);
                    if (!spanAbove && needsFillAbove)
                    {
                        pixels.Push(new Vector2Int(scanX, y - 1));
                        spanAbove = true;
                    }
                    else if (spanAbove && !needsFillAbove)
                    {
                        spanAbove = false;
                    }
                }

                // 檢查下方
                if (y < canvasHeight - 1)
                {
                    bool needsFillBelow = ColorsEqual(drawingTexture.GetPixel(scanX, y + 1), targetColor);
                    if (!spanBelow && needsFillBelow)
                    {
                        pixels.Push(new Vector2Int(scanX, y + 1));
                        spanBelow = true;
                    }
                    else if (spanBelow && !needsFillBelow)
                    {
                        spanBelow = false;
                    }
                }
            }

            processedLines++;
            linesThisFrame++;

            // 每處理指定數量的線就暫停一幀
            if (linesThisFrame >= linesPerFrame)
            {
                drawingTexture.Apply();
                linesThisFrame = 0;

                if (enableDebugLogs && processedLines % 50 == 0)
                    Debug.Log($"填充進度: {processedLines}/{maxLines} 條線");

                yield return null; // 等待下一幀
            }
        }

        // 最終更新
        drawingTexture.Apply();
        isFloodFilling = false;

        if (enableDebugLogs)
        {
            if (processedLines >= maxLines)
                Debug.LogWarning($"掃描線填充達到上限 ({maxLines} 條線)");
            else
                Debug.Log($"掃描線填充完成，處理了 {processedLines} 條線");
        }
    }

    private void FillEntireCanvas(Color newColor)
    {
        Color[] fillColors = new Color[canvasWidth * canvasHeight];
        for (int i = 0; i < fillColors.Length; i++)
        {
            fillColors[i] = newColor;
        }
        drawingTexture.SetPixels(fillColors);
        drawingTexture.Apply();

        if (enableDebugLogs)
            Debug.Log($"整個畫布填色完成，顏色: {newColor}");
    }

    private bool ColorsEqual(Color a, Color b)
    {
        return Mathf.Approximately(a.r, b.r) &&
               Mathf.Approximately(a.g, b.g) &&
               Mathf.Approximately(a.b, b.b) &&
               Mathf.Approximately(a.a, b.a);
    }

    private void SetupColorPicker()
    {
        if (fcp != null)
        {
            fcp.onColorChange.AddListener(OnColorPickerChanged);
            SetBrushColor(fcp.color);
            UpdateUIColorDisplay();
        }
    }

    private void OnColorPickerChanged(Color newColor)
    {
        SetBrushColor(newColor);
        UpdateUIColorDisplay();
    }

    private void UpdateUIColorDisplay()
    {
        if (uiManager != null && uiManager.ColorPageButtonForDraw2D != null)
        {
            uiManager.ColorPageButtonForDraw2D.GetComponent<Image>().color = currentBrushColor;
        }
    }

    private void Complete()
    {
        ClearCanvas();
        uiManager.FounctionUI.SetActive(true);
        uiManager.inDraw = false;
        uiManager.isInColorPage = false;
        uiManager.ColorPage3.SetActive(false);
        uiManager.BasicEditPage2D.SetActive(true);

        drawFunction.in3DDraw = false;
        drawFunction.StraightLine = false;
        drawFunction.in2DDraw = false;
        drawFunction.LineBrush = false;
        drawFunction.ParticleBrush = false;

        Canvas2D.SetActive(false);
        uiManager.DrawPanel1?.SetActive(false);
        uiManager.DrawPanel2?.SetActive(false);
        uiManager.BrushPanel2D?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);
        uiManager.ClearModeButton.SetActive(true);
    }

    public void Leave()
    {
        ClearCanvas();
        Hide2DCanvas();
        drawFunction.in2DDraw = false;
        uiManager.FounctionUI.SetActive(true);
        uiManager.SwitchToPanel(uiManager.DrawPanel1);
    }
}