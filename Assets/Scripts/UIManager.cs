using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.NavigationMesh;

public class UIManager : MonoBehaviour
{
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    private bool UI_on;
    public GameObject BackButton;

    [Header("目前主要面板")]
    public GameObject UIHome;                    // 主頁面板
    public GameObject SculptPanel1;              // 形狀選擇面板
    public GameObject SculptPanel2;              // 參數調整面板

    [Header("預覽設定")]
    public Material previewMaterial;             // 預覽狀態的材質
    public Material finalMaterial;               // 最終狀態的材質

    [Header("導航網格設定")]
    [SerializeField] private LightshipNavMeshManager navMeshManager; // 導航網格管理器的引用
    [SerializeField] private float baseForwardDistance = 1.5f;           // 基準前方距離
    [SerializeField] private float downwardCheckDistance = 10f;      // 向下檢測距離
    [SerializeField] private float defaultHeightOffset = 0.5f;
    private float dynamicForwardDistance;                            // 動態計算的前方距離

    [Header("SculptPanel1 - 形狀選擇按鈕")]
    public Button ShapeButton_Cube;              // 立方體按鈕
    public Button ShapeButton_Sphere;            // 球體按鈕
    public Button ShapeButton_Capsule;           // 膠囊體按鈕
    public Button ShapeButton_Cylinder;          // 圓柱體按鈕

    [Header("SculptPanel2 - 參數調整UI")]
    // 主要縮放控制
    public Slider MainScaleSlider;               // 主縮放滑桿
    public Text MainScaleValue;                  // 主縮放數值顯示(百分比)
    public Slider HeightSlider;
    public Text HeightValue;

    // 個別軸縮放控制
    public Slider ScaleXSlider;                  // X軸縮放滑桿
    public Slider ScaleYSlider;                  // Y軸縮放滑桿
    public Slider ScaleZSlider;                  // Z軸縮放滑桿
    public InputField ScaleXInputField;          // X軸輸入欄位
    public InputField ScaleYInputField;          // Y軸輸入欄位
    public InputField ScaleZInputField;          // Z軸輸入欄位

    // Grid設定
    public InputField GridInputField;            // 格子Grid輸入欄位

    // 控制按鈕
    public Button GenerateButton;                // 最終生成按鈕
    public Button ResetButton;                   // 重置參數按鈕

    [Header("功能管理器")]
    public FounctionManager founctionManager;

    // 狀態管理
    private VoxelShape selectedShape = VoxelShape.Cube;
    private GameObject previewModel;             // 預覽模型
    private GameObject finalModel;               // 最終模型

    // 參數值
    private float mainScale = 1f;
    private Vector3 individualScale = Vector3.one;
    private int gridSize = 10;
    private float heightOffset = 0f;             // 新增：高度偏移值

    // 更新控制
    private bool isUpdatingUI = false;           // 防止遞歸更新
    private float lastUpdateTime = 0f;           // 控制更新頻率
    private const float updateInterval = 0.1f;   // 更新間隔

    // 存儲預覽物件的最後 Transform
    private Vector3 lastPreviewPosition;
    private Quaternion lastPreviewRotation;

    [Header("旋轉控制設定")]
    [SerializeField] private float rotationSpeed = 0.5f;        // 旋轉速度
    [SerializeField] private bool allowRotationControl = true;   // 是否允許旋轉控制

    // 旋轉控制狀態
    private bool isRotating = false;
    private Vector2 lastTouchPosition;
    private float currentRotationY = 0f;  // 當前Y軸旋轉角度

    [Header("簡單UI優化")]
    [SerializeField] private float raycastCacheTime = 0.15f;  // 射線檢測快取時間
    private float lastRaycastTime = 0f;
    private RaycastHit cachedHit;
    private bool cachedHitResult = false;

    [Header("調試設定")]
    [SerializeField] private bool showDebugInfo = false;

    void Start()
    {
        UI_on = false;
        FounctionUI_RT.anchoredPosition = new Vector3(0, -400, 0);
        HandleArrow_RT.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        // 初始化UI狀態
        InitializeUI();

        // 設定所有按鈕事件
        SetupAllButtonEvents();

        // 設定滑桿和輸入欄位事件
        SetupSliderAndInputEvents();

        dynamicForwardDistance = baseForwardDistance;
    }

