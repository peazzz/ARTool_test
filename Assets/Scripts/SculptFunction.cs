using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.NavigationMesh;

public class SculptFunction : MonoBehaviour
{
    [Header("基本設定")]
    public GameObject ClearButton;
    public GameObject cubeCarvingSystemPrefab;
    public Material previewMaterial, finalMaterial;
    public Material selectedMaterial;  // 選中物件的發光材質
    public Transform parentObject;
    public Camera targetCamera;
    public UIManager uiManager;

    [Header("導航網格設定")]
    [SerializeField] private LightshipNavMeshManager navMeshManager;
    [SerializeField] private float baseForwardDistance = 1.5f;
    [SerializeField] private float downwardCheckDistance = 10f;
    [SerializeField] private float defaultHeightOffset = 0.5f;

    [Header("形狀選擇按鈕")]
    public Button ShapeButton_Cube, ShapeButton_Sphere, ShapeButton_Capsule, ShapeButton_Cylinder;

    [Header("主要控制 UI")]
    public Slider MainScaleSlider, HeightSlider;
    public Text MainScaleValue, HeightValue;
    public GameObject PositionLockButton;

    // 縮放頁面
    public Slider ScaleXSlider, ScaleYSlider, ScaleZSlider;
    public InputField ScaleXInputField, ScaleYInputField, ScaleZInputField;

    // 旋轉頁面
    public Slider RotationXSlider, RotationYSlider, RotationZSlider;
    public InputField RotationXInputField, RotationYInputField, RotationZInputField;

    // 其他頁面
    public InputField GridInputField;
    public Button GenerateButton, ResetButton;
    public FlexibleColorPicker fcp;
    public Material ColorfulMaterial;

    [Header("預設參數")]
    public float defaultCubeSize = 1f;
    public int defaultGridSize = 10;

    [Header("旋轉控制設定")]
    [SerializeField] private float rotationSpeed = 0.5f;
    [SerializeField] private bool allowRotationControl = true;

    [Header("性能優化")]
    [SerializeField] private float raycastCacheTime = 0.15f;
    [SerializeField] private bool showDebugInfo = false;

