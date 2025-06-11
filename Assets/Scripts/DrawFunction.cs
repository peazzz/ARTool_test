using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DrawFunction : MonoBehaviour
{
    [Header("References")]
    public Camera arCamera;
    public UIManager uiManager;
    public FlexibleColorPicker fcp;
    public GameObject DrawPanel;

    [Header("Mode Selection")]
    public Button _3DDraw;
    public Button _2DDraw;

    public GameObject SpaceModeButton;
    public GameObject TextureModeButton;

    [Header("Brush Selection")]
    public GameObject LBrush;
    public GameObject PBrush;
    public GameObject LineSettingPage;
    public GameObject ParticleSettingPage;

    [Header("3D Object Painting")]
    private bool isPaintingOn3D = false;
    private PaintManager currentPaintManager;
    private GameObject lastHitObject;
    private Vector3 lastPaintPosition;

    [Header("Two Point Line Mode")]
    public GameObject TwoPointActionButton;
    public bool StraightLine = false;
    public bool waitingForSecondPoint = false;
    private Vector3 firstPoint;
    public LineRenderer tempLineRenderer;

    [Header("Particle Settings")]
    public GameObject particlePrefab;
    public List<ParticleSystem> particleList = new List<ParticleSystem>();
    private ParticleSystem currentParticleSystem;
    private bool particleActive = false;
    private Vector3 lastParticlePosition;

    [Header("UI Objects")]
    public GameObject Warning;
    public GameObject Distance;

    [Header("UI Buttons")]
    public Button UndoButton;
    public Button ResetButton;
    public Button FinishButton;
    public Button ClearAllButton;

    [Header("UI Sliders")]
    public Slider WidthSlider;
    public Slider ScaleSlider;
    public Slider DistanceSlider;
    public Slider GapSlider;

    [Header("UI Input Fields")]
    public InputField WidthInputField;
    public InputField ScaleInputField;
    public InputField DistanceInputField;
    public InputField GapInputField;

    [Header("Materials")]
    public Material LineMaterial;

    [Header("Drawing Variables")]
    Vector3 anchor = new Vector3(0, 0, 0.3f);
    Vector3 lastAnchor;
    bool anchorUpdate = false;

    public GameObject linePrefab;
    LineRenderer lineRenderer;
    public List<LineRenderer> lineList = new List<LineRenderer>();
    public Transform linePool;

    public bool use;
    public bool startLine;
    public bool in3DDraw;
    public bool in2DDraw;
    public bool LineBrush;
    public bool ParticleBrush;
    public bool SpaceMode;
    public bool TextureMode;
    private bool firstWarning;

    [Header("Line Settings")]
    public float lineWidth = 0.02f;
    public float ParticleScale = 0.02f;
    public float cameraDistance = 0.3f;
    public float gapThreshold = 0.01f;

    [Header("3D Paint Settings")]
    public float paintGapThreshold = 0.02f;

    [Header("Texture Mode Click Control")]
    private bool textureClickEnabled = false;
    private bool hasProcessedClick = false;

    void Start()
    {
        SetupAllButtonEvents();
        InitializeUI();
        SetupUIListeners();
        LineBrushSelection();

        SpaceModeSelection();

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
            LineBrushSelection();
            SpaceModeSelection();
        });

        _2DDraw?.onClick.AddListener(() => {
            LineBrush = true;
            ParticleBrush = false;
            in2DDraw = true;
            in3DDraw = false;
            //DrawPanel.SetActive(true);
            uiManager.SwitchToPanel(uiManager.BrushPanel);
        });

        LBrush.GetComponent<Button>().onClick.AddListener(() => LineBrushSelection());
        PBrush.GetComponent<Button>().onClick.AddListener(() => ParticleBrushSelection());

        SpaceModeButton.GetComponent<Button>().onClick.AddListener(() => SpaceModeSelection());
        TextureModeButton.GetComponent<Button>().onClick.AddListener(() => TextureModeSelection());

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
            GapSlider.minValue = 0.001f;
            GapSlider.maxValue = 0.1f;
        }

        UpdateInputFields();
    }

    void SetupUIListeners()
    {
        if (WidthSlider != null)
            WidthSlider.onValueChanged.AddListener(OnWidthSliderChanged);

        if (ScaleSlider != null)
            ScaleSlider.onValueChanged.AddListener(OnScaleSliderChanged);

        if (DistanceSlider != null)
            DistanceSlider.onValueChanged.AddListener(OnDistanceSliderChanged);

        if (GapSlider != null)
            GapSlider.onValueChanged.AddListener(OnGapSliderChanged);

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
            if (TextureMode && in3DDraw && LineBrush)
            {
                Handle3DPainting();
            }
            else if (SpaceMode && startLine)
            {
                UpdateAnchor();
                DrawLinewContinue();
            }
        }

        if (ParticleBrush && particleActive)
        {
            if (TextureMode && in3DDraw)
            {
                Handle3DParticlePainting();
            }
            else if (SpaceMode)
            {
                UpdateAnchor();
                DrawParticleContinue();
            }
        }

        // 預覽線條更新 - 只在Space模式或Texture模式等待第二個點時更新
        if (StraightLine && waitingForSecondPoint && tempLineRenderer != null)
        {
            if (SpaceMode)
            {
                UpdateAnchor();
                tempLineRenderer.SetPosition(1, anchor);
            }
            else if (TextureMode)
            {
                Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    tempLineRenderer.SetPosition(1, hit.point);
                }
            }
        }
    }

    void Handle3DPainting()
    {
        Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            // 如果啟用直線模式，只在點擊時處理
            if (StraightLine)
            {
                if (textureClickEnabled && !hasProcessedClick)
                {
                    HandleTexture3DTwoPointDrawing(hit);
                    hasProcessedClick = true;
                }
                return;
            }

            // 連續繪製模式的原有邏輯
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
            {
                CubeCarvingSystem carvingSystem = hit.collider.GetComponent<CubeCarvingSystem>();
                if (carvingSystem != null)
                {
                    float distance = Vector3.Distance(hit.point, lastPaintPosition);
                    if (distance >= gapThreshold || lastHitObject != hit.collider.gameObject)
                    {
                        PaintManager paintManager = hit.collider.GetComponent<PaintManager>();
                        if (paintManager == null)
                        {
                            paintManager = hit.collider.gameObject.AddComponent<PaintManager>();
                        }

                        paintManager.paintColor = LineMaterial.color;
                        paintManager.brushSize = lineWidth;

                        Vector3 adjustedNormal = hit.normal;
                        if (Vector3.Dot(adjustedNormal, ray.direction) > 0)
                        {
                            adjustedNormal = -adjustedNormal;
                        }

                        paintManager.PaintAt(hit.point, hit.normal);

                        currentPaintManager = paintManager;
                        lastHitObject = hit.collider.gameObject;
                        lastPaintPosition = hit.point;
                    }
                }
            }
            else
            {
                float distance = Vector3.Distance(hit.point, lastPaintPosition);
                if (distance >= gapThreshold || lastHitObject != hit.collider.gameObject)
                {
                    if (lineRenderer == null || lastHitObject != hit.collider.gameObject)
                    {
                        CreateSurfaceLineRenderer(hit.point);
                    }
                    else
                    {
                        AddPointToSurfaceLine(hit.point);
                    }

                    lastHitObject = hit.collider.gameObject;
                    lastPaintPosition = hit.point;
                }
            }
        }
    }

    void CreateSurfaceLineRenderer(Vector3 startPoint)
    {
        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = Vector3.one;

        lineRenderer = tempLine.GetComponent<LineRenderer>();
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, startPoint);

        Material lineMaterialInstance = new Material(LineMaterial);
        lineRenderer.material = lineMaterialInstance;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;

        lineList.Add(lineRenderer);
    }

    // 新增方法：向表面線條添加點
    void AddPointToSurfaceLine(Vector3 newPoint)
    {
        if (lineRenderer != null)
        {
            lineRenderer.positionCount++;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, newPoint);
        }
    }

    void Handle3DParticlePainting()
    {
        Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            // 如果啟用直線模式，只在點擊時處理
            if (StraightLine)
            {
                if (textureClickEnabled && !hasProcessedClick)
                {
                    HandleTexture3DTwoPointParticle(hit);
                    hasProcessedClick = true;
                }
                return;
            }

            // 連續繪製模式的原有邏輯
            float distance = Vector3.Distance(hit.point, lastParticlePosition);
            if (distance >= gapThreshold)
            {
                CreateParticleAtPosition(hit.point);
                lastParticlePosition = hit.point;
            }
        }
    }

    void HandleTexture3DTwoPointDrawing(RaycastHit hit)
    {
        if (!waitingForSecondPoint)
        {
            // 第一個點
            firstPoint = hit.point;
            waitingForSecondPoint = true;

            // 創建預覽線條
            CreateTempTextureLineRenderer(hit.point);

            Debug.Log("設置第一個點: " + firstPoint);
        }
        else
        {
            // 第二個點 - 完成繪製
            Vector3 secondPoint = hit.point;

            Debug.Log("設置第二個點: " + secondPoint + "，完成直線繪製");

            // 判斷是否為SculptObject來決定繪製方式
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
            {
                DrawStraightLineOnSculptObject(firstPoint, secondPoint, hit.collider.gameObject);
            }
            else
            {
                CompleteTextureSurfaceLine(firstPoint, secondPoint);
            }

            // 重置狀態，準備下一條直線
            ResetTwoPointState();
        }
    }

    // 新增：處理Texture模式下的兩點粒子繪製
    void HandleTexture3DTwoPointParticle(RaycastHit hit)
    {
        if (!waitingForSecondPoint)
        {
            // 第一個點
            firstPoint = hit.point;
            waitingForSecondPoint = true;

            // 創建第一個粒子作為起點標記
            CreateParticleAtPosition(hit.point);

            Debug.Log("設置第一個粒子點: " + firstPoint);
        }
        else
        {
            // 第二個點 - 繪製直線粒子
            Vector3 secondPoint = hit.point;

            Debug.Log("設置第二個粒子點: " + secondPoint + "，完成直線粒子繪製");

            DrawStraightParticleLine(firstPoint, secondPoint);

            // 重置狀態，準備下一條直線
            ResetTwoPointState();
        }
    }

    void ResetTwoPointState()
    {
        waitingForSecondPoint = false;
        if (tempLineRenderer != null)
        {
            Destroy(tempLineRenderer.gameObject);
            tempLineRenderer = null;
        }

        Debug.Log("重置兩點狀態，準備下一條直線");
    }


    // 新增：創建Texture模式的預覽線條
    void CreateTempTextureLineRenderer(Vector3 startPoint)
    {
        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = Vector3.one;

        tempLineRenderer = tempLine.GetComponent<LineRenderer>();
        tempLineRenderer.positionCount = 2;
        tempLineRenderer.SetPosition(0, startPoint);
        tempLineRenderer.SetPosition(1, startPoint);

        Material tempMaterial = new Material(LineMaterial);
        tempMaterial.color = new Color(LineMaterial.color.r, LineMaterial.color.g, LineMaterial.color.b, 0.5f);
        tempLineRenderer.material = tempMaterial;
        tempLineRenderer.startWidth = lineWidth;
        tempLineRenderer.endWidth = lineWidth;
    }

    // 新增：在SculptObject上繪製直線
    void DrawStraightLineOnSculptObject(Vector3 startPoint, Vector3 endPoint, GameObject sculptObject)
    {
        PaintManager paintManager = sculptObject.GetComponent<PaintManager>();
        if (paintManager == null)
        {
            paintManager = sculptObject.AddComponent<PaintManager>();
        }

        paintManager.paintColor = LineMaterial.color;
        paintManager.brushSize = lineWidth;

        // 在起點和終點之間插值繪製
        float distance = Vector3.Distance(startPoint, endPoint);
        int steps = Mathf.RoundToInt(distance / (gapThreshold * 0.5f));
        steps = Mathf.Max(steps, 10); // 增加最小步數確保連續性

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 interpolatedPoint = Vector3.Lerp(startPoint, endPoint, t);

            Vector3 hitPoint;
            Vector3 hitNormal;

            // 嘗試找到表面點
            if (FindSurfacePointForSculpt(interpolatedPoint, sculptObject, out hitPoint, out hitNormal))
            {
                paintManager.PaintAt(hitPoint, hitNormal);
            }
        }
    }

    bool FindSurfacePointForSculpt(Vector3 targetPoint, GameObject sculptObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero;
        hitNormal = Vector3.zero;

        // 獲取物件的碰撞器
        Collider objectCollider = sculptObject.GetComponent<Collider>();
        if (objectCollider == null)
        {
            Debug.LogWarning("SculptObject沒有碰撞器！");
            return false;
        }

        // 使用ClosestPoint來找到最近的表面點
        Vector3 closestPoint = objectCollider.ClosestPoint(targetPoint);

        // 計算法線（從物件中心指向表面點）
        Vector3 center = objectCollider.bounds.center;
        Vector3 normal = (closestPoint - center).normalized;

        // 進行微調射線檢測來獲得更精確的法線
        Ray ray = new Ray(closestPoint + normal * 0.01f, -normal);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 0.02f))
        {
            if (hit.collider.gameObject == sculptObject)
            {
                hitPoint = hit.point;
                hitNormal = hit.normal;
                return true;
            }
        }

        // 如果射線檢測失敗，使用ClosestPoint的結果
        hitPoint = closestPoint;
        hitNormal = normal;
        return true;
    }


    // 新增：完成表面線條繪製
    void CompleteTextureSurfaceLine(Vector3 startPoint, Vector3 endPoint)
    {
        GameObject finalLine = Instantiate(linePrefab);
        finalLine.transform.SetParent(linePool);
        finalLine.transform.position = Vector3.zero;
        finalLine.transform.localScale = Vector3.one;

        LineRenderer finalLineRenderer = finalLine.GetComponent<LineRenderer>();
        finalLineRenderer.positionCount = 2;
        finalLineRenderer.SetPosition(0, startPoint);
        finalLineRenderer.SetPosition(1, endPoint);

        Material lineMaterialInstance = new Material(LineMaterial);
        finalLineRenderer.material = lineMaterialInstance;
        finalLineRenderer.startWidth = lineWidth;
        finalLineRenderer.endWidth = lineWidth;

        lineList.Add(finalLineRenderer);
    }

    // 新增：繪製直線粒子
    void DrawStraightParticleLine(Vector3 startPoint, Vector3 endPoint)
    {
        float distance = Vector3.Distance(startPoint, endPoint);
        int particleCount = Mathf.RoundToInt(distance / (gapThreshold * 2f));
        particleCount = Mathf.Max(particleCount, 2);

        for (int i = 0; i <= particleCount; i++)
        {
            float t = (float)i / particleCount;
            Vector3 particlePosition = Vector3.Lerp(startPoint, endPoint, t);
            CreateParticleAtPosition(particlePosition);
        }
    }

    void UpdateAnchor()
    {
        if (anchorUpdate)
        {
            Vector3 temp = Input.mousePosition;
            temp.z = cameraDistance;
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

    private void SpaceModeSelection()
    {
        SpaceMode = true;
        TextureMode = false;
        isPaintingOn3D = false;
        Distance.SetActive(true);

        SpaceModeButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        TextureModeButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    private void TextureModeSelection()
    {
        if (!firstWarning)
        {
            Warning.SetActive(true);
            firstWarning = true;
        }

        SpaceMode = false;
        TextureMode = true;
        Distance.SetActive(false);

        SpaceModeButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        TextureModeButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    public void MakeLineRenderer()
    {
        if (TextureMode)
        {
            return;
        }

        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = Vector3.one;

        anchorUpdate = true;
        UpdateAnchor();
        lastAnchor = anchor;

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
        float distance = Vector3.Distance(anchor, lastAnchor);

        if (distance >= gapThreshold)
        {
            lineRenderer.positionCount = lineRenderer.positionCount + 1;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, anchor);
            lastAnchor = anchor;
        }
    }

    void DrawParticleContinue()
    {
        float distance = Vector3.Distance(anchor, lastParticlePosition);

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
            var main = newParticleSystem.main;
            main.startColor = LineMaterial.color;
            main.startSize = ParticleScale * 5f;

            particleList.Add(newParticleSystem);

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
        if (TextureMode)
        {
            return;
        }

        if (!waitingForSecondPoint)
        {
            anchorUpdate = true;
            UpdateAnchor();
            firstPoint = anchor;

            waitingForSecondPoint = true;
            CreateTempLineRenderer();
        }
        else
        {
            anchorUpdate = true;
            UpdateAnchor();
            Vector3 secondPoint = anchor;

            CompleteTwoPointLine(firstPoint, secondPoint);

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
        tempLine.transform.localScale = Vector3.one;

        tempLineRenderer = tempLine.GetComponent<LineRenderer>();
        tempLineRenderer.positionCount = 2;
        tempLineRenderer.SetPosition(0, firstPoint);
        tempLineRenderer.SetPosition(1, firstPoint);

        Material tempMaterial = new Material(LineMaterial);
        tempMaterial.color = new Color(LineMaterial.color.r, LineMaterial.color.g, LineMaterial.color.b, 0.5f);
        tempLineRenderer.material = tempMaterial;
        tempLineRenderer.startWidth = lineWidth;
        tempLineRenderer.endWidth = lineWidth;
    }

    void CompleteTwoPointLine(Vector3 point1, Vector3 point2)
    {
        GameObject finalLine = Instantiate(linePrefab);
        finalLine.transform.SetParent(linePool);
        finalLine.transform.position = Vector3.zero;
        finalLine.transform.localScale = Vector3.one;

        LineRenderer finalLineRenderer = finalLine.GetComponent<LineRenderer>();
        finalLineRenderer.positionCount = 2;
        finalLineRenderer.SetPosition(0, point1);
        finalLineRenderer.SetPosition(1, point2);

        Material lineMaterialInstance = new Material(LineMaterial);
        finalLineRenderer.material = lineMaterialInstance;
        finalLineRenderer.startWidth = lineWidth;
        finalLineRenderer.endWidth = lineWidth;

        lineList.Add(finalLineRenderer);
    }

    public void ToggleTwoPointAction()
    {
        StraightLine = !StraightLine;

        if (!StraightLine)
        {
            TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);

            // 重置所有兩點繪製相關狀態
            ResetTwoPointState();
            textureClickEnabled = false;
            hasProcessedClick = false;
        }
        else
        {
            TwoPointActionButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        }
    }

    void HandleSpaceParticleTwoPoint()
    {
        if (!waitingForSecondPoint)
        {
            anchorUpdate = true;
            UpdateAnchor();
            firstPoint = anchor;
            waitingForSecondPoint = true;

            // 創建第一個粒子
            CreateParticleAtPosition(anchor);
        }
        else
        {
            anchorUpdate = true;
            UpdateAnchor();
            Vector3 secondPoint = anchor;

            // 繪製直線粒子
            DrawStraightParticleLine(firstPoint, secondPoint);

            waitingForSecondPoint = false;
        }
    }

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
        string cleanValue = value.Replace("%", "").Trim();

        if (float.TryParse(cleanValue, out float displayValue))
        {
            displayValue = Mathf.Clamp(displayValue, 10f, 500f);
            float actualValue = (displayValue / 100f) * 0.01f;
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
            float actualValue = (displayValue / 100f) * 0.01f;
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
            gapThreshold = Mathf.Clamp(inputValue, 0.001f, 0.05f);
            if (GapSlider != null)
                GapSlider.value = gapThreshold;
        }
        UpdateInputFields();
    }

    void UpdateInputFields()
    {
        if (WidthInputField != null)
        {
            float displayWidth = (lineWidth / 0.01f) * 100f;
            WidthInputField.text = displayWidth.ToString("F0");
        }

        if (ScaleInputField != null)
        {
            float displayScale = (ParticleScale / 0.01f) * 100f;
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

    private void OnChangeColor(Color co)
    {
        LineMaterial.color = co;
    }

    public void StartDrawLine()
    {
        if (LineBrush)
        {
            if (in3DDraw)
            {
                if (TextureMode)
                {
                    if (StraightLine)
                    {
                        // Texture模式直線：啟用點擊觸發
                        textureClickEnabled = true;
                        hasProcessedClick = false;
                        use = true;
                    }
                    else
                    {
                        // Texture模式連續繪製
                        use = true;
                    }
                    return;
                }

                // Space模式邏輯保持不變
                if (StraightLine)
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
            else if (in2DDraw)
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

    public void StopDrawLine()
    {
        if (TextureMode && LineBrush)
        {
            use = false;

            // 重置點擊控制狀態
            textureClickEnabled = false;
            hasProcessedClick = false;

            // 重置線條渲染器引用
            lineRenderer = null;
            return;
        }

        // Space模式的原有邏輯保持不變
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
            if (SpaceMode)
            {
                if (StraightLine)
                {
                    HandleSpaceParticleTwoPoint();
                }
                else
                {
                    anchorUpdate = true;
                    UpdateAnchor();
                    CreateParticleAtPosition(anchor);
                    lastParticlePosition = anchor;
                    particleActive = true;
                }
            }
            else if (TextureMode && in3DDraw)
            {
                if (StraightLine)
                {
                    // Texture模式直線粒子：啟用點擊觸發
                    textureClickEnabled = true;
                    hasProcessedClick = false;
                    particleActive = true;
                }
                else
                {
                    Ray ray = arCamera.ScreenPointToRay(Input.mousePosition);
                    RaycastHit hit;

                    if (Physics.Raycast(ray, out hit, 100f))
                    {
                        CreateParticleAtPosition(hit.point);
                        lastParticlePosition = hit.point;
                        particleActive = true;
                    }
                    else
                    {
                        return;
                    }
                }
            }
        }
    }

    public void StopDrawParticle()
    {
        if (particleActive)
        {
            particleActive = false;
            anchorUpdate = false;

            // 如果是Texture模式，重置點擊控制狀態
            if (TextureMode)
            {
                textureClickEnabled = false;
                hasProcessedClick = false;
            }
        }
    }

    public void Undo()
    {
        if (ParticleBrush && particleList.Count > 0)
        {
            ParticleSystem undoParticle = particleList[particleList.Count - 1];
            Destroy(undoParticle.gameObject);
            particleList.RemoveAt(particleList.Count - 1);
        }
        else if (LineBrush && lineList.Count > 0)
        {
            LineRenderer undo = lineList[lineList.Count - 1];
            Destroy(undo.gameObject);
            lineList.RemoveAt(lineList.Count - 1);
        }

        if (currentPaintManager != null)
        {

        }
    }

    public void ClearScreen()
    {
        foreach (LineRenderer item in lineList)
        {
            Destroy(item.gameObject);
        }
        lineList.Clear();

        foreach (ParticleSystem particle in particleList)
        {
            Destroy(particle.gameObject);
        }
        particleList.Clear();

        PaintManager[] allPaintManagers = FindObjectsOfType<PaintManager>();
        foreach (PaintManager pm in allPaintManagers)
        {
            pm.ClearPaint();
        }
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

        isPaintingOn3D = false;
        currentPaintManager = null;
        lastHitObject = null;

        SpaceModeSelection();
    }

    void OnFinishButtonClicked()
    {
        uiManager.inDraw = false;
        uiManager.isInColorPage = false;
        uiManager.ColorPage2.SetActive(false);
        uiManager.BasicEditPage.SetActive(true);

        in3DDraw = false;
        StraightLine = false;
        in2DDraw = false;
        LineBrush = false;
        ParticleBrush = false;
        isPaintingOn3D = false;

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

        currentPaintManager = null;
        lastHitObject = null;

        SpaceModeSelection();
    }

    void ResetAllDrawingStates()
    {
        ResetTwoPointState();
        textureClickEnabled = false;
        hasProcessedClick = false;
        use = false;
        particleActive = false;
        startLine = false;
        anchorUpdate = false;
        lineRenderer = null;
    }
}