    void Update()
    {
        // 控制預覽更新頻率
        if (previewModel != null && Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
            UpdatePreviewModelPosition();
        }

        if (previewModel != null && allowRotationControl && SculptPanel2 != null && SculptPanel2.activeInHierarchy)
        {
            HandleRotationInput();
        }
    }

    void InitializeUI()
    {
        // 初始只顯示UIHome
        if (UIHome != null) UIHome.SetActive(true);
        if (SculptPanel1 != null) SculptPanel1.SetActive(false);
        if (SculptPanel2 != null) SculptPanel2.SetActive(false);
        if (BackButton != null) BackButton.SetActive(false);

        // 初始化參數值
        UpdateAllUIValues();
    }

    void SetupAllButtonEvents()
    {
        // 形狀選擇按鈕
        if (ShapeButton_Cube != null)
        {
            ShapeButton_Cube.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cube));
        }
        if (ShapeButton_Sphere != null)
        {
            ShapeButton_Sphere.onClick.AddListener(() => OnShapeSelected(VoxelShape.Sphere));
        }
        if (ShapeButton_Capsule != null)
        {
            ShapeButton_Capsule.onClick.AddListener(() => OnShapeSelected(VoxelShape.Capsule));
        }
        if (ShapeButton_Cylinder != null)
        {
            ShapeButton_Cylinder.onClick.AddListener(() => OnShapeSelected(VoxelShape.Cylinder));
        }

        // 控制按鈕
        if (GenerateButton != null)
        {
            GenerateButton.onClick.AddListener(OnGenerateButtonClicked);
        }
        if (ResetButton != null)
        {
            ResetButton.onClick.AddListener(OnResetButtonClicked);
        }

        // 返回按鈕
        if (BackButton != null)
        {
            BackButton.GetComponent<Button>().onClick.AddListener(OnBackButtonClicked);
        }
    }

    void SetupSliderAndInputEvents()
    {
        // 主縮放滑桿
        if (MainScaleSlider != null)
        {
            MainScaleSlider.minValue = 0.1f;
            MainScaleSlider.maxValue = 3f;
            MainScaleSlider.value = 1f;
            MainScaleSlider.onValueChanged.AddListener(OnMainScaleChanged);
        }

        // 高度滑桿設定
        if (HeightSlider != null)
        {
            HeightSlider.minValue = -1f;
            HeightSlider.maxValue = 1f;
            HeightSlider.value = 0f;
            HeightSlider.onValueChanged.AddListener(OnHeightChanged);
        }

        // 個別軸滑桿
        if (ScaleXSlider != null)
        {
            ScaleXSlider.minValue = 0.1f;
            ScaleXSlider.maxValue = 3f;
            ScaleXSlider.value = 1f;
            ScaleXSlider.onValueChanged.AddListener(OnScaleXSliderChanged);
        }
        if (ScaleYSlider != null)
        {
            ScaleYSlider.minValue = 0.1f;
            ScaleYSlider.maxValue = 3f;
            ScaleYSlider.value = 1f;
            ScaleYSlider.onValueChanged.AddListener(OnScaleYSliderChanged);
        }
        if (ScaleZSlider != null)
        {
            ScaleZSlider.minValue = 0.1f;
            ScaleZSlider.maxValue = 3f;
            ScaleZSlider.value = 1f;
            ScaleZSlider.onValueChanged.AddListener(OnScaleZSliderChanged);
        }

        // 縮放輸入欄位
        if (ScaleXInputField != null)
        {
            ScaleXInputField.text = "1.00";
            ScaleXInputField.onEndEdit.AddListener(OnScaleXInputChanged);
        }
        if (ScaleYInputField != null)
        {
            ScaleYInputField.text = "1.00";
            ScaleYInputField.onEndEdit.AddListener(OnScaleYInputChanged);
        }
        if (ScaleZInputField != null)
        {
            ScaleZInputField.text = "1.00";
            ScaleZInputField.onEndEdit.AddListener(OnScaleZInputChanged);
        }

        // 格子Grid輸入欄位
        if (GridInputField != null)
        {
            GridInputField.text = "10";
            GridInputField.onEndEdit.AddListener(OnGridInputChanged);
        }
    }

    // === 主要流程控制 ===

    public void SculptButton()
    {
        // UIHome -> SculptPanel1
        if (UIHome != null) UIHome.SetActive(false);
        if (SculptPanel1 != null) SculptPanel1.SetActive(true);
        if (SculptPanel2 != null) SculptPanel2.SetActive(false);
        if (BackButton != null) BackButton.SetActive(true);

        Debug.Log("進入形狀選擇面板");
    }

    void OnShapeSelected(VoxelShape shape)
    {
        selectedShape = shape;
        currentRotationY = 0f;

        // SculptPanel1 -> SculptPanel2
        if (SculptPanel1 != null) SculptPanel1.SetActive(false);
        if (SculptPanel2 != null) SculptPanel2.SetActive(true);
        // BackButton保持顯示

        // 立即生成預覽模型
        CreatePreviewModel();

        Debug.Log($"選擇形狀: {shape}，進入參數調整面板，生成預覽模型");
    }

    void CreatePreviewModel()
    {
        if (previewModel != null)
        {
            Destroy(previewModel);
        }

        // 生成新的預覽模型
        if (founctionManager != null)
        {
            // 計算當前縮放值
            Vector3 currentScale = new Vector3(
                mainScale * individualScale.x,
                mainScale * individualScale.y,
                mainScale * individualScale.z
            );

            // 生成預覽模型
            previewModel = founctionManager.GenerateShapeWithParameters(
                selectedShape,
                currentScale,
                gridSize,
                true  // 標記為預覽模型
            );

            if (previewModel != null)
            {
                // 設定預覽材質
                if (previewMaterial != null)
                {
                    MeshRenderer renderer = previewModel.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.material = previewMaterial;
                    }
                }

                int previewLayer = LayerMask.NameToLayer("PreviewObject");
                if (previewLayer != -1)
                {
                    SetLayerRecursively(previewModel, previewLayer);
                }
                else
                {
                    Debug.LogWarning("PreviewObject 層級不存在，請在 Unity 中創建此層級");
                }

                CalculateDynamicForwardDistance();
            }
        }
    }

    void UpdatePreviewModelPosition()
    {
        if (previewModel == null || Camera.main == null) return;

        Vector3 cameraPosition = Camera.main.transform.position;
        Vector3 cameraForward = Camera.main.transform.forward;

        // 將基準前方向量投影到水平面
        Vector3 horizontalForward = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;

        // 如果基準接近垂直向量，使用預設前方
        if (horizontalForward.magnitude < 0.1f)
        {
            horizontalForward = Vector3.forward;
        }

        bool shouldRaycast = Time.time - lastRaycastTime > raycastCacheTime;

        if (shouldRaycast)
        {
            Vector3 rayOrigin = cameraPosition + horizontalForward * dynamicForwardDistance;
            Ray ray = new Ray(rayOrigin, Vector3.down);

            int previewLayer = LayerMask.NameToLayer("PreviewObject");
            int layerMask = ~(1 << previewLayer);

            cachedHitResult = Physics.Raycast(ray, out cachedHit, downwardCheckDistance, layerMask);
            lastRaycastTime = Time.time;
        }

        // 使用快取結果
        if (cachedHitResult)
        {
            Vector3 targetPosition = cachedHit.point;

            Renderer renderer = previewModel.GetComponent<Renderer>();
            if (renderer != null)
            {
                UnityEngine.Bounds bounds = renderer.bounds;
                float halfHeight = bounds.extents.y;
                targetPosition.y += halfHeight;
            }

            targetPosition.y += heightOffset;
            previewModel.transform.position = targetPosition;

            Quaternion baseRotation = Quaternion.identity;
            if (horizontalForward != Vector3.zero)
            {
                baseRotation = Quaternion.LookRotation(horizontalForward);
            }

            Quaternion userRotation = Quaternion.Euler(0, currentRotationY, 0);
            previewModel.transform.rotation = baseRotation * userRotation;
        }
        else
        {
            Vector3 defaultPosition = cameraPosition + horizontalForward * dynamicForwardDistance;
            defaultPosition.y = cameraPosition.y - defaultHeightOffset + heightOffset;
            previewModel.transform.position = defaultPosition;

            Quaternion baseRotation = Quaternion.identity;
            if (horizontalForward != Vector3.zero)
            {
                baseRotation = Quaternion.LookRotation(horizontalForward);
            }

            Quaternion userRotation = Quaternion.Euler(0, currentRotationY, 0);
            previewModel.transform.rotation = baseRotation * userRotation;
        }

        lastPreviewPosition = previewModel.transform.position;
        lastPreviewRotation = previewModel.transform.rotation;
    }

    void CalculateDynamicForwardDistance()
    {
        // 計算實際的Z軸縮放值
        float actualZScale = mainScale * individualScale.z;

        // 計算Z軸縮放與1.0的差值
        float zScaleDifference = actualZScale - 1.0f;

        // 根據你的需求：Z軸每多0.01，Forward distance也多0.01
        // 這樣當前 Z軸每多1.0，Forward distance多1.0
        dynamicForwardDistance = baseForwardDistance + zScaleDifference;

        // 確保距離不會太小
        dynamicForwardDistance = Mathf.Max(dynamicForwardDistance, 0.5f);

        if (showDebugInfo)
        {
            Debug.Log($"動態前方距離: {dynamicForwardDistance} (Z軸縮放: {actualZScale})");
        }
    }

    void HandleRotationInput()
    {
        // 檢查是否點擊到UI元素
        if (IsPointerOverUIElement())
        {
            isRotating = false;
            return;
        }

#if UNITY_EDITOR
    // 編輯器中使用滑鼠控制
    if (Input.GetMouseButtonDown(0))
    {
        isRotating = true;
        lastTouchPosition = Input.mousePosition;
    }
    else if (Input.GetMouseButton(0) && isRotating)
    {
        Vector2 currentMousePosition = Input.mousePosition;
        Vector2 deltaPosition = currentMousePosition - lastTouchPosition;
        
        // 根據水平滑動旋轉物件
        float rotationDelta = deltaPosition.x * rotationSpeed;
        currentRotationY -= rotationDelta;
        
        lastTouchPosition = currentMousePosition;
    }
    else if (Input.GetMouseButtonUp(0))
    {
        isRotating = false;
    }
#else
        // 手機上使用觸控控制
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

                        // 根據水平滑動旋轉物件
                        float rotationDelta = deltaPosition.x * rotationSpeed;
                        currentRotationY -= rotationDelta;

                        lastTouchPosition = touch.position;
                    }
                    break;

                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    isRotating = false;
                    break;
            }
        }
