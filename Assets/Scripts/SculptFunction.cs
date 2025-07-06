using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum SculptMode { Carve, Fill }

public class SculptFunction : MonoBehaviour
{
    public Button TestButton;
    public Texture2D TestTexture;
    public GameObject SaveButton;
    public GameObject SaveButton2;

    public GameObject cubeCarvingSystemPrefab;
    public Transform parentObject;
    public Camera targetCamera;
    public UIManager uiManager;
    [SerializeField] private float baseForwardDistance = 1.5f;
    [SerializeField] private float downwardCheckDistance = 10f;
    [SerializeField] private float defaultHeightOffset = 0.5f;
    public GameObject CuttingTool, PreviewCuttingArea, CutButton;
    public Slider CuttingTool_A;
    private bool AreaSwitch = false;
    public Button ShapeButton_Cube, ShapeButton_Sphere, ShapeButton_Capsule, ShapeButton_Cylinder;
    public Slider MainScaleSlider, HeightSlider;
    public Text MainScaleValue, HeightValue;
    public GameObject PositionLockButton;
    public Slider ScaleXSlider, ScaleYSlider, ScaleZSlider;
    public InputField ScaleXInputField, ScaleYInputField, ScaleZInputField;
    public Slider RotationXSlider, RotationYSlider, RotationZSlider;
    public InputField RotationXInputField, RotationYInputField, RotationZInputField;
    public InputField GridInputField;
    public GameObject GenerateButtonObj;
    public Button GenerateButton, ResetButton;
    public FlexibleColorPicker fcp;
    public FlexibleColorPicker fcp_pen;
    public GameObject ImageSelector;
    public Material ColorMaterial;
    public Material TextureMaterial;
    public float defaultCubeSize = 1f;
    public int defaultGridSize = 10;
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private bool allowRotationControl = true;
    [SerializeField] private float raycastCacheTime = 0.15f;
    [SerializeField] private float distanceDetectionInterval = 0.2f;
    [SerializeField] private float maxDetectionDistance = 10f;
    private float lastDistanceDetectionTime = 0f;
    private float detectedDistance = 0f;
    private bool hasDetectedObject = false;
    private bool isCutButtonPressed = false;
    private float currentCutDistance = 0f;
    private Vector3 cuttingToolOriginalPosition;
    private bool cuttingToolInitialized = false;
    private VoxelShape selectedShape;
    private GameObject previewModel, finalModel;
    public GameObject currentSelectedObject;
    private Material originalMaterial;
    public bool isEditingExistingObject = false;
    private float mainScale = 1f, heightOffset = 0f, dynamicForwardDistance, currentRotationY = 0f;
    private Vector3 individualScale = Vector3.one;
    private Vector3 modelRotation = Vector3.zero;
    private int gridSize = 10;
    private bool isUpdatingUI = false, isRotating = false;
    private float lastUpdateTime = 0f, lastRaycastTime = 0f;
    private const float updateInterval = 0.03f;
    private Vector3 lastPreviewPosition;
    private Quaternion lastPreviewRotation;
    private Vector2 lastTouchPosition;
    private RaycastHit cachedHit;
    private bool cachedHitResult = false;
    private bool positionLock;
    private Vector3 lockedPosition;
    private int originalLayer;
    private bool useTool;
    private Vector3 originalObjectScale;
    private Vector3 originalObjectRotation;
    private Vector3 originalObjectPosition;
    private List<CubeCarvingSystem> allCarvingSystems = new List<CubeCarvingSystem>();
    private bool originalHasTexture = false;
    private Texture2D originalTexture = null;
    private Color originalColor = Color.white;
    public ObjectSaveLoadSystem objectSaveLoadSystem;
    private Vector3 lockedRotation;

    public Slider SculptScaleSlider;
    public Slider DepthScaleSlider;
    public Text SculptScaleValue;
    public Text DepthScaleValue;
    public bool isInSculptMode = false;

    private GameObject currentSculptTarget = null;
    private Vector3 sculptHitPoint;
    private bool isActiveSculpting = false;
    private int currentCarveDepth = 1;
    private int maxDepth;

    //[SerializeField] private float sculptingInterval = 0.3f;
    [SerializeField] private float sculptingDuration = 0.1f;
private float lastSculptTime = 0f;
private bool isSculptingActive = false;
private float sculptStartTime = 0f;

    public bool enablePreviewOutline = true;
    public Color previewOutlineColor = Color.cyan;
    public float previewOutlineWidth = 0.02f;

    public GameObject CarveButton;
    public GameObject FillButton;
    public SculptMode currentSculptMode = SculptMode.Carve;
    public Text SculptHint;

    [Header("Movement-based Sculpting")]
    [SerializeField] private float movementThreshold = 0.05f;  // 移動閾值距離
    [SerializeField] private float sculptingInterval = 0.1f;   // 雕刻間隔
    [SerializeField] private bool enableMovementBasedSculpting = true;

    // 移動檢測相關變數
    private Vector3 lastSculptPosition = Vector3.zero;
    private bool hasInitialSculptPosition = false;
    private float lastMovementSculptTime = 0f;

    public DrawFunction drawFunction; 

    void Start()
    {
        targetCamera = targetCamera ?? Camera.main;
        SetupAllButtonEvents();
        SetupSliderAndInputEvents();
        dynamicForwardDistance = baseForwardDistance;
        if (fcp && ColorMaterial)
        {
            //fcp.color = ColorMaterial.color;
            fcp.color = new Color(1, 1, 1, 1);
            fcp.onColorChange.AddListener(OnChangeColor);
        }
        if (CuttingTool)
        {
            cuttingToolOriginalPosition = CuttingTool.transform.localPosition;
            cuttingToolInitialized = true;
        }
        lastDistanceDetectionTime = Time.time;
        RefreshCarvingSystems();
        SetupSculptSliders();
        UpdateModeUI();
    }

    void Update()
    {
        if (!isEditingExistingObject && Input.GetMouseButtonDown(0) && !IsPointerOverUIElement() && (uiManager.UIHome && uiManager.UIHome.activeInHierarchy || drawFunction.TextureMode))
            CheckForObjectSelection();
        if ((previewModel || (isEditingExistingObject && currentSelectedObject)) && Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdatePreviewModelPosition();
        }
        if ((previewModel || isEditingExistingObject) && allowRotationControl && uiManager.SculptPanel2 && uiManager.SculptPanel2.activeInHierarchy)
            HandleRotationInput();
        if (Time.time - lastDistanceDetectionTime >= distanceDetectionInterval)
        {
            DetectSculptObjectDistance();
            lastDistanceDetectionTime = Time.time;
        }
        HandleCuttingToolMovement();
        UpdateCarvingState();

        if (isInSculptMode && Input.GetMouseButtonDown(0) && !IsPointerOverUIElement())
        {
            HandleSculptModeClick();
        }

        if (isInSculptMode && isActiveSculpting && Input.GetMouseButton(0) && !IsPointerOverUIElement())
        {
            UpdateSculptingPosition();
        }

        if (isInSculptMode && Input.GetMouseButtonUp(0) && isActiveSculpting)
        {
            StopSculpting();
        }
    }

    private void UpdateCarvingState()
    {
        if (Time.frameCount % 60 == 0) RefreshCarvingSystems();

        bool shouldCarve = false;

        if (isInSculptMode && isActiveSculpting)
        {
            float currentTime = Time.time;

            if (enableMovementBasedSculpting)
            {
                // 使用移動距離檢測
                shouldCarve = CheckMovementBasedSculpting();
            }
            else
            {
                // 原本的時間間隔檢測
                if (!isSculptingActive && currentTime - lastSculptTime >= sculptingInterval)
                {
                    isSculptingActive = true;
                    sculptStartTime = currentTime;
                    lastSculptTime = currentTime;
                    shouldCarve = true;
                }
                else if (isSculptingActive && currentTime - sculptStartTime >= sculptingDuration)
                {
                    isSculptingActive = false;
                    shouldCarve = false;
                }
                else if (isSculptingActive)
                {
                    shouldCarve = true;
                }
            }
        }

        foreach (CubeCarvingSystem system in allCarvingSystems)
        {
            if (system)
            {
                system.SetCarvingEnabled(shouldCarve);
                system.SetSculptMode(currentSculptMode == SculptMode.Fill);
            }
        }
    }

    private bool CheckMovementBasedSculpting()
    {
        if (!hasInitialSculptPosition)
        {
            // 第一次雕刻，直接觸發
            lastSculptPosition = sculptHitPoint;
            hasInitialSculptPosition = true;
            lastMovementSculptTime = Time.time;
            Debug.Log($"初次雕刻位置: {lastSculptPosition}");
            return true;
        }

        // 檢查移動距離
        float distanceMoved = Vector3.Distance(sculptHitPoint, lastSculptPosition);
        float timeSinceLastSculpt = Time.time - lastMovementSculptTime;

        Debug.Log($"移動距離: {distanceMoved:F3}, 閾值: {movementThreshold}, 時間間隔: {timeSinceLastSculpt:F2}");

        // 如果移動距離超過閾值且有最小時間間隔，則觸發雕刻
        if (distanceMoved >= movementThreshold && timeSinceLastSculpt >= sculptingInterval)
        {
            lastSculptPosition = sculptHitPoint;
            lastMovementSculptTime = Time.time;
            Debug.Log($"觸發雕刻！新位置: {lastSculptPosition}");
            return true;
        }

        return false;
    }

