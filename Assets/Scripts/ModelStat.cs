using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class ModelStat : MonoBehaviour
{
    [SerializeField] private ModelData modelData;

    [SerializeField] private string fileName;
    [SerializeField] private string shapeType;
    [SerializeField] private Vector3 position;
    [SerializeField] private Vector3 rotation;
    [SerializeField] private Vector3 scale;
    [SerializeField] private Color materialColor; // 新增顏色欄位
    [SerializeField] private string timestamp;

    [SerializeField] private bool showDebugInfo = false;

    public ModelData ModelData
    {
        get { return modelData; }
        set { modelData = value; }
    }

    void Start()
    {
        if (string.IsNullOrEmpty(modelData.filename))
        {
            InitializeModelData();
        }
    }

    void Update()
    {
        UpdateTransformData();
    }

    public void InitializeModelData()
    {
        CubeCarvingSystem carvingSystem = GetComponent<CubeCarvingSystem>();
        string shapeType = "Cube";

        if (carvingSystem != null)
        {
            shapeType = carvingSystem.GetShapeType().ToString();
        }

        // 嘗試從材質獲取當前顏色
        Color currentColor = Color.white;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            currentColor = renderer.material.color;
        }

        modelData = new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType,
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            materialColor = currentColor, // 保存顏色
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        // 更新序列化欄位
        UpdateSerializedFields();
    }

    public void SetModelData(ModelData data)
    {
        modelData = data;
        UpdateSerializedFields();
    }

    private void UpdateSerializedFields()
    {
        fileName = modelData.filename;
        shapeType = modelData.shapeType;
        position = modelData.position;
        rotation = modelData.rotation;
        scale = modelData.scale;
        materialColor = modelData.materialColor; // 更新顏色欄位
        timestamp = modelData.timestamp;
    }

    public ModelData CreateModelData(string filename, string shapeType, Color color)
    {
        modelData = new ModelData
        {
            filename = filename,
            shapeType = shapeType,
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            materialColor = color, // 設定顏色
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

        UpdateSerializedFields();
        return modelData;
    }

    // 保留原來的方法以向後兼容
    public ModelData CreateModelData(string filename, string shapeType)
    {
        Color currentColor = Color.white;
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            currentColor = renderer.material.color;
        }

        return CreateModelData(filename, shapeType, currentColor);
    }

    private void UpdateTransformData()
    {
        if (HasTransformChanged())
        {
            modelData.position = transform.position;
            modelData.rotation = transform.eulerAngles;
            modelData.scale = transform.localScale;

            // 同步更新序列化欄位
            position = modelData.position;
            rotation = modelData.rotation;
            scale = modelData.scale;
        }
    }

    private bool HasTransformChanged()
    {
        return modelData.position != transform.position ||
               modelData.rotation != transform.eulerAngles ||
               modelData.scale != transform.localScale;
    }

    public void UpdateModelData()
    {
        // 更新顏色資訊
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            modelData.materialColor = renderer.material.color;
        }

        modelData.position = transform.position;
        modelData.rotation = transform.eulerAngles;
        modelData.scale = transform.localScale;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        UpdateSerializedFields();
    }

    public void UpdateColor(Color newColor)
    {
        modelData.materialColor = newColor;
        materialColor = newColor;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timestamp = modelData.timestamp;
    }

    public void UpdateShapeType(string newShapeType)
    {
        modelData.shapeType = newShapeType;
        shapeType = newShapeType;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        timestamp = modelData.timestamp;
    }

    public bool IsModelDataValid()
    {
        return !string.IsNullOrEmpty(modelData.filename) &&
               !string.IsNullOrEmpty(modelData.shapeType) &&
               !string.IsNullOrEmpty(modelData.timestamp);
    }

    public void ResetModelData()
    {
        modelData = new ModelData();
        UpdateSerializedFields();
    }

    // 新增方法：獲取保存的顏色
    public Color GetSavedColor()
    {
        return modelData.materialColor;
    }

    [System.Serializable]
    public struct ModelDataDisplay
    {
        public string filename;
        public string shapeType;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Color materialColor; // 新增顏色顯示
        public string timestamp;
    }

#if UNITY_EDITOR
    [SerializeField] private ModelDataDisplay displayData;

    void OnValidate()
    {
        displayData.filename = modelData.filename;
        displayData.shapeType = modelData.shapeType;
        displayData.position = modelData.position;
        displayData.rotation = modelData.rotation;
        displayData.scale = modelData.scale;
        displayData.materialColor = modelData.materialColor; // 顯示顏色
        displayData.timestamp = modelData.timestamp;
    }
#endif
}