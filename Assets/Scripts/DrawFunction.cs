using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawFunction : MonoBehaviour
{
    public Camera arCamera;
    public UIManager uiManager;
    public FlexibleColorPicker fcp;
    public GameObject DrawPanel;

    [Header("ModeSelection")]
    public Button _3DDraw;
    public Button _3DDraw_SL;
    public Button _2DDraw;

    [Header("BrushSelection")]
    public Button LBrush;
    public Button PBrush;

    public Button UndoButton;
    public Button ResetButton;
    public Button FinishButton;
    public Button ClearAllButton;

    public Slider WidthSlider;
    public Slider DistanceSlider;
    public Slider QualitySlider;

    public InputField WidthInputField;
    public InputField DistanceInputField;
    public InputField QualityInputField;

    public Material LineMaterial;

    Vector3 anchor = new Vector3(0, 0, 0.3f);
    Vector3 lastAnchor; // 記錄上一個錨點位置

    bool anchorUpdate = false; //should anchor update or not

    public GameObject linePrefab; //prefab which genrate the line for user

    LineRenderer lineRenderer; //LineRenderer which connects and generate line

    public List<LineRenderer> lineList = new List<LineRenderer>(); //List of lines drawn

    public Transform linePool; //parent object

    public bool use; //code is in use or not

    public bool startLine; //already started line or not

    public bool in3DDraw;
    public bool in3DDraw_SL;
    public bool in2DDraw;
    public bool LineBrush;
    public bool ParticleBrush;

    // 控制參數
    [Header("Line Settings")]
    public float lineWidth = 0.02f;
    public float cameraDistance = 0.3f;
    public float qualityThreshold = 0.01f; // 兩點間最小距離

    void Start()
    {
        SetupAllButtonEvents();
        InitializeUI();
        SetupUIListeners();
        
        if (fcp != null && LineMaterial != null)
        {
            fcp.color = new Color(1, 1, 1, 1);
            LineMaterial.color = fcp.color;
            fcp.onColorChange.AddListener(OnChangeColor);
        }
    }

    void SetupAllButtonEvents()
    {
        _3DDraw?.onClick.AddListener(() => {
            in3DDraw = true;
            in3DDraw_SL = false;
            in2DDraw = false;
            DrawPanel.SetActive(true);
            uiManager.SwitchToPanel(uiManager.DrawPanel2);
        });

        _3DDraw_SL?.onClick.AddListener(() => {
            in3DDraw_SL = true;
            in3DDraw = false;     // 確保其他模式為false
            in2DDraw = false;
            DrawPanel.SetActive(true);
            uiManager.SwitchToPanel(uiManager.DrawPanel2);
        });

        _2DDraw?.onClick.AddListener(() => {
            in2DDraw = true;
            in3DDraw = false;     // 確保其他模式為false
            in3DDraw_SL = false;
        });

        LBrush?.onClick.AddListener(() => {
            LineBrush = true;
            uiManager.SwitchToPanel(uiManager.LineRenenderPanel);
        });
        //PBrush?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cylinder));

        UndoButton?.onClick.AddListener(Undo);
        ResetButton?.onClick.AddListener(OnResetButtonClicked);
        FinishButton?.onClick.AddListener(OnFinishButtonClicked);
        ClearAllButton?.onClick.AddListener(ClearScreen);
    }

    void InitializeUI()
    {
        if (WidthSlider != null)
        {
            WidthSlider.value = lineWidth;
            WidthSlider.minValue = 0.001f;
            WidthSlider.maxValue = 0.1f;
        }

        if (DistanceSlider != null)
        {
            DistanceSlider.value = cameraDistance;
            DistanceSlider.minValue = 0.1f;
            DistanceSlider.maxValue = 2.0f;
        }

        if (QualitySlider != null)
        {
            // 將實際值(0.05f~0.001f)轉換為顯示值(0.01~1)用於Slider
            float normalizedActual = (qualityThreshold - 0.001f) / (0.05f - 0.001f); // 0~1
            float displayQuality = 0.01f + (1f - normalizedActual) * (1f - 0.01f); // 反向計算
            QualitySlider.value = displayQuality;
            QualitySlider.minValue = 0.01f;
            QualitySlider.maxValue = 1.0f;
        }

        // 同步InputField
        UpdateInputFields();
    }

    void SetupUIListeners()
    {
        // Slider事件
        if (WidthSlider != null)
            WidthSlider.onValueChanged.AddListener(OnWidthSliderChanged);

        if (DistanceSlider != null)
            DistanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);

        if (QualitySlider != null)
            QualitySlider.onValueChanged.AddListener(OnQualitySliderChanged);

        // InputField事件
        if (WidthInputField != null)
            WidthInputField.onEndEdit.AddListener(OnWidthInputChanged);

        if (DistanceInputField != null)
            DistanceInputField.onEndEdit.AddListener(OnDistanceInputChanged);

        if (QualityInputField != null)
            QualityInputField.onEndEdit.AddListener(OnQualityInputChanged);
    }

    void Update()
    {
        if (use)
        {
            if (startLine)
            {
                UpdateAnchor();
                DrawLinewContinue();
            }
        }
    }

    void UpdateAnchor()
    {
        if (anchorUpdate)
        {
            Vector3 temp = Input.mousePosition;
            temp.z = cameraDistance; // 使用可調整的距離
            anchor = arCamera.ScreenToWorldPoint(temp);
        }
    }

    public void MakeLineRenderer()
    {
        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = new Vector3(1, 1, 1);

        anchorUpdate = true;
        UpdateAnchor();
        lastAnchor = anchor; // 記錄初始位置

        lineRenderer = tempLine.GetComponent<LineRenderer>();
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, anchor);

        Material lineMaterialInstance = new Material(LineMaterial);
        lineRenderer.material = lineMaterialInstance;

        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        startLine = true;
        lineList.Add(lineRenderer);
    }

    public void DrawLinewContinue()
    {
        // 檢查是否滿足quality條件（兩點間距離）
        float distance = Vector3.Distance(anchor, lastAnchor);

        if (distance >= qualityThreshold)
        {
            lineRenderer.positionCount = lineRenderer.positionCount + 1;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, anchor);
            lastAnchor = anchor; // 更新上一個錨點
        }
    }

    // UI事件處理方法
    public void OnWidthSliderChanged(float value)
    {
        lineWidth = value;
        UpdateInputFields();
        ApplyWidthToCurrentLine();
    }

    public void OnDistanceSliderChanged(float value)
    {
        cameraDistance = value;
        UpdateInputFields();
    }

    public void OnQualitySliderChanged(float value)
    {
        // Slider值是顯示值(0.01~1)，需要轉換為實際值(0.05f~0.001f)
        float normalizedDisplay = (value - 0.01f) / (1f - 0.01f); // 0~1
        qualityThreshold = Mathf.Lerp(0.05f, 0.001f, normalizedDisplay); // 反向插值
        UpdateInputFields();
    }

    public void OnWidthInputChanged(string value)
    {
        // 移除%符號和空格
        string cleanValue = value.Replace("%", "").Trim();

        if (float.TryParse(cleanValue, out float displayValue))
        {
            // 將顯示值(10~1000)轉換為實際值(0.001f~0.1f)
            displayValue = Mathf.Clamp(displayValue, 10f, 500f);
            float actualValue = (displayValue / 100f) * 0.01f; // 100 = 0.01f
            lineWidth = actualValue;
            if (WidthSlider != null)
                WidthSlider.value = lineWidth;
            ApplyWidthToCurrentLine();
        }
        UpdateInputFields();
    }

    public void OnDistanceInputChanged(string value)
    {
        if (float.TryParse(value, out float newDistance))
        {
            cameraDistance = Mathf.Clamp(newDistance, 0.1f, 2.0f);
            if (DistanceSlider != null)
                DistanceSlider.value = cameraDistance;
        }
        UpdateInputFields();
    }

    public void OnQualityInputChanged(string value)
    {
        // 清理輸入值
        string cleanValue = value.Trim();

        if (float.TryParse(cleanValue, out float displayValue))
        {
            // 將顯示值(0.01~1)反向轉換為實際值(0.05f~0.001f)
            displayValue = Mathf.Clamp(displayValue, 0.01f, 1f);
            float normalizedDisplay = (displayValue - 0.01f) / (1f - 0.01f); // 0~1
            float actualValue = Mathf.Lerp(0.05f, 0.001f, normalizedDisplay); // 反向插值
            qualityThreshold = actualValue;
            if (QualitySlider != null)
                QualitySlider.value = displayValue; // 直接設定顯示值給Slider
        }
        UpdateInputFields();
    }

    void UpdateInputFields()
    {
        if (WidthInputField != null)
        {
            // 將實際值(0.001f~0.1f)轉換為顯示值(10~1000)
            float displayWidth = (lineWidth / 0.01f) * 100f; // 0.01f = 100
            WidthInputField.text = displayWidth.ToString("F0");
        }

        if (DistanceInputField != null)
            DistanceInputField.text = cameraDistance.ToString("F2");

        if (QualityInputField != null)
        {
            // 將實際值(0.05f~0.001f)反向轉換為顯示值(0.01~1)
            float normalizedActual = (qualityThreshold - 0.001f) / (0.05f - 0.001f); // 0~1
            float displayQuality = 0.01f + (1f - normalizedActual) * (1f - 0.01f); // 反向計算
            QualityInputField.text = displayQuality.ToString("F2");
        }
    }

    void ApplyWidthToCurrentLine()
    {
        if (lineRenderer != null)
        {
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
        }
    }

    // 應用設定到所有現有線條
    public void ApplySettingsToAllLines()
    {
        foreach (LineRenderer line in lineList)
        {
            if (line != null)
            {
                line.startWidth = lineWidth;
                line.endWidth = lineWidth;
            }
        }
    }

    private void OnChangeColor(Color co)
    {
        LineMaterial.color = co;
    }

    //to start drawing line
    public void StartDrawLine()
    {
        if (LineBrush)
        {
            use = true;

            if (!startLine)
            {
                MakeLineRenderer();
            }
        }
    }

    //to End the line which user started drawing
    public void StopDrawLine()
    {
        if (LineBrush && lineRenderer != null)
        {
            if (lineRenderer.positionCount == 1)
            {
                UpdateAnchor();
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(1, anchor);
            }
            else
            {
                UpdateAnchor();
                float distance = Vector3.Distance(anchor, lastAnchor);

                if (distance > 0.001f)
                {
                    lineRenderer.positionCount = lineRenderer.positionCount + 1;
                    lineRenderer.SetPosition(lineRenderer.positionCount - 1, anchor);
                }
            }

            use = false;
            startLine = false;
            lineRenderer = null;
            anchorUpdate = false;
        }
    }

    //To Undo Last Drawn Line
    public void Undo()
    {
        if (lineList.Count > 0)
        {
            LineRenderer undo = lineList[lineList.Count - 1];
            Destroy(undo.gameObject);
            lineList.RemoveAt(lineList.Count - 1);
        }
    }

    //To clear all the lines
    public void ClearScreen()
    {
        foreach (LineRenderer item in lineList)
        {
            Destroy(item.gameObject);
        }
        lineList.Clear();
    }

    void OnResetButtonClicked()
    {
        lineWidth = 0.02f;
        if (WidthSlider != null)
            WidthSlider.value = lineWidth;

        cameraDistance = 0.3f;
        if (DistanceSlider != null)
            DistanceSlider.value = cameraDistance;

        qualityThreshold = 0.01f;
        if (QualitySlider != null)
        {
            float normalizedActual = (qualityThreshold - 0.001f) / (0.05f - 0.001f);
            float displayQuality = 0.01f + (1f - normalizedActual) * (1f - 0.01f);
            QualitySlider.value = displayQuality;
        }

        Color defaultColor = new Color(1f, 1f, 1f, 1f);
        LineMaterial.color = defaultColor;
        if (fcp != null)
            fcp.color = defaultColor;

        UpdateInputFields();
        ApplyWidthToCurrentLine();
    }

    void OnFinishButtonClicked()
    {
        uiManager.inDraw = false;
        uiManager.isInColorPage = false;

        in3DDraw = false;
        in3DDraw_SL = false;
        in2DDraw = false;
        LineBrush = false;
        ParticleBrush = false;

        DrawPanel.SetActive(false);
        uiManager.DrawPanel1?.SetActive(false);
        uiManager.DrawPanel2?.SetActive(false);
        uiManager.LineRenenderPanel?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);
    }
}