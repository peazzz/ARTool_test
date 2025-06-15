using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class DrawFunction : MonoBehaviour
{
    public Camera arCamera;
    public UIManager uiManager;
    public FlexibleColorPicker fcp;
    public GameObject DrawPanel;
    public Button _3DDraw, _2DDraw;
    public GameObject SpaceModeButton, TextureModeButton;
    public GameObject LBrush, PBrush;
    public GameObject LineSettingPage, ParticleSettingPage;
    public Canvas2DManager canvas2DManager;
    private bool isPaintingOn3D = false;
    private PaintManager currentPaintManager;
    private GameObject lastHitObject;
    private Vector3 lastPaintPosition;
    public GameObject TwoPointActionButton;
    public bool StraightLine = false;
    public bool waitingForSecondPoint = false;
    private Vector3 firstPoint;
    public LineRenderer tempLineRenderer;
    public GameObject particlePrefab;
    public List<ParticleSystem> particleList = new List<ParticleSystem>();
    private ParticleSystem currentParticleSystem;
    private bool particleActive = false;
    private Vector3 lastParticlePosition;
    public GameObject Warning, WarningText_Texture, WarningText_2DPaint, OkButton, LeaveButton, CancelButton, Distance;
    public Button UndoButton, ResetButton, FinishButton, ClearAllButton;
    public Slider WidthSlider, ScaleSlider, DistanceSlider, GapSlider;
    public InputField WidthInputField, ScaleInputField, DistanceInputField, GapInputField;

    // 新增的Slider和InputField變數
    public Slider DistanceSlider2, GapSlider2;
    public InputField DistanceInputField2, GapInputField2;

    // 粒子圖片和數量控制
    public GameObject ImageSelectorButton;
    public InputField CountInputField;
    public Material ParticleMaterial;

    [Header("粒子設定")]
    public int particleCount = 10; // 預設粒子數量
    private Texture2D currentParticleTexture; // 當前粒子紋理

    public Material LineMaterial;
    Vector3 anchor = new Vector3(0, 0, 0.3f);
    Vector3 lastAnchor;
    bool anchorUpdate = false;
    public GameObject linePrefab;
    LineRenderer lineRenderer;
    public List<LineRenderer> lineList = new List<LineRenderer>();
    public Transform linePool;
    public bool use, startLine, in3DDraw, in2DDraw, LineBrush, ParticleBrush, SpaceMode, TextureMode;
    private bool firstWarning;
    public float lineWidth = 0.02f;
    public float ParticleScale = 0.02f;
    public float cameraDistance = 0.3f;
    public float gapThreshold = 0.01f;
    public float paintGapThreshold = 0.02f;
    private bool textureClickEnabled = false;
    private bool hasProcessedClick = false;
    private bool isMouseDown = false;
    private bool isTouchActive = false;
    private Vector2 lastInputPosition;
    private float inputSensitivity = 2f;

    void Start()
    {
        SetupAllButtonEvents();
        InitializeUI();
        SetupUIListeners();
        InitializeParticleSettings();
        LineBrushSelection();
        SpaceModeSelection();
        if (fcp && LineMaterial)
        {
            fcp.color = new Color(1, 1, 1, 1);
            LineMaterial.color = fcp.color;
            // 同時初始化粒子材質顏色
            if (ParticleMaterial)
            {
                ParticleMaterial.color = fcp.color;
            }
            fcp.onColorChange.AddListener(OnChangeColor);
        }
    }

    void InitializeParticleSettings()
    {
        // 初始化粒子數量輸入框
        if (CountInputField)
        {
            CountInputField.text = particleCount.ToString();
            CountInputField.onEndEdit.AddListener(OnParticleCountChanged);
        }
    }

    void SetupAllButtonEvents()
    {
        _3DDraw?.onClick.AddListener(() => {
            LineBrush = true; ParticleBrush = false; in3DDraw = true; in2DDraw = false;
            DrawPanel.SetActive(true); uiManager.SwitchToPanel(uiManager.BrushPanel);
            uiManager.SetUIVisibility(false); uiManager.UI_on = false;
            LineBrushSelection(); SpaceModeSelection();
        });
        _2DDraw?.onClick.AddListener(() => {
            LineBrush = true; ParticleBrush = false; in2DDraw = true; in3DDraw = false;
            if (canvas2DManager) canvas2DManager.Show2DCanvas_Auto();
            uiManager.FounctionUI.SetActive(false);
        });
        LBrush.GetComponent<Button>().onClick.AddListener(() => LineBrushSelection());
        PBrush.GetComponent<Button>().onClick.AddListener(() => ParticleBrushSelection());
        SpaceModeButton.GetComponent<Button>().onClick.AddListener(() => SpaceModeSelection());
        TextureModeButton.GetComponent<Button>().onClick.AddListener(() => TextureModeSelection());
        TwoPointActionButton.GetComponent<Button>().onClick.AddListener(() => ToggleTwoPointAction());
        UndoButton?.onClick.AddListener(Undo);
        ResetButton?.onClick.AddListener(OnResetButtonClicked);
        FinishButton?.onClick.AddListener(OnFinishButtonClicked);
        ClearAllButton?.onClick.AddListener(ClearScreen);

        // 新增圖片選擇按鈕事件
        if (ImageSelectorButton)
        {
            ImageSelectorButton.GetComponent<Button>().onClick.AddListener(OpenImageSelector);
        }
    }

    void InitializeUI()
    {
        if (WidthSlider) { WidthSlider.value = lineWidth; WidthSlider.minValue = 0.001f; WidthSlider.maxValue = 0.1f; }
        if (ScaleSlider) { ScaleSlider.value = ParticleScale; ScaleSlider.minValue = 0.001f; ScaleSlider.maxValue = 0.1f; }

        // 原始Distance和Gap Slider設定
        if (DistanceSlider) { DistanceSlider.value = cameraDistance; DistanceSlider.minValue = 0.1f; DistanceSlider.maxValue = 2.0f; }
        if (GapSlider) { GapSlider.value = gapThreshold; GapSlider.minValue = 0.001f; GapSlider.maxValue = 0.1f; }

        // 新增的Slider設定（與原始Slider相同參數）
        if (DistanceSlider2) { DistanceSlider2.value = cameraDistance; DistanceSlider2.minValue = 0.1f; DistanceSlider2.maxValue = 2.0f; }
        if (GapSlider2) { GapSlider2.value = gapThreshold; GapSlider2.minValue = 0.001f; GapSlider2.maxValue = 0.1f; }

        UpdateInputFields();
    }

    void SetupUIListeners()
    {
        WidthSlider?.onValueChanged.AddListener(OnWidthSliderChanged);
        ScaleSlider?.onValueChanged.AddListener(OnScaleSliderChanged);

        // 原始Slider監聽器
        DistanceSlider?.onValueChanged.AddListener(OnDistanceSliderChanged);
        GapSlider?.onValueChanged.AddListener(OnGapSliderChanged);

        // 新增Slider監聽器（使用相同的回調函數）
        DistanceSlider2?.onValueChanged.AddListener(OnDistanceSliderChanged);
        GapSlider2?.onValueChanged.AddListener(OnGapSliderChanged);

        // 原始InputField監聽器
        WidthInputField?.onEndEdit.AddListener(OnWidthInputChanged);
        ScaleInputField?.onEndEdit.AddListener(OnScaleInputChanged);
        DistanceInputField?.onEndEdit.AddListener(OnDistanceInputChanged);
        GapInputField?.onEndEdit.AddListener(OnGapInputChanged);

        // 新增InputField監聽器（使用相同的回調函數）
        DistanceInputField2?.onEndEdit.AddListener(OnDistanceInputChanged);
        GapInputField2?.onEndEdit.AddListener(OnGapInputChanged);
    }

    void Update()
    {
        if (use)
        {
            if (TextureMode && in3DDraw && LineBrush) Handle3DPainting();
            else if (SpaceMode && startLine) { UpdateAnchor(); DrawLinewContinue(); }
        }
        if (ParticleBrush && particleActive)
        {
            if (TextureMode && in3DDraw) Handle3DParticlePainting();
            else if (SpaceMode) { UpdateAnchor(); DrawParticleContinue(); }
        }
        if (in2DDraw && canvas2DManager) Handle2DCanvasDrawing();
        if (StraightLine && waitingForSecondPoint && tempLineRenderer)
        {
            if (SpaceMode) { UpdateAnchor(); tempLineRenderer.SetPosition(1, anchor); }
            else if (TextureMode)
            {
                Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
                if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    tempLineRenderer.SetPosition(1, hit.point);
            }
        }
    }

    Vector2 GetInputPosition() => Input.touchCount > 0 ? Input.GetTouch(0).position : Input.mousePosition;
    bool GetInputDown() => Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    bool GetInput() => Input.GetMouseButton(0) || (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary));
    bool GetInputUp() => Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);

    void Handle2DCanvasDrawing()
    {
        Vector2 currentPosition = GetInputPosition();
        RawImage currentCanvasImage = canvas2DManager.GetCurrentCanvasImage();
        if (!currentCanvasImage) return;
        RectTransform canvasRect = currentCanvasImage.rectTransform;
        Canvas parentCanvas = currentCanvasImage.GetComponentInParent<Canvas>();
        Camera uiCam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
        if (!RectTransformUtility.RectangleContainsScreenPoint(canvasRect, currentPosition, uiCam))
        { isMouseDown = false; canvas2DManager.StopDrawing(); return; }
        if (GetInputDown())
        {
            if (IsPointerOnCanvas(currentPosition))
            { isMouseDown = true; lastInputPosition = currentPosition; canvas2DManager.StartDrawing(currentPosition); }
        }
        else if (GetInput() && isMouseDown)
        {
            if (IsPointerOnCanvas(currentPosition))
            {
                if (Vector2.Distance(currentPosition, lastInputPosition) > inputSensitivity)
                { canvas2DManager.UpdateDrawing(currentPosition); lastInputPosition = currentPosition; }
            }
            else { isMouseDown = false; canvas2DManager.StopDrawing(); }
        }
        else if (GetInputUp() || (!GetInput() && isMouseDown))
        { isMouseDown = false; canvas2DManager.StopDrawing(); }
    }

    private bool IsPointerOnCanvas(Vector2 inputPosition)
    {
        if (!canvas2DManager) return false;
        RawImage currentCanvasImage = canvas2DManager.GetCurrentCanvasImage();
        if (!currentCanvasImage) return false;
        RectTransform canvasRect = currentCanvasImage.rectTransform;
        Canvas canvas = currentCanvasImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, inputPosition, uiCamera, out Vector2 localPoint);
    }

    void Handle3DPainting()
    {
        Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (StraightLine)
            {
                if (textureClickEnabled && !hasProcessedClick)
                {
                    HandleTexture3DTwoPointDrawing(hit);
                    hasProcessedClick = true;
                }
                return;
            }

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

    void AddPointToSurfaceLine(Vector3 newPoint)
    {
        if (lineRenderer)
        {
            lineRenderer.positionCount++;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, newPoint);
        }
    }

    void Handle3DParticlePainting()
    {
        Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
        if (Physics.Raycast(ray, out RaycastHit hit, 100f))
        {
            if (StraightLine)
            {
                if (textureClickEnabled && !hasProcessedClick)
                { HandleTexture3DTwoPointParticle(hit); hasProcessedClick = true; }
                return;
            }
            float distance = Vector3.Distance(hit.point, lastParticlePosition);
            if (distance >= gapThreshold)
            {
                // 使用 hit.point 確保粒子創建在物件表面
                CreateParticleAtPosition(hit.point);
                lastParticlePosition = hit.point;
            }
        }
    }

    void HandleTexture3DTwoPointDrawing(RaycastHit hit)
    {
        if (!waitingForSecondPoint)
        {
            firstPoint = hit.point;
            waitingForSecondPoint = true;
            CreateTempTextureLineRenderer(hit.point);
        }
        else
        {
            Vector3 secondPoint = hit.point;
            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
                DrawStraightLineOnSculptObject(firstPoint, secondPoint, hit.collider.gameObject);
            else CompleteTextureSurfaceLine(firstPoint, secondPoint);
            ResetTwoPointState();
        }
    }

    void HandleTexture3DTwoPointParticle(RaycastHit hit)
    {
        if (!waitingForSecondPoint)
        {
            firstPoint = hit.point;
            waitingForSecondPoint = true;
            CreateParticleAtPosition(hit.point);
        }
        else
        {
            Vector3 secondPoint = hit.point;
            DrawStraightParticleLine(firstPoint, secondPoint);
            ResetTwoPointState();
        }
    }

    void ResetTwoPointState()
    {
        waitingForSecondPoint = false;
        if (tempLineRenderer) { Destroy(tempLineRenderer.gameObject); tempLineRenderer = null; }
    }

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

    void DrawStraightLineOnSculptObject(Vector3 startPoint, Vector3 endPoint, GameObject sculptObject)
    {
        DualMaterialManager dualManager = sculptObject.GetComponent<DualMaterialManager>();
        if (dualManager && !dualManager.SupportsPainting())
        {
            return;
        }

        PaintManager paintManager = sculptObject.GetComponent<PaintManager>();
        if (!paintManager) paintManager = sculptObject.AddComponent<PaintManager>();
        paintManager.paintColor = LineMaterial.color;
        paintManager.brushSize = lineWidth;
        float distance = Vector3.Distance(startPoint, endPoint);
        int steps = Mathf.Max(Mathf.RoundToInt(distance / (gapThreshold * 0.5f)), 10);
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 interpolatedPoint = Vector3.Lerp(startPoint, endPoint, t);
            if (FindSurfacePointForSculpt(interpolatedPoint, sculptObject, out Vector3 hitPoint, out Vector3 hitNormal))
                paintManager.PaintAt(hitPoint, hitNormal);
        }
    }

    bool FindSurfacePointForSculpt(Vector3 targetPoint, GameObject sculptObject, out Vector3 hitPoint, out Vector3 hitNormal)
    {
        hitPoint = Vector3.zero; hitNormal = Vector3.zero;
        Collider objectCollider = sculptObject.GetComponent<Collider>();
        if (!objectCollider) return false;
        Vector3 closestPoint = objectCollider.ClosestPoint(targetPoint);
        Vector3 center = objectCollider.bounds.center;
        Vector3 normal = (closestPoint - center).normalized;
        Ray ray = new Ray(closestPoint + normal * 0.01f, -normal);
        if (Physics.Raycast(ray, out RaycastHit hit, 0.02f) && hit.collider.gameObject == sculptObject)
        { hitPoint = hit.point; hitNormal = hit.normal; return true; }
        hitPoint = closestPoint; hitNormal = normal; return true;
    }

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

    void DrawStraightParticleLine(Vector3 startPoint, Vector3 endPoint)
    {
        float distance = Vector3.Distance(startPoint, endPoint);
        int particleSteps = Mathf.Max(Mathf.RoundToInt(distance / (gapThreshold * 2f)), 2);

        for (int i = 0; i <= particleSteps; i++)
        {
            float t = (float)i / particleSteps;
            Vector3 particlePosition = Vector3.Lerp(startPoint, endPoint, t);

            // 在TextureMode下，確保每個粒子都貼合表面
            if (TextureMode)
            {
                // 對每個插值點進行射線檢測，確保粒子貼合表面
                Vector3 rayStart = particlePosition + Vector3.up * 1f; // 從上方開始射線
                Ray ray = new Ray(rayStart, Vector3.down);

                if (Physics.Raycast(ray, out RaycastHit hit, 2f))
                {
                    // 使用射線檢測到的點，並稍微偏移避免Z-fighting
                    particlePosition = hit.point + hit.normal * 0.01f;
                }
            }

            CreateParticleAtPosition(particlePosition);
        }
    }

    void UpdateAnchor()
    {
        if (anchorUpdate)
        {
            Vector3 temp = GetInputPosition();
            temp.z = cameraDistance;
            anchor = arCamera.ScreenToWorldPoint(temp);
        }
    }

    private void LineBrushSelection()
    {
        LineBrush = true; ParticleBrush = false;
        LBrush.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        PBrush.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        LineSettingPage.SetActive(true); ParticleSettingPage.SetActive(false);
    }

    private void ParticleBrushSelection()
    {
        LineBrush = false; ParticleBrush = true;
        LBrush.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        PBrush.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        LineSettingPage.SetActive(false); ParticleSettingPage.SetActive(true);
    }

    private void SpaceModeSelection()
    {
        SpaceMode = true; TextureMode = false; isPaintingOn3D = false;
        Distance.SetActive(true);
        SpaceModeButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        TextureModeButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    private void TextureModeSelection()
    {
        SpaceMode = false; TextureMode = true; Distance.SetActive(false);
        SpaceModeButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        TextureModeButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    public void MakeLineRenderer()
    {
        if (TextureMode) return;
        GameObject tempLine = Instantiate(linePrefab);
        tempLine.transform.SetParent(linePool);
        tempLine.transform.position = Vector3.zero;
        tempLine.transform.localScale = Vector3.one;
        anchorUpdate = true; UpdateAnchor(); lastAnchor = anchor;
        lineRenderer = tempLine.GetComponent<LineRenderer>();
        lineRenderer.positionCount = 1;
        lineRenderer.SetPosition(0, anchor);
        Material lineMaterialInstance = new Material(LineMaterial);
        lineRenderer.material = lineMaterialInstance;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        startLine = true; lineList.Add(lineRenderer);
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
        { CreateParticleAtPosition(anchor); lastParticlePosition = anchor; }
    }

    // 修改後的CreateParticleAtPosition方法，解決粒子碰撞問題
    void CreateParticleAtPosition(Vector3 position)
    {
        if (!particlePrefab) return;

        GameObject tempParticle = Instantiate(particlePrefab);
        tempParticle.transform.SetParent(linePool);

        // 最終位置處理
        Vector3 finalPosition = position;
        Vector3 surfaceNormal = Vector3.up;

        // 在TextureMode下，重新進行射線檢測確保準確性
        if (TextureMode)
        {
            Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                finalPosition = hit.point + hit.normal * 0.005f; // 更小的偏移
                surfaceNormal = hit.normal;

                // 讓粒子系統朝向表面法線
                Vector3 lookDirection = -surfaceNormal;
                tempParticle.transform.rotation = Quaternion.LookRotation(lookDirection);
            }
        }

        tempParticle.transform.position = finalPosition;
        tempParticle.transform.localScale = Vector3.one;

        ParticleSystem newParticleSystem = tempParticle.GetComponent<ParticleSystem>();
        if (newParticleSystem)
        {
            // 設定主要屬性
            var main = newParticleSystem.main;
            main.startColor = ParticleMaterial ? ParticleMaterial.color : fcp.color;
            main.startSize = ParticleScale * 5f;
            main.maxParticles = particleCount;
            main.startLifetime = 5.0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            // 根據模式調整粒子行為
            if (TextureMode)
            {
                // TextureMode: 讓粒子緊貼表面，幾乎不移動
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.05f);

                // 設定形狀 - 讓粒子在表面小範圍內發射
                var shape = newParticleSystem.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.01f;
                shape.radiusThickness = 1f;

                // 關閉速度模組
                var velocityOverLifetime = newParticleSystem.velocityOverLifetime;
                velocityOverLifetime.enabled = false;

                // 重力設為0
                main.gravityModifier = 0f;

                // 設定粒子朝向
                main.startRotation3D = true;
                main.startRotationX = 0f;
                main.startRotationY = 0f;
                main.startRotationZ = 0f;
            }
            else
            {
                // SpaceMode: 保持原有的自由擴散效果
                main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 1f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.gravityModifier = -0.1f;
            }

            // **關鍵修正：禁用粒子碰撞**
            var collision = newParticleSystem.collision;
            collision.enabled = false; // 完全關閉碰撞

            // 設定發射器
            var emission = newParticleSystem.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] {
                new ParticleSystem.Burst(0.0f, particleCount)
            });

            // 套用自定義材質
            var renderer = newParticleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer && ParticleMaterial)
            {
                Material particleMaterialInstance = new Material(ParticleMaterial);
                renderer.material = particleMaterialInstance;

                if (currentParticleTexture)
                {
                    particleMaterialInstance.mainTexture = currentParticleTexture;
                }

                particleMaterialInstance.color = ParticleMaterial.color;
            }

            // 播放粒子系統
            newParticleSystem.Play();

            particleList.Add(newParticleSystem);
            var stopAction = newParticleSystem.main;
            stopAction.stopAction = ParticleSystemStopAction.Destroy;
        }
        else
        {
            Destroy(tempParticle);
        }

        // 移除任何可能的碰撞器組件
        Collider[] colliders = tempParticle.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            if (col.gameObject != tempParticle) continue; // 只移除粒子對象本身的碰撞器
            Destroy(col);
        }
    }

    void HandleTwoPointDrawing()
    {
        if (TextureMode) return;
        if (!waitingForSecondPoint)
        {
            anchorUpdate = true; UpdateAnchor(); firstPoint = anchor;
            waitingForSecondPoint = true; CreateTempLineRenderer();
        }
        else
        {
            anchorUpdate = true; UpdateAnchor(); Vector3 secondPoint = anchor;
            CompleteTwoPointLine(firstPoint, secondPoint);
            waitingForSecondPoint = false;
            if (tempLineRenderer) { Destroy(tempLineRenderer.gameObject); tempLineRenderer = null; }
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
            ResetTwoPointState(); textureClickEnabled = false; hasProcessedClick = false;
        }
        else TwoPointActionButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    void HandleSpaceParticleTwoPoint()
    {
        if (!waitingForSecondPoint)
        {
            anchorUpdate = true; UpdateAnchor(); firstPoint = anchor;
            waitingForSecondPoint = true; CreateParticleAtPosition(anchor);
        }
        else
        {
            anchorUpdate = true; UpdateAnchor(); Vector3 secondPoint = anchor;
            DrawStraightParticleLine(firstPoint, secondPoint); waitingForSecondPoint = false;
        }
    }

    // 粒子數量改變事件
    public void OnParticleCountChanged(string value)
    {
        if (int.TryParse(value, out int newCount))
        {
            particleCount = Mathf.Clamp(newCount, 1, 100); // 限制範圍1-100
            CountInputField.text = particleCount.ToString();
        }
        else
        {
            CountInputField.text = particleCount.ToString(); // 無效輸入時恢復原值
        }
    }

    // 修改Distance Slider變更事件，同步更新兩個Slider
    public void OnDistanceSliderChanged(float value)
    {
        cameraDistance = value;

        // 同步更新所有Distance相關UI
        SetDistanceSliderValues(value);
        SetDistanceInputFieldValues(value.ToString("F2"));
    }

    // 修改Gap Slider變更事件，同步更新兩個Slider
    public void OnGapSliderChanged(float value)
    {
        gapThreshold = value;

        // 同步更新所有Gap相關UI
        SetGapSliderValues(value);
        SetGapInputFieldValues(value.ToString("F3"));
    }

    public void OnWidthSliderChanged(float value)
    {
        lineWidth = value; UpdateInputFields(); ApplyWidthToCurrentLine();
    }

    public void OnScaleSliderChanged(float value)
    {
        ParticleScale = value; UpdateInputFields();
    }

    // 修改Distance InputField變更事件，同步更新兩個InputField
    public void OnDistanceInputChanged(string value)
    {
        if (float.TryParse(value, out float newDistance))
        {
            cameraDistance = Mathf.Clamp(newDistance, 0.1f, 2.0f);

            // 同步更新所有Distance相關UI
            SetDistanceSliderValues(cameraDistance);
            SetDistanceInputFieldValues(cameraDistance.ToString("F2"));
        }
        else
        {
            // 輸入無效時恢復原值
            SetDistanceInputFieldValues(cameraDistance.ToString("F2"));
        }
    }

    // 修改Gap InputField變更事件，同步更新兩個InputField
    public void OnGapInputChanged(string value)
    {
        if (float.TryParse(value.Trim(), out float inputValue))
        {
            gapThreshold = Mathf.Clamp(inputValue, 0.001f, 0.05f);

            // 同步更新所有Gap相關UI
            SetGapSliderValues(gapThreshold);
            SetGapInputFieldValues(gapThreshold.ToString("F3"));
        }
        else
        {
            // 輸入無效時恢復原值
            SetGapInputFieldValues(gapThreshold.ToString("F3"));
        }
    }

    public void OnWidthInputChanged(string value)
    {
        string cleanValue = value.Replace("%", "").Trim();
        if (float.TryParse(cleanValue, out float percentageValue))
        {
            percentageValue = Mathf.Clamp(percentageValue, 10f, 1000f);
            float actualValue = Mathf.Lerp(0.001f, 0.1f, (percentageValue - 10f) / 990f);
            lineWidth = actualValue;
            if (WidthSlider) WidthSlider.value = lineWidth;
            ApplyWidthToCurrentLine();
        }
        UpdateInputFields();
    }

    public void OnScaleInputChanged(string value)
    {
        string cleanValue = value.Replace("%", "").Trim();
        if (float.TryParse(cleanValue, out float percentageValue))
        {
            percentageValue = Mathf.Clamp(percentageValue, 10f, 1000f);
            float actualValue = Mathf.Lerp(0.001f, 0.1f, (percentageValue - 10f) / 990f);
            ParticleScale = actualValue;
            if (ScaleSlider) ScaleSlider.value = ParticleScale;
        }
        UpdateInputFields();
    }

    // 新增方法來統一設定所有相關Slider的值
    private void SetDistanceSliderValues(float value)
    {
        // 暫時移除監聽器避免循環調用
        DistanceSlider?.onValueChanged.RemoveListener(OnDistanceSliderChanged);
        DistanceSlider2?.onValueChanged.RemoveListener(OnDistanceSliderChanged);

        // 設定值
        if (DistanceSlider) DistanceSlider.value = value;
        if (DistanceSlider2) DistanceSlider2.value = value;

        // 重新添加監聽器
        DistanceSlider?.onValueChanged.AddListener(OnDistanceSliderChanged);
        DistanceSlider2?.onValueChanged.AddListener(OnDistanceSliderChanged);
    }

    private void SetGapSliderValues(float value)
    {
        // 暫時移除監聽器避免循環調用
        GapSlider?.onValueChanged.RemoveListener(OnGapSliderChanged);
        GapSlider2?.onValueChanged.RemoveListener(OnGapSliderChanged);

        // 設定值
        if (GapSlider) GapSlider.value = value;
        if (GapSlider2) GapSlider2.value = value;

        // 重新添加監聽器
        GapSlider?.onValueChanged.AddListener(OnGapSliderChanged);
        GapSlider2?.onValueChanged.AddListener(OnGapSliderChanged);
    }

    // 新增方法來統一設定所有相關InputField的值
    private void SetDistanceInputFieldValues(string value)
    {
        // 暫時移除監聽器避免循環調用
        DistanceInputField?.onEndEdit.RemoveListener(OnDistanceInputChanged);
        DistanceInputField2?.onEndEdit.RemoveListener(OnDistanceInputChanged);

        // 設定值
        if (DistanceInputField) DistanceInputField.text = value;
        if (DistanceInputField2) DistanceInputField2.text = value;

        // 重新添加監聽器
        DistanceInputField?.onEndEdit.AddListener(OnDistanceInputChanged);
        DistanceInputField2?.onEndEdit.AddListener(OnDistanceInputChanged);
    }

    private void SetGapInputFieldValues(string value)
    {
        // 暫時移除監聽器避免循環調用
        GapInputField?.onEndEdit.RemoveListener(OnGapInputChanged);
        GapInputField2?.onEndEdit.RemoveListener(OnGapInputChanged);

        // 設定值
        if (GapInputField) GapInputField.text = value;
        if (GapInputField2) GapInputField2.text = value;

        // 重新添加監聽器
        GapInputField?.onEndEdit.AddListener(OnGapInputChanged);
        GapInputField2?.onEndEdit.AddListener(OnGapInputChanged);
    }

    // 修改UpdateInputFields方法以包含新的InputField
    void UpdateInputFields()
    {
        if (WidthInputField)
        {
            float normalizedValue = Mathf.InverseLerp(0.001f, 0.1f, lineWidth);
            float displayWidth = Mathf.Lerp(10f, 1000f, normalizedValue);
            WidthInputField.text = displayWidth.ToString("F0") + "%";
        }
        if (ScaleInputField)
        {
            float normalizedValue = Mathf.InverseLerp(0.001f, 0.1f, ParticleScale);
            float displayScale = Mathf.Lerp(10f, 1000f, normalizedValue);
            ScaleInputField.text = displayScale.ToString("F0") + "%";
        }

        // 更新所有Distance InputField
        string distanceValue = cameraDistance.ToString("F2");
        SetDistanceInputFieldValues(distanceValue);

        // 更新所有Gap InputField
        string gapValue = gapThreshold.ToString("F3");
        SetGapInputFieldValues(gapValue);
    }

    void ApplyWidthToCurrentLine()
    {
        if (lineRenderer) { lineRenderer.startWidth = lineWidth; lineRenderer.endWidth = lineWidth; }
    }

    private void OnChangeColor(Color co)
    {
        LineMaterial.color = co;

        // 同時更新粒子材質顏色
        if (ParticleMaterial)
        {
            ParticleMaterial.color = co;
        }

        // 如果正在3D繪圖模式，更新當前PaintManager的繪圖顏色
        if (currentPaintManager != null)
        {
            currentPaintManager.SetPaintColor(co);
        }
    }

    // 打開圖片選擇器
    public void OpenImageSelector()
    {
        bool hasPermission = NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);

        if (hasPermission)
        {
            PickImageFromGallery();
        }
        else
        {
            NativeGallery.RequestPermissionAsync((permission) =>
            {
                if (permission == NativeGallery.Permission.Granted)
                {
                    PickImageFromGallery();
                }
                else
                {
                    Debug.Log("圖片存取權限被拒絕");
                    if (permission == NativeGallery.Permission.Denied)
                    {
                        Debug.Log("請到設定中開啟圖片存取權限");
                    }
                }
            }, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        }
    }

    private void PickImageFromGallery()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            Debug.Log("選取的圖片路徑: " + path);

            if (path != null)
            {
                StartCoroutine(LoadParticleImageCoroutine(path));
            }
            else
            {
                Debug.Log("未選取任何圖片");
            }
        }, "選擇粒子圖片", "image/*");
    }

    private System.Collections.IEnumerator LoadParticleImageCoroutine(string imagePath)
    {
        Texture2D loadedTexture = NativeGallery.LoadImageAtPath(imagePath, maxSize: 512, markTextureNonReadable: false);

        if (loadedTexture != null)
        {
            Debug.Log($"成功載入粒子圖片，尺寸: {loadedTexture.width}x{loadedTexture.height}");
            OnParticleTextureLoaded(loadedTexture);
        }
        else
        {
            Debug.LogError("無法載入圖片: " + imagePath);
        }

        yield return null;
    }

    // 套用紋理到粒子材質
    public void OnParticleTextureLoaded(Texture2D loadedTexture)
    {
        if (loadedTexture && ParticleMaterial)
        {
            currentParticleTexture = loadedTexture;

            // 將紋理套用到粒子材質
            ParticleMaterial.mainTexture = loadedTexture;

            // 如果使用的是Sprite材質，也可以設定其他屬性
            if (ParticleMaterial.HasProperty("_MainTex"))
            {
                ParticleMaterial.SetTexture("_MainTex", loadedTexture);
            }

            Debug.Log($"粒子材質已套用新圖片: {loadedTexture.name}");
        }
    }

    // 清除粒子紋理
    public void ClearParticleTexture()
    {
        if (ParticleMaterial)
        {
            ParticleMaterial.mainTexture = null;
            currentParticleTexture = null;
            Debug.Log("已清除粒子紋理");
        }
    }

    // 重置粒子設定
    public void ResetParticleSettings()
    {
        particleCount = 10;
        if (CountInputField)
        {
            CountInputField.text = particleCount.ToString();
        }
        ClearParticleTexture();
    }

    public void StartDrawLine()
    {
        if (LineBrush)
        {
            if (in3DDraw)
            {
                if (TextureMode)
                {
                    if (StraightLine) { textureClickEnabled = true; hasProcessedClick = false; use = true; }
                    else use = true;
                    return;
                }
                if (StraightLine) HandleTwoPointDrawing();
                else { use = true; if (!startLine) MakeLineRenderer(); }
            }
            else if (in2DDraw) { use = true; if (!startLine) MakeLineRenderer(); }
        }
        else if (ParticleBrush) StartDrawParticle();
    }

    public void StopDrawLine()
    {
        if (TextureMode && LineBrush)
        {
            use = false; textureClickEnabled = false; hasProcessedClick = false; lineRenderer = null;
            return;
        }
        if (LineBrush && lineRenderer)
        {
            if (lineRenderer.positionCount == 1)
            { UpdateAnchor(); lineRenderer.positionCount = 2; lineRenderer.SetPosition(1, anchor); }
            else
            {
                UpdateAnchor(); float distance = Vector3.Distance(anchor, lastAnchor);
                if (distance > 0.001f)
                {
                    lineRenderer.positionCount = lineRenderer.positionCount + 1;
                    lineRenderer.SetPosition(lineRenderer.positionCount - 1, anchor);
                }
            }
            use = false; startLine = false; lineRenderer = null; anchorUpdate = false;
        }
        else if (ParticleBrush) StopDrawParticle();
    }

    public void StartDrawParticle()
    {
        if (!particleActive)
        {
            if (SpaceMode)
            {
                if (StraightLine) HandleSpaceParticleTwoPoint();
                else
                {
                    anchorUpdate = true; UpdateAnchor(); CreateParticleAtPosition(anchor);
                    lastParticlePosition = anchor; particleActive = true;
                }
            }
            else if (TextureMode && in3DDraw)
            {
                if (StraightLine) { textureClickEnabled = true; hasProcessedClick = false; particleActive = true; }
                else
                {
                    Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f))
                    {
                        CreateParticleAtPosition(hit.point); lastParticlePosition = hit.point; particleActive = true;
                    }
                    else return;
                }
            }
        }
    }

    public void StopDrawParticle()
    {
        if (particleActive)
        {
            particleActive = false; anchorUpdate = false;
            if (TextureMode) { textureClickEnabled = false; hasProcessedClick = false; }
        }
    }

    public void Undo()
    {
        if (ParticleBrush && particleList.Count > 0)
        {
            ParticleSystem undoParticle = particleList[particleList.Count - 1];
            Destroy(undoParticle.gameObject); particleList.RemoveAt(particleList.Count - 1);
        }
        else if (LineBrush && lineList.Count > 0)
        {
            LineRenderer undo = lineList[lineList.Count - 1];
            Destroy(undo.gameObject); lineList.RemoveAt(lineList.Count - 1);
        }
    }

    public void ClearScreen()
    {
        foreach (LineRenderer item in lineList) Destroy(item.gameObject);
        lineList.Clear();
        foreach (ParticleSystem particle in particleList) Destroy(particle.gameObject);
        particleList.Clear();
        PaintManager[] allPaintManagers = FindObjectsOfType<PaintManager>();
        foreach (PaintManager pm in allPaintManagers) pm.ClearPaint();
    }

    void OnResetButtonClicked()
    {
        lineWidth = 0.02f; if (WidthSlider) WidthSlider.value = lineWidth;
        ParticleScale = 0.02f; if (ScaleSlider) ScaleSlider.value = ParticleScale;

        // 重置Distance相關
        cameraDistance = 0.3f;
        SetDistanceSliderValues(cameraDistance);

        // 重置Gap相關
        gapThreshold = 0.01f;
        SetGapSliderValues(gapThreshold);

        Color defaultColor = new Color(1f, 1f, 1f, 1f);
        LineMaterial.color = defaultColor;
        // 同時重置粒子材質顏色
        if (ParticleMaterial) ParticleMaterial.color = defaultColor;
        if (fcp) fcp.color = defaultColor;

        UpdateInputFields(); ApplyWidthToCurrentLine();
        StraightLine = false; waitingForSecondPoint = false;
        if (tempLineRenderer) { Destroy(tempLineRenderer.gameObject); tempLineRenderer = null; }
        isPaintingOn3D = false; currentPaintManager = null; lastHitObject = null;
        SpaceModeSelection();

        // 重置粒子設定
        ResetParticleSettings();
    }

    void OnFinishButtonClicked()
    {
        uiManager.inDraw = false; uiManager.isInColorPage = false;
        uiManager.ColorPage2.SetActive(false); uiManager.BasicEditPage.SetActive(true);
        in3DDraw = false; StraightLine = false; in2DDraw = false; LineBrush = false; ParticleBrush = false; isPaintingOn3D = false;
        DrawPanel.SetActive(false); uiManager.DrawPanel1?.SetActive(false); uiManager.DrawPanel2?.SetActive(false);
        uiManager.BrushPanel?.SetActive(false); uiManager.UIHome?.SetActive(true); uiManager.BackButton?.SetActive(false);
        uiManager.ClearModeButton.SetActive(true);
        TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        waitingForSecondPoint = false;
        if (tempLineRenderer) { Destroy(tempLineRenderer.gameObject); tempLineRenderer = null; }
        currentPaintManager = null; lastHitObject = null; SpaceModeSelection();

        // 重置粒子設定
        ResetParticleSettings();
    }
}