    private void RefreshCarvingSystems()
    {
        allCarvingSystems.Clear();
        allCarvingSystems.AddRange(FindObjectsOfType<CubeCarvingSystem>());
    }

    private void DetectSculptObjectDistance()
    {
        if (!targetCamera) return;
        Ray ray = new Ray(targetCamera.transform.position, targetCamera.transform.forward);
        int sculptObjectLayer = LayerMask.NameToLayer("SculptObject");
        int layerMask = sculptObjectLayer == -1 ? ~0 : 1 << sculptObjectLayer;
        if (Physics.Raycast(ray, out RaycastHit hit, maxDetectionDistance, layerMask))
        {
            if (sculptObjectLayer == -1 && !hit.collider.CompareTag("SculptObject"))
            {
                hasDetectedObject = false;
                detectedDistance = 0f;
                return;
            }
            hasDetectedObject = true;
            detectedDistance = hit.distance;
            if (AreaSwitch) PreviewCuttingArea.transform.localPosition = new Vector3(0, 0, detectedDistance);
        }
        else
        {
            hasDetectedObject = false;
            detectedDistance = 0f;
        }
    }

    private void HandleCuttingToolMovement()
    {
        if (!cuttingToolInitialized || !CuttingTool) return;

if (isInSculptMode && isActiveSculpting) return;

        if (isCutButtonPressed)
        {
            CuttingTool.SetActive(true);

            if (CuttingTool_A && Mathf.Approximately(CuttingTool_A.value, CuttingTool_A.minValue))
            {
                CuttingTool.transform.localPosition = new Vector3(0, 0, 0.05f);
                CuttingTool.transform.localScale = Vector3.one * 0.08f;
            }
            else
            {
                CuttingTool.transform.localPosition = new Vector3(0, 0, detectedDistance);
                if (CuttingTool_A)
                {
                    Vector3 currentScale = Vector3.one * CuttingTool_A.value;
                    CuttingTool.transform.localScale = currentScale;
                }
            }      
        }
        else
        {
            CuttingTool.SetActive(false);
            CuttingTool.transform.localPosition = new Vector3(0, 0, 0.5f);
            CuttingTool.transform.localScale = Vector3.zero;
        }
    }

    public void AreaSwitchFunction()
    {
        AreaSwitch = !AreaSwitch;
        PreviewCuttingArea.SetActive(AreaSwitch);
    }

