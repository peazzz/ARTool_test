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
    public Button _2DDraw;

    [Header("BrushSelection")]
    public GameObject LBrush;
    public GameObject PBrush;

    public GameObject LineSettingPage;
    public GameObject ParticleSettingPage;

    [Header("Two Point Line Mode")]
    public GameObject TwoPointActionButton;
    public bool StraightLine = false;
    public bool waitingForSecondPoint = false;
    private Vector3 firstPoint;
    public LineRenderer tempLineRenderer;

    [Header("Particle Settings")]
    public GameObject particlePrefab; // 粒子系統預製體
    public List<ParticleSystem> particleList = new List<ParticleSystem>();
    private ParticleSystem currentParticleSystem;
    private bool particleActive = false;
    private Vector3 lastParticlePosition;

    public Button UndoButton;
    public Button ResetButton;
    public Button FinishButton;
    public Button ClearAllButton;

    public Slider WidthSlider;
    public Slider ScaleSlider;
    public Slider DistanceSlider;
    public Slider GapSlider;

    public InputField WidthInputField;
    public InputField ScaleInputField;
    public InputField DistanceInputField;
    public InputField GapInputField;

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
    public bool in2DDraw;
    public bool LineBrush;
    public bool ParticleBrush;

    // 控制參數
    [Header("Line Settings")]
    public float lineWidth = 0.02f;
    public float ParticleScale = 0.02f;
    public float cameraDistance = 0.3f;
    public float gapThreshold = 0.01f; // 兩點間最小距離

    void Start()
    {
        SetupAllButtonEvents();
        InitializeUI();
        SetupUIListeners();
        LineBrushSelection();

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
            LineBrush = true;
            ParticleBrush = false;
            in3DDraw = true;
            in2DDraw = false;
            DrawPanel.SetActive(true);
            uiManager.SwitchToPanel(uiManager.BrushPanel);
        });

        _2DDraw?.onClick.AddListener(() => {
            LineBrush = true;
            ParticleBrush = false;
            in2DDraw = true;
            in3DDraw = false;     // 確保其他模式為false
        });

        LBrush.GetComponent<Button>().onClick.AddListener(() => LineBrushSelection());
        PBrush.GetComponent<Button>().onClick.AddListener(() => ParticleBrushSelection());

        TwoPointActionButton.GetComponent<Button>().onClick.AddListener(() => {
            ToggleTwoPointAction();
        });

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

        if (ScaleSlider != null)
        {
            ScaleSlider.value = ParticleScale;
            ScaleSlider.minValue = 0.001f;
            ScaleSlider.maxValue = 0.1f;
        }

        if (DistanceSlider != null)
        {
            DistanceSlider.value = cameraDistance;
            DistanceSlider.minValue = 0.1f;
            DistanceSlider.maxValue = 2.0f;
        }

        if (GapSlider != null)
        {
            GapSlider.value = gapThreshold;
            GapSlider.minValue = 0.001f;  // 最小間隔
            GapSlider.maxValue = 0.1f;
        }

        // 同步InputField
        UpdateInputFields();
    }

    void SetupUIListeners()
    {
        // Slider事件
        if (WidthSlider != null)
            WidthSlider.onValueChanged.AddListener(OnWidthSliderChanged);

        if (ScaleSlider != null)
            ScaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);

        if (DistanceSlider != null)
            DistanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);

        if (GapSlider != null)
            GapSlider.onValueChanged.AddListener(OnGapSliderChanged);

        // InputField事件
        if (WidthInputField != null)
            WidthInputField.onEndEdit.AddListener(OnWidthInputChanged);

        if (ScaleInputField != null)
            ScaleInputField.onEndEdit.AddListener(OnScaleInputChanged);

        if (DistanceInputField != null)
            DistanceInputField.onEndEdit.AddListener(OnDistanceInputChanged);

        if (GapInputField != null)
            GapInputField.onEndEdit.AddListener(OnGapInputChanged);
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

        if (ParticleBrush && particleActive)
        {
            UpdateAnchor();
            DrawParticleContinue();
        }

        if (in3DDraw && StraightLine && waitingForSecondPoint && tempLineRenderer != null)
        {
            UpdateAnchor();
            tempLineRenderer.SetPosition(1, anchor);
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

    private void LineBrushSelection()
    {
        LineBrush = true;
        ParticleBrush = false;
        LBrush.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        PBrush.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        LineSettingPage.SetActive(true);
        ParticleSettingPage.SetActive(false);
    }

    private void ParticleBrushSelection()
    {
        LineBrush = false;
        ParticleBrush = true;
        LBrush.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        PBrush.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        LineSettingPage.SetActive(false);
        ParticleSettingPage.SetActive(true);
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
        // 檢查是否滿足Gap條件（兩點間距離）
        float distance = Vector3.Distance(anchor, lastAnchor);

        if (distance >= gapThreshold)
        {
            lineRenderer.positionCount = lineRenderer.positionCount + 1;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, anchor);
            lastAnchor = anchor; // 更新上一個錨點
        }
    }

    void MakeParticleSystem()
    {
        if (particlePrefab == null)
        {
            Debug.LogError("Particle Prefab is not assigned!");
            return;
        }

        GameObject tempParticle = Instantiate(particlePrefab);
        tempParticle.transform.SetParent(linePool);
        tempParticle.transform.position = anchor;
        tempParticle.transform.localScale = Vector3.one;

        currentParticleSystem = tempParticle.GetComponent<ParticleSystem>();

        if (currentParticleSystem != null)
        {
            // 設定基本粒子參數
            var main = currentParticleSystem.main;
            main.startColor = LineMaterial.color; // 使用當前選擇的顏色

            particleList.Add(currentParticleSystem);
        }
        else
        {
            Debug.LogError("Particle Prefab doesn't have ParticleSystem component!");
        }
    }

    public void DrawParticleContinue()
    {
        float distance = Vector3.Distance(anchor, lastParticlePosition);

        // 使用與線條相同的品質閾值來控制粒子生成間隔
        if (distance >= gapThreshold)
        {
            CreateParticleAtPosition(anchor);
            lastParticlePosition = anchor;
        }
    }

    void CreateParticleAtPosition(Vector3 position)
    {
        if (particlePrefab == null)
        {
            Debug.LogError("Particle Prefab is not assigned!");
            return;
        }

        GameObject tempParticle = Instantiate(particlePrefab);
        tempParticle.transform.SetParent(linePool);
        tempParticle.transform.position = position;
        tempParticle.transform.localScale = Vector3.one;

        ParticleSystem newParticleSystem = tempParticle.GetComponent<ParticleSystem>();

        if (newParticleSystem != null)
        {
            // 設定粒子系統參數
            var main = newParticleSystem.main;
            main.startColor = LineMaterial.color;

            // 可以設定粒子的其他屬性
            main.startSize = ParticleScale * 5f;

            particleList.Add(newParticleSystem);

            // 設定粒子系統在播放完畢後自動停止
            var stopAction = newParticleSystem.main;
            stopAction.stopAction = ParticleSystemStopAction.Destroy;
        }
        else
        {
            Debug.LogError("Particle Prefab doesn't have ParticleSystem component!");
            Destroy(tempParticle);
        }
    }

    void HandleTwoPointDrawing()
    {
        if (!waitingForSecondPoint)
        {
            // 設置第一個點
            anchorUpdate = true;
            UpdateAnchor();
            firstPoint = anchor;
            waitingForSecondPoint = true;

            // 創建暫時的線條來預覽
            CreateTempLineRenderer();
        }
        else
        {
            // 設置第二個點並完成線條
            anchorUpdate = true;
            UpdateAnchor();
            Vector3 secondPoint = anchor;

            // 完成線條
            CompleteTwoPointLine(firstPoint, secondPoint);

            // 重置狀態
            waitingForSecondPoint = false;
            if (tempLineRenderer != null)
            {
                Destroy(tempLineRenderer.gameObject);
                tempLineRenderer = null;
            }
        }
    }

    void CreateTempLineRenderer()
    {
        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = new Vector3(1, 1, 1);

        tempLineRenderer = tempLine.GetComponent<LineRenderer>();
        tempLineRenderer.positionCount = 2;
        tempLineRenderer.SetPosition(0, firstPoint);
        tempLineRenderer.SetPosition(1, firstPoint); // 初始時兩點相同

        // 設置材質和寬度
        Material tempMaterial = new Material(LineMaterial);
        tempMaterial.color = new Color(LineMaterial.color.r, LineMaterial.color.g, LineMaterial.color.b, 0.5f); // 半透明預覽
        tempLineRenderer.material = tempMaterial;
        tempLineRenderer.startWidth = lineWidth;
        tempLineRenderer.endWidth = lineWidth;
    }

    void CompleteTwoPointLine(Vector3 point1, Vector3 point2)
    {
        GameObject finalLine = Instantiate(linePrefab);
        finalLine.transform.SetParent(linePool);
        finalLine.transform.position = Vector3.zero;
        finalLine.transform.localScale = new Vector3(1, 1, 1);

        LineRenderer finalLineRenderer = finalLine.GetComponent<LineRenderer>();
        finalLineRenderer.positionCount = 2;
        finalLineRenderer.SetPosition(0, point1);
        finalLineRenderer.SetPosition(1, point2);

        // 設置材質和寬度
        Material lineMaterialInstance = new Material(LineMaterial);
        finalLineRenderer.material = lineMaterialInstance;
        finalLineRenderer.startWidth = lineWidth;
        finalLineRenderer.endWidth = lineWidth;

        // 添加到線條列表
        lineList.Add(finalLineRenderer);
    }

    public void ToggleTwoPointAction()
    {
        if (!in3DDraw) return;

        StraightLine = !StraightLine;

        if (!StraightLine)
        {
            TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
            waitingForSecondPoint = false;
            if (tempLineRenderer != null)
            {
                Destroy(tempLineRenderer.gameObject);
                tempLineRenderer = null;
            }
        }
        else
        {
            TwoPointActionButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        }
    }

    // UI事件處理方法
    public void OnWidthSliderChanged(float value)
    {
        lineWidth = value;
        UpdateInputFields();
        ApplyWidthToCurrentLine();
    }

    public void OnScaleSliderChanged(float value)
    {
        ParticleScale = value;
        UpdateInputFields();
    }

    public void OnDistanceSliderChanged(float value)
    {
        cameraDistance = value;
        UpdateInputFields();
    }

    public void OnGapSliderChanged(float value)
    {
        gapThreshold = value;
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

    public void OnScaleInputChanged(string value)
    {
        string cleanValue = value.Replace("%", "").Trim();

        if (float.TryParse(cleanValue, out float displayValue))
        {
            displayValue = Mathf.Clamp(displayValue, 10f, 500f);
            float actualValue = (displayValue / 100f) * 0.01f; // 100 = 0.01f
            ParticleScale = actualValue;
            if (ScaleSlider != null)
                ScaleSlider.value = ParticleScale;
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

    public void OnGapInputChanged(string value)
    {
        string cleanValue = value.Trim();

        if (float.TryParse(cleanValue, out float inputValue))
        {
            // 直接限制在實際範圍內
            gapThreshold = Mathf.Clamp(inputValue, 0.001f, 0.05f);
            if (GapSlider != null)
                GapSlider.value = gapThreshold; // 直接設定實際值給Slider
        }
        UpdateInputFields();
    }

    void UpdateInputFields()
    {
        if (WidthInputField != null)
        {
            float displayWidth = (lineWidth / 0.01f) * 100f; // 0.01f = 100
            WidthInputField.text = displayWidth.ToString("F0");
        }

        if (ScaleInputField != null)
        {
            float displayScale = (ParticleScale / 0.01f) * 100f; // 0.01f = 100
            ScaleInputField.text = displayScale.ToString("F0");
        }

        if (DistanceInputField != null)
            DistanceInputField.text = cameraDistance.ToString("F2");

        if (GapInputField != null)
        {
            GapInputField.text = gapThreshold.ToString("F3");
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
            if (in3DDraw && StraightLine)
            {
                HandleTwoPointDrawing();
            }
            else
            {
                use = true;
                if (!startLine)
                {
                    MakeLineRenderer();
                }
            }
        }
        else if (ParticleBrush)
        {
            StartDrawParticle();
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
        else if (ParticleBrush)
        {
            StopDrawParticle();
        }
    }

    public void StartDrawParticle()
    {
        if (!particleActive)
        {
            anchorUpdate = true;
            UpdateAnchor();
            MakeParticleSystem();
            CreateParticleAtPosition(anchor);
            particleActive = true;
        }
    }

    public void StopDrawParticle()
    {
        if (particleActive)
        {
            particleActive = false;
            anchorUpdate = false;
        }
    }

    //To Undo Last Drawn Line
    public void Undo()
    {
        if (ParticleBrush && particleList.Count > 0)
        {
            // 撤銷最後一個粒子系統
            ParticleSystem undoParticle = particleList[particleList.Count - 1];
            Destroy(undoParticle.gameObject);
            particleList.RemoveAt(particleList.Count - 1);
        }
        else if (LineBrush && lineList.Count > 0)
        {
            // 原有的線條撤銷邏輯
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

        // 清除所有粒子
        foreach (ParticleSystem particle in particleList)
        {
            Destroy(particle.gameObject);
        }
        particleList.Clear();
    }

    void OnResetButtonClicked()
    {
        lineWidth = 0.02f;
        if (WidthSlider != null)
            WidthSlider.value = lineWidth;

        ParticleScale = 0.02f;
        if (ScaleSlider != null)
            ScaleSlider.value = ParticleScale;

        cameraDistance = 0.3f;
        if (DistanceSlider != null)
            DistanceSlider.value = cameraDistance;

        gapThreshold = 0.01f;
        if (GapSlider != null)
        {
            GapSlider.value = gapThreshold;
        }

        Color defaultColor = new Color(1f, 1f, 1f, 1f);
        LineMaterial.color = defaultColor;
        if (fcp != null)
            fcp.color = defaultColor;

        UpdateInputFields();
        ApplyWidthToCurrentLine();

        StraightLine = false;
        waitingForSecondPoint = false;
        if (tempLineRenderer != null)
        {
            Destroy(tempLineRenderer.gameObject);
            tempLineRenderer = null;
        }
    }

    void OnFinishButtonClicked()
    {
        uiManager.inDraw = false;
        uiManager.isInColorPage = false;

        in3DDraw = false;
        StraightLine = false;
        in2DDraw = false;
        LineBrush = false;
        ParticleBrush = false;

        DrawPanel.SetActive(false);
        uiManager.DrawPanel1?.SetActive(false);
        uiManager.DrawPanel2?.SetActive(false);
        uiManager.BrushPanel?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);

        TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        waitingForSecondPoint = false;
        if (tempLineRenderer != null)
        {
            Destroy(tempLineRenderer.gameObject);
            tempLineRenderer = null;
        }
    }
}