using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    private bool UI_on;
    public GameObject BackButton;

    [Header("三個主要面板")]
    public GameObject UIHome;                    // 主頁面板
    public GameObject SculptPanel1;              // 形狀選擇面板
    public GameObject SculptPanel2;              // 參數調整面板

    [Header("預覽設定")]
    public Material previewMaterial;             // 預覽狀態的材質
    public Material finalMaterial;               // 最終狀態的材質

    [Header("舊版Sculpt - 保留向後兼容")]
    public GameObject SculptContent;
    public InputField CubeScale;
    public InputField GridScale;
    public Button CubeInstantiate_A;
    public Button CubeInstantiate_B;

    [Header("SculptPanel1 - 形狀選擇按鈕")]
    public Button ShapeButton_Cube;              // 正方體按鈕
    public Button ShapeButton_Sphere;            // 圓體按鈕
    public Button ShapeButton_Capsule;           // 膠囊體按鈕
    public Button ShapeButton_Cylinder;          // 圓柱體按鈕

    [Header("SculptPanel2 - 參數調整UI")]
    // 主要縮放控制
    public Slider MainScaleSlider;               // 主縮放滑桿
    public Text MainScaleValue;                  // 主縮放數值顯示(百分比)

    // 個別軸縮放控制
    public Slider ScaleXSlider;                  // X軸縮放滑桿
    public Slider ScaleYSlider;                  // Y軸縮放滑桿
    public Slider ScaleZSlider;                  // Z軸縮放滑桿
    public InputField ScaleXInputField;          // X軸輸入欄位
    public InputField ScaleYInputField;          // Y軸輸入欄位
    public InputField ScaleZInputField;          // Z軸輸入欄位

    // Grid設定 - 統一為單一InputField
    public InputField GridInputField;            // 統一Grid輸入欄位

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
    private int gridSize = 10;                   // 統一的Grid大小

    // 更新控制
    private bool isUpdatingUI = false;           // 防止循環更新
    private float lastUpdateTime = 0f;           // 用於限制更新頻率
    private const float updateInterval = 0.1f;   // 更新間隔

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

        // 舊版功能保留
        SetupLegacyEvents();
    }

    void Update()
    {
        // 限制預覽更新頻率
        if (previewModel != null && Time.time - lastUpdateTime > updateInterval)
        {
            lastUpdateTime = Time.time;
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

        // 後退按鈕
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

        // 統一Grid輸入欄位
        if (GridInputField != null)
        {
            GridInputField.text = "10";
            GridInputField.onEndEdit.AddListener(OnGridInputChanged);
        }
    }

    void SetupLegacyEvents()
    {
        // 舊版功能保留
        if (founctionManager != null)
        {
            if (CubeInstantiate_A != null)
            {
                CubeInstantiate_A.onClick.AddListener(() => {
                    founctionManager.SculptModelA();
                    Debug.Log("生成VoxelCube模型");
                });
            }

            if (CubeInstantiate_B != null)
            {
                CubeInstantiate_B.onClick.AddListener(() => {
                    founctionManager.SculptModelB();
                    Debug.Log("生成CubeCarvingSystem模型");
                });
            }
        }

        // 舊版輸入欄位監聽
        if (CubeScale != null) CubeScale.onValueChanged.AddListener(OnInputFieldChanged);
        if (GridScale != null) GridScale.onValueChanged.AddListener(OnInputFieldChanged);
        UpdateButtonInteractable();
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
        // 清除舊的預覽模型
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
                gridSize,  // 使用統一的Grid值
                true  // 標記為預覽模型
            );

            // 設定預覽材質
            if (previewModel != null && previewMaterial != null)
            {
                MeshRenderer renderer = previewModel.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = previewMaterial;
                }
            }
        }
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
    }

    void OnGenerateButtonClicked()
    {
        // 刪除預覽模型
        if (previewModel != null)
        {
            Destroy(previewModel);
            previewModel = null;
        }

        // 刪除之前的最終模型（如果有的話）
        if (finalModel != null)
        {
            Destroy(finalModel);
        }

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
                gridSize,  // 使用統一的Grid值
                false  // 標記為最終模型
            );

            // 設定最終材質
            if (finalModel != null && finalMaterial != null)
            {
                MeshRenderer renderer = finalModel.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.material = finalMaterial;
                }
            }
        }

        // 生成完畢後回到UIHome
        if (SculptPanel2 != null) SculptPanel2.SetActive(false);
        if (UIHome != null) UIHome.SetActive(true);
        if (BackButton != null) BackButton.SetActive(false);

        Debug.Log($"生成最終模型完成: {selectedShape}");
    }

    void OnResetButtonClicked()
    {
        // 重置所有參數到預設值
        mainScale = 1f;
        individualScale = Vector3.one;
        gridSize = 10;  // 重置統一Grid值

        // 更新UI
        UpdateAllUIValues();

        // 更新預覽模型
        CreatePreviewModel();

        Debug.Log("參數已重置");
    }

    void OnBackButtonClicked()
    {
        // 根據當前面板狀態決定後退行為
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

    // 統一Grid輸入欄位處理
    void OnGridInputChanged(string value)
    {
        if (isUpdatingUI) return;

        if (int.TryParse(value, out int result))
        {
            result = Mathf.Clamp(result, 1, 100);
            gridSize = result;  // 設定統一Grid值

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
        if (ScaleXSlider != null) ScaleXSlider.value = individualScale.x;
        if (ScaleYSlider != null) ScaleYSlider.value = individualScale.y;
        if (ScaleZSlider != null) ScaleZSlider.value = individualScale.z;

        // 更新主縮放顯示
        if (MainScaleValue != null)
        {
            MainScaleValue.text = $"{Mathf.RoundToInt(mainScale * 100)}%";
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

    // === 舊版UI控制方法（保留向後兼容）===

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
        // 舊版後退功能
        if (SculptContent != null) SculptContent.SetActive(false);
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

    private void OnInputFieldChanged(string value)
    {
        UpdateButtonInteractable();
    }

    private void UpdateButtonInteractable()
    {
        bool cubeScaleValid = IsValidInput(CubeScale != null ? CubeScale.text : "");
        bool gridScaleValid = IsValidInput(GridScale != null ? GridScale.text : "");
        bool shouldEnable = cubeScaleValid && gridScaleValid;

        if (CubeInstantiate_A != null) CubeInstantiate_A.interactable = shouldEnable;
        if (CubeInstantiate_B != null) CubeInstantiate_B.interactable = shouldEnable;
    }

    private bool IsValidInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        float result;
        return float.TryParse(input, out result) && result > 0;
    }
}