    void CheckForObjectSelection()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        int layerMask = (1 << LayerMask.NameToLayer("SculptObject")) | (1 << LayerMask.NameToLayer("PreviewObject"));
        if (LayerMask.NameToLayer("SculptObject") == -1 || LayerMask.NameToLayer("PreviewObject") == -1) layerMask = ~0;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
        {
            GameObject hitObject = hit.collider.gameObject;
            CubeCarvingSystem carvingSystem = hitObject.GetComponent<CubeCarvingSystem>() ?? hitObject.GetComponentInParent<CubeCarvingSystem>();
            if (carvingSystem) SelectObject(carvingSystem.gameObject);
        }
    }

    public void SelectObject(GameObject obj)
    {
        if (drawFunction.TextureMode)
        {
            uiManager.FounctionUI.SetActive(true);
            uiManager.SelectObjectHint.SetActive(false);

            if (currentSelectedObject == null)
            {
                currentSelectedObject = obj;
                SetObjectGlow(currentSelectedObject, true);
            }
            else if (currentSelectedObject != null)
            {
                SetObjectGlow(currentSelectedObject, false);
                currentSelectedObject = obj;
                SetObjectGlow(currentSelectedObject, true);
            }

            DualMaterialManager dualManager = currentSelectedObject.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = currentSelectedObject.GetComponent<OBJMaterialManager>();

            if (dualManager)
            {
                fcp.color = dualManager.GetCurrentColor();
            }
            else
            {
                MeshRenderer renderer = currentSelectedObject.GetComponent<MeshRenderer>();
                if (renderer && renderer.material)
                {
                    fcp.color = renderer.material.color;
                }
            }

            if (dualManager)
            {
                dualManager.SetColor(fcp.color);
            }
            else if (objManager)
            {
                objManager.SetColor(fcp.color);
            }
            else
            {
                ApplyColorToModel(currentSelectedObject, fcp.color);
            }
        }
        else
        {
            SaveButton.SetActive(true);
            SaveButton2.SetActive(true);
            uiManager.inSculpt = true;

            DeselectCurrentObject();
            currentSelectedObject = obj;
            isEditingExistingObject = true;
            SetObjectGlow(currentSelectedObject, true);
            originalLayer = currentSelectedObject.layer;
            SetLayerRecursively(currentSelectedObject, LayerMask.NameToLayer("PreviewObject"));

            PaintManager paintManager = currentSelectedObject.GetComponent<PaintManager>();
            if (!paintManager)
            {
                paintManager = currentSelectedObject.AddComponent<PaintManager>();
                paintManager.EnsurePaintTextureExists();
            }
           
            CubeCarvingSystem carvingSystem = currentSelectedObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem)
            {
                CubeCarvingSystem.ModelState modelState = carvingSystem.GetCurrentModelState();
                LoadModelStateToUI(modelState);
                SaveOriginalState(modelState);
                if (!allCarvingSystems.Contains(carvingSystem)) allCarvingSystems.Add(carvingSystem);
                selectedShape = modelState.shapeType;
                gridSize = modelState.gridSize;
                GridInputField.interactable = false;
            }
            else
            {
                LoadParametersFromObject(currentSelectedObject);
                GridInputField.interactable = true;
            }

            uiManager.SculptPanel1?.SetActive(false);
            uiManager.SculptPanel2?.SetActive(true);
            uiManager.UIHome?.SetActive(false);
            uiManager.BackButton?.SetActive(true);
            uiManager.BackToPanel2();

            UpdateAllUIValues();
            SetDefaultPositionLockState(true);       
        }
    }

    private void LoadModelStateToUI(CubeCarvingSystem.ModelState modelState)
    {
        originalObjectPosition = modelState.position;
    originalObjectRotation = modelState.rotation;
    originalObjectScale = modelState.scale;

    Vector3 scale = modelState.scale;
    float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
    if (maxScale > 0)
    {
        mainScale = maxScale;
        individualScale = new Vector3(scale.x / maxScale, scale.y / maxScale, scale.z / maxScale);
    }
    else
    {
        individualScale = Vector3.one;
        mainScale = 1f;
    }

    Vector3 eulerAngles = modelState.rotation;
    modelRotation = new Vector3(NormalizeAngle(eulerAngles.x), NormalizeAngle(eulerAngles.y), NormalizeAngle(eulerAngles.z));
    currentRotationY = modelRotation.y;

    heightOffset = 0f;

    fcp.onColorChange.RemoveListener(OnChangeColor);
    fcp.color = modelState.color;
    ColorMaterial.color = modelState.color;
    fcp.onColorChange.AddListener(OnChangeColor);
    }

    private void SaveOriginalState(CubeCarvingSystem.ModelState modelState)
    {
        originalHasTexture = modelState.hasTexture;
        originalTexture = modelState.texture;
        originalColor = modelState.color;
    }

    void DeselectCurrentObject()
    {
        if (currentSelectedObject)
        {
            SetObjectGlow(currentSelectedObject, false);
            SetLayerRecursively(currentSelectedObject, originalLayer);
            currentSelectedObject = null;
        }
        isEditingExistingObject = false;
        GridInputField.interactable = true;
    }

    public void SyncCurrentModelColorToUI()
    {
        ColorMaterial.color = fcp.color;

        if (isEditingExistingObject && currentSelectedObject)
        {
            ApplyColorToModel(currentSelectedObject, fcp.color);
        }
        else if (previewModel)
        {
            ApplyColorToModel(previewModel, fcp.color);
        }
    }

    public void SetObjectGlow(GameObject obj, bool glow)
    {
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer && glow)
        {
            //originalMaterial = renderer.material;

            OutlineEffect outline = obj.GetComponent<OutlineEffect>();
            if (!outline)
            {
                outline = obj.AddComponent<OutlineEffect>();
            }
            outline.ToggleOutline(true);
            outline.SetOutlineColor(Color.yellow);
        }
        else
        {
            OutlineEffect outline = obj.GetComponent<OutlineEffect>();
            if (outline)
            {
                outline.ToggleOutline(false);
            }
        }
    }

    void LoadParametersFromObject(GameObject obj)
    {
        Vector3 scale = obj.transform.localScale;
    float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
    if (maxScale > 0) 
    {
        mainScale = maxScale;
        individualScale = new Vector3(scale.x / maxScale, scale.y / maxScale, scale.z / maxScale);
    }
    else 
    { 
        individualScale = Vector3.one; 
        mainScale = 1f; 
    }
    Vector3 eulerAngles = obj.transform.eulerAngles;
    modelRotation = new Vector3(NormalizeAngle(eulerAngles.x), NormalizeAngle(eulerAngles.y), NormalizeAngle(eulerAngles.z));
    currentRotationY = modelRotation.y;
    gridSize = defaultGridSize;
    heightOffset = 0f;
    bool isCarved = IsObjectCarved(obj);
    }

    void SetupAllButtonEvents()
    {
        TestButton.onClick.AddListener(() => OnTextureLoaded(TestTexture));
        ImageSelector.GetComponent<Button>().onClick.AddListener(OpenImageSelector);

        ShapeButton_Cube?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cube));
        ShapeButton_Sphere?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Sphere));
        ShapeButton_Capsule?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Capsule));
        ShapeButton_Cylinder?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cylinder));
        GenerateButton?.onClick.AddListener(OnGenerateButtonClicked);
        ResetButton?.onClick.AddListener(OnResetButtonClicked);
        PositionLockButton.GetComponent<Button>().onClick.AddListener(TogglePositionLock);

        if (CarveButton)
            CarveButton.GetComponent<Button>().onClick.AddListener(() => SetSculptMode(SculptMode.Carve));
        if (FillButton)
            FillButton.GetComponent<Button>().onClick.AddListener(() => SetSculptMode(SculptMode.Fill));

        if (CutButton)
        {
            UnityEngine.EventSystems.EventTrigger trigger = CutButton.GetComponent<UnityEngine.EventSystems.EventTrigger>() ?? CutButton.AddComponent<UnityEngine.EventSystems.EventTrigger>();
            UnityEngine.EventSystems.EventTrigger.Entry pointerDownEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerDownEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown;
            pointerDownEntry.callback.AddListener((data) => { OnCutButtonPressed(); });
            trigger.triggers.Add(pointerDownEntry);
            UnityEngine.EventSystems.EventTrigger.Entry pointerUpEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerUpEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp;
            pointerUpEntry.callback.AddListener((data) => { OnCutButtonReleased(); });
            trigger.triggers.Add(pointerUpEntry);
            UnityEngine.EventSystems.EventTrigger.Entry pointerExitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
            pointerExitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
            pointerExitEntry.callback.AddListener((data) => { OnCutButtonReleased(); });
            trigger.triggers.Add(pointerExitEntry);
        }
    }

    void SetupSliderAndInputEvents()
    {
        SetupSlider(MainScaleSlider, 0.1f, 3f, 1f, OnMainScaleChanged);
        SetupSlider(HeightSlider, -1f, 1f, 0f, OnHeightChanged);
        SetupSlider(ScaleXSlider, 0.01f, 3f, 1f, OnScaleXSliderChanged);
        SetupSlider(ScaleYSlider, 0.01f, 3f, 1f, OnScaleYSliderChanged);
        SetupSlider(ScaleZSlider, 0.01f, 3f, 1f, OnScaleZSliderChanged);
        SetupSlider(RotationXSlider, 0f, 360f, 0f, OnRotationXSliderChanged);
        SetupSlider(RotationYSlider, 0f, 360f, 0f, OnRotationYSliderChanged);
        SetupSlider(RotationZSlider, 0f, 360f, 0f, OnRotationZSliderChanged);
        if (CuttingTool_A)
        {
            CuttingTool_A.minValue = 0.001f;
            CuttingTool_A.maxValue = 0.3f;
            CuttingTool_A.value = 0.05f;
            CuttingTool_A.onValueChanged.AddListener(OnCuttingHeadScaleChanged);
        }
        SetupInputField(ScaleXInputField, "1.00", OnScaleXInputChanged);
        SetupInputField(ScaleYInputField, "1.00", OnScaleYInputChanged);
        SetupInputField(ScaleZInputField, "1.00", OnScaleZInputChanged);
        SetupInputField(RotationXInputField, "0", OnRotationXInputChanged);
        SetupInputField(RotationYInputField, "0", OnRotationYInputChanged);
        SetupInputField(RotationZInputField, "0", OnRotationZInputChanged);
        SetupInputField(GridInputField, "10", OnGridInputChanged);
    }

    void SetupSculptSliders()
    {
        if (SculptScaleSlider)
    {
        SculptScaleSlider.minValue = 1f;
        SculptScaleSlider.maxValue = 5f;
        SculptScaleSlider.value = 1f;
        SculptScaleSlider.wholeNumbers = true;
        SculptScaleSlider.onValueChanged.AddListener(OnSculptScaleChanged);
    }
        
        UpdateSculptScaleValues();
    }

    public void SetSculptMode(SculptMode mode)
    {
        currentSculptMode = mode;
        UpdateModeUI();
        UpdateAllCarvingSystems();

        if(mode == SculptMode.Carve)
        {
            SculptHint.text = "點擊模型進行雕刻";
        }
        else if(mode == SculptMode.Fill)
        {
            SculptHint.text = "點擊模型進行填補";
        }
    }

    private void UpdateModeUI()
    {
        if (CarveButton)
        {
            CarveButton.GetComponent<Image>().color = currentSculptMode == SculptMode.Carve ? 
                new Color(143f / 255f, 255f / 255f, 196f / 255f) : 
                new Color(128f / 255f, 128f / 255f, 128f / 255f);            
        }
        
        if (FillButton)
        {
            FillButton.GetComponent<Image>().color = currentSculptMode == SculptMode.Fill ? 
                new Color(143f / 255f, 255f / 255f, 196f / 255f) : 
                new Color(128f / 255f, 128f / 255f, 128f / 255f);           
        }
    }
    
    private void UpdateAllCarvingSystems()
    {
        foreach (CubeCarvingSystem system in allCarvingSystems)
        {
            if (system)
            {
                system.SetSculptMode(currentSculptMode == SculptMode.Fill);
            }
        }
    }

    private void OnCuttingHeadScaleChanged(float scaleValue)
{
    if (!CuttingTool || !PreviewCuttingArea) return;

    if (isInSculptMode)
    {
        float sculptScale = GetSculptScaleValue();
        
        Vector3 newScale = new Vector3(sculptScale, sculptScale, sculptScale);
        PreviewCuttingArea.transform.localScale = newScale;

        if (isCutButtonPressed)
        {
            CuttingTool.transform.localScale = newScale;
            CuttingTool.transform.localPosition = new Vector3(0, 0, detectedDistance);
        }
        else
        {
            CuttingTool.transform.localScale = Vector3.zero;
        }

        CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
        if (carvingTool)
        {
            carvingTool.SetToolSize(newScale);
        }
    }
    else
    {
        float actualScale = Mathf.Approximately(scaleValue, CuttingTool_A.minValue) ? 0.08f : scaleValue;
        Vector3 newScale = Vector3.one * scaleValue;
        PreviewCuttingArea.transform.localScale = newScale;

        if (isCutButtonPressed)
        {
            CuttingTool.transform.localScale = newScale;

            if (Mathf.Approximately(scaleValue, CuttingTool_A.minValue))
            {
                CuttingTool.transform.localPosition = new Vector3(0, 0, 0.05f);
            }
            else
            {
                CuttingTool.transform.localPosition = new Vector3(0, 0, detectedDistance);
            }
        }
        else
        {
            CuttingTool.transform.localScale = Vector3.zero;
        }

        CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
        if (carvingTool)
        {
            Vector3 toolSize = Vector3.one * scaleValue * 0.2f;
            carvingTool.SetToolSize(toolSize);
        }
    }
}

