using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ModelStat : MonoBehaviour
{
    [Header("模型數據")]
    [SerializeField] private ModelData modelData;

    [Header("模型數據詳情 (Inspector顯示)")]
    [SerializeField] private string fileName;
    [SerializeField] private string shapeType;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;
    [SerializeField] private Vector3 scale;
    [SerializeField] private string timestamp;

    [Header("調試信息")]
    [SerializeField] private bool showDebugInfo = false;

    // 提供外部訪問模型數據的屬性
    public ModelData ModelData
    {
        get { return modelData; }
        set { modelData = value; }
    }

    void Start()
    {
        // 如果模型數據還沒有初始化，嘗試從當前物件獲取
        if (string.IsNullOrEmpty(modelData.filename))
        {
            InitializeModelData();
        }

        if (showDebugInfo)
        {
            LogModelData();
        }
    }

    void Update()
    {
        // 實時更新位置、旋轉、縮放數據（可選）
        UpdateTransformData();
    }

    /// <summary>
    /// 初始化模型數據
    /// </summary>
    public void InitializeModelData()
    {
        // 從CubeCarvingSystem獲取形狀類型
        CubeCarvingSystem carvingSystem = GetComponent<CubeCarvingSystem>();
        string shapeType = "Cube"; // 默認值

        if (carvingSystem != null)
        {
            shapeType = carvingSystem.GetShapeType().ToString();
        }

        modelData = new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType,
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        if (showDebugInfo)
        {
            Debug.Log($"ModelStat 初始化完成: {gameObject.name}");
        }
    }

    /// <summary>
    /// 設置模型數據
    /// </summary>
    /// <param name="data">要設置的模型數據</param>
    public void SetModelData(ModelData data)
    {
        modelData = data;

        // 同步到 Inspector 顯示字段
        fileName = data.filename;
        shapeType = data.shapeType;
        position = data.position;
        rotation = data.rotation;
        scale = data.scale;
        timestamp = data.timestamp;

        if (showDebugInfo)
        {
            Debug.Log($"ModelStat 數據已更新: {data.filename}");
        }
    }

    /// <summary>
    /// 創建模型數據
    /// </summary>
    /// <param name="filename">文件名</param>
    /// <param name="shapeType">形狀類型</param>
    /// <returns>創建的模型數據</returns>
    public ModelData CreateModelData(string filename, string shapeType)
    {
        modelData = new ModelData
        {
            filename = filename,
            shapeType = shapeType,
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // 同步到 Inspector 顯示字段
        this.fileName = filename;
        this.shapeType = shapeType;
        this.position = transform.position;
        this.rotation = transform.eulerAngles;
        this.scale = transform.localScale;
        this.timestamp = modelData.timestamp;

        return modelData;
    }

    /// <summary>
    /// 更新變換數據（位置、旋轉、縮放）
    /// </summary>
    private void UpdateTransformData()
    {
        // 只在數據有變化時更新，避免不必要的性能消耗
        if (HasTransformChanged())
        {
            modelData.position = transform.position;
            modelData.rotation = transform.eulerAngles;
            modelData.scale = transform.localScale;
        }
    }

    /// <summary>
    /// 檢查變換是否有變化
    /// </summary>
    /// <returns>如果有變化返回true</returns>
    private bool HasTransformChanged()
    {
        return modelData.position != transform.position ||
               modelData.rotation != transform.eulerAngles ||
               modelData.scale != transform.localScale;
    }

    /// <summary>
    /// 手動更新模型數據
    /// </summary>
    public void UpdateModelData()
    {
        modelData.position = transform.position;
        modelData.rotation = transform.eulerAngles;
        modelData.scale = transform.localScale;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // 同步到 Inspector 顯示字段
        position = modelData.position;
        rotation = modelData.rotation;
        scale = modelData.scale;
        timestamp = modelData.timestamp;

        if (showDebugInfo)
        {
            Debug.Log($"ModelStat 手動更新: {modelData.filename}");
        }
    }

    /// <summary>
    /// 更新形狀類型
    /// </summary>
    /// <param name="newShapeType">新的形狀類型</param>
    public void UpdateShapeType(string newShapeType)
    {
        modelData.shapeType = newShapeType;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        if (showDebugInfo)
        {
            Debug.Log($"形狀類型已更新為: {newShapeType}");
        }
    }

    /// <summary>
    /// 獲取模型的詳細信息字符串
    /// </summary>
    /// <returns>包含所有模型信息的字符串</returns>
    public string GetModelInfoString()
    {
        return $"文件名: {modelData.filename}\n" +
               $"形狀: {modelData.shapeType}\n" +
               $"位置: {modelData.position}\n" +
               $"旋轉: {modelData.rotation}\n" +
               $"縮放: {modelData.scale}\n" +
               $"創建時間: {modelData.timestamp}";
    }

    /// <summary>
    /// 記錄模型數據到控制台
    /// </summary>
    private void LogModelData()
    {
        Debug.Log($"=== ModelStat 信息 ===\n{GetModelInfoString()}");
    }

    /// <summary>
    /// 驗證模型數據是否完整
    /// </summary>
    /// <returns>如果數據完整返回true</returns>
    public bool IsModelDataValid()
    {
        return !string.IsNullOrEmpty(modelData.filename) &&
               !string.IsNullOrEmpty(modelData.shapeType) &&
               !string.IsNullOrEmpty(modelData.timestamp);
    }

    /// <summary>
    /// 重置模型數據
    /// </summary>
    public void ResetModelData()
    {
        modelData = new ModelData();

        if (showDebugInfo)
        {
            Debug.Log("ModelStat 數據已重置");
        }
    }

    // 在Inspector中顯示當前模型數據（僅在編輯器中）
    [System.Serializable]
    public struct ModelDataDisplay
    {
        public string filename;
        public string shapeType;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public string timestamp;
    }

#if UNITY_EDITOR
    [Header("當前模型數據 (只讀)")]
    [SerializeField] private ModelDataDisplay displayData;

    void OnValidate()
    {
        // 在編輯器中同步顯示數據
        displayData.filename = modelData.filename;
        displayData.shapeType = modelData.shapeType;
        displayData.position = modelData.position;
        displayData.rotation = modelData.rotation;
        displayData.scale = modelData.scale;
        displayData.timestamp = modelData.timestamp;
    }
#endif
}