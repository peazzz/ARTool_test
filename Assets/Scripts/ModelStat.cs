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

        modelData = new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType,
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };

    }

    public void SetModelData(ModelData data)
    {
        modelData = data;

        fileName = data.filename;
        shapeType = data.shapeType;
        position = data.position;
        rotation = data.rotation;
        scale = data.scale;
        timestamp = data.timestamp;

    }

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

        this.fileName = filename;
        this.shapeType = shapeType;
        this.position = transform.position;
        this.rotation = transform.eulerAngles;
        this.scale = transform.localScale;
        this.timestamp = modelData.timestamp;

        return modelData;
    }

    private void UpdateTransformData()
    {
        if (HasTransformChanged())
        {
            modelData.position = transform.position;
            modelData.rotation = transform.eulerAngles;
            modelData.scale = transform.localScale;
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
        modelData.position = transform.position;
        modelData.rotation = transform.eulerAngles;
        modelData.scale = transform.localScale;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        position = modelData.position;
        rotation = modelData.rotation;
        scale = modelData.scale;
        timestamp = modelData.timestamp;

    }


    public void UpdateShapeType(string newShapeType)
    {
        modelData.shapeType = newShapeType;
        modelData.timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
    }

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
    [SerializeField] private ModelDataDisplay displayData;

    void OnValidate()
    {
        displayData.filename = modelData.filename;
        displayData.shapeType = modelData.shapeType;
        displayData.position = modelData.position;
        displayData.rotation = modelData.rotation;
        displayData.scale = modelData.scale;
        displayData.timestamp = modelData.timestamp;
    }
#endif
}