private float GetSculptScaleValue()
{
    int level = SculptScaleSlider ? Mathf.RoundToInt(SculptScaleSlider.value) : 1;
    
    switch (level)
    {
        case 1: return 0.01f;
        case 2: return 0.1f;
        case 3: return 0.2f;
        case 4: return 0.3f;
        case 5: return 0.4f;
        default: return 0.01f;
    }
}

    private void OnCutButtonPressed() => isCutButtonPressed = true;
    private void OnCutButtonReleased() => isCutButtonPressed = false;

    private void OnChangeColor(Color co)
    {
        ColorMaterial.color = co;

        if (drawFunction.TextureMode && currentSelectedObject)
        {
            DualMaterialManager dualManager = currentSelectedObject.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = currentSelectedObject.GetComponent<OBJMaterialManager>();
            if (dualManager)
            {
                dualManager.SetColor(co);
            }
            else if (objManager)
            {
                objManager.SetColor(co);
            }
            else
            {
                ApplyColorToModel(currentSelectedObject, co);
            }
        }
        else if (previewModel)
        {
            DualMaterialManager dualManager = previewModel.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = previewModel.GetComponent<OBJMaterialManager>();

            if (dualManager)
            {
                dualManager.SetColor(co);
            }
            else if (objManager)
            {
                objManager.SetColor(co);
            }
            else
            {
                ApplyColorToModel(previewModel, co);
            }
        }
    }

    private void ApplyColorToCurrentModel(Color color)
    {
        GameObject targetModel = (isEditingExistingObject && currentSelectedObject) ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            PaintManager paintManager = targetModel.GetComponent<PaintManager>();
            if (!paintManager) ApplyColorToModel(targetModel, color);
        }
    }

    private void ApplyColorToModel(GameObject model, Color color)
    {
        PaintManager paintManager = model.GetComponent<PaintManager>();
        if (paintManager != null)
        {
            return;
        }

        CubeCarvingSystem carvingSystem = model.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            MeshRenderer renderer = model.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material currentMaterial = renderer.material;

                if (currentMaterial.HasProperty("_ColorTint"))
                {
                    currentMaterial.SetColor("_ColorTint", color);
                }
                else if (currentMaterial.HasProperty("_Color"))
                {
                    currentMaterial.SetColor("_Color", color);
                }
                else if (currentMaterial.HasProperty("_BaseColor"))
                {
                    currentMaterial.SetColor("_BaseColor", color);
                }
                else if (currentMaterial.HasProperty("_MainColor"))
                {
                    currentMaterial.SetColor("_MainColor", color);
                }
                else if (currentMaterial.HasProperty("_Albedo"))
                {
                    currentMaterial.SetColor("_Albedo", color);
                }
                else
                {
                    currentMaterial.color = color;
                }
            }
        }
        else
        {
            MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material currentMaterial = renderer.material;

                    if (currentMaterial.HasProperty("_ColorTint"))
                    {
                        currentMaterial.SetColor("_ColorTint", color);
                    }
                    else if (currentMaterial.HasProperty("_Color"))
                    {
                        currentMaterial.SetColor("_Color", color);
                    }
                    else if (currentMaterial.HasProperty("_BaseColor"))
                    {
                        currentMaterial.SetColor("_BaseColor", color);
                    }
                    else if (currentMaterial.HasProperty("_MainColor"))
                    {
                        currentMaterial.SetColor("_MainColor", color);
                    }
                    else if (currentMaterial.HasProperty("_Albedo"))
                    {
                        currentMaterial.SetColor("_Albedo", color);
                    }
                    else
                    {
                        currentMaterial.color = color;
                    }
                }
            }
        }
    }

    void SetupSlider(Slider slider, float min, float max, float defaultValue, UnityEngine.Events.UnityAction<float> callback)
    {
        if (!slider) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.onValueChanged.AddListener(callback);
    }

    void SetupInputField(InputField inputField, string defaultValue, UnityEngine.Events.UnityAction<string> callback)
    {
        if (!inputField) return;
        inputField.text = defaultValue;
        inputField.onEndEdit.AddListener(callback);
    }

    void OnShapeSelected(VoxelShape shape)
    {
        selectedShape = shape;
        currentRotationY = 0f;
        modelRotation = Vector3.zero;
        isEditingExistingObject = false;
        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(true);
        uiManager.FunctionUISwitch();
        uiManager.isGroundChecking = true;
        uiManager.lightshipNavMeshRenderer.enabled = true;
        uiManager.GroundCheck.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        CreatePreviewModel();
        UpdateRotationYUI();
        SetDefaultPositionLockState(false);
    }

    void CreatePreviewModel()
    {
        if (previewModel) Destroy(previewModel);
        Vector3 currentScale = new Vector3(mainScale * individualScale.x, mainScale * individualScale.y, mainScale * individualScale.z);
        previewModel = GenerateShapeWithParameters(selectedShape, currentScale, gridSize, true);
        if (previewModel)
        {
            ApplyColorToModel(previewModel, fcp.color);
            SetLayerRecursively(previewModel, LayerMask.NameToLayer("PreviewObject"));
            AddPreviewOutline(previewModel);
            CalculateDynamicForwardDistance();
        }
    }

    public GameObject GenerateShapeWithParameters(VoxelShape shapeType, Vector3 scale, int gridSize, bool isPreview = false)
    {
        if (!cubeCarvingSystemPrefab) return null;
    Vector3 spawnPosition = GetSpawnPosition();
    GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);
    newCarvingSystem.transform.localScale = scale;
    newCarvingSystem.name = $"{shapeType}{(isPreview ? "_Preview" : "_Final")}";

    CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
    if (carvingSystem)
    {
        carvingSystem.SetParameters(defaultCubeSize, gridSize, shapeType);
        if (!allCarvingSystems.Contains(carvingSystem)) allCarvingSystems.Add(carvingSystem);
    }

    DualMaterialManager dualManager = newCarvingSystem.GetComponent<DualMaterialManager>();
    if (!dualManager) dualManager = newCarvingSystem.AddComponent<DualMaterialManager>();

    StartCoroutine(AddPaintManagerAfterFrame(newCarvingSystem, isPreview));

    if (isPreview)
    {
        SetLayerRecursively(newCarvingSystem, LayerMask.NameToLayer("PreviewObject"));
        if (dualManager)
        {
            dualManager.SetColor(fcp.color);
        }
    }
    else
    {
        SetLayerRecursively(newCarvingSystem, LayerMask.NameToLayer("SculptObject"));
        newCarvingSystem.tag = "SculptObject";
    }

    return newCarvingSystem;
    }

    private System.Collections.IEnumerator AddPaintManagerAfterFrame(GameObject obj, bool isPreview)
{
    yield return null;
    yield return null;
    
    PaintManager paintManager = obj.GetComponent<PaintManager>();
    if (!paintManager) 
    {
        paintManager = obj.AddComponent<PaintManager>();
    }
    
    yield return null;
    
    if (paintManager != null)
    {
        paintManager.EnsurePaintTextureExists();
    }
}

    private Vector3 GetSpawnPosition() => targetCamera ? targetCamera.transform.position + targetCamera.transform.forward * 1.5f : Vector3.forward;

    void SetMaterialAndLayer(GameObject obj, Material material, string layerName)
    {
        DualMaterialManager dualManager = obj.GetComponent<DualMaterialManager>();
        if (material && (!dualManager || !dualManager.IsInTextureMode()))
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer) renderer.material = material;
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1) SetLayerRecursively(obj, layer);
        else if (layerName == "SculptObject") SetLayerRecursively(obj, 0);
        if (layerName == "SculptObject") obj.tag = "SculptObject";
    }

    void UpdatePreviewModelPosition()
    {
        if (!Camera.main) return;
        if (isEditingExistingObject && currentSelectedObject)
        {
            if (positionLock)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + heightOffset;
                currentSelectedObject.transform.position = newPosition;
                currentSelectedObject.transform.rotation = Quaternion.Euler(modelRotation);
            }
            else
            {
                CalculateDynamicForwardDistanceForObject(currentSelectedObject);
                UpdateObjectToFollowCamera(currentSelectedObject);
            }
            return;
        }
        if (previewModel)
        {
            if (positionLock)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + heightOffset;
                previewModel.transform.position = newPosition;
                previewModel.transform.rotation = Quaternion.Euler(modelRotation);
            }
            else UpdateObjectToFollowCamera(previewModel);
        }
    }

    void CalculateDynamicForwardDistanceForObject(GameObject obj)
    {
        if (!obj) return;
        Vector3 scale = obj.transform.localScale;
        float actualZScale = scale.z;
        float zScaleDifference = actualZScale - 1.0f;
        dynamicForwardDistance = Mathf.Max(baseForwardDistance + zScaleDifference, 0.5f);
    }

    private Vector3 GetHorizontalForward()
    {
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 horizontalForward = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
        if (horizontalForward.magnitude < 0.1f) horizontalForward = Vector3.forward;
        return horizontalForward;
    }

    private void UpdateObjectToFollowCamera(GameObject targetObject)
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 horizontalForward = GetHorizontalForward();

        bool shouldRaycast = Time.time - lastRaycastTime > raycastCacheTime;
        if (shouldRaycast)
        {
            if (uiManager.isGroundChecking)
            {
                Vector3 rayDirection = Camera.main.transform.forward;
                Ray ray = new Ray(cameraPosition, rayDirection);
                int layerMask = LayerMask.NameToLayer("PreviewObject") == -1 ? ~0 : ~(1 << LayerMask.NameToLayer("PreviewObject"));

                if (Physics.Raycast(ray, out cachedHit, maxDetectionDistance, layerMask))
                {
                    cachedHitResult = true;

                    float hitDistance = Vector3.Distance(cameraPosition, cachedHit.point);

                    float minDistance = 0.3f;
                    float maxDistance = 8f;

                    if (hitDistance < minDistance)
                    {
                        cachedHit.point = cameraPosition + rayDirection * minDistance;
                    }
                    else if (hitDistance > maxDistance)
                    {
                        cachedHit.point = cameraPosition + rayDirection * maxDistance;
                    }
                }
                else
                {
                    cachedHitResult = true;
                    cachedHit.point = cameraPosition + rayDirection * 2f;
                }
            }
            else
            {
                Vector3 rayOrigin = cameraPosition + horizontalForward * dynamicForwardDistance;
                Ray ray = new Ray(rayOrigin, Vector3.down);
                int layerMask = LayerMask.NameToLayer("PreviewObject") == -1 ? ~0 : ~(1 << LayerMask.NameToLayer("PreviewObject"));
                cachedHitResult = Physics.Raycast(ray, out cachedHit, downwardCheckDistance, layerMask);
            }
            lastRaycastTime = Time.time;
        }

        Vector3 targetPosition = (cachedHitResult && uiManager.isGroundChecking) ?
            GetAdaptiveGroundPosition(cachedHit.point, targetObject) :
            GetDefaultPosition(cameraPosition, horizontalForward);

        targetObject.transform.position = targetPosition;
        targetObject.transform.rotation = GetModelRotation(horizontalForward);
        lastPreviewPosition = targetObject.transform.position;
        lastPreviewRotation = targetObject.transform.rotation;
    }

    Vector3 GetAdaptiveGroundPosition(Vector3 hitPoint, GameObject targetObject)
    {
        Vector3 position = hitPoint;

        Renderer renderer = targetObject.GetComponent<Renderer>();
        if (renderer)
        {
            float halfHeight = renderer.bounds.extents.y;
            position.y += halfHeight;
        }

        position.y += heightOffset;

        return position;
    }

    Vector3 GetGroundPositionForObject(Vector3 hitPoint, GameObject targetObject)
    {
        Vector3 position = hitPoint;
        Renderer renderer = targetObject.GetComponent<Renderer>();
        if (renderer)
        {
            float halfHeight = renderer.bounds.extents.y;
            position.y += halfHeight;
        }
        position.y += heightOffset;
        return position;
    }

    Vector3 GetDefaultPosition(Vector3 cameraPosition, Vector3 horizontalForward)
    {
        Vector3 position = cameraPosition + horizontalForward * dynamicForwardDistance;
        position.y = cameraPosition.y - defaultHeightOffset + heightOffset;
        return position;
    }

    Quaternion GetModelRotation(Vector3 horizontalForward)
    {
        if (positionLock) return Quaternion.Euler(modelRotation);
        Quaternion baseRotation = horizontalForward != Vector3.zero ? Quaternion.LookRotation(horizontalForward) : Quaternion.identity;
        Quaternion uiRotation = Quaternion.Euler(modelRotation.x, modelRotation.y, modelRotation.z);
        return baseRotation * uiRotation;
    }

    void CalculateDynamicForwardDistance()
    {
        float actualZScale = mainScale * individualScale.z;
        float zScaleDifference = actualZScale - 1.0f;
        dynamicForwardDistance = Mathf.Max(baseForwardDistance + zScaleDifference, 0.5f);
    }

    public void TogglePositionLock()
    {
        positionLock = !positionLock;
        if (positionLock)
        {
            if (isEditingExistingObject && currentSelectedObject)
            {
                lockedPosition = currentSelectedObject.transform.position;
                lockedPosition.y -= heightOffset;
                //lockedRotation = currentSelectedObject.transform.eulerAngles;
                Vector3 currentEuler = currentSelectedObject.transform.eulerAngles;
                modelRotation = new Vector3(
                    NormalizeAngle(currentEuler.x), 
                    NormalizeAngle(currentEuler.y), 
                    NormalizeAngle(currentEuler.z)
                );
                currentRotationY = modelRotation.y;
            }
            else if (previewModel)
            { 
                lockedPosition = previewModel.transform.position;
                //lockedRotation = previewModel.transform.eulerAngles;
                Vector3 currentEuler = previewModel.transform.eulerAngles;
                modelRotation = new Vector3(
                    NormalizeAngle(currentEuler.x), 
                    NormalizeAngle(currentEuler.y), 
                    NormalizeAngle(currentEuler.z)
                );
                currentRotationY = modelRotation.y;
            }
        }
        else if (isEditingExistingObject && currentSelectedObject) CalculateDynamicForwardDistanceForObject(currentSelectedObject);
        UpdatePositionLockUI();
    }

    private void UpdatePositionLockUI()
    {
        if (!PositionLockButton) return;
        PositionLockButton.GetComponent<Image>().color = positionLock ? new Color(143f / 255f, 255f / 255f, 196f / 255f) : new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    private void SetDefaultPositionLockState(bool isEditMode)
    {
        if (isEditMode)
        {
            positionLock = true;
            lockedPosition = currentSelectedObject.transform.position;
            lockedPosition.y -= heightOffset;
        }
        else positionLock = false;
        UpdatePositionLockUI();
    }

    void HandleRotationInput()
    {
        if (IsPointerOverUIElement()) { isRotating = false; return; }
#if UNITY_EDITOR
        HandleMouseRotation();
#else
        HandleTouchRotation();
#endif
    }

    void HandleMouseRotation()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isRotating = true;
            lastTouchPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButton(0) && isRotating)
        {
            Vector2 deltaPosition = (Vector2)Input.mousePosition - lastTouchPosition;
            currentRotationY -= deltaPosition.x * rotationSpeed;
            currentRotationY = NormalizeAngle(currentRotationY);
            modelRotation.y = currentRotationY;
            UpdateRotationYUI();
            lastTouchPosition = Input.mousePosition;
        }
        else if (Input.GetMouseButtonUp(0)) isRotating = false;
    }

    void HandleTouchRotation()
    {
        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    isRotating = true;
                    lastTouchPosition = touch.position;
                    break;
                case TouchPhase.Moved:
                    if (isRotating)
                    {
                        Vector2 deltaPosition = touch.position - lastTouchPosition;
                        currentRotationY -= deltaPosition.x * rotationSpeed;
                        currentRotationY = NormalizeAngle(currentRotationY);
                        modelRotation.y = currentRotationY;
                        UpdateRotationYUI();
                        lastTouchPosition = touch.position;
                    }
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isRotating = false;
                    break;
            }
        }
    }

    bool IsPointerOverUIElement()
    {
        if (!UnityEngine.EventSystems.EventSystem.current) return false;
#if UNITY_EDITOR
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#else
        return Input.touchCount > 0 && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif
    }

    void OnMainScaleChanged(float value)
    {
        if (isUpdatingUI) return;
    mainScale = value;
    if (MainScaleValue) MainScaleValue.text = $"{Mathf.RoundToInt(value * 100)}%";
    UpdateTargetModel();
    }

    void SyncMainScaleToIndividualSliders(float value)
    {
        isUpdatingUI = true;
        if (ScaleXSlider) ScaleXSlider.value = value;
        if (ScaleYSlider) ScaleYSlider.value = value;
        if (ScaleZSlider) ScaleZSlider.value = value;
        isUpdatingUI = false;
    }

    void OnHeightChanged(float value)
    {
        if (isUpdatingUI) return;
        heightOffset = value;
        if (HeightValue) HeightValue.text = value.ToString("F2");
        if (positionLock)
        {
            if (isEditingExistingObject && currentSelectedObject)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + value;
                currentSelectedObject.transform.position = newPosition;
            }
            else if (previewModel)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + value;
                previewModel.transform.position = newPosition;
            }
        }
    }

    void OnScaleXSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        individualScale.x = value;
        if (ScaleXInputField) ScaleXInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleYSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        individualScale.y = value;
        if (ScaleYInputField) ScaleYInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleZSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        individualScale.z = value;
        if (ScaleZInputField) ScaleZInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleXInputChanged(string value) => HandleScaleInputChange(value, 0);
    void OnScaleYInputChanged(string value) => HandleScaleInputChange(value, 1);
    void OnScaleZInputChanged(string value) => HandleScaleInputChange(value, 2);

    void OnRotationXSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.x = value;
        if (RotationXInputField) RotationXInputField.text = Mathf.RoundToInt(value).ToString();
        UpdateTargetModel();
    }

    void OnRotationYSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.y = value;
        currentRotationY = value;
        if (RotationYInputField) RotationYInputField.text = Mathf.RoundToInt(value).ToString();
        UpdateTargetModel();
    }

    void OnRotationZSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.z = value;
        if (RotationZInputField) RotationZInputField.text = Mathf.RoundToInt(value).ToString();
        UpdateTargetModel();
    }

    void OnRotationXInputChanged(string value) => HandleRotationInputChange(value, 0);
    void OnRotationYInputChanged(string value) => HandleRotationInputChange(value, 1);
    void OnRotationZInputChanged(string value) => HandleRotationInputChange(value, 2);

    void HandleRotationInputChange(string value, int axis)
    {
        if (isUpdatingUI || !float.TryParse(value, out float result)) return;
        result = NormalizeAngle(result);
        switch (axis)
        {
            case 0:
                modelRotation.x = result;
                UpdateRotationSliderAndInput(RotationXSlider, RotationXInputField, result);
                break;
            case 1:
                modelRotation.y = result;
                currentRotationY = result;
                UpdateRotationSliderAndInput(RotationYSlider, RotationYInputField, result);
                break;
            case 2:
                modelRotation.z = result;
                UpdateRotationSliderAndInput(RotationZSlider, RotationZInputField, result);
                break;
        }
        UpdateTargetModel();
    }

    void UpdateRotationSliderAndInput(Slider slider, InputField inputField, float value)
    {
        isUpdatingUI = true;
        if (slider) slider.value = value;
        if (inputField) inputField.text = Mathf.RoundToInt(value).ToString();
        isUpdatingUI = false;
    }

    float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle < 0) angle += 360f;
        return angle;
    }

    void UpdateRotationYUI()
    {
        if (isUpdatingUI) return;
        isUpdatingUI = true;
        if (RotationYSlider) RotationYSlider.value = modelRotation.y;
        if (RotationYInputField) RotationYInputField.text = Mathf.RoundToInt(modelRotation.y).ToString();
        isUpdatingUI = false;
    }

    void HandleScaleInputChange(string value, int axis)
    {
        if (isUpdatingUI || !float.TryParse(value, out float result)) return;
        result = Mathf.Clamp(result, 0.1f, 3f);
        switch (axis)
        {
            case 0: individualScale.x = result; UpdateSliderAndInput(ScaleXSlider, ScaleXInputField, result); break;
            case 1: individualScale.y = result; UpdateSliderAndInput(ScaleYSlider, ScaleYInputField, result); break;
            case 2: individualScale.z = result; UpdateSliderAndInput(ScaleZSlider, ScaleZInputField, result); break;
        }
        UpdateTargetModel();
    }

    void UpdateSliderAndInput(Slider slider, InputField inputField, float value)
    {
        isUpdatingUI = true;
        if (slider) slider.value = value;
        if (inputField) inputField.text = value.ToString("F2");
        isUpdatingUI = false;
    }

    void OnGridInputChanged(string value)
    {
        if (isUpdatingUI || !int.TryParse(value, out int result)) return;
        result = Mathf.Clamp(result, 1, 100);
        gridSize = result;
        if (GridInputField) GridInputField.text = result.ToString();
        UpdateTargetModel();
    }

    void OnGenerateButtonClicked()
    {
        if (isEditingExistingObject) ApplyEditChanges();
        else CreateNewObject();
        SwitchToHome();

        SaveButton.SetActive(false);
        SaveButton2.SetActive(false);
    }

    void CreateNewObject()
    {
        if (previewModel)
        {
            lastPreviewPosition = previewModel.transform.position;
            lastPreviewRotation = previewModel.transform.rotation;

            DualMaterialManager previewDualManager = previewModel.GetComponent<DualMaterialManager>();
            bool hasTexture = false;
            Texture2D currentTexture = null;
            Color currentColor = fcp.color;

            if (previewDualManager)
            {
                hasTexture = previewDualManager.IsInTextureMode();
                currentTexture = previewDualManager.GetCurrentTexture();
                currentColor = previewDualManager.GetCurrentColor();
            }
            RemovePreviewOutline(previewModel);

            Destroy(previewModel);
            previewModel = null;

            Vector3 finalScale = new Vector3(mainScale * individualScale.x, mainScale * individualScale.y, mainScale * individualScale.z);
            finalModel = GenerateShapeWithParameters(selectedShape, finalScale, gridSize, false);

            if (finalModel)
            {
                finalModel.transform.position = lastPreviewPosition;
                finalModel.transform.rotation = lastPreviewRotation;
                SetLayerRecursively(finalModel, LayerMask.NameToLayer("SculptObject"));
                finalModel.tag = "SculptObject";

                DualMaterialManager finalDualManager = finalModel.GetComponent<DualMaterialManager>();
                if (finalDualManager)
                {
                    if (hasTexture && currentTexture)
                    {
                        finalDualManager.SetTextureMode(currentTexture);
                        finalDualManager.SetColor(currentColor);
                    }
                    else
                    {
                        finalDualManager.SetPaintMode();
                        finalDualManager.SetColor(currentColor);
                    }
                }

                CubeCarvingSystem carvingSystem = finalModel.GetComponent<CubeCarvingSystem>();
                if (carvingSystem)
                {
                    carvingSystem.UpdateModelInfo(
                        position: lastPreviewPosition,
                        rotation: lastPreviewRotation.eulerAngles,
                        scale: finalScale,
                        color: currentColor,
                        hasTexture: hasTexture,
                        texture: currentTexture
                    );

                    carvingSystem.CommitCurrentState();
                }
            }
        }
    }

    void ApplyEditChanges()
    {
        if (currentSelectedObject == null) return;

        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        currentSelectedObject.transform.localScale = finalScale;
        currentSelectedObject.transform.rotation = Quaternion.Euler(modelRotation);
        SetLayerRecursively(currentSelectedObject, LayerMask.NameToLayer("SculptObject"));

        PaintManager paintManager = currentSelectedObject.GetComponent<PaintManager>();
        if (paintManager == null)
        {
            ApplyColorToModel(currentSelectedObject, fcp.color);
        }

        ModelStat modelStat = currentSelectedObject.GetComponent<ModelStat>();
        if (modelStat != null)
        {
            Color colorToSave = fcp.color;
            if (paintManager != null)
            {
                colorToSave = modelStat.ModelData.materialColor;
            }

            ModelData updatedData = new ModelData
            {
                filename = currentSelectedObject.name,
                shapeType = selectedShape.ToString(),
                position = currentSelectedObject.transform.position,
                rotation = modelRotation,
                scale = currentSelectedObject.transform.localScale,
                materialColor = colorToSave,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            modelStat.SetModelData(updatedData);
        }

        DeselectCurrentObject();
    }

    private IEnumerator ApplyColorAfterMeshGeneration(GameObject model, Color color)
    {
        yield return null;
        yield return null;
        ApplyColorToModel(model, color);
    }

    void OnResetButtonClicked()
    {
        if (isEditingExistingObject) ResetToOriginalParameters();
        else ResetAllParameters();
        UpdateAllUIValues();
        if (isEditingExistingObject)
        {
            if (currentSelectedObject)
            {
                currentSelectedObject.transform.localScale = originalObjectScale;
                currentSelectedObject.transform.rotation = Quaternion.Euler(originalObjectRotation);
                currentSelectedObject.transform.position = originalObjectPosition;
                lockedPosition = originalObjectPosition;
            }
        }
        else CreatePreviewModel();
    }

    void ResetToOriginalParameters()
    {
        if (!currentSelectedObject) return;
    Vector3 scale = originalObjectScale;
    float maxScale = Mathf.Max(scale.x, scale.y, scale.z);
    if (maxScale > 0) 
    {
        mainScale = maxScale;
        individualScale = new Vector3(scale.x / maxScale, scale.y / maxScale, scale.z / maxScale);
    }
    else 
    { 
        individualScale = Vector3.one; 
        mainScale = 1f; 
    }
    modelRotation = new Vector3(NormalizeAngle(originalObjectRotation.x), NormalizeAngle(originalObjectRotation.y), NormalizeAngle(originalObjectRotation.z));
    currentRotationY = modelRotation.y;
    gridSize = defaultGridSize;
    heightOffset = 0f;
    }

    public void OnBackButtonClicked()
    {
        if (!uiManager.isInColorPage)
        {
            if (isEditingExistingObject) DeselectCurrentObject();
            if (previewModel) { Destroy(previewModel); previewModel = null; }
        }
    }

    void SwitchToHome()
    {
        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);
        isEditingExistingObject = false;
        uiManager.inSculpt = false;
        uiManager.lightshipNavMeshRenderer.enabled = false;
        uiManager.ClearModeButton.SetActive(true);
        //uiManager.isInColorPage = false;
        //uiManager.ColorPage1.SetActive(false);
        //uiManager.ScalePageSelect();
    }

    void ResetAllParameters()
    {
        mainScale = 1f;
        individualScale = Vector3.one;
        gridSize = 10;
        currentRotationY = 0f;
        heightOffset = 0f;
        modelRotation = Vector3.zero;
    }

    void UpdateTargetModel()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
    if (!targetModel) return;
    Vector3 finalScale = new Vector3(mainScale * individualScale.x, mainScale * individualScale.y, mainScale * individualScale.z);
    targetModel.transform.localScale = finalScale;
    if (positionLock) targetModel.transform.rotation = Quaternion.Euler(modelRotation);
    if (isEditingExistingObject) return;
    if (previewModel)
    {
        CubeCarvingSystem carvingSystem = targetModel.GetComponent<CubeCarvingSystem>();
        if (carvingSystem) carvingSystem.SetParameters(1f, gridSize, selectedShape);
        CalculateDynamicForwardDistance();
    }
    }

    private bool IsObjectCarved(GameObject obj)
    {
        CubeCarvingSystem carvingSystem = obj.GetComponent<CubeCarvingSystem>();
        if (!carvingSystem) return false;
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        return meshFilter?.mesh && meshFilter.mesh.vertexCount > 0;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (!obj) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
    }

    public void Clear()
    {
        if (currentSelectedObject)
        {
            CubeCarvingSystem carvingSystem = currentSelectedObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem) allCarvingSystems.Remove(carvingSystem);
            Destroy(currentSelectedObject);
            currentSelectedObject = null;
            isEditingExistingObject = false;
            SwitchToHome();
        }
    }

    void OnDestroy() => allCarvingSystems.Clear();

    public void CancelEditChanges()
    {
        if (!isEditingExistingObject || !currentSelectedObject) return;

        //CubeCarvingSystem carvingSystem = currentSelectedObject.GetComponent<CubeCarvingSystem>();
        //if (carvingSystem)
        //{
        //    carvingSystem.RevertToSavedState();
        //}
        //
        //fcp.onColorChange.RemoveListener(OnChangeColor);
        //fcp.color = originalColor;
        //ColorMaterial.color = originalColor;
        //fcp.onColorChange.AddListener(OnChangeColor);

        SetLayerRecursively(currentSelectedObject, originalLayer);

        DeselectCurrentObject();
        isEditingExistingObject = false;

        SaveButton.SetActive(false);
        SaveButton2.SetActive(false);
    }

    public void UpdateAllUIValues()
    {
        isUpdatingUI = true;
        if (MainScaleSlider) MainScaleSlider.value = mainScale;
        if (HeightSlider) HeightSlider.value = heightOffset;
        if (ScaleXSlider) ScaleXSlider.value = individualScale.x;
        if (ScaleYSlider) ScaleYSlider.value = individualScale.y;
        if (ScaleZSlider) ScaleZSlider.value = individualScale.z;
        if (RotationXSlider) RotationXSlider.value = modelRotation.x;
        if (RotationYSlider) RotationYSlider.value = modelRotation.y;
        if (RotationZSlider) RotationZSlider.value = modelRotation.z;
        if (MainScaleValue) MainScaleValue.text = $"{Mathf.RoundToInt(mainScale * 100)}%";
        if (HeightValue) HeightValue.text = heightOffset.ToString("F2");
        UpdateIndividualScaleInputs();
        UpdateRotationInputs();
        UpdateGridInput();
        isUpdatingUI = false;
    }

    void UpdateIndividualScaleInputs()
    {
        if (ScaleXInputField) ScaleXInputField.text = individualScale.x.ToString("F2");
        if (ScaleYInputField) ScaleYInputField.text = individualScale.y.ToString("F2");
        if (ScaleZInputField) ScaleZInputField.text = individualScale.z.ToString("F2");
    }

    void UpdateRotationInputs()
    {
        if (RotationXInputField) RotationXInputField.text = Mathf.RoundToInt(modelRotation.x).ToString();
        if (RotationYInputField) RotationYInputField.text = Mathf.RoundToInt(modelRotation.y).ToString();
        if (RotationZInputField) RotationZInputField.text = Mathf.RoundToInt(modelRotation.z).ToString();
    }

    void UpdateGridInput()
    {
        if (GridInputField) GridInputField.text = gridSize.ToString();
    }

    private void RestoreObjectOriginalColor(GameObject obj)
    {
        if (!obj) return;

        CubeCarvingSystem carvingSystem = obj.GetComponent<CubeCarvingSystem>();
        if (carvingSystem)
        {
            carvingSystem.RevertToSavedState();
        }
    }

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
            }, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        }
    }

    private void PickImageFromGallery()
    {
        NativeGallery.GetImageFromGallery((path) =>
        {
            if (path != null)
            {
                StartCoroutine(LoadImageCoroutine(path));
            }

        }, "Select Image", "image/*");
    }

    private System.Collections.IEnumerator LoadImageCoroutine(string imagePath)
    {
        Texture2D loadedTexture = NativeGallery.LoadImageAtPath(imagePath, maxSize: 2048, markTextureNonReadable: false);

        if (loadedTexture != null)
        {
            OnTextureLoaded(loadedTexture);
        }

        yield return null;
    }

    public void OnTextureLoaded(Texture2D loadedTexture)
    {
        GameObject targetModel = currentSelectedObject;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();
            if (dualManager)
            {
                if (loadedTexture)
                {
                    dualManager.SetTextureMode(loadedTexture);
                    dualManager.SetColor(fcp.color);
                }
                else
                {
                    dualManager.SetPaintMode();
                    dualManager.SetColor(fcp.color);
                }
            }
            else if (objManager)
            {
                if (loadedTexture)
                {
                    objManager.SetTextureMode(loadedTexture);
                    objManager.SetColor(fcp.color);
                }
                else
                {
                    objManager.SetPaintMode();
                    objManager.SetColor(fcp.color);
                }
            }
        }
    }

    public void ClearTexture()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();

            if (dualManager)
            {
                dualManager.ClearTexture();
            }
            else if (objManager)
            {
                objManager.ClearTexture();
            }
        }
    }

    public bool CanPaintOnCurrentObject()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();

            if (dualManager)
            {
                return dualManager.SupportsPainting();
            }
            else if (objManager)
            {
                return objManager.SupportsPainting();
            }
        }
        return false;
    }

    public bool HasMaterialManager(GameObject obj)
    {
        if (!obj) return false;

        DualMaterialManager dualManager = obj.GetComponent<DualMaterialManager>();
        OBJMaterialManager objManager = obj.GetComponent<OBJMaterialManager>();

        return dualManager != null || objManager != null;
    }

    public UVMode GetCurrentUVMode()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            CubeCarvingSystem carvingSystem = targetModel.GetComponent<CubeCarvingSystem>();
            if (carvingSystem)
            {
                return carvingSystem.GetUVMode();
            }

            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();
            if (objManager)
            {
                return objManager.GetUVMode();
            }
        }
        return UVMode.Continuous;
    }

    public void SetCurrentUVMode(UVMode mode)
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            CubeCarvingSystem carvingSystem = targetModel.GetComponent<CubeCarvingSystem>();
            if (carvingSystem)
            {
                carvingSystem.SetUVMode(mode);
                return;
            }

            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();
            if (objManager)
            {
                objManager.SetUVMode(mode);
            }
        }
    }

    public bool SupportsTextureMode()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();

            return dualManager != null || objManager != null;
        }
        return false;
    }

    public Texture2D GetCurrentTexture()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            if (dualManager)
            {
                return dualManager.GetCurrentTexture();
            }

            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();
            if (objManager)
            {
                return objManager.GetCurrentTexture();
            }
        }
        return null;
    }

    public bool IsInTextureMode()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel)
        {
            DualMaterialManager dualManager = targetModel.GetComponent<DualMaterialManager>();
            if (dualManager)
            {
                return dualManager.IsInTextureMode();
            }

            OBJMaterialManager objManager = targetModel.GetComponent<OBJMaterialManager>();
            if (objManager)
            {
                return objManager.IsInTextureMode();
            }
        }
        return false;
    }

    private void AddPreviewOutline(GameObject previewObj)
    {
        if (!enablePreviewOutline || !previewObj) return;
    
        // 移除現有的outline（如果有的話）
        OutlineEffect existingOutline = previewObj.GetComponent<OutlineEffect>();
        if (existingOutline)
        {
            DestroyImmediate(existingOutline);
        }
    
        // 添加新的outline效果
        OutlineEffect outline = previewObj.AddComponent<OutlineEffect>();
        outline.outlineColor = previewOutlineColor;
        //outline.outlineWidth = previewOutlineWidth;
        outline.useOutline = true;
    }

    private void RemovePreviewOutline(GameObject previewObj)
    {
        if (!previewObj) return;
    
        OutlineEffect outline = previewObj.GetComponent<OutlineEffect>();
        if (outline)
        {
            DestroyImmediate(outline);
        }
    }

    public void EnterSculptMode()
    {
        isInSculptMode = true;
    currentCarveDepth = 1;
    
    if (DepthScaleSlider)
    {
        DepthScaleSlider.value = 1f;
    }
    
    UpdateAllCarvingDepths();
    UpdateSculptScaleValues();
    }

    // 新增：退出雕刻模式
    public void ExitSculptMode()
    {
        isInSculptMode = false;
        
        // 停止任何正在進行的雕刻
        if (isActiveSculpting)
        {
            StopSculpting();
        }
        
        // 清理狀態
        currentSculptTarget = null;
        isActiveSculpting = false;
        
        // 隱藏切割工具
        if (CuttingTool)
        {
            CuttingTool.SetActive(false);
        }
    }

    // 新增：雕刻模式下的點擊處理
    private void HandleSculptModeClick()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        int layerMask = 1 << LayerMask.NameToLayer("SculptObject");
        
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            // 檢查是否擊中了可雕刻的物件
            if (hitObject.CompareTag("SculptObject"))
            {
                currentSculptTarget = hitObject;
                sculptHitPoint = hit.point;
                
                // 將CuttingTool移動到點擊位置
                PositionCuttingToolAtHitPoint(hit.point, hit.normal);
                
                // 開始雕刻
                StartSculpting(hitObject, hit.point);
            }
        }
    }

    private void PositionCuttingToolAtHitPoint(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (!CuttingTool) return;
        float sculptScale = GetSculptScaleValue();
        Vector3 toolSize = new Vector3(sculptScale, sculptScale, sculptScale);
        float offsetDistance = toolSize.magnitude * 0.5f;
        Vector3 toolPosition = hitPoint;
        CuttingTool.transform.position = toolPosition;

        if (hitNormal != Vector3.zero)
        {
            CuttingTool.transform.rotation = Quaternion.LookRotation(hitNormal);
        }

        CuttingTool.SetActive(true);
        CuttingTool.transform.localScale = toolSize;
    }

    private void StartSculpting(GameObject targetObject, Vector3 hitPoint)
    {
        isActiveSculpting = true;
        currentSculptTarget = targetObject;
        lastSculptTime = Time.time - sculptingInterval;
        isSculptingActive = false;

        // 重置移動檢測
        hasInitialSculptPosition = false;
        lastSculptPosition = Vector3.zero;
        lastMovementSculptTime = 0f;

        Debug.Log("開始雕刻會話");
    }