    // 私有變數
    private VoxelShape selectedShape;
    private GameObject previewModel, finalModel;
    private GameObject currentSelectedObject;  // 當前選中的物件
    private Material originalMaterial;         // 存儲原始材質
    private bool isEditingExistingObject = false; // 是否正在編輯現有物件
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

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        SetupAllButtonEvents();
        SetupSliderAndInputEvents();
        dynamicForwardDistance = baseForwardDistance;
        fcp.color = ColorfulMaterial.color;
        fcp.onColorChange.AddListener(OnChangeColor);
    }

    void Update()
    {
        if (!isEditingExistingObject && Input.GetMouseButtonDown(0) &&
        !IsPointerOverUIElement() &&
        uiManager.UIHome != null && uiManager.UIHome.activeInHierarchy)
        {
            CheckForObjectSelection();
        }

        // 預覽模式或編輯模式的位置更新 - 修正這裡的條件
        if ((previewModel != null || (isEditingExistingObject && currentSelectedObject != null)) &&
            Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdatePreviewModelPosition();
        }

        // 旋轉控制（預覽模式或編輯模式）
        if ((previewModel != null || isEditingExistingObject) && allowRotationControl &&
            uiManager.SculptPanel2 != null && uiManager.SculptPanel2.activeInHierarchy)
        {
            HandleRotationInput();
        }
    }

    #region 物件選取和編輯系統
    void CheckForObjectSelection()
    {
        Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // 修復：使用更寬鬆的 LayerMask 檢測
        int layerMask = (1 << LayerMask.NameToLayer("SculptObject")) |
                    (1 << LayerMask.NameToLayer("PreviewObject"));

        // 如果 SculptObject 層不存在，使用默認層
        if (LayerMask.NameToLayer("SculptObject") == -1 ||
        LayerMask.NameToLayer("PreviewObject") == -1)
        {
            layerMask = ~0;
        }

        // 增加檢測距離
        float maxDistance = 100f;

        if (showDebugInfo)
        {
            // 顯示射線用於調試
            Debug.DrawRay(ray.origin, ray.direction * maxDistance, Color.red, 1f);
            Debug.Log($"執行 Raycast - Origin: {ray.origin}, Direction: {ray.direction}");
        }

        if (Physics.Raycast(ray, out hit, maxDistance, layerMask))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (showDebugInfo)
            {
                Debug.Log($"Raycast 命中物件: {hitObject.name}, Layer: {hitObject.layer}, Distance: {hit.distance}");
            }

            // 檢查是否有 CubeCarvingSystem 組件
            CubeCarvingSystem carvingSystem = hitObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem == null)
            {
                carvingSystem = hitObject.GetComponentInParent<CubeCarvingSystem>();
            }

            if (carvingSystem != null)
            {
                SelectObject(carvingSystem.gameObject);
            }
            else
            {
                if (showDebugInfo)
                {
                    Debug.Log($"命中的物件 {hitObject.name} 沒有 CubeCarvingSystem 組件");
                }
            }
        }
        else
        {
            if (showDebugInfo)
            {
                Debug.Log("Raycast 沒有命中任何物件");
            }
        }
    }

    public void SelectObject(GameObject obj)
    {
        // 取消之前的選擇
        DeselectCurrentObject();

        // 選擇新物件
        currentSelectedObject = obj;
        isEditingExistingObject = true;

        // 設定發光效果
        SetObjectGlow(currentSelectedObject, true);

        originalLayer = currentSelectedObject.layer;
        SetLayerRecursively(currentSelectedObject, LayerMask.NameToLayer("PreviewObject"));

        // 從物件讀取當前參數
        LoadParametersFromObject(currentSelectedObject);

        // 計算該物件的動態前進距離
        CalculateDynamicForwardDistanceForObject(currentSelectedObject);

        // 推斷物件的形狀類型
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

        // 切換到編輯面板
        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(true);
        uiManager.UIHome?.SetActive(false);
        uiManager.BackButton?.SetActive(true);

        // 更新 UI 顯示
        UpdateAllUIValues();
        SetDefaultPositionLockState(true);
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
    }

    void SetObjectGlow(GameObject obj, bool glow)
    {
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            if (glow)
            {
                // 保存原始材質
                originalMaterial = renderer.material;
                // 設定發光材質
                if (selectedMaterial != null)
                {
                    renderer.material = selectedMaterial;
                }
                else
                {
                    Debug.LogWarning("selectedMaterial 未設定，無法顯示發光效果");
                }
            }
            else
            {
                // 恢復原始材質
                if (originalMaterial != null)
                {
                    renderer.material = originalMaterial;
                }
            }
        }
    }

    void LoadParametersFromObject(GameObject obj)
    {
        // 載入縮放參數
        Vector3 scale = obj.transform.localScale;

        // 計算主縮放（使用最大值）
        mainScale = Mathf.Max(scale.x, scale.y, scale.z);

        // 計算個別軸縮放比例
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

        // 載入旋轉參數
        Vector3 eulerAngles = obj.transform.eulerAngles;
        modelRotation = new Vector3(
            NormalizeAngle(eulerAngles.x),
            NormalizeAngle(eulerAngles.y),
            NormalizeAngle(eulerAngles.z)
        );
        currentRotationY = modelRotation.y;

        // 載入 Grid 參數（但不會用於重新生成 mesh）
        gridSize = defaultGridSize;

        // 重置高度偏移
        heightOffset = 0f;

        // 檢查物件是否已被雕刻
        bool isCarved = IsObjectCarved(obj);
        if (isCarved)
        {
            Debug.Log($"檢測到 {obj.name} 已被雕刻，編輯時將保持原有 mesh");
        }

        Debug.Log($"載入物件參數 - Scale: {scale}, MainScale: {mainScale}, IndividualScale: {individualScale}, Rotation: {modelRotation}");
        Debug.Log($"物件雕刻狀態: {(isCarved ? "已雕刻" : "未雕刻")}");
    }
    #endregion

    #region 按鈕事件設定
    void SetupAllButtonEvents()
    {
        // 形狀選擇按鈕
        ShapeButton_Cube?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cube));
        ShapeButton_Sphere?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Sphere));
        ShapeButton_Capsule?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Capsule));
        ShapeButton_Cylinder?.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cylinder));

        // 控制按鈕
        GenerateButton?.onClick.AddListener(OnGenerateButtonClicked);
        ResetButton?.onClick.AddListener(OnResetButtonClicked);
        uiManager.BackButton?.GetComponent<Button>().onClick.AddListener(OnBackButtonClicked);
        PositionLockButton.GetComponent<Button>().onClick.AddListener(TogglePositionLock);
    }

    void SetupSliderAndInputEvents()
    {
        // 主縮放滑桿
        SetupSlider(MainScaleSlider, 0.1f, 3f, 1f, OnMainScaleChanged);
        SetupSlider(HeightSlider, -1f, 1f, 0f, OnHeightChanged);

        // 個別軸滑桿
        SetupSlider(ScaleXSlider, 0.1f, 3f, 1f, OnScaleXSliderChanged);
        SetupSlider(ScaleYSlider, 0.1f, 3f, 1f, OnScaleYSliderChanged);
        SetupSlider(ScaleZSlider, 0.1f, 3f, 1f, OnScaleZSliderChanged);

        // 旋轉滑桿
        SetupSlider(RotationXSlider, 0f, 360f, 0f, OnRotationXSliderChanged);
        SetupSlider(RotationYSlider, 0f, 360f, 0f, OnRotationYSliderChanged);
        SetupSlider(RotationZSlider, 0f, 360f, 0f, OnRotationZSliderChanged);

        // 縮放輸入欄位
        SetupInputField(ScaleXInputField, "1.00", OnScaleXInputChanged);
        SetupInputField(ScaleYInputField, "1.00", OnScaleYInputChanged);
        SetupInputField(ScaleZInputField, "1.00", OnScaleZInputChanged);

        // 旋轉輸入欄位
        SetupInputField(RotationXInputField, "0", OnRotationXInputChanged);
        SetupInputField(RotationYInputField, "0", OnRotationYInputChanged);
        SetupInputField(RotationZInputField, "0", OnRotationZInputChanged);

        SetupInputField(GridInputField, "10", OnGridInputChanged);
    }

    private void OnChangeColor(Color co)
    {
        ColorfulMaterial.color = co;
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

    #region 形狀選擇和模型生成
    void OnShapeSelected(VoxelShape shape)
    {
        selectedShape = shape;
        currentRotationY = 0f;
        modelRotation = Vector3.zero;
        isEditingExistingObject = false; // 確保是新建模式

        // 切換面板
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
            SetMaterialAndLayer(previewModel, previewMaterial, "PreviewObject");
            CalculateDynamicForwardDistance();
        }
    }

    public GameObject GenerateShapeWithParameters(VoxelShape shapeType, Vector3 scale, int gridSize, bool isPreview = false)
    {
        if (cubeCarvingSystemPrefab == null)
        {
            Debug.LogError("CubeCarvingSystem預製物件未設定!");
            return null;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);
        newCarvingSystem.transform.localScale = scale;
        newCarvingSystem.name = $"CubeCarvingSystem_{shapeType}{(isPreview ? "_Preview" : "_Final")}";

        // 設定 CubeCarvingSystem 參數
        CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            carvingSystem.SetParameters(defaultCubeSize, gridSize, shapeType);
        }
        else
        {
            Debug.LogError("預製物上找不到CubeCarvingSystem組件!");
            return newCarvingSystem;
        }

        // 設定材質和層級
        if (isPreview)
        {
            SetMaterialAndLayer(newCarvingSystem, previewMaterial, "PreviewObject");
        }
        else
        {
            SetMaterialAndLayer(newCarvingSystem, finalMaterial, "SculptObject");

            // 等待 MeshCollider 完全設定後再掛載 ModelStat
            StartCoroutine(InitializeModelStatAfterMesh(newCarvingSystem, shapeType));

            ClearButton.SetActive(true);
        }

        Debug.Log($"生成{(isPreview ? "預覽" : "最終")}{shapeType}模型 - Scale: {scale}, GridSize: {gridSize}");
        return newCarvingSystem;
    }

    private IEnumerator InitializeModelStatAfterMesh(GameObject gameObject, VoxelShape shapeType)
    {
        // 等待兩幀確保 Mesh 和 MeshCollider 都完全設定
        yield return null;
        yield return null;

        Debug.Log("開始為最終模型掛載 ModelStat 組件...");

        // 檢查 MeshCollider 是否正確設定
        MeshCollider meshCollider = gameObject.GetComponent<MeshCollider>();
        if (meshCollider != null && meshCollider.sharedMesh != null)
        {
            Debug.Log($"MeshCollider 已正確設定，Mesh: {meshCollider.sharedMesh.name}, 頂點數: {meshCollider.sharedMesh.vertexCount}");
        }
        else
        {
            Debug.LogWarning("MeshCollider 或其 Mesh 未正確設定，這可能導致無法點擊物件");
        }

        // 添加 ModelStat 組件
        ModelStat modelStat = gameObject.GetComponent<ModelStat>();
        if (modelStat == null)
        {
            modelStat = gameObject.AddComponent<ModelStat>();
            Debug.Log("成功添加 ModelStat 組件");
        }

        // 創建並設定模型數據
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

        Debug.Log($"已為最終模型 {gameObject.name} 設定 ModelStat 數據");
    }

    // 新增協程來延遲初始化 ModelStat
    private IEnumerator InitializeModelStatNextFrame(ModelStat modelStat, GameObject gameObject, VoxelShape shapeType)
    {
        yield return null; // 等待一幀

        // 創建並設定模型數據
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

        Debug.Log($"已為最終模型 {gameObject.name} 設定 ModelStat 數據:");
        Debug.Log($"  文件名: {modelData.filename}");
        Debug.Log($"  形狀: {modelData.shapeType}");
        Debug.Log($"  位置: {modelData.position}");
        Debug.Log($"  旋轉: {modelData.rotation}");
        Debug.Log($"  縮放: {modelData.scale}");
        Debug.Log($"  時間: {modelData.timestamp}");
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
            else
            {
                Debug.LogWarning($"物件 {obj.name} 沒有 MeshRenderer 組件");
            }
        }

        // 確保層級存在
        int layer = LayerMask.NameToLayer(layerName);
        if (layer != -1)
        {
            SetLayerRecursively(obj, layer);
            if (showDebugInfo)
            {
                Debug.Log($"已將物件 {obj.name} 設定為層級 {layerName} (索引: {layer})");
            }
        }
        else
        {
            Debug.LogWarning($"層級 {layerName} 不存在，請在 Unity 中創建此層級，或物件將使用默認層級");

            // 如果是 SculptObject 層不存在，使用默認層 (0)
            if (layerName == "SculptObject")
            {
                SetLayerRecursively(obj, 0);
                Debug.Log($"使用默認層級 (0) 替代 {layerName}");
            }
        }
    }
    #endregion

    #region 模型位置和旋轉更新
    void UpdatePreviewModelPosition()
    {
        if (Camera.main == null) return;

        // 編輯模式的位置控制
        if (isEditingExistingObject && currentSelectedObject != null)
        {
            if (positionLock)
            {
                // 編輯模式 + 鎖定：保持在鎖定位置，使用固定角度
                currentSelectedObject.transform.position = lockedPosition;
                // **修改：位置鎖定時，直接使用 UI 設定的角度，不跟隨相機**
                currentSelectedObject.transform.rotation = Quaternion.Euler(modelRotation);

                if (showDebugInfo)
                {
                    Debug.Log($"編輯模式鎖定 - 位置: {lockedPosition}, 固定角度: {modelRotation}");
                }
            }
            else
            {
                // 編輯模式 + 解鎖：跟隨相機
                CalculateDynamicForwardDistanceForObject(currentSelectedObject);
                UpdateObjectToFollowCamera(currentSelectedObject);
            }
            return;
        }

        // 預覽模式的位置控制
        if (previewModel != null)
        {
            if (positionLock)
            {
                // 預覽模式 + 鎖定：保持在鎖定位置，使用固定角度
                previewModel.transform.position = lockedPosition;
                // **修改：位置鎖定時，直接使用 UI 設定的角度，不跟隨相機**
                previewModel.transform.rotation = Quaternion.Euler(modelRotation);

                if (showDebugInfo)
                {
                    Debug.Log($"預覽模式鎖定 - 位置: {lockedPosition}, 固定角度: {modelRotation}");
                }
            }
            else
            {
                // 預覽模式 + 解鎖：正常跟隨相機
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

        if (showDebugInfo)
        {
            Debug.Log($"計算物件 {obj.name} 的動態前進距離: {dynamicForwardDistance} (Z軸縮放: {actualZScale})");
        }
    }

    // 新增輔助方法：獲取水平前進方向
    private Vector3 GetHorizontalForward()
    {
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 horizontalForward = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

        if (horizontalForward.magnitude < 0.1f)
            horizontalForward = Vector3.forward;

        return horizontalForward;
    }

    // 新增輔助方法：物件跟隨相機的邏輯
    private void UpdateObjectToFollowCamera(GameObject targetObject)
    {
        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 horizontalForward = GetHorizontalForward();

        bool shouldRaycast = Time.time - lastRaycastTime > raycastCacheTime;
        if (shouldRaycast)
        {
            Vector3 rayOrigin = cameraPosition + horizontalForward * dynamicForwardDistance;
            Ray ray = new Ray(rayOrigin, Vector3.down);

            // **修改：排除 PreviewObject layer（這樣編輯中的物件也會被排除）**
            int layerMask = ~(1 << LayerMask.NameToLayer("PreviewObject"));

            // 如果 PreviewObject layer 不存在，使用所有層
            if (LayerMask.NameToLayer("PreviewObject") == -1)
            {
                layerMask = ~0;
            }

            cachedHitResult = Physics.Raycast(ray, out cachedHit, downwardCheckDistance, layerMask);
            lastRaycastTime = Time.time;

            if (showDebugInfo)
            {
                string mode = isEditingExistingObject ? "編輯模式" : "預覽模式";
                Debug.Log($"{mode} Raycast - Origin: {rayOrigin}, Hit: {cachedHitResult}, " +
                         $"HitPoint: {(cachedHitResult ? cachedHit.point.ToString() : "None")}, " +
                         $"Excluded Layer: PreviewObject");
            }
        }

        Vector3 targetPosition;
        if (cachedHitResult)
        {
            targetPosition = GetGroundPositionForObject(cachedHit.point, targetObject);
        }
        else
        {
            targetPosition = GetDefaultPosition(cameraPosition, horizontalForward);
        }

        targetObject.transform.position = targetPosition;
        targetObject.transform.rotation = GetModelRotation(horizontalForward);

        // 更新最後位置記錄
        lastPreviewPosition = targetObject.transform.position;
        lastPreviewRotation = targetObject.transform.rotation;

        if (showDebugInfo)
        {
            Debug.Log($"{(isEditingExistingObject ? "編輯模式" : "預覽模式")}物件位置更新: {targetPosition}, 角度: {targetObject.transform.rotation.eulerAngles}");
        }
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

        // 位置未鎖定時，結合相機方向和 UI 旋轉
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

        if (showDebugInfo)
        {
            Debug.Log($"動態前進距離: {dynamicForwardDistance} (Z軸縮放: {actualZScale})");
        }
    }
    #endregion

    public void TogglePositionLock()
    {
        positionLock = !positionLock;

        if (positionLock)
        {
            // 鎖定時，記錄當前位置和角度
            if (isEditingExistingObject && currentSelectedObject != null)
            {
                lockedPosition = currentSelectedObject.transform.position;
                //lockedRotation = currentSelectedObject.transform.rotation;
            }
            else if (previewModel != null)
            {
                lockedPosition = previewModel.transform.position;
                //lockedRotation = previewModel.transform.rotation;
            }
        }
        else
        {
            // 解鎖時，如果是編輯模式，重新計算動態前進距離
            if (isEditingExistingObject && currentSelectedObject != null)
            {
                // 重新計算動態前進距離
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
            // **移除：不再設定 lockedRotation**
        }
        else
        {
            positionLock = false;
            // **移除：不再重置 lockedRotation**
        }

        UpdatePositionLockUI();
    }

    #region 旋轉控制
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

            // 將角度限制在 0-360 範圍內
            currentRotationY = NormalizeAngle(currentRotationY);

            // 更新 Y 軸旋轉的 UI（將手勢旋轉同步到 modelRotation.y）
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

                        // 將角度限制在 0-360 範圍內
                        currentRotationY = NormalizeAngle(currentRotationY);

                        // 更新 Y 軸旋轉的 UI（將手勢旋轉同步到 modelRotation.y）
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

    #region UI 事件處理
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

    // 旋轉滑桿事件
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
        currentRotationY = value; // 同步手勢旋轉值
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

    // 旋轉輸入欄位事件
    void OnRotationXInputChanged(string value) => HandleRotationInputChange(value, 0);
    void OnRotationYInputChanged(string value) => HandleRotationInputChange(value, 1);
    void OnRotationZInputChanged(string value) => HandleRotationInputChange(value, 2);

    void HandleRotationInputChange(string value, int axis)
    {
        if (isUpdatingUI || !float.TryParse(value, out float result)) return;

        // 將角度限制在 0-360 範圍內
        result = NormalizeAngle(result);

        switch (axis)
        {
            case 0:
                modelRotation.x = result;
                UpdateRotationSliderAndInput(RotationXSlider, RotationXInputField, result);
                break;
            case 1:
                modelRotation.y = result;
                currentRotationY = result; // 同步手勢旋轉值
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

    // 正規化角度到 0-360 範圍
    float NormalizeAngle(float angle)
    {
        angle = angle % 360f;
        if (angle < 0) angle += 360f;
        return angle;
    }

    // 更新 Y 軸旋轉 UI
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

    #region 主要按鈕事件
    void OnGenerateButtonClicked()
    {
        if (isEditingExistingObject)
        {
            // 編輯模式：應用變更並取消選擇
            ApplyEditChanges();
        }
        else
        {
            // 新建模式：生成新物件
            CreateNewObject();
        }

        // 返回主頁
        SwitchToHome();
    }

    void CreateNewObject()
    {
        // 移除預覽模型
        if (previewModel != null)
        {
            lastPreviewPosition = previewModel.transform.position;
            lastPreviewRotation = Quaternion.Euler(modelRotation);
            Destroy(previewModel);
            previewModel = null;
        }

        // 生成最終模型
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
        }

        Debug.Log($"生成最終模型完成: {selectedShape}，位置: {lastPreviewPosition}");
    }

    void ApplyEditChanges()
    {
        if (currentSelectedObject == null) return;

        Debug.Log("開始套用編輯變更（保持原有 mesh）...");

        // 只套用變換變更
        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        currentSelectedObject.transform.localScale = finalScale;
        // **重要：使用與編輯過程中相同的角度計算方式**
        currentSelectedObject.transform.rotation = Quaternion.Euler(modelRotation);

        // 確保恢復為 SculptObject layer
        SetLayerRecursively(currentSelectedObject, LayerMask.NameToLayer("SculptObject"));

        if (showDebugInfo)
        {
            Debug.Log($"物件 {currentSelectedObject.name} 編輯完成，最終角度: {modelRotation}");
        }

        // 只更新 ModelStat 數據
        ModelStat modelStat = currentSelectedObject.GetComponent<ModelStat>();
        if (modelStat != null)
        {
            ModelData updatedData = new ModelData
            {
                filename = currentSelectedObject.name,
                shapeType = selectedShape.ToString(),
                position = currentSelectedObject.transform.position,
                rotation = modelRotation,  // **使用 modelRotation 而不是 transform.eulerAngles**
                scale = currentSelectedObject.transform.localScale,
                timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            modelStat.SetModelData(updatedData);
            Debug.Log($"已更新 {currentSelectedObject.name} 的 ModelStat 數據（保持原有 mesh）");
        }

        DeselectCurrentObject();
        Debug.Log($"編輯完成：{currentSelectedObject?.name} - 雕刻效果已保留");
    }

    void OnResetButtonClicked()
    {
        ResetAllParameters();
        UpdateAllUIValues();

        if (isEditingExistingObject)
        {
            // 編輯模式：重新載入物件參數
            if (currentSelectedObject != null)
            {
                LoadParametersFromObject(currentSelectedObject);
                UpdateAllUIValues();
            }
        }
        else
        {
            // 新建模式：重新創建預覽模型
            CreatePreviewModel();
        }

        Debug.Log("參數已重置");
    }

    void OnBackButtonClicked()
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
        SwitchToHome();

    }

    void SwitchToHome()
    {
        uiManager.SculptPanel1?.SetActive(false);
        uiManager.SculptPanel2?.SetActive(false);
        uiManager.UIHome?.SetActive(true);
        uiManager.BackButton?.SetActive(false);

        // 重置編輯狀態
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

    #region 工具方法
    void UpdateTargetModel()
    {
        GameObject targetModel = isEditingExistingObject ? currentSelectedObject : previewModel;
        if (targetModel == null) return;

        // 計算最終縮放值
        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        // 更新縮放
        targetModel.transform.localScale = finalScale;

        // **修改：位置鎖定時使用固定角度，否則會在 UpdateObjectToFollowCamera 中更新**
        if (positionLock)
        {
            // 位置鎖定時，使用 UI 設定的固定角度
            targetModel.transform.rotation = Quaternion.Euler(modelRotation);

            if (showDebugInfo)
            {
                Debug.Log($"UpdateTargetModel - 位置鎖定，使用固定角度: {modelRotation}");
            }
        }

        // 編輯模式：只更新變換，不重新生成 mesh
        if (isEditingExistingObject)
        {
            if (showDebugInfo)
            {
                Debug.Log($"編輯模式：更新變換 - 縮放: {finalScale}, 旋轉: {modelRotation}");
            }
            return;
        }

        // 預覽模式：正常重新生成 mesh
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

    // 新增方法：檢查物件是否已被雕刻過
    private bool IsObjectCarved(GameObject obj)
    {
        CubeCarvingSystem carvingSystem = obj.GetComponent<CubeCarvingSystem>();
        if (carvingSystem == null) return false;

        // 這裡可以添加檢查邏輯來判斷物件是否已被雕刻
        // 比如檢查 mesh 的頂點數是否與原始形狀不同
        MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            // 如果 mesh 已經被修改過，我們認為它已被雕刻
            // 可以通過比較頂點數或其他特徵來判斷
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
        // 取消當前選擇
        DeselectCurrentObject();

        if (parentObject != null)
        {
            for (int i = parentObject.childCount - 1; i >= 0; i--)
            {
                Transform child = parentObject.GetChild(i);
                if (child.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 清除當前生成的模型
        if (previewModel != null)
        {
            Destroy(previewModel);
            previewModel = null;
        }
        if (finalModel != null)
        {
            Destroy(finalModel);
            finalModel = null;
        }

        ClearButton.SetActive(false);
    }

    public void UpdateAllUIValues()
    {
        isUpdatingUI = true;

        // 更新滑桿
        if (MainScaleSlider != null) MainScaleSlider.value = mainScale;
        if (HeightSlider != null) HeightSlider.value = heightOffset;
        if (ScaleXSlider != null) ScaleXSlider.value = individualScale.x;
        if (ScaleYSlider != null) ScaleYSlider.value = individualScale.y;
        if (ScaleZSlider != null) ScaleZSlider.value = individualScale.z;

        // 更新旋轉滑桿
        if (RotationXSlider != null) RotationXSlider.value = modelRotation.x;
        if (RotationYSlider != null) RotationYSlider.value = modelRotation.y;
        if (RotationZSlider != null) RotationZSlider.value = modelRotation.z;

        // 更新文字顯示
        if (MainScaleValue != null) MainScaleValue.text = $"{Mathf.RoundToInt(mainScale * 100)}%";
        if (HeightValue != null) HeightValue.text = heightOffset.ToString("F2");

        // 更新輸入欄位
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
    #endregion
}