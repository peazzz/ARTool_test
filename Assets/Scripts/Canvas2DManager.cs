using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class Canvas2DManager : MonoBehaviour
{
    [Header("2D Canvas References")]
    public GameObject Canvas2D;
    public GameObject Canvas2D_Tablet;
    public RawImage canvasImage;
    public RawImage canvasImage_Tablet;
    public Camera canvas2DCamera;

    [Header("Drawing Icon")]
    public GameObject Pen;
    public GameObject Eraser;
    public GameObject PaintBucket;
    public GameObject Eyedropper;

    // 平板專用工具按鈕
    [Header("Drawing Icon - Tablet")]
    public GameObject Pen_Tablet;
    public GameObject Eraser_Tablet;
    public GameObject PaintBucket_Tablet;
    public GameObject Eyedropper_Tablet;
    public GameObject ColorButton_Tablet;

    private bool usePen;
    private bool useEraser;
    private bool usePaintBucket;
    private bool useEyedropper;

    [Header("2D Canvas UI Input")]
    public Slider Canvas2DWidthSlider;
    public InputField Canvas2DWidthInputField;

    // 平板專用UI控制項
    [Header("2D Canvas UI Input - Tablet")]
    public Slider Canvas2DWidthSlider_Tablet;
    public InputField Canvas2DWidthInputField_Tablet;

    private float canvas2DLineWidth = 1f;

    [Header("2D Canvas System")]
    public Button Finish;
    public Button Finish_Tablet;
    public UIManager uiManager;
    public DrawFunction drawFunction;
    public FlexibleColorPicker fcp;
    public FlexibleColorPicker fcp_Tablet;
    public int canvasWidth = 1024;
    public int canvasHeight = 1024;
    public Color backgroundColor = Color.white;

    // Undo/Redo 系統
    [Header("Undo/Redo System")]
    public Button UndoButton;
    public Button RedoButton;
    public Button UndoButton_Tablet;
    public Button RedoButton_Tablet;
    [Range(5, 50)]
    public int maxHistoryStates = 20;

    private List<Color[]> historyStates;
    private int currentHistoryIndex = -1;
    private bool isRestoringState = false;

    // 圖片保存系統
    [Header("Image Save System")]
    public bool autoSaveOnFinish = true;
    public string saveFileName = "ARTool";
    public ImageFormat saveFormat = ImageFormat.PNG;
    [Range(50, 100)]
    public int jpegQuality = 90;
    public bool showSaveDialog = true;
    public Text saveStatusText; // 用於顯示保存狀態的UI文字

    public enum ImageFormat
    {
        PNG,
        JPG
    }

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

    private bool isCurrentlyTablet = false;

    #region Image Save System

    /// <summary>
    /// 保存當前畫布內容到本機
    /// </summary>
    public void SaveImageToDevice()
    {
        StartCoroutine(SaveImageCoroutine());
    }

    private IEnumerator SaveImageCoroutine()
    {
        if (drawingTexture == null)
        {
            ShowSaveStatus("錯誤：沒有可保存的圖片", false);
            yield break;
        }

        ShowSaveStatus("正在保存圖片...", true);

        // 確保紋理是最新的
        ForceTextureUpdate();

        try
        {
            // 創建一個新的紋理副本來避免修改原始紋理
            Texture2D saveTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false);
            saveTexture.SetPixels(drawingTexture.GetPixels());
            saveTexture.Apply();

            // 根據選擇的格式編碼圖片
            byte[] imageData;
            string fileExtension;

            if (saveFormat == ImageFormat.PNG)
            {
                imageData = saveTexture.EncodeToPNG();
                fileExtension = ".png";
            }
            else
            {
                imageData = saveTexture.EncodeToJPG(jpegQuality);
                fileExtension = ".jpg";
            }

            // 生成唯一的檔案名稱
            string fileName = GenerateUniqueFileName(saveFileName, fileExtension);

            // 保存到設備
            bool saveSuccess = SaveToDevice(imageData, fileName);

            // 清理記憶體
            DestroyImmediate(saveTexture);

            if (saveSuccess)
            {
                ShowSaveStatus($"圖片已保存：{fileName}", true);

                if (enableDebugLogs)
                    Debug.Log($"圖片保存成功：{fileName}");
            }
            else
            {
                ShowSaveStatus("保存失敗", false);
            }
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("保存時發生錯誤", false);
            Debug.LogError($"保存圖片時發生錯誤：{e.Message}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    private bool SaveToDevice(byte[] imageData, string fileName)
    {
        try
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return SaveToAndroid(imageData, fileName);
#elif UNITY_IOS && !UNITY_EDITOR
            return SaveToIOS(imageData, fileName);
#else
            return SaveToPC(imageData, fileName);
#endif
        }
        catch (System.Exception e)
        {
            Debug.LogError($"保存到設備時發生錯誤：{e.Message}");
            return false;
        }
    }

    private bool SaveToAndroid(byte[] imageData, string fileName)
    {
        try
        {
            // Android：保存到 Pictures/ARTool 資料夾
            string folderPath = Path.Combine(Application.persistentDataPath, "Pictures");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(filePath, imageData);

            // 通知 Android 媒體庫更新
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject context = currentActivity.Call<AndroidJavaObject>("getApplicationContext"))
            {
                using (AndroidJavaClass mediaStoreClass = new AndroidJavaClass("android.provider.MediaStore"))
                using (AndroidJavaClass mediaScannerConnectionClass = new AndroidJavaClass("android.media.MediaScannerConnection"))
                {
                    mediaScannerConnectionClass.CallStatic("scanFile", context, new string[] { filePath }, null, null);
                }
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Android 保存失敗：{e.Message}");
            return false;
        }
    }

    private bool SaveToIOS(byte[] imageData, string fileName)
    {
        try
        {
            // iOS：保存到相簿
            string tempPath = Path.Combine(Application.persistentDataPath, fileName);
            File.WriteAllBytes(tempPath, imageData);

            // 這裡需要原生 iOS 插件來保存到相簿
            // 暫時保存到 Documents 資料夾
            if (enableDebugLogs)
                Debug.Log($"iOS: 圖片已保存到 Documents 資料夾：{tempPath}");

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"iOS 保存失敗：{e.Message}");
            return false;
        }
    }

    private bool SaveToPC(byte[] imageData, string fileName)
    {
        try
        {
            // PC：保存到 Documents/MyApp 資料夾
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
            string folderPath = Path.Combine(documentsPath, "ARTool");

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string filePath = Path.Combine(folderPath, fileName);
            File.WriteAllBytes(filePath, imageData);

            if (enableDebugLogs)
                Debug.Log($"PC: 圖片已保存到：{filePath}");

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"PC 保存失敗：{e.Message}");
            return false;
        }
    }

    private string GenerateUniqueFileName(string baseName, string extension)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{baseName}_{timestamp}{extension}";
    }

    private void ShowSaveStatus(string message, bool isProgress)
    {
        if (saveStatusText != null)
        {
            saveStatusText.text = message;
            saveStatusText.color = isProgress ? Color.blue : Color.red;
            saveStatusText.gameObject.SetActive(true);
        }

        if (enableDebugLogs)
            Debug.Log($"保存狀態：{message}");
    }

    private void HideSaveStatus()
    {
        if (saveStatusText != null)
        {
            saveStatusText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 手動觸發保存圖片（可以綁定到按鈕）
    /// </summary>
    public void ManualSaveImage()
    {
        SaveImageToDevice();
    }

    #endregion

    private void Start()
    {
        DetectAndSetupDevice();

        if (Canvas2D == null && Canvas2D_Tablet == null)
        {
            CreateCanvas2D();
        }

        InitializeCanvas();
        InitializeUndoRedoSystem();
        SetupAllButtonEvents();
        WidthSetting();
        SetupColorPicker();

        usePen = true;
        useEraser = false;
        usePaintBucket = false;
        useEyedropper = false;
        SelectTool(1, 0, 0, 0);
    }

    #region Undo/Redo System

    private void InitializeUndoRedoSystem()
    {
        historyStates = new List<Color[]>();

        if (UndoButton != null)
            UndoButton.onClick.AddListener(Undo);
        if (RedoButton != null)
            RedoButton.onClick.AddListener(Redo);
        if (UndoButton_Tablet != null)
            UndoButton_Tablet.onClick.AddListener(Undo);
        if (RedoButton_Tablet != null)
            RedoButton_Tablet.onClick.AddListener(Redo);

        SaveCurrentState();

        if (enableDebugLogs)
            Debug.Log("Undo/Redo系統初始化完成");
    }

    public void SaveCurrentState()
    {
        if (isRestoringState || drawingTexture == null) return;

        if (currentHistoryIndex < historyStates.Count - 1)
        {
            int removeCount = historyStates.Count - currentHistoryIndex - 1;
            historyStates.RemoveRange(currentHistoryIndex + 1, removeCount);
        }

        Color[] currentState = drawingTexture.GetPixels();
        Color[] stateCopy = new Color[currentState.Length];
        System.Array.Copy(currentState, stateCopy, currentState.Length);

        historyStates.Add(stateCopy);
        currentHistoryIndex++;

        if (historyStates.Count > maxHistoryStates)
        {
            historyStates.RemoveAt(0);
            currentHistoryIndex--;
        }

        UpdateUndoRedoButtons();

        if (enableDebugLogs)
            Debug.Log($"保存狀態 - 當前索引: {currentHistoryIndex}, 總狀態數: {historyStates.Count}");
    }

    public void Undo()
    {
        if (!CanUndo()) return;

        currentHistoryIndex--;
        RestoreState(currentHistoryIndex);

        if (enableDebugLogs)
            Debug.Log($"執行Undo - 當前索引: {currentHistoryIndex}");
    }

    public void Redo()
    {
        if (!CanRedo()) return;

        currentHistoryIndex++;
        RestoreState(currentHistoryIndex);

        if (enableDebugLogs)
            Debug.Log($"執行Redo - 當前索引: {currentHistoryIndex}");
    }

    private bool CanUndo()
    {
        return currentHistoryIndex > 0;
    }

    private bool CanRedo()
    {
        return currentHistoryIndex < historyStates.Count - 1;
    }

    private void RestoreState(int index)
    {
        if (index < 0 || index >= historyStates.Count) return;

        isRestoringState = true;

        Color[] stateToRestore = historyStates[index];
        drawingTexture.SetPixels(stateToRestore);
        drawingTexture.Apply();

        isRestoringState = false;
        UpdateUndoRedoButtons();
    }

    private void UpdateUndoRedoButtons()
    {
        bool canUndo = CanUndo();
        bool canRedo = CanRedo();

        if (UndoButton != null)
            UndoButton.interactable = canUndo;
        if (RedoButton != null)
            RedoButton.interactable = canRedo;

        if (UndoButton_Tablet != null)
            UndoButton_Tablet.interactable = canUndo;
        if (RedoButton_Tablet != null)
            RedoButton_Tablet.interactable = canRedo;

        UpdateButtonAlpha(UndoButton, canUndo);
        UpdateButtonAlpha(RedoButton, canRedo);
        UpdateButtonAlpha(UndoButton_Tablet, canUndo);
        UpdateButtonAlpha(RedoButton_Tablet, canRedo);
    }

    private void UpdateButtonAlpha(Button button, bool enabled)
    {
        if (button == null) return;

        Image buttonImage = button.GetComponent<Image>();
        if (buttonImage != null)
        {
            Color color = buttonImage.color;
            color.a = enabled ? 1f : 0.5f;
            buttonImage.color = color;
        }
    }

    public void ClearHistory()
    {
        historyStates.Clear();
        currentHistoryIndex = -1;
        SaveCurrentState();

        if (enableDebugLogs)
            Debug.Log("清除所有歷史記錄");
    }

    #endregion

    private void DetectAndSetupDevice()
    {
        if (DeviceDetector.Instance != null)
        {
            isCurrentlyTablet = DeviceDetector.Instance.IsTablet();

            if (enableDebugLogs)
            {
                Debug.Log($"偵測到設備類型: {(isCurrentlyTablet ? "平板" : "手機")}");
            }
        }
        else
        {
            float aspectRatio = (float)Screen.width / Screen.height;
            isCurrentlyTablet = aspectRatio < 1.7f;

            if (enableDebugLogs)
            {
                Debug.LogWarning("DeviceDetector 不存在，使用預設邏輯判斷設備類型");
            }
        }
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
        if (Canvas2DWidthSlider != null)
        {
            Canvas2DWidthSlider.minValue = 0.1f;
            Canvas2DWidthSlider.maxValue = 5f;
            Canvas2DWidthSlider.value = canvas2DLineWidth;
            Canvas2DWidthSlider.onValueChanged.AddListener(OnWidthSliderChanged);
        }

        if (Canvas2DWidthSlider_Tablet != null)
        {
            Canvas2DWidthSlider_Tablet.minValue = 0.1f;
            Canvas2DWidthSlider_Tablet.maxValue = 5f;
            Canvas2DWidthSlider_Tablet.value = canvas2DLineWidth;
            Canvas2DWidthSlider_Tablet.onValueChanged.AddListener(OnWidthSliderChanged);
        }

        if (Canvas2DWidthInputField != null)
        {
            Canvas2DWidthInputField.text = FloatToPercentageString(canvas2DLineWidth);
            Canvas2DWidthInputField.onEndEdit.AddListener(OnWidthInputChanged);
        }

        if (Canvas2DWidthInputField_Tablet != null)
        {
            Canvas2DWidthInputField_Tablet.text = FloatToPercentageString(canvas2DLineWidth);
            Canvas2DWidthInputField_Tablet.onEndEdit.AddListener(OnWidthInputChanged);
        }
    }

    public void OnWidthSliderChanged(float value)
    {
        canvas2DLineWidth = value;

        if (Canvas2DWidthInputField != null)
            Canvas2DWidthInputField.text = FloatToPercentageString(value);
        if (Canvas2DWidthInputField_Tablet != null)
            Canvas2DWidthInputField_Tablet.text = FloatToPercentageString(value);

        SetBrushSize(value);
    }

    public void OnWidthInputChanged(string inputText)
    {
        float percentageValue = PercentageStringToFloat(inputText);
        canvas2DLineWidth = percentageValue;

        if (Canvas2DWidthSlider != null)
            Canvas2DWidthSlider.value = percentageValue;
        if (Canvas2DWidthSlider_Tablet != null)
            Canvas2DWidthSlider_Tablet.value = percentageValue;

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
        if (Pen != null)
            Pen.GetComponent<Button>().onClick.AddListener(() => { usePen = true; useEraser = false; usePaintBucket = false; useEyedropper = false; SelectTool(1, 0, 0, 0); });
        if (Eraser != null)
            Eraser.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = true; usePaintBucket = false; useEyedropper = false; SelectTool(0, 1, 0, 0); });
        if (PaintBucket != null)
            PaintBucket.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = true; useEyedropper = false; SelectTool(0, 0, 1, 0); });
        if (Eyedropper != null)
            Eyedropper.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = false; useEyedropper = true; SelectTool(0, 0, 0, 1); });
        if (Finish != null)
            Finish.onClick.AddListener(() => Complete());

        if (Pen_Tablet != null)
            Pen_Tablet.GetComponent<Button>().onClick.AddListener(() => { usePen = true; useEraser = false; usePaintBucket = false; useEyedropper = false; SelectTool(1, 0, 0, 0); });
        if (Eraser_Tablet != null)
            Eraser_Tablet.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = true; usePaintBucket = false; useEyedropper = false; SelectTool(0, 1, 0, 0); });
        if (PaintBucket_Tablet != null)
            PaintBucket_Tablet.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = true; useEyedropper = false; SelectTool(0, 0, 1, 0); });
        if (Eyedropper_Tablet != null)
            Eyedropper_Tablet.GetComponent<Button>().onClick.AddListener(() => { usePen = false; useEraser = false; usePaintBucket = false; useEyedropper = true; SelectTool(0, 0, 0, 1); });
        if (Finish_Tablet != null)
            Finish_Tablet.onClick.AddListener(() => Complete());
    }

    private void SelectTool(float _pen, float _eraser, float _paintBucket, float _eyedropper)
    {
        if (Pen != null)
            Pen.GetComponent<Image>().color = new Color(1, 1, 1, _pen);
        if (Eraser != null)
            Eraser.GetComponent<Image>().color = new Color(1, 1, 1, _eraser);
        if (PaintBucket != null)
            PaintBucket.GetComponent<Image>().color = new Color(1, 1, 1, _paintBucket);
        if (Eyedropper != null)
            Eyedropper.GetComponent<Image>().color = new Color(1, 1, 1, _eyedropper);

        if (Pen_Tablet != null)
            Pen_Tablet.GetComponent<Image>().color = new Color(1, 1, 1, _pen);
        if (Eraser_Tablet != null)
            Eraser_Tablet.GetComponent<Image>().color = new Color(1, 1, 1, _eraser);
        if (PaintBucket_Tablet != null)
            PaintBucket_Tablet.GetComponent<Image>().color = new Color(1, 1, 1, _paintBucket);
        if (Eyedropper_Tablet != null)
            Eyedropper_Tablet.GetComponent<Image>().color = new Color(1, 1, 1, _eyedropper);
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

        GameObject canvas2DTabletObj = new GameObject("Canvas2D_Tablet");
        canvas2DTabletObj.transform.SetParent(mainCanvas.transform, false);

        RectTransform canvas2DTabletRect = canvas2DTabletObj.AddComponent<RectTransform>();
        canvas2DTabletRect.anchorMin = Vector2.zero;
        canvas2DTabletRect.anchorMax = Vector2.one;
        canvas2DTabletRect.offsetMin = Vector2.zero;
        canvas2DTabletRect.offsetMax = Vector2.zero;

        Canvas2D_Tablet = canvas2DTabletObj;

        CreateDrawingArea(Canvas2D, out canvasImage, new Vector2(800, 600));
        CreateDrawingArea(Canvas2D_Tablet, out canvasImage_Tablet, new Vector2(1000, 750));

        Debug.Log("自動創建了手機版和平板版 2D Canvas UI");
    }

    void CreateDrawingArea(GameObject parentCanvas, out RawImage rawImage, Vector2 size)
    {
        GameObject backgroundObj = new GameObject("Background");
        backgroundObj.transform.SetParent(parentCanvas.transform, false);

        RectTransform bgRect = backgroundObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        Image bgImage = backgroundObj.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.5f);

        GameObject drawingAreaObj = new GameObject("DrawingArea");
        drawingAreaObj.transform.SetParent(parentCanvas.transform, false);

        RectTransform drawingRect = drawingAreaObj.AddComponent<RectTransform>();
        drawingRect.anchorMin = new Vector2(0.5f, 0.5f);
        drawingRect.anchorMax = new Vector2(0.5f, 0.5f);
        drawingRect.sizeDelta = size;
        drawingRect.anchoredPosition = Vector2.zero;

        rawImage = drawingAreaObj.AddComponent<RawImage>();
        rawImage.color = Color.white;
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
        if (canvasImage_Tablet != null)
        {
            canvasImage_Tablet.texture = drawingTexture;
        }

        SetBrushSize(canvas2DLineWidth);

        if (Canvas2D != null)
        {
            Canvas2D.SetActive(false);
        }
        if (Canvas2D_Tablet != null)
        {
            Canvas2D_Tablet.SetActive(false);
        }
    }

    public void Show2DCanvas_Mobile()
    {
        if (Canvas2D != null)
        {
            uiManager.DrawPanel1.SetActive(false);

            if (Canvas2D_Tablet != null)
                Canvas2D_Tablet.SetActive(false);

            Canvas2D.SetActive(true);

            if (enableDebugLogs)
                Debug.Log("顯示手機版 2D Canvas");
        }
    }

    public void Show2DCanvas_Tablet()
    {
        if (Canvas2D_Tablet != null)
        {
            uiManager.DrawPanel1.SetActive(false);

            if (Canvas2D != null)
                Canvas2D.SetActive(false);

            Canvas2D_Tablet.SetActive(true);

            if (enableDebugLogs)
                Debug.Log("顯示平板版 2D Canvas");
        }
    }

    public void Show2DCanvas_Auto()
    {
        uiManager.LoadButton.SetActive(false);

        if (isCurrentlyTablet)
        {
            Show2DCanvas_Tablet();
            ColorButtonChange();
        }
        else
        {
            Show2DCanvas_Mobile();
        }
    }

    public void Hide2DCanvas()
    {
        if (Canvas2D != null)
        {
            Canvas2D.SetActive(false);
        }
        if (Canvas2D_Tablet != null)
        {
            Canvas2D_Tablet.SetActive(false);
        }

        if (enableDebugLogs)
            Debug.Log("2D Canvas已隱藏");
    }

    public RawImage GetCurrentCanvasImage()
    {
        if (isCurrentlyTablet && canvasImage_Tablet != null)
        {
            return canvasImage_Tablet;
        }
        else if (!isCurrentlyTablet && canvasImage != null)
        {
            return canvasImage;
        }

        return canvasImage != null ? canvasImage : canvasImage_Tablet;
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
        RawImage currentCanvas = GetCurrentCanvasImage();
        if (!IsPositionInCanvas(screenPosition, currentCanvas))
        {
            return;
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition, currentCanvas);
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

        RawImage currentCanvas = GetCurrentCanvasImage();
        if (!IsPositionInCanvas(screenPosition, currentCanvas))
        {
            StopDrawing();
            return;
        }

        Vector2 canvasPos = ScreenToCanvasPosition(screenPosition, currentCanvas);
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

            // 繪圖完成後保存狀態，用於Undo/Redo
            SaveCurrentState();

            if (enableDebugLogs)
                Debug.Log("停止繪圖");
        }
    }

    private bool IsPositionInCanvas(Vector2 screenPosition, RawImage targetCanvas = null)
    {
        if (targetCanvas == null)
            targetCanvas = GetCurrentCanvasImage();

        if (targetCanvas == null) return false;

        Canvas canvas = targetCanvas.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransform rectTransform = targetCanvas.rectTransform;
        Vector2 localPoint;
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform, screenPosition, uiCamera, out localPoint);

        return isInside;
    }

    private Vector2 ScreenToCanvasPosition(Vector2 screenPosition, RawImage targetCanvas = null)
    {
        if (targetCanvas == null)
            targetCanvas = GetCurrentCanvasImage();

        if (targetCanvas == null)
        {
            if (enableDebugLogs)
                Debug.LogError("targetCanvas為null！");
            return Vector2.zero;
        }

        Canvas canvas = targetCanvas.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        RectTransform rectTransform = targetCanvas.rectTransform;
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

        // 清除畫布後保存狀態
        SaveCurrentState();
    }

    private void PickColor(Vector2 position)
    {
        int x = Mathf.RoundToInt(position.x);
        int y = Mathf.RoundToInt(position.y);

        if (x >= 0 && x < canvasWidth && y >= 0 && y < canvasHeight)
        {
            Color pickedColor = drawingTexture.GetPixel(x, y);
            SetBrushColor(pickedColor);

            // 更新兩邊顏色選擇器
            if (fcp != null)
            {
                fcp.color = pickedColor;
            }
            if (fcp_Tablet != null)
            {
                fcp_Tablet.color = pickedColor;
                ColorButton_Tablet.GetComponent<Image>().color = pickedColor;
            }

            // 自動切換到筆刷工具
            usePen = true;
            useEraser = false;
            usePaintBucket = false;
            useEyedropper = false;
            SelectTool(1, 0, 0, 0);

            if (enableDebugLogs)
                Debug.Log($"吸取顏色: {pickedColor}，已自動切換到筆刷工具");
        }
    }

    private void FloodFill(Vector2 startPosition, Color newColor)
    {
        if (isFloodFilling) return;

        int startX = Mathf.RoundToInt(startPosition.x);
        int startY = Mathf.RoundToInt(startPosition.y);

        if (startX < 0 || startX >= canvasWidth || startY < 0 || startY >= canvasHeight)
            return;

        newColor.a = 1f;
        FastFloodFill(startPosition, newColor);

        // 填充完成後保存狀態
        SaveCurrentState();
    }

    private void FastFloodFill(Vector2 startPos, Color newColor)
    {
        int w = canvasWidth, h = canvasHeight;
        int x0 = Mathf.Clamp(Mathf.RoundToInt(startPos.x), 0, w - 1);
        int y0 = Mathf.Clamp(Mathf.RoundToInt(startPos.y), 0, h - 1);

        Color32[] pix = drawingTexture.GetPixels32();
        int startIdx = y0 * w + x0;
        Color32 target = pix[startIdx];
        Color32 replace = new Color32(
            (byte)(newColor.r * 255),
            (byte)(newColor.g * 255),
            (byte)(newColor.b * 255),
            255);

        if (target.Equals(replace)) return;

        Stack<int> stack = new Stack<int>();
        stack.Push(startIdx);

        while (stack.Count > 0)
        {
            int idx = stack.Pop();
            if (!pix[idx].Equals(target)) continue;

            pix[idx] = replace;
            int x = idx % w, y = idx / w;

            if (x > 0) stack.Push(idx - 1);
            if (x < w - 1) stack.Push(idx + 1);
            if (y > 0) stack.Push(idx - w);
            if (y < h - 1) stack.Push(idx + w);
        }

        drawingTexture.SetPixels32(pix);
        drawingTexture.Apply();
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
        }

        if (fcp_Tablet != null)
        {
            fcp_Tablet.onColorChange.AddListener(OnColorPickerChanged);
            if (fcp != null)
                fcp_Tablet.color = fcp.color;
        }

        UpdateUIColorDisplay();
    }

    private void OnColorPickerChanged(Color newColor)
    {
        SetBrushColor(newColor);

        if (fcp != null && !Mathf.Approximately(fcp.color.r, newColor.r))
            fcp.color = newColor;
        if (fcp_Tablet != null && !Mathf.Approximately(fcp_Tablet.color.r, newColor.r))
            fcp_Tablet.color = newColor;

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
        // 在清除畫布前先保存圖片（如果啟用自動保存）
        if (autoSaveOnFinish)
        {
            SaveImageToDevice();
        }

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

        if (Canvas2D != null)
            Canvas2D.SetActive(false);
        if (Canvas2D_Tablet != null)
            Canvas2D_Tablet.SetActive(false);

        uiManager.DrawPanel1?.SetActive(false);
        uiManager.DrawPanel2?.SetActive(false);
        uiManager.BrushPanel2D?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);
        uiManager.ClearModeButton.SetActive(true);
        uiManager.LoadButton.SetActive(true);

        // 完成時清除歷史記錄
        ClearHistory();
    }

    public void Leave()
    {
        ClearCanvas();
        Hide2DCanvas();
        drawFunction.in2DDraw = false;
        uiManager.FounctionUI.SetActive(true);
        uiManager.SwitchToPanel(uiManager.DrawPanel1);

        // 離開時清除歷史記錄
        ClearHistory();
    }

    public bool IsCurrentDeviceTablet()
    {
        return isCurrentlyTablet;
    }

    public void RefreshDeviceDetection()
    {
        DetectAndSetupDevice();

        if (enableDebugLogs)
            Debug.Log($"重新偵測設備類型: {(isCurrentlyTablet ? "平板" : "手機")}");
    }

    public void ColorButtonChange()
    {
        ColorButton_Tablet.GetComponent<Image>().color = fcp_Tablet.color;
    }
}