private System.Collections.IEnumerator LimitedDepthCarve(int targetDepth)
{
    for (int i = 0; i < targetDepth; i++)
    {
        OnCutButtonPressed();
        yield return new WaitForSeconds(0.03f);
        OnCutButtonReleased();
        yield return new WaitForSeconds(0.02f);
    }
    StopSculpting();
}

    private void SetSurfaceOnlyCarving()
    {
        if (CuttingTool)
        {
            CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
            if (carvingTool)
            {
                // 嘗試多種方法設置只雕刻表面
                try
                {
                    // 可能有效的組合
                    carvingTool.SendMessage("SetCarveDepth", 0, SendMessageOptions.DontRequireReceiver);
                    carvingTool.SendMessage("SetMaxDepth", 1, SendMessageOptions.DontRequireReceiver);
                    carvingTool.SendMessage("SetSurfaceOnly", true, SendMessageOptions.DontRequireReceiver);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("設置表面雕刻失敗: " + e.Message);
                }
            }
        }
    }

    // 新增：設置限制深度雕刻
    private void SetLimitedDepthCarving(int maxLayers)
    {
        if (CuttingTool)
        {
            CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
            if (carvingTool)
            {
                try
                {
                    // 連續雕刻但限制最大深度
                    carvingTool.SendMessage("SetCarveDepth", 1, SendMessageOptions.DontRequireReceiver);
                    carvingTool.SendMessage("SetMaxDepth", maxLayers, SendMessageOptions.DontRequireReceiver);
                    carvingTool.SendMessage("SetMaxLayers", maxLayers, SendMessageOptions.DontRequireReceiver);
                    carvingTool.SendMessage("SetSurfaceOnly", false, SendMessageOptions.DontRequireReceiver);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("設置限制深度雕刻失敗: " + e.Message);
                }
            }
        }
    }

    public void ListCarvingToolMethods()
    {
        if (CuttingTool)
        {
            CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
            if (carvingTool)
            {
                Debug.Log("=== CubeCarvingTool 可用方法 ===");
                var methods = carvingTool.GetType().GetMethods();
                foreach (var method in methods)
                {
                    if (!method.IsSpecialName && method.DeclaringType == carvingTool.GetType())
                    {
                        Debug.Log("方法: " + method.Name + " 參數: " + method.GetParameters().Length);
                    }
                }
                
                Debug.Log("=== CubeCarvingTool 可用屬性 ===");
                var properties = carvingTool.GetType().GetProperties();
                foreach (var prop in properties)
                {
                    if (prop.DeclaringType == carvingTool.GetType())
                    {
                        Debug.Log("屬性: " + prop.Name + " 類型: " + prop.PropertyType + " 可寫: " + prop.CanWrite);
                    }
                }
                
                Debug.Log("=== CubeCarvingTool 可用字段 ===");
                var fields = carvingTool.GetType().GetFields();
                foreach (var field in fields)
                {
                    if (field.DeclaringType == carvingTool.GetType())
                    {
                        Debug.Log("字段: " + field.Name + " 類型: " + field.FieldType);
                    }
                }
            }
        }
    }

    private System.Collections.IEnumerator StopSculptingAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (isActiveSculpting)
        {
            StopSculpting();
        }
    }

    private void StopSculpting()
    {
        isActiveSculpting = false;
        isSculptingActive = false;

        foreach (CubeCarvingSystem system in allCarvingSystems)
            if (system) system.SetCarvingEnabled(false);

        if (CuttingTool)
        {
            CuttingTool.SetActive(false);
        }

        currentSculptTarget = null;

        // 重置移動檢測
        hasInitialSculptPosition = false;
        lastSculptPosition = Vector3.zero;
    }


    private void OnSculptScaleChanged(float value)
    {
        float sculptScale = GetSculptScaleValue();
    Vector3 newScale = new Vector3(sculptScale, sculptScale, sculptScale);
    
    if (CuttingTool)
    {
        CuttingTool.transform.localScale = newScale;
    }
    
    if (PreviewCuttingArea)
    {
        PreviewCuttingArea.transform.localScale = newScale;
    }

    if (CuttingTool)
    {
        CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
        if (carvingTool)
        {
            carvingTool.SetToolSize(newScale);
        }
    }

    UpdateSculptScaleValues();
    }

    private void OnDepthScaleChanged(float value)
    {
        currentCarveDepth = Mathf.RoundToInt(value);
    UpdateAllCarvingDepths();
    UpdateSculptScaleValues();
    }

    private void UpdateAllCarvingDepths()
    {
        foreach (CubeCarvingSystem system in allCarvingSystems)
    {
        if (system)
        {
            system.SetCarveDepth(currentCarveDepth);
        }
    }
    
    if (CuttingTool)
    {
        CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
        if (carvingTool)
        {
            carvingTool.SetCarveDepth(currentCarveDepth);
        }
    }
    }

    // 新增：更新滑桿顯示值
    private void UpdateSculptScaleValues()
    {
        if (SculptScaleValue && SculptScaleSlider)
    {
        SculptScaleValue.text = Mathf.RoundToInt(SculptScaleSlider.value).ToString();
    }
    }

    private void UpdateSculptingPosition()
    {
        if (!currentSculptTarget) return;

    Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
    int layerMask = 1 << LayerMask.NameToLayer("SculptObject");
    
    if (Physics.Raycast(ray, out RaycastHit hit, 100f, layerMask))
    {
        if (hit.collider.gameObject == currentSculptTarget)
        {
            PositionCuttingToolAtHitPoint(hit.point, hit.normal);
            sculptHitPoint = hit.point;
        }
    }
    }

    public void ManualStopSculpting()
    {
        if (isActiveSculpting)
        {
            StopSculpting();
        }
    }

    // 新增：獲取當前雕刻狀態
    public bool IsCurrentlySculpting()
    {
        return isActiveSculpting;
    }

    private void SetCarvingDepth(CubeCarvingSystem carvingSystem, int depth)
    {
        try
        {
            carvingSystem.SendMessage("SetCarveDepth", depth, SendMessageOptions.DontRequireReceiver);
            Debug.Log("SendMessage SetCarveDepth 成功");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("SendMessage SetCarveDepth 失敗: " + e.Message);
        }
    }

    // 新增：設置雕刻工具的深度
    private void SetCarvingToolDepth(CubeCarvingTool carvingTool, int depth)
    {
        try
        {
            carvingTool.SendMessage("SetMaxDepth", maxDepth, SendMessageOptions.DontRequireReceiver);
            carvingTool.SendMessage("SetMaxCarveDepth", maxDepth, SendMessageOptions.DontRequireReceiver);
            carvingTool.SendMessage("SetDepthLimit", maxDepth, SendMessageOptions.DontRequireReceiver);
            Debug.Log("最大深度相關方法嘗試完成");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("最大深度方法失敗: " + e.Message);
        }
    }

    public void TestDepthCombinations()
    {
        if (CuttingTool)
        {
            CubeCarvingTool carvingTool = CuttingTool.GetComponent<CubeCarvingTool>();
            if (carvingTool)
            {
                Debug.Log("=== 測試深度組合 ===");
                
                // 測試表面雕刻（深度1）
                Debug.Log("測試深度1（只雕刻表面）:");
                try
                {
                    // 嘗試多種可能的組合
                    carvingTool.SendMessage("SetCarveDepth", 0, SendMessageOptions.DontRequireReceiver);
                    Debug.Log("- 嘗試設置 CarveDepth = 0");
                }
                catch { }
                
                try
                {
                    carvingTool.SendMessage("SetCarveDepth", 100, SendMessageOptions.DontRequireReceiver);
                    Debug.Log("- 嘗試設置 CarveDepth = 100（大值）");
                }
                catch { }
                
                try
                {
                    carvingTool.SendMessage("SetMaxDepth", 1, SendMessageOptions.DontRequireReceiver);
                    Debug.Log("- 嘗試設置 MaxDepth = 1");
                }
                catch { }
            }
        }
    }

    public void SetSculptDepth(int depth)
    {
        depth = Mathf.Clamp(depth, 1, 5);
        currentCarveDepth = depth;
        if (DepthScaleSlider)
        {
            DepthScaleSlider.value = depth;
        }
        UpdateSculptScaleValues();
    }

    // 新增：獲取當前雕刻深度
    public int GetSculptDepth()
    {
        return currentCarveDepth;
    }
}