using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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

    [Header("2D Canvas System")]
    public Canvas2DManager canvas2DManager;

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
    public GameObject WarningText_Texture;
    public GameObject WarningText_2DPaint;
    public GameObject OkButton;
    public GameObject LeaveButton;
    public GameObject CancelButton;
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

    [Header("Input Optimization")]
    private bool isMouseDown = false;
    private bool isTouchActive = false;
    private Vector2 lastInputPosition;
    private float inputSensitivity = 2f;

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
            uiManager.SetUIVisibility(false);
            uiManager.UI_on = false;
            LineBrushSelection();
            SpaceModeSelection();
        });

        _2DDraw?.onClick.AddListener(() => {
            LineBrush = true;
            ParticleBrush = false;
            in2DDraw = true;
            in3DDraw = false;

            if (canvas2DManager != null)
            {
                canvas2DManager.Show2DCanvas();
            }

            uiManager.FounctionUI.SetActive(false);
            //uiManager.SwitchToPanel(uiManager.BrushPanel2D);
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

        if (in2DDraw && canvas2DManager != null)
        {
            Handle2DCanvasDrawing();
        }

        if (StraightLine && waitingForSecondPoint && tempLineRenderer != null)
        {
            if (SpaceMode)
            {
                UpdateAnchor();
                tempLineRenderer.SetPosition(1, anchor);
            }
            else if (TextureMode)
            {
                Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 100f))
                {
                    tempLineRenderer.SetPosition(1, hit.point);
                }
            }
        }
    }

    Vector2 GetInputPosition()
    {
        if (Input.touchCount > 0)
        {
            return Input.GetTouch(0).position;
        }
        return Input.mousePosition;
    }

    bool GetInputDown()
    {
        return Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
    }

    bool GetInput()
    {
        return Input.GetMouseButton(0) || (Input.touchCount > 0 && (Input.GetTouch(0).phase == TouchPhase.Moved || Input.GetTouch(0).phase == TouchPhase.Stationary));
    }

    bool GetInputUp()
    {
        return Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);
    }

    void Handle2DCanvasDrawing()
    {
        Vector2 currentPosition = GetInputPosition();

        if (GetInputDown())
        {
            // 移除或簡化UI檢測
            // if (IsClickingOtherUI(currentPosition))
            // {
            //     return;
            // }

            // 直接檢查是否在畫布範圍內
            if (IsPointerOnCanvas(currentPosition))
            {
                isMouseDown = true;
                lastInputPosition = currentPosition;
                canvas2DManager.StartDrawing(currentPosition);
            }
        }
        else if (GetInput() && isMouseDown)
        {
            if (IsPointerOnCanvas(currentPosition))
            {
                if (Vector2.Distance(currentPosition, lastInputPosition) > inputSensitivity)
                {
                    canvas2DManager.UpdateDrawing(currentPosition);
                    lastInputPosition = currentPosition;
                }
            }
            else
            {
                isMouseDown = false;
                canvas2DManager.StopDrawing();
            }
        }
        else if (GetInputUp() || (!GetInput() && isMouseDown))
        {
            isMouseDown = false;
            canvas2DManager.StopDrawing();
        }
    }

    // 檢查是否點擊到其他UI元素（非畫布）
    private bool IsClickingOtherUI(Vector2 inputPosition)
    {
        if (canvas2DManager == null || canvas2DManager.canvasImage == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = inputPosition;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        // 檢查所有UI元素
        foreach (RaycastResult result in raycastResults)
        {
            // 如果是畫布，跳過檢查
            if (result.gameObject == canvas2DManager.canvasImage.gameObject)
            {
                continue;
            }

            // 如果是其他UI元素（Button, Panel, Image等）
            if (result.gameObject.GetComponent<Selectable>() != null ||
                result.gameObject.GetComponent<Button>() != null ||
                (result.gameObject.GetComponent<Graphic>() != null &&
                 result.gameObject.GetComponent<Graphic>().raycastTarget))
            {
                return true; // 點擊到其他UI元素
            }
        }

        return false;
    }

    private bool IsPointerOnCanvas(Vector2 inputPosition)
    {
        if (canvas2DManager == null || canvas2DManager.canvasImage == null)
            return false;

        RectTransform canvasRect = canvas2DManager.canvasImage.rectTransform;
        Canvas canvas = canvas2DManager.canvasImage.GetComponentInParent<Canvas>();
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        Vector2 localPoint;
        bool isInside = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, inputPosition, uiCamera, out localPoint);

        return isInside;
    }

    // 備用方法：如果上面的方法有問題，可以使用這個更嚴格的檢測
    private bool IsPointerOnCanvasStrict(Vector2 inputPosition)
    {
        if (canvas2DManager == null || canvas2DManager.canvasImage == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = inputPosition;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        // 檢查所有射線檢測結果
        foreach (RaycastResult result in raycastResults)
        {
            // 如果第一個檢測到的UI元素就是畫布，才允許繪圖
            if (result.gameObject == canvas2DManager.canvasImage.gameObject)
            {
                return true;
            }
            // 如果第一個檢測到的是其他UI元素，就不允許繪圖
            else if (result.gameObject.GetComponent<Graphic>() != null)
            {
                return false;
            }
        }

        return false;
    }

    // 最嚴格的方法：檢查是否點擊到畫布且沒有其他UI阻擋
    private bool IsPointerOnCanvasOnly(Vector2 inputPosition)
    {
        if (canvas2DManager == null || canvas2DManager.canvasImage == null)
            return false;

        PointerEventData pointerData = new PointerEventData(EventSystem.current);
        pointerData.position = inputPosition;

        List<RaycastResult> raycastResults = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        bool foundCanvas = false;

        foreach (RaycastResult result in raycastResults)
        {
            // 如果是畫布
            if (result.gameObject == canvas2DManager.canvasImage.gameObject)
            {
                foundCanvas = true;
            }
            // 如果是其他有Graphic組件的UI元素（且不是畫布）
            else if (result.gameObject.GetComponent<Graphic>() != null)
            {
                // 檢查這個UI元素是否在畫布前面（sortingOrder更高）
                Canvas otherCanvas = result.gameObject.GetComponentInParent<Canvas>();
                Canvas canvasCanvas = canvas2DManager.canvasImage.GetComponentInParent<Canvas>();

                if (otherCanvas != null && canvasCanvas != null)
                {
                    if (otherCanvas.sortingOrder > canvasCanvas.sortingOrder)
                    {
                        return false; // 有其他UI在畫布前面
                    }
                }
            }
        }

        return foundCanvas;
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
        if (lineRenderer != null)
        {
            lineRenderer.positionCount++;
            lineRenderer.SetPosition(lineRenderer.positionCount - 1, newPoint);
        }
    }

    void Handle3DParticlePainting()
    {
        Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f))
        {
            if (StraightLine)
            {
                if (textureClickEnabled && !hasProcessedClick)
                {
                    HandleTexture3DTwoPointParticle(hit);
                    hasProcessedClick = true;
                }
                return;
            }

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
            firstPoint = hit.point;
            waitingForSecondPoint = true;
            CreateTempTextureLineRenderer(hit.point);
        }
        else
        {
            Vector3 secondPoint = hit.point;

            if (hit.collider.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
            {
                DrawStraightLineOnSculptObject(firstPoint, secondPoint, hit.collider.gameObject);
            }
            else
            {
                CompleteTextureSurfaceLine(firstPoint, secondPoint);
            }

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
        if (tempLineRenderer != null)
        {
            Destroy(tempLineRenderer.gameObject);
            tempLineRenderer = null;
        }
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
        PaintManager paintManager = sculptObject.GetComponent<PaintManager>();
        if (paintManager == null)
        {
            paintManager = sculptObject.AddComponent<PaintManager>();
        }

        paintManager.paintColor = LineMaterial.color;
        paintManager.brushSize = lineWidth;

        float distance = Vector3.Distance(startPoint, endPoint);
        int steps = Mathf.RoundToInt(distance / (gapThreshold * 0.5f));
        steps = Mathf.Max(steps, 10);

        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            Vector3 interpolatedPoint = Vector3.Lerp(startPoint, endPoint, t);

            Vector3 hitPoint;
            Vector3 hitNormal;

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

        Collider objectCollider = sculptObject.GetComponent<Collider>();
        if (objectCollider == null)
        {
            return false;
        }

        Vector3 closestPoint = objectCollider.ClosestPoint(targetPoint);
        Vector3 center = objectCollider.bounds.center;
        Vector3 normal = (closestPoint - center).normalized;

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

        hitPoint = closestPoint;
        hitNormal = normal;
        return true;
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
            Vector3 temp = GetInputPosition();
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
            WarningText_Texture.SetActive(true);
            OkButton.SetActive(true);
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
            CreateParticleAtPosition(anchor);
        }
        else
        {
            anchorUpdate = true;
            UpdateAnchor();
            Vector3 secondPoint = anchor;
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

        if (float.TryParse(cleanValue, out float percentageValue))
        {
            percentageValue = Mathf.Clamp(percentageValue, 10f, 1000f);

            float actualValue = Mathf.Lerp(0.001f, 0.1f, (percentageValue - 10f) / 990f);

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

        if (float.TryParse(cleanValue, out float percentageValue))
        {
            percentageValue = Mathf.Clamp(percentageValue, 10f, 1000f);

            float actualValue = Mathf.Lerp(0.001f, 0.1f, (percentageValue - 10f) / 990f);

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
            // 將實際值 (0.001f ~ 0.1f) 轉換為百分比顯示 (10% ~ 1000%)
            float normalizedValue = Mathf.InverseLerp(0.001f, 0.1f, lineWidth);
            float displayWidth = Mathf.Lerp(10f, 1000f, normalizedValue);
            WidthInputField.text = displayWidth.ToString("F0") + "%";
        }

        if (ScaleInputField != null)
        {
            // 將實際值 (0.001f ~ 0.1f) 轉換為百分比顯示 (10% ~ 1000%)
            float normalizedValue = Mathf.InverseLerp(0.001f, 0.1f, ParticleScale);
            float displayScale = Mathf.Lerp(10f, 1000f, normalizedValue);
            ScaleInputField.text = displayScale.ToString("F0") + "%";
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
                        textureClickEnabled = true;
                        hasProcessedClick = false;
                        use = true;
                    }
                    else
                    {
                        use = true;
                    }
                    return;
                }

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
            textureClickEnabled = false;
            hasProcessedClick = false;
            lineRenderer = null;
            return;
        }

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
                    textureClickEnabled = true;
                    hasProcessedClick = false;
                    particleActive = true;
                }
                else
                {
                    Ray ray = arCamera.ScreenPointToRay(GetInputPosition());
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
        uiManager.ClearModeButton.SetActive(true);

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
        isMouseDown = false;
        isTouchActive = false;
    }
}