#endif
    }

    // 檢查是否點擊到UI元素
    bool IsPointerOverUIElement()
    {
        // 檢查滑鼠是否在UI上
        if (UnityEngine.EventSystems.EventSystem.current != null)
        {
#if UNITY_EDITOR
        return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
#else
            // 手機上檢查第一個觸控點
            if (Input.touchCount > 0)
            {
                return UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            }
#endif
        }
        return false;
    }

    void UpdatePreviewModel()
    {
        if (previewModel == null) return;

        // 計算最終縮放值
        Vector3 finalScale = new Vector3(
            mainScale * individualScale.x,
            mainScale * individualScale.y,
            mainScale * individualScale.z
        );

        // 更新縮放
        previewModel.transform.localScale = finalScale;

        // 更新Grid參數
        CubeCarvingSystem carvingSystem = previewModel.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            carvingSystem.SetParameters(1f, gridSize, selectedShape);
        }

        CalculateDynamicForwardDistance();
    }

    void OnGenerateButtonClicked()
    {
        // 刪除預覽模型
        if (previewModel != null)
        {
            lastPreviewPosition = previewModel.transform.position;
            lastPreviewRotation = previewModel.transform.rotation;

            Destroy(previewModel);
            previewModel = null;
        }

        // 刪除之前的最終模型（如果有的話）
        //if (finalModel != null)
        //{
        //    Destroy(finalModel);
        //}

        // 生成最終模型
        if (founctionManager != null)
        {
            // 計算最終縮放值
            Vector3 finalScale = new Vector3(
                mainScale * individualScale.x,
                mainScale * individualScale.y,
                mainScale * individualScale.z
            );

            // 生成最終模型
            finalModel = founctionManager.GenerateShapeWithParameters(
                selectedShape,
                finalScale,
                gridSize,  // 使用格子的Grid值
                false  // 標記為最終模型
            );

            // 設定最終材質
            if (finalModel != null)
            {
                // 複製預覽物件的位置和旋轉
                finalModel.transform.position = lastPreviewPosition;
                finalModel.transform.rotation = lastPreviewRotation;

                // 設定最終材質
                if (finalMaterial != null)
                {
                    MeshRenderer renderer = finalModel.GetComponent<MeshRenderer>();
                    if (renderer != null)
                    {
                        renderer.material = finalMaterial;
                    }
                }

                // 設定最終物件的層級為 SculptObject
                int sculptLayer = LayerMask.NameToLayer("SculptObject");
                if (sculptLayer != -1)
                {
                    SetLayerRecursively(finalModel, sculptLayer);
                }
                else
                {
                    Debug.LogWarning("SculptObject 層級不存在，請在 Unity 中創建此層級");
                }
            }
        }

        // 生成完畢返回到UIHome
        if (SculptPanel2 != null) SculptPanel2.SetActive(false);
        if (UIHome != null) UIHome.SetActive(true);
        if (BackButton != null) BackButton.SetActive(false);

        Debug.Log($"生成最終模型完成: {selectedShape}，位置: {lastPreviewPosition}");
    }

    void OnResetButtonClicked()
    {
        // 重置所有參數到預設值
        mainScale = 1f;
        individualScale = Vector3.one;
        gridSize = 10;  // 重置格子Grid值
        currentRotationY = 0f;
        heightOffset = 0f;  // 重置高度偏移

        // 更新UI
        UpdateAllUIValues();

        // 更新預覽模型
        CreatePreviewModel();

        Debug.Log("參數已重置");
    }

    void OnBackButtonClicked()
    {
        // 根據當前面板狀態決定返回行為
        if (SculptPanel1 != null && SculptPanel1.activeInHierarchy)
        {
            // 從SculptPanel1回到UIHome
            SculptPanel1.SetActive(false);
            if (UIHome != null) UIHome.SetActive(true);
            if (BackButton != null) BackButton.SetActive(false);
            Debug.Log("從形狀選擇面板回到主頁");
        }
        else if (SculptPanel2 != null && SculptPanel2.activeInHierarchy)
        {
            // 從SculptPanel2回到UIHome，並刪除預覽模型
            if (previewModel != null)
            {
                Destroy(previewModel);
                previewModel = null;
            }
            SculptPanel2.SetActive(false);
            if (UIHome != null) UIHome.SetActive(true);
            if (BackButton != null) BackButton.SetActive(false);
            Debug.Log("從參數調整面板回到主頁，刪除預覽模型");
        }
    }

    // === 滑桿和輸入欄位事件處理 ===

    void OnMainScaleChanged(float value)
    {
        if (isUpdatingUI) return;

        mainScale = value;
        if (MainScaleValue != null)
        {
            MainScaleValue.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        // 同步更新個別軸滑桿
        isUpdatingUI = true;
        if (ScaleXSlider != null) ScaleXSlider.value = value;
        if (ScaleYSlider != null) ScaleYSlider.value = value;
        if (ScaleZSlider != null) ScaleZSlider.value = value;
        isUpdatingUI = false;

        // 更新個別縮放值
        individualScale = Vector3.one * value;
        UpdateIndividualScaleInputs();

        // 更新預覽模型
        UpdatePreviewModel();
    }

    // 新增：高度滑桿事件處理
    void OnHeightChanged(float value)
    {
        if (isUpdatingUI) return;

        heightOffset = value;
        if (HeightValue != null)
        {
            HeightValue.text = value.ToString("F2");
        }

        // 不需要重新生成模型，只需要更新位置
        // UpdatePreviewModelPosition會在Update中自動調用
    }

    void OnScaleXSliderChanged(float value)
    {
        if (isUpdatingUI) return;

        individualScale.x = value;
        if (ScaleXInputField != null)
        {
            ScaleXInputField.text = value.ToString("F2");
        }
        UpdatePreviewModel();
    }

    void OnScaleYSliderChanged(float value)
    {
        if (isUpdatingUI) return;

        individualScale.y = value;
        if (ScaleYInputField != null)
        {
            ScaleYInputField.text = value.ToString("F2");
        }
        UpdatePreviewModel();
    }

    void OnScaleZSliderChanged(float value)
    {
        if (isUpdatingUI) return;

        individualScale.z = value;
        if (ScaleZInputField != null)
        {
            ScaleZInputField.text = value.ToString("F2");
        }
        UpdatePreviewModel();
    }

    void OnScaleXInputChanged(string value)
    {
        if (isUpdatingUI) return;

        if (float.TryParse(value, out float result))
        {
            result = Mathf.Clamp(result, 0.1f, 3f);
            individualScale.x = result;

            isUpdatingUI = true;
            if (ScaleXSlider != null) ScaleXSlider.value = result;
            if (ScaleXInputField != null) ScaleXInputField.text = result.ToString("F2");
            isUpdatingUI = false;

            UpdatePreviewModel();
        }
    }

    void OnScaleYInputChanged(string value)
    {
        if (isUpdatingUI) return;

        if (float.TryParse(value, out float result))
        {
            result = Mathf.Clamp(result, 0.1f, 3f);
            individualScale.y = result;

            isUpdatingUI = true;
            if (ScaleYSlider != null) ScaleYSlider.value = result;
            if (ScaleYInputField != null) ScaleYInputField.text = result.ToString("F2");
            isUpdatingUI = false;

            UpdatePreviewModel();
        }
    }

    void OnScaleZInputChanged(string value)
    {
        if (isUpdatingUI) return;

        if (float.TryParse(value, out float result))
        {
            result = Mathf.Clamp(result, 0.1f, 3f);
            individualScale.z = result;

            isUpdatingUI = true;
            if (ScaleZSlider != null) ScaleZSlider.value = result;
            if (ScaleZInputField != null) ScaleZInputField.text = result.ToString("F2");
            isUpdatingUI = false;

            UpdatePreviewModel();
        }
    }

    // 格子Grid輸入處理
    void OnGridInputChanged(string value)
    {
        if (isUpdatingUI) return;

        if (int.TryParse(value, out int result))
        {
            result = Mathf.Clamp(result, 1, 100);
            gridSize = result;  // 設定格子Grid值

            if (GridInputField != null) GridInputField.text = result.ToString();
            UpdatePreviewModel();
        }
    }

    // === 輔助方法 ===

    void UpdateAllUIValues()
    {
        isUpdatingUI = true;

        // 更新滑桿
        if (MainScaleSlider != null) MainScaleSlider.value = mainScale;
        if (HeightSlider != null) HeightSlider.value = heightOffset;
        if (ScaleXSlider != null) ScaleXSlider.value = individualScale.x;
        if (ScaleYSlider != null) ScaleYSlider.value = individualScale.y;
        if (ScaleZSlider != null) ScaleZSlider.value = individualScale.z;

        // 更新主縮放顯示
        if (MainScaleValue != null)
        {
            MainScaleValue.text = $"{Mathf.RoundToInt(mainScale * 100)}%";
        }

        // 更新高度顯示
        if (HeightValue != null)
        {
            HeightValue.text = heightOffset.ToString("F2");
        }

        // 更新輸入欄位
        UpdateIndividualScaleInputs();
        UpdateGridInput();

        isUpdatingUI = false;
    }

    void UpdateIndividualScaleInputs()
    {
        if (ScaleXInputField != null) ScaleXInputField.text = individualScale.x.ToString("F2");
        if (ScaleYInputField != null) ScaleYInputField.text = individualScale.y.ToString("F2");
        if (ScaleZInputField != null) ScaleZInputField.text = individualScale.z.ToString("F2");
    }

    void UpdateGridInput()
    {
        if (GridInputField != null) GridInputField.text = gridSize.ToString();
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

    // === 向後相容UI控制方法（保留向後兼容）===

    public void FunctionUISwitch()
    {
        UI_on = !UI_on;

        if (UI_on)
        {
            FounctionUI_RT.anchoredPosition = new Vector3(0, 0, 0);
            HandleArrow_RT.localScale = new Vector3(0.6f, -0.6f, 0.6f);
        }
        else
        {
            FounctionUI_RT.anchoredPosition = new Vector3(0, -400, 0);
            HandleArrow_RT.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        }
    }

    public void Back()
    {
        if (UIHome != null) UIHome.SetActive(true);
        if (BackButton != null) BackButton.SetActive(false);
    }

    public void Clear()
    {
        if (founctionManager != null && founctionManager.parentObject != null)
        {
            for (int i = founctionManager.parentObject.childCount - 1; i >= 0; i--)
            {
                Transform child = founctionManager.parentObject.GetChild(i);
                if (child.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
                {
                    Destroy(child.gameObject);
                }
            }
        }

        // 清除當前生成的模型引用
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

        founctionManager.ClearButton.SetActive(false);
    }

    private bool IsValidInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        float result;
        return float.TryParse(input, out result) && result > 0;
    }
}