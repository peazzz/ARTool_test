using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    private bool UI_on;
    private bool SculptUI_on;
    public GameObject BackButton;
    public GameObject UIHome;

    [Header("Sculpt")]
    public GameObject SculptContent;
    public InputField CubeScale;
    public InputField GridScale;
    public Button CubeInstantiate_A;
    public Button CubeInstantiate_B;

    [Header("形狀選擇按鈕 - 直接生成")]
    public Button ShapeButton_Cube;      // 正方體按鈕
    public Button ShapeButton_Sphere;    // 圓體按鈕
    public Button ShapeButton_Capsule;   // 膠囊體按鈕
    public Button ShapeButton_Cylinder;  // 圓柱體按鈕

    [Header("動態調整UI")]
    public GameObject DynamicAdjustPanel;     // 動態調整面板
    public InputField DynamicCubeScale;       // 動態大小調整
    public InputField DynamicGridScale;       // 動態Grid調整
    public Button ApplyChangesButton;         // 應用變更按鈕
    public Button CloseDynamicPanelButton;    // 關閉面板按鈕
    public Text CurrentObjectInfo;            // 顯示當前選中物件資訊

    [Header("功能管理器")]
    public FounctionManager functionManager;  // 必須加FounctionManager的引用

    // 當前選中的CubeCarvingSystem物件
    private CubeCarvingSystem selectedCarvingSystem;

    // Start is called before the first frame update
    void Start()
    {
        UI_on = false;
        FounctionUI_RT.anchoredPosition = new Vector3(0, -320, 0);
        HandleArrow_RT.localScale = new Vector3(0.6f, 0.6f, 0.6f);

        // 初始時按鈕不可互動
        UpdateButtonInteractable();

        // 監聽輸入欄位的變化
        if (CubeScale != null) CubeScale.onValueChanged.AddListener(OnInputFieldChanged);
        if (GridScale != null) GridScale.onValueChanged.AddListener(OnInputFieldChanged);

        // 連接按鈕事件
        SetupButtonEvents();

        // 設定形狀按鈕事件 - 直接生成
        SetupShapeButtonEvents();

        // 設定動態調整事件
        SetupDynamicAdjustEvents();

        // 初始隱藏動態調整面板
        if (DynamicAdjustPanel != null)
            DynamicAdjustPanel.SetActive(false);
    }

    void Update()
    {
        // 檢測滑鼠點擊選擇物件
        if (Input.GetMouseButtonDown(0))
        {
            CheckObjectSelection();
        }
    }

    private void SetupButtonEvents()
    {
        if (functionManager != null)
        {
            // 連接按鈕A到SculptModelA方法
            if (CubeInstantiate_A != null)
            {
                CubeInstantiate_A.onClick.AddListener(() => {
                    functionManager.SculptModelA();
                    Debug.Log("生成VoxelCube模型");
                });
            }

            // 連接按鈕B到SculptModelB方法
            if (CubeInstantiate_B != null)
            {
                CubeInstantiate_B.onClick.AddListener(() => {
                    functionManager.SculptModelB();
                    Debug.Log("生成CubeCarvingSystem模型");
                });
            }
        }
        else
        {
            Debug.LogError("FounctionManager未設定!請在Inspector中指派");
        }
    }

    private void SetupShapeButtonEvents()
    {
        if (functionManager != null)
        {
            // 設定形狀選擇按鈕的事件 - 直接生成模型
            if (ShapeButton_Cube != null)
            {
                ShapeButton_Cube.onClick.AddListener(() => {
                    functionManager.GenerateCube();
                    Debug.Log("直接生成正方體模型");
                });
            }

            if (ShapeButton_Sphere != null)
            {
                ShapeButton_Sphere.onClick.AddListener(() => {
                    functionManager.GenerateSphere();
                    Debug.Log("直接生成圓體模型");
                });
            }

            if (ShapeButton_Capsule != null)
            {
                ShapeButton_Capsule.onClick.AddListener(() => {
                    functionManager.GenerateCapsule();
                    Debug.Log("直接生成膠囊體模型");
                });
            }

            if (ShapeButton_Cylinder != null)
            {
                ShapeButton_Cylinder.onClick.AddListener(() => {
                    functionManager.GenerateCylinder();
                    Debug.Log("直接生成圓柱體模型");
                });
            }
        }
        else
        {
            Debug.LogError("FounctionManager未設定!無法設定形狀按鈕事件");
        }
    }

    private void SetupDynamicAdjustEvents()
    {
        // 應用變更按鈕
        if (ApplyChangesButton != null)
        {
            ApplyChangesButton.onClick.AddListener(ApplyDynamicChanges);
        }

        // 關閉面板按鈕
        if (CloseDynamicPanelButton != null)
        {
            CloseDynamicPanelButton.onClick.AddListener(CloseDynamicPanel);
        }
    }

    private void CheckObjectSelection()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            CubeCarvingSystem carvingSystem = hit.collider.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                SelectCarvingSystem(carvingSystem);
            }
        }
    }

    private void SelectCarvingSystem(CubeCarvingSystem carvingSystem)
    {
        selectedCarvingSystem = carvingSystem;

        // 顯示動態調整面板
        if (DynamicAdjustPanel != null)
        {
            DynamicAdjustPanel.SetActive(true);
        }

        // 更新當前數值到輸入欄位
        UpdateDynamicInputFields();

        // 更新物件資訊顯示
        UpdateCurrentObjectInfo();

        Debug.Log($"選中物件: {carvingSystem.gameObject.name}");
    }

    private void UpdateDynamicInputFields()
    {
        if (selectedCarvingSystem == null) return;

        // 使用反射獲取當前數值
        var cubeSizeField = typeof(CubeCarvingSystem).GetField("cubeSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var gridSizeField = typeof(CubeCarvingSystem).GetField("gridSize",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (cubeSizeField != null && DynamicCubeScale != null)
        {
            float currentCubeSize = (float)cubeSizeField.GetValue(selectedCarvingSystem);
            DynamicCubeScale.text = currentCubeSize.ToString();
        }

        if (gridSizeField != null && DynamicGridScale != null)
        {
            int currentGridSize = (int)gridSizeField.GetValue(selectedCarvingSystem);
            DynamicGridScale.text = currentGridSize.ToString();
        }
    }

    private void UpdateCurrentObjectInfo()
    {
        if (selectedCarvingSystem == null || CurrentObjectInfo == null) return;

        // 獲取當前物件資訊
        var shapeTypeField = typeof(CubeCarvingSystem).GetField("shapeType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (shapeTypeField != null)
        {
            VoxelShape currentShape = (VoxelShape)shapeTypeField.GetValue(selectedCarvingSystem);
            CurrentObjectInfo.text = $"選中物件: {GetShapeDisplayName(currentShape)}\n物件名稱: {selectedCarvingSystem.gameObject.name}";
        }
    }

    private string GetShapeDisplayName(VoxelShape shape)
    {
        switch (shape)
        {
            case VoxelShape.Cube: return "正方體";
            case VoxelShape.Sphere: return "圓體";
            case VoxelShape.Capsule: return "膠囊體";
            case VoxelShape.Cylinder: return "圓柱體";
            default: return "未知形狀";
        }
    }

    private void ApplyDynamicChanges()
    {
        if (selectedCarvingSystem == null) return;

        // 獲取新的數值
        float newCubeSize = 1f;
        int newGridSize = 10;

        if (DynamicCubeScale != null && float.TryParse(DynamicCubeScale.text, out float cubeSize) && cubeSize > 0)
        {
            newCubeSize = cubeSize;
        }

        if (DynamicGridScale != null && int.TryParse(DynamicGridScale.text, out int gridSize) && gridSize > 0)
        {
            newGridSize = gridSize;
        }

        // 獲取當前形狀類型
        var shapeTypeField = typeof(CubeCarvingSystem).GetField("shapeType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        VoxelShape currentShape = VoxelShape.Cube;
        if (shapeTypeField != null)
        {
            currentShape = (VoxelShape)shapeTypeField.GetValue(selectedCarvingSystem);
        }

        // 應用新參數
        selectedCarvingSystem.SetParameters(newCubeSize, newGridSize, currentShape);

        Debug.Log($"應用變更 - CubeSize: {newCubeSize}, GridSize: {newGridSize}, Shape: {currentShape}");
    }

    private void CloseDynamicPanel()
    {
        if (DynamicAdjustPanel != null)
        {
            DynamicAdjustPanel.SetActive(false);
        }
        selectedCarvingSystem = null;
        Debug.Log("關閉動態調整面板");
    }

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
            FounctionUI_RT.anchoredPosition = new Vector3(0, -320, 0);
            HandleArrow_RT.localScale = new Vector3(0.6f, 0.6f, 0.6f);
        }
    }

    public void SculptButton()
    {
        SculptUI_on = true;
        SculptContent.SetActive(true);
        UIHome.SetActive(false);

        BackButton.SetActive(true);
    }

    public void Back()
    {
        SculptUI_on = false;
        UIHome.SetActive(true);
        SculptContent.SetActive(false);
        BackButton.SetActive(false);
    }

    public void Clear()
    {
        for (int i = functionManager.parentObject.childCount - 1; i >= 0; i--)
        {
            Transform child = functionManager.parentObject.GetChild(i);

            if (child.gameObject.layer == LayerMask.NameToLayer("SculptObject"))
            {
                Destroy(child.gameObject);
            }
        }

        // 清除時也關閉動態調整面板
        CloseDynamicPanel();
    }

    private void OnInputFieldChanged(string value)
    {
        UpdateButtonInteractable();
    }

    // 更新按鈕的可互動狀態
    private void UpdateButtonInteractable()
    {
        bool cubeScaleValid = IsValidInput(CubeScale != null ? CubeScale.text : "");
        bool gridScaleValid = IsValidInput(GridScale != null ? GridScale.text : "");

        // 當兩個輸入欄位都有有效值時，按鈕才可互動
        bool shouldEnable = cubeScaleValid && gridScaleValid;

        if (CubeInstantiate_A != null) CubeInstantiate_A.interactable = shouldEnable;
        if (CubeInstantiate_B != null) CubeInstantiate_B.interactable = shouldEnable;
    }

    // 檢查輸入是否有效（非空且為有效數字）
    private bool IsValidInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        float result;
        return float.TryParse(input, out result) && result > 0; // 假設需要正數
    }
}