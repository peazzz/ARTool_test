using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SculptFunction : MonoBehaviour
{
    [Header("BasicSetting")]
    public GameObject ClearButton;
    public GameObject cubeCarvingSystemPrefab;
    public Material previewMaterial, finalMaterial;
    public Transform parentObject;
    public Camera targetCamera;
    public UIManager uiManager;

    [Header("NavMeshSetting")]
    [SerializeField] private float baseForwardDistance = 1.5f;
    [SerializeField] private float downwardCheckDistance = 10f;
    [SerializeField] private float defaultHeightOffset = 0.5f;

    [Header("ShapeButton")]
    public Button ShapeButton_Cube, ShapeButton_Sphere, ShapeButton_Capsule, ShapeButton_Cylinder;

    [Header("MainControlUI")]
    public Slider MainScaleSlider, HeightSlider;
    public Text MainScaleValue, HeightValue;
    public GameObject PositionLockButton;

    public Slider ScaleXSlider, ScaleYSlider, ScaleZSlider;
    public InputField ScaleXInputField, ScaleYInputField, ScaleZInputField;

    public Slider RotationXSlider, RotationYSlider, RotationZSlider;
    public InputField RotationXInputField, RotationYInputField, RotationZInputField;

    public InputField GridInputField;
    public Button GenerateButton, ResetButton;
    public FlexibleColorPicker fcp;
    public Material ColorfulMaterial;

    [Header("defaultValue")]
    public float defaultCubeSize = 1f;
    public int defaultGridSize = 10;

    [Header("RotationSetting")]
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private bool allowRotationControl = true;

    [Header("Debug")]
    [SerializeField] private float raycastCacheTime = 0.15f;
    [SerializeField] private bool showDebugInfo = false;

    private VoxelShape selectedShape;
    private GameObject previewModel, finalModel;
    private GameObject currentSelectedObject;
    private Material originalMaterial;
    private bool isEditingExistingObject = false;
    private float mainScale = 1f, heightOffset = 0f, dynamicForwardDistance, currentRotationY = 0f;
    private Vector3 individualScale = Vector3.one;
    private Vector3 modelRotation = Vector3.zero;
    private int gridSize = 10;
    private bool isUpdatingUI = false, isRotating = false;
    private float lastUpdateTime = 0f, lastRaycastTime = 0f;
    private const float updateInterval = 0.1f;
    private Vector3 lastPreviewPosition;
    private Quaternion lastPreviewRotation;
    private Vector2 lastTouchPosition;
    private RaycastHit cachedHit;
    private bool cachedHitResult = false;
    private bool positionLock;
    private Vector3 lockedPosition;
    private int originalLayer;

    private Vector3 originalObjectScale;
    private Vector3 originalObjectRotation;
    private Vector3 originalObjectPosition;

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        SetupAllButtonEvents();
        SetupSliderAndInputEvents();
        dynamicForwardDistance = baseForwardDistance;

        if (fcp != null && ColorfulMaterial != null)
        {
            fcp.color = ColorfulMaterial.color;
            fcp.onColorChange.AddListener(OnChangeColor);
        }

        ClearButton.SetActive(false);
    }

    void Update()
    {
        if (!isEditingExistingObject && Input.GetMouseButtonDown(0) &&
        !IsPointerOverUIElement() &&
        uiManager.UIHome != null && uiManager.UIHome.activeInHierarchy)
        {
            CheckForObjectSelection();
        }

        if ((previewModel != null || (isEditingExistingObject && currentSelectedObject != null)) &&
            Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdatePreviewModelPosition();
        }

        if ((previewModel != null || isEditingExistingObject) && allowRotationControl &&
            uiManager.SculptPanel2 != null && uiManager.SculptPanel2.activeInHierarchy)
        {
            HandleRotationInput();
        }
    }

    #region Select&Edit
    void CheckForObjectSelection()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        int layerMask = (1 << LayerMask.NameToLayer("SculptObject")) |
                    (1 << LayerMask.NameToLayer("PreviewObject"));

        if (LayerMask.NameToLayer("SculptObject") == -1 ||
        LayerMask.NameToLayer("PreviewObject") == -1)
        {
            layerMask = ~0;
        }

        float maxDistance = 100f;

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            GameObject hitObject = hit.collider.gameObject;

            CubeCarvingSystem carvingSystem = hitObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem == null)
            {
                carvingSystem = hitObject.GetComponentInParent<CubeCarvingSystem>();
            }

            if (carvingSystem != null)
            {
                SelectObject(carvingSystem.gameObject);
            }
        }
    }

    public void SelectObject(GameObject obj)
    {
        DeselectCurrentObject();
        currentSelectedObject = obj;
        isEditingExistingObject = true;
        SetObjectGlow(currentSelectedObject, true);
        originalLayer = currentSelectedObject.layer;
        SetLayerRecursively(currentSelectedObject, LayerMask.NameToLayer("PreviewObject"));
        LoadParametersFromObject(currentSelectedObject);
        CalculateDynamicForwardDistanceForObject(currentSelectedObject);

        originalObjectScale = currentSelectedObject.transform.localScale;
        originalObjectRotation = currentSelectedObject.transform.eulerAngles;
        originalObjectPosition = currentSelectedObject.transform.position;

        LoadColorFromObject(currentSelectedObject);

        CubeCarvingSystem carvingSystem = currentSelectedObject.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            string objectName = currentSelectedObject.name.ToLower();
            if (objectName.Contains("cube")) selectedShape = VoxelShape.Cube;
            else if (objectName.Contains("sphere")) selectedShape = VoxelShape.Sphere;
            else if (objectName.Contains("capsule")) selectedShape = VoxelShape.Capsule;
            else if (objectName.Contains("cylinder")) selectedShape = VoxelShape.Cylinder;
            else selectedShape = VoxelShape.Cube;
        }

        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(true);
        uiManager.UIHome?.SetActive(false);
        uiManager.BackButton?.SetActive(true);
        ClearButton.SetActive(true);
        GridInputField.interactable = false;

        UpdateAllUIValues();
        SetDefaultPositionLockState(true);

        ApplyColorToModel(currentSelectedObject, fcp.color);
    }

    private void LoadColorFromObject(GameObject obj)
    {
        ModelStat modelStat = obj.GetComponent<ModelStat>();
        Color objectColor = Color.white;

        if (modelStat != null && modelStat.IsModelDataValid())
        {
            objectColor = modelStat.ModelData.materialColor;
        }
        else
        {
            var renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null && renderer.material != null)
                objectColor = renderer.material.color;
        }

        fcp.onColorChange.RemoveListener(OnChangeColor);
        fcp.color = objectColor;
        fcp.onColorChange.AddListener(OnChangeColor);
        ColorfulMaterial.color = objectColor;

        ApplyColorToModel(obj, objectColor);
    }

    void DeselectCurrentObject()
    {
        if (currentSelectedObject != null)
        {
            SetObjectGlow(currentSelectedObject, false);
            SetLayerRecursively(currentSelectedObject, originalLayer);
            currentSelectedObject = null;
        }
        isEditingExistingObject = false;
        ClearButton.SetActive(false);
        GridInputField.interactable = true;
    }

    public void SyncCurrentModelColorToUI()
    {
        if (isEditingExistingObject && currentSelectedObject != null)
        {
            ApplyColorToModel(currentSelectedObject, fcp.color);
        }
        else if (previewModel != null)
        {
            ApplyColorToModel(previewModel, fcp.color);
        }
    }

    void SetObjectGlow(GameObject obj, bool glow)
    {
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (glow)
            {
                originalMaterial = renderer.material;
            }
        }
    }

    void LoadParametersFromObject(GameObject obj)
    {
        Vector3 scale = obj.transform.localScale;
        mainScale = Mathf.Max(scale.x, scale.y, scale.z);

        if (mainScale > 0)
        {
            individualScale = new Vector3(
                scale.x / mainScale,
                scale.y / mainScale,
                scale.z / mainScale
            );
        }
        else
        {
            individualScale = Vector3.one;
            mainScale = 1f;
        }

        Vector3 eulerAngles = obj.transform.eulerAngles;
        modelRotation = new Vector3(
            NormalizeAngle(eulerAngles.x),
            NormalizeAngle(eulerAngles.y),
            NormalizeAngle(eulerAngles.z)
        );
        currentRotationY = modelRotation.y;
        gridSize = defaultGridSize;
        heightOffset = 0f;

        bool isCarved = IsObjectCarved(obj);
    }
    #endregion

    #region Event&Value
    void SetupAllButtonEvents()
    {
        ShapeButton_Cube?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cube));
        ShapeButton_Sphere?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Sphere));
        ShapeButton_Capsule?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Capsule));
        ShapeButton_Cylinder?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cylinder));

        GenerateButton?.onClick.AddListener(OnGenerateButtonClicked);
        ResetButton?.onClick.AddListener(OnResetButtonClicked);
        PositionLockButton.GetComponent<Button>().onClick.AddListener(TogglePositionLock);
    }

    void SetupSliderAndInputEvents()
    {
        SetupSlider(MainScaleSlider, 0.1f, 3f, 1f, OnMainScaleChanged);
        SetupSlider(HeightSlider, -1f, 1f, 0f, OnHeightChanged);

        SetupSlider(ScaleXSlider, 0.1f, 3f, 1f, OnScaleXSliderChanged);
        SetupSlider(ScaleYSlider, 0.1f, 3f, 1f, OnScaleYSliderChanged);
        SetupSlider(ScaleZSlider, 0.1f, 3f, 1f, OnScaleZSliderChanged);

        SetupSlider(RotationXSlider, 0f, 360f, 0f, OnRotationXSliderChanged);
        SetupSlider(RotationYSlider, 0f, 360f, 0f, OnRotationYSliderChanged);
        SetupSlider(RotationZSlider, 0f, 360f, 0f, OnRotationZSliderChanged);

        SetupInputField(ScaleXInputField, "1.00", OnScaleXInputChanged);
        SetupInputField(ScaleYInputField, "1.00", OnScaleYInputChanged);
        SetupInputField(ScaleZInputField, "1.00", OnScaleZInputChanged);

        SetupInputField(RotationXInputField, "0", OnRotationXInputChanged);
        SetupInputField(RotationYInputField, "0", OnRotationYInputChanged);
        SetupInputField(RotationZInputField, "0", OnRotationZInputChanged);

        SetupInputField(GridInputField, "10", OnGridInputChanged);
    }

    private void OnChangeColor(Color co)
    {
        ColorfulMaterial.color = co;
        ApplyColorToCurrentModel(co);
    }

    private IEnumerator SyncColorWithDelay()
    {
        yield return null;
        
        if (isEditingExistingObject && currentSelectedObject != null)
        {
            Debug.Log($"Syncing color to current object: {fcp.color}");
            ApplyColorToModel(currentSelectedObject, fcp.color);
        }
        else if (previewModel != null)
        {
            Debug.Log($"Syncing color to preview model: {fcp.color}");
            ApplyColorToModel(previewModel, fcp.color);
        }
    }

    private void ApplyColorToCurrentModel(Color color)
    {
        GameObject targetModel = null;

        if (isEditingExistingObject && currentSelectedObject != null)
        {
            targetModel = currentSelectedObject;
        }
        else if (previewModel != null)
        {
            targetModel = previewModel;
        }

        if (targetModel != null)
        {
            ApplyColorToModel(targetModel, color);
        }
    }

    private void ApplyColorToModel(GameObject model, Color color)
    {
        CubeCarvingSystem carvingSystem = model.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            MeshRenderer renderer = model.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material newMaterial = new Material(ColorfulMaterial);
                newMaterial.color = color;

                if (newMaterial.HasProperty("_Color"))
                    newMaterial.SetColor("_Color", color);
                if (newMaterial.HasProperty("_BaseColor"))
                    newMaterial.SetColor("_BaseColor", color);
                if (newMaterial.HasProperty("_MainColor"))
                    newMaterial.SetColor("_MainColor", color);
                if (newMaterial.HasProperty("_Albedo"))
                    newMaterial.SetColor("_Albedo", color);

                renderer.material = newMaterial;
            }
        }
        else
        {
            MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>();

            foreach (MeshRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    Material newMaterial = new Material(ColorfulMaterial);
                    newMaterial.color = color;

                    if (newMaterial.HasProperty("_Color"))
                        newMaterial.SetColor("_Color", color);
                    if (newMaterial.HasProperty("_BaseColor"))
                        newMaterial.SetColor("_BaseColor", color);
                    if (newMaterial.HasProperty("_MainColor"))
                        newMaterial.SetColor("_MainColor", color);
                    if (newMaterial.HasProperty("_Albedo"))
                        newMaterial.SetColor("_Albedo", color);

                    renderer.material = newMaterial;
                }
            }
        }
    }

    void SetupSlider(Slider slider, float min, float max, float defaultValue, UnityEngine.Events.UnityAction<float> callback)
    {
        if (slider == null) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = defaultValue;
        slider.onValueChanged.AddListener(callback);
    }

    void SetupInputField(InputField inputField, string defaultValue, UnityEngine.Events.UnityAction<string> callback)
    {
        if (inputField == null) return;
        inputField.text = defaultValue;
        inputField.onEndEdit.AddListener(callback);
    }
    #endregion

    #region Shape&Generate
    void OnShapeSelected(VoxelShape shape)
    {
        selectedShape = shape;
        currentRotationY = 0f;
        modelRotation = Vector3.zero;
        isEditingExistingObject = false;

        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(true);
        uiManager.FunctionUISwitch();

        CreatePreviewModel();
        UpdateRotationYUI();
        SetDefaultPositionLockState(false);
    }

    void CreatePreviewModel()
    {
        if (previewModel != null) Destroy(previewModel);

        Vector3 currentScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        previewModel = GenerateShapeWithParameters(selectedShape, currentScale, gridSize, true);

        if (previewModel != null)
        {
            ApplyColorToModel(previewModel, fcp.color);
            SetLayerRecursively(previewModel, LayerMask.NameToLayer("PreviewObject"));

            CalculateDynamicForwardDistance();
        }
    }

    private IEnumerator ApplyColorAfterMeshGeneration(GameObject model, Color color)
    {
        yield return null;
        yield return null;

        ApplyColorToModel(model, color);
    }

    public GameObject GenerateShapeWithParameters(VoxelShape shapeType, Vector3 scale, int gridSize, bool isPreview = false)
    {
        if (cubeCarvingSystemPrefab == null)
        {
            return null;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);
        newCarvingSystem.transform.localScale = scale;
        newCarvingSystem.name = $"CubeCarvingSystem_{shapeType}{(isPreview ? "_Preview" : "_Final")}";

        CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            carvingSystem.SetParameters(defaultCubeSize, gridSize, shapeType);
        }
        else
        {
            return newCarvingSystem;
        }

        if (isPreview)
        {
            SetMaterialAndLayer(newCarvingSystem, previewMaterial, "PreviewObject");
        }
        else
        {
            SetMaterialAndLayer(newCarvingSystem, finalMaterial, "SculptObject");
            StartCoroutine(InitializeModelStatAfterMesh(newCarvingSystem, shapeType));
        }

        return newCarvingSystem;
    }

    private IEnumerator InitializeModelStatAfterMesh(GameObject gameObject, VoxelShape shapeType)
    {
        yield return null;
        yield return null;

        ModelStat modelStat = gameObject.GetComponent<ModelStat>();
        if (modelStat == null)
        {
            modelStat = gameObject.AddComponent<ModelStat>();
        }

        ModelData modelData = new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType.ToString(),
            position = gameObject.transform.position,
            rotation = gameObject.transform.eulerAngles,
            scale = gameObject.transform.localScale,
            materialColor = fcp.color,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        modelStat.SetModelData(modelData);
    }

    private IEnumerator InitializeModelStatNextFrame(ModelStat modelStat, GameObject gameObject, VoxelShape shapeType)
    {
        yield return null;

        ModelData modelData = new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType.ToString(),
            position = gameObject.transform.position,
            rotation = gameObject.transform.eulerAngles,
            scale = gameObject.transform.localScale,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        modelStat.SetModelData(modelData);
    }


    private Vector3 GetSpawnPosition()
    {
        return targetCamera != null ?
            targetCamera.transform.position + targetCamera.transform.forward * 1.5f :
            Vector3.forward;
    }

    void SetMaterialAndLayer(GameObject obj, Material material, string layerName)
    {
        if (material != null)
        {
            MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.material = material;
            }
        }

        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1)
        {
            SetLayerRecursively(obj, layer);
        }
        else
        {
            if (layerName == "SculptObject")
            {
                SetLayerRecursively(obj, 0);
            }
        }

        if (layerName == "SculptObject")
        {
            obj.tag = "SculptObject";
        }
    }
    #endregion

    #region ModelTransform
    void UpdatePreviewModelPosition()
    {
        if (Camera.main == null) return;

        if (isEditingExistingObject && currentSelectedObject != null)
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

        if (previewModel != null)
        {
            if (positionLock)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + heightOffset;
                previewModel.transform.position = newPosition;
                previewModel.transform.rotation = Quaternion.Euler(modelRotation);
            }
            else
            {
                UpdateObjectToFollowCamera(previewModel);
            }
        }
    }

    void CalculateDynamicForwardDistanceForObject(GameObject obj)
    {
        if (obj == null) return;

        Vector3 scale = obj.transform.localScale;
        float actualZScale = scale.z;
        float zScaleDifference = actualZScale - 1.0f;
        dynamicForwardDistance = Mathf.Max(baseForwardDistance + zScaleDifference, 0.5f);
    }

    private Vector3 GetHorizontalForward()
    {
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 horizontalForward = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

        if (horizontalForward.magnitude < 0.1f)
            horizontalForward = Vector3.forward;

        return horizontalForward;
    }

    private void UpdateObjectToFollowCamera(GameObject targetObject)
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 horizontalForward = GetHorizontalForward();

        bool shouldRaycast = Time.time - lastRaycastTime > raycastCacheTime;
        if (shouldRaycast)
        {
            Vector3 rayOrigin = cameraPosition + horizontalForward * dynamicForwardDistance;
            Ray ray = new Ray(rayOrigin, Vector3.down);

            int layerMask = ~(1 << LayerMask.NameToLayer("PreviewObject"));

            if (LayerMask.NameToLayer("PreviewObject") == -1)
            {
                layerMask = ~0;
            }

            cachedHitResult = Physics.Raycast(ray, out cachedHit, downwardCheckDistance, layerMask);
            lastRaycastTime = Time.time;
        }

        Vector3 targetPosition;
        if (cachedHitResult && uiManager.isGroundChecking)
        {
            targetPosition = GetGroundPositionForObject(cachedHit.point, targetObject);
        }
        else
        {
            targetPosition = GetDefaultPosition(cameraPosition, horizontalForward);
        }

        targetObject.transform.position = targetPosition;
        targetObject.transform.rotation = GetModelRotation(horizontalForward);

        lastPreviewPosition = targetObject.transform.position;
        lastPreviewRotation = targetObject.transform.rotation;
    }

    Vector3 GetGroundPositionForObject(Vector3 hitPoint, GameObject targetObject)
    {
        Vector3 position = hitPoint;
        Renderer renderer = targetObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            float halfHeight = renderer.bounds.extents.y;
            position.y += halfHeight;
        }
        position.y += heightOffset;
        return position;
    }

    Vector3 GetGroundPosition(Vector3 hitPoint)
    {
        Vector3 position = hitPoint;
        Renderer renderer = previewModel.GetComponent<Renderer>();
        if (renderer != null)
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
        if (positionLock)
        {
            return Quaternion.Euler(modelRotation);
        }

        Quaternion baseRotation = horizontalForward != Vector3.zero ?
            Quaternion.LookRotation(horizontalForward) : Quaternion.identity;

        Quaternion uiRotation = Quaternion.Euler(modelRotation.x, modelRotation.y, modelRotation.z);

        return baseRotation * uiRotation;
    }

    void CalculateDynamicForwardDistance()
    {
        float actualZScale = mainScale * individualScale.z;
        float zScaleDifference = actualZScale - 1.0f;
        dynamicForwardDistance = Mathf.Max(baseForwardDistance + zScaleDifference, 0.5f);
    }
    #endregion

    public void TogglePositionLock()
    {
        positionLock = !positionLock;

        if (positionLock)
        {
            if (isEditingExistingObject && currentSelectedObject != null)
            {
                lockedPosition = currentSelectedObject.transform.position;
                lockedPosition.y -= heightOffset;
            }
            else if (previewModel != null)
            {
                lockedPosition = previewModel.transform.position;
            }
        }
        else
        {
            if (isEditingExistingObject && currentSelectedObject != null)
            {
                CalculateDynamicForwardDistanceForObject(currentSelectedObject);
            }
        }

        UpdatePositionLockUI();
    }

    private void UpdatePositionLockUI()
    {
        if (PositionLockButton == null) return;

        if (positionLock)
        {
            PositionLockButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        }
        else
        {
            PositionLockButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        }
    }

    private void SetDefaultPositionLockState(bool isEditMode)
    {
        if (isEditMode)
        {
            positionLock = true;
            lockedPosition = currentSelectedObject.transform.position;
            lockedPosition.y -= heightOffset;
        }
        else
        {
            positionLock = false;
        }

        UpdatePositionLockUI();
    }

    #region RotationControl
    void HandleRotationInput()
    {
        if (IsPointerOverUIElement())
        {
            isRotating = false;
            return;
        }

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
        else if (Input.GetMouseButtonUp(0))
        {
            isRotating = false;
        }
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
        if (UnityEngine.EventSystems.EventSystem.current == null) return false;

#if UNITY_EDITOR
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#else
        return Input.touchCount > 0 &&
               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
#endif
    }
    #endregion

    #region UIEvent
    void OnMainScaleChanged(float value)
    {
        if (isUpdatingUI) return;
        mainScale = value;
        if (MainScaleValue != null) MainScaleValue.text = $"{Mathf.RoundToInt(value * 100)}%";

        SyncMainScaleToIndividualSliders(value);
        individualScale = Vector3.one * value;
        UpdateIndividualScaleInputs();
        UpdateTargetModel();
    }

    void SyncMainScaleToIndividualSliders(float value)
    {
        isUpdatingUI = true;
        if (ScaleXSlider != null) ScaleXSlider.value = value;
        if (ScaleYSlider != null) ScaleYSlider.value = value;
        if (ScaleZSlider != null) ScaleZSlider.value = value;
        isUpdatingUI = false;
    }

    void OnHeightChanged(float value)
    {
        if (isUpdatingUI) return;
        heightOffset = value;
        if (HeightValue != null) HeightValue.text = value.ToString("F2");

        if (positionLock)
        {
            if (isEditingExistingObject && currentSelectedObject != null)
            {
                Vector3 newPosition = lockedPosition;
                newPosition.y = lockedPosition.y + value;
                currentSelectedObject.transform.position = newPosition;
            }
            else if (previewModel != null)
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
        if (ScaleXInputField != null) ScaleXInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleYSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        individualScale.y = value;
        if (ScaleYInputField != null) ScaleYInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleZSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        individualScale.z = value;
        if (ScaleZInputField != null) ScaleZInputField.text = value.ToString("F2");
        UpdateTargetModel();
    }

    void OnScaleXInputChanged(string value) => HandleScaleInputChange(value, 0);
    void OnScaleYInputChanged(string value) => HandleScaleInputChange(value, 1);
    void OnScaleZInputChanged(string value) => HandleScaleInputChange(value, 2);

    void OnRotationXSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.x = value;
        if (RotationXInputField != null) RotationXInputField.text = Mathf.RoundToInt(value).ToString();
        UpdateTargetModel();
    }

    void OnRotationYSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.y = value;
        currentRotationY = value;
        if (RotationYInputField != null) RotationYInputField.text = Mathf.RoundToInt(value).ToString();
        UpdateTargetModel();
    }

    void OnRotationZSliderChanged(float value)
    {
        if (isUpdatingUI) return;
        modelRotation.z = value;
        if (RotationZInputField != null) RotationZInputField.text = Mathf.RoundToInt(value).ToString();
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
        if (slider != null) slider.value = value;
        if (inputField != null) inputField.text = Mathf.RoundToInt(value).ToString();
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
        if (RotationYSlider != null) RotationYSlider.value = modelRotation.y;
        if (RotationYInputField != null) RotationYInputField.text = Mathf.RoundToInt(modelRotation.y).ToString();
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
        if (slider != null) slider.value = value;
        if (inputField != null) inputField.text = value.ToString("F2");
        isUpdatingUI = false;
    }

    void OnGridInputChanged(string value)
    {
        if (isUpdatingUI || !int.TryParse(value, out int result)) return;

        result = Mathf.Clamp(result, 1, 100);
        gridSize = result;
        if (GridInputField != null) GridInputField.text = result.ToString();
        UpdateTargetModel();
    }
    #endregion

    #region ButtonEvent
    void OnGenerateButtonClicked()
    {
        if (isEditingExistingObject)
        {
            ApplyEditChanges();
        }
        else
        {
            CreateNewObject();
        }
        SwitchToHome();
    }

    void CreateNewObject()
    {
        if (previewModel != null)
        {
            lastPreviewPosition = previewModel.transform.position;
            lastPreviewRotation = previewModel.transform.rotation;
            Destroy(previewModel);
            previewModel = null;
        }

        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        finalModel = GenerateShapeWithParameters(selectedShape, finalScale, gridSize, false);

        if (finalModel != null)
        {
            finalModel.transform.position = lastPreviewPosition;
            finalModel.transform.rotation = lastPreviewRotation;
            SetMaterialAndLayer(finalModel, finalMaterial, "SculptObject");

            StartCoroutine(ApplyColorAfterMeshGeneration(finalModel, fcp.color));
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
        StartCoroutine(ApplyColorAfterMeshGeneration(currentSelectedObject, fcp.color));

        ModelStat modelStat = currentSelectedObject.GetComponent<ModelStat>();
        if (modelStat != null)
        {
            ModelData updatedData = new ModelData
            {
                filename = currentSelectedObject.name,
                shapeType = selectedShape.ToString(),
                position = currentSelectedObject.transform.position,
                rotation = modelRotation,
                scale = currentSelectedObject.transform.localScale,
                materialColor = fcp.color,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            modelStat.SetModelData(updatedData);
        }

        DeselectCurrentObject();
    }

    void OnResetButtonClicked()
    {
        if (isEditingExistingObject)
        {
            ResetToOriginalParameters();
        }
        else
        {
            ResetAllParameters();
        }

        UpdateAllUIValues();

        if (isEditingExistingObject)
        {
            if (currentSelectedObject != null)
            {
                currentSelectedObject.transform.localScale = originalObjectScale;
                currentSelectedObject.transform.rotation = Quaternion.Euler(originalObjectRotation);
                currentSelectedObject.transform.position = originalObjectPosition;
                lockedPosition = originalObjectPosition;
            }
        }
        else
        {
            CreatePreviewModel();
        }
    }

    void ResetToOriginalParameters()
    {
        if (currentSelectedObject == null) return;

        Vector3 scale = originalObjectScale;
        mainScale = Mathf.Max(scale.x, scale.y, scale.z);

        if (mainScale > 0)
        {
            individualScale = new Vector3(
                scale.x / mainScale,
                scale.y / mainScale,
                scale.z / mainScale
            );
        }
        else
        {
            individualScale = Vector3.one;
            mainScale = 1f;
        }

        modelRotation = new Vector3(
            NormalizeAngle(originalObjectRotation.x),
            NormalizeAngle(originalObjectRotation.y),
            NormalizeAngle(originalObjectRotation.z)
        );

        currentRotationY = modelRotation.y;
        gridSize = defaultGridSize;
        heightOffset = 0f;
    }

    public void OnBackButtonClicked()
    {
        if (!uiManager.isInColorPage)
        {
            if (isEditingExistingObject)
            {
                DeselectCurrentObject();
            }

            if (previewModel != null)
            {
                Destroy(previewModel);
                previewModel = null;
            }
        }
    }

    void SwitchToHome()
    {
        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);

        isEditingExistingObject = false;
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
    #endregion

    #region ToolFunction
    void UpdateTargetModel()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel == null) return;

        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        targetModel.transform.localScale = finalScale;

        if (positionLock)
        {
            targetModel.transform.rotation = Quaternion.Euler(modelRotation);
        }

        if (isEditingExistingObject)
        {
            return;
        }

        if (previewModel != null)
        {
            CubeCarvingSystem carvingSystem = targetModel.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                carvingSystem.SetParameters(1f, gridSize, selectedShape);
            }
            CalculateDynamicForwardDistance();
        }
    }

    private bool IsObjectCarved(GameObject obj)
    {
        CubeCarvingSystem carvingSystem = obj.GetComponent<CubeCarvingSystem>();
        if (carvingSystem == null) return false;

        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            return meshFilter.mesh.vertexCount > 0;
        }

        return false;
    }

    private void SetLayerRecursively(GameObject obj, int layer)
    {
        if (obj == null) return;
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    public void Clear()
    {
        if (currentSelectedObject != null)
        {
            Destroy(currentSelectedObject);
            currentSelectedObject = null;
            isEditingExistingObject = false;
            ClearButton.SetActive(false);

            uiManager.SwitchToPanel(uiManager.UIHome);
        }
    }

    public void UpdateAllUIValues()
    {
        isUpdatingUI = true;

        if (MainScaleSlider != null) MainScaleSlider.value = mainScale;
        if (HeightSlider != null) HeightSlider.value = heightOffset;
        if (ScaleXSlider != null) ScaleXSlider.value = individualScale.x;
        if (ScaleYSlider != null) ScaleYSlider.value = individualScale.y;
        if (ScaleZSlider != null) ScaleZSlider.value = individualScale.z;

        if (RotationXSlider != null) RotationXSlider.value = modelRotation.x;
        if (RotationYSlider != null) RotationYSlider.value = modelRotation.y;
        if (RotationZSlider != null) RotationZSlider.value = modelRotation.z;

        if (MainScaleValue != null) MainScaleValue.text = $"{Mathf.RoundToInt(mainScale * 100)}%";
        if (HeightValue != null) HeightValue.text = heightOffset.ToString("F2");

        UpdateIndividualScaleInputs();
        UpdateRotationInputs();
        UpdateGridInput();

        isUpdatingUI = false;
    }

    void UpdateIndividualScaleInputs()
    {
        if (ScaleXInputField != null) ScaleXInputField.text = individualScale.x.ToString("F2");
        if (ScaleYInputField != null) ScaleYInputField.text = individualScale.y.ToString("F2");
        if (ScaleZInputField != null) ScaleZInputField.text = individualScale.z.ToString("F2");
    }

    void UpdateRotationInputs()
    {
        if (RotationXInputField != null) RotationXInputField.text = Mathf.RoundToInt(modelRotation.x).ToString();
        if (RotationYInputField != null) RotationYInputField.text = Mathf.RoundToInt(modelRotation.y).ToString();
        if (RotationZInputField != null) RotationZInputField.text = Mathf.RoundToInt(modelRotation.z).ToString();
    }

    void UpdateGridInput()
    {
        if (GridInputField != null) GridInputField.text = gridSize.ToString();
    }

    private void RestoreObjectOriginalColor(GameObject obj)
    {
        ModelStat modelStat = obj.GetComponent<ModelStat>();
        if (modelStat != null && modelStat.IsModelDataValid())
        {
            Color savedColor = modelStat.ModelData.materialColor;
            ApplyColorToModel(obj, savedColor);
            Debug.Log($"Restored original color: {savedColor}");
        }
    }
    #endregion
}