using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FounctionManager : MonoBehaviour
{
    public GameObject ClearButton;

    [Header("預製物件設定")]
    public GameObject voxelCubePrefab;          // 帶有VoxelCube組件的預製物
    public GameObject cubeCarvingSystemPrefab;  // 帶有CubeCarvingSystem組件的預製物

    [Header("場景設定")]
    public Transform parentObject;              // 生成物件的父物件
    public Camera targetCamera;                 // 目標攝影機(如果為空則使用主攝影機)

    [Header("預設參數")]
    public float defaultCubeSize = 1f;          // 預設大小
    public int defaultGridSize = 10;            // 預設Grid大小

    [Header("引用")]
    public UIManager uiManager;                 // UIManager的引用

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    // === 新版生成方法（支援完整參數控制和預覽模式）===

    public GameObject GenerateShapeWithParameters(VoxelShape shapeType, Vector3 scale, int gridSize, bool isPreview = false)
    {
        if (cubeCarvingSystemPrefab == null)
        {
            Debug.LogError("CubeCarvingSystem預製物未設定!");
            return null;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);

        //if (parentObject != null)
        //    newCarvingSystem.transform.SetParent(parentObject);

        // 應用縮放
        newCarvingSystem.transform.localScale = scale;

        // 設定物件名稱
        string suffix = isPreview ? "_Preview" : "_Final";
        newCarvingSystem.name = $"CubeCarvingSystem_{shapeType}{suffix}";

        CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            // 直接使用統一的gridSize
            carvingSystem.SetParameters(defaultCubeSize, gridSize, shapeType);

            // 如果是預覽模式，可以設定特殊屬性
            if (isPreview)
            {
                // 預覽模式下可以禁用某些功能，比如雕刻
                // 未來可以添加 carvingSystem.SetPreviewMode(true);
            }
        }
        else
        {
            Debug.LogError("預製物上找不到CubeCarvingSystem組件!");
        }

        // 只有最終模型才顯示清除按鈕
        if (!isPreview)
        {
            ClearButton.SetActive(true);
        }

        Debug.Log($"生成{(isPreview ? "預覽" : "最終")}{shapeType}模型 - Scale: {scale}, GridSize: {gridSize}");

        return newCarvingSystem;
    }

    // === 舊版生成方法（保留向後兼容）===

    public void SculptModelA()
    {
        GenerateVoxelCube();
        ClearButton.SetActive(true);
    }

    public void SculptModelB()
    {
        GenerateCubeCarvingSystem();
        ClearButton.SetActive(true);
    }

    // 直接生成指定形狀的CubeCarvingSystem（舊版方法）
    public void GenerateShape(VoxelShape shapeType)
    {
        if (cubeCarvingSystemPrefab == null)
        {
            Debug.LogError("CubeCarvingSystem預製物未設定!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();
        GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);

        if (parentObject != null)
            newCarvingSystem.transform.SetParent(parentObject);

        CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            carvingSystem.SetParameters(defaultCubeSize, defaultGridSize, shapeType);
        }
        else
        {
            Debug.LogError("預製物上找不到CubeCarvingSystem組件!");
        }

        ClearButton.SetActive(true);
        Debug.Log($"生成{shapeType}模型 - CubeSize: {defaultCubeSize}, GridSize: {defaultGridSize}");
    }

    // 個別的形狀生成方法（舊版方法）
    public void GenerateCube()
    {
        GenerateShape(VoxelShape.Cube);
    }

    public void GenerateSphere()
    {
        GenerateShape(VoxelShape.Sphere);
    }

    public void GenerateCapsule()
    {
        GenerateShape(VoxelShape.Capsule);
    }

    public void GenerateCylinder()
    {
        GenerateShape(VoxelShape.Cylinder);
    }

    private void GenerateVoxelCube()
    {
        if (voxelCubePrefab == null)
        {
            Debug.LogError("VoxelCube預製物未設定!");
            return;
        }

        if (uiManager == null)
        {
            Debug.LogError("UIManager未設定!");
            return;
        }

        float cubeSize = GetCubeScaleValue();
        int gridSize = GetGridScaleValue();

        if (cubeSize <= 0 || gridSize <= 0)
        {
            Debug.LogError("輸入值無效!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();

        GameObject newVoxelCube = Instantiate(voxelCubePrefab, spawnPosition, Quaternion.identity);

        if (parentObject != null)
            newVoxelCube.transform.SetParent(parentObject);

        VoxelCube voxelCube = newVoxelCube.GetComponent<VoxelCube>();
        if (voxelCube != null)
        {
            voxelCube.splitCount = gridSize;
            newVoxelCube.transform.localScale = Vector3.one * cubeSize;
        }
        else
        {
            Debug.LogError("預製物上找不到VoxelCube組件!");
        }

        Debug.Log($"生成VoxelCube - CubeSize: {cubeSize}, GridSize: {gridSize}, Position: {spawnPosition}");
    }

    private void GenerateCubeCarvingSystem()
    {
        if (cubeCarvingSystemPrefab == null)
        {
            Debug.LogError("CubeCarvingSystem預製物未設定!");
            return;
        }

        if (uiManager == null)
        {
            Debug.LogError("UIManager未設定!");
            return;
        }

        float cubeSize = GetCubeScaleValue();
        int gridSize = GetGridScaleValue();

        if (cubeSize <= 0 || gridSize <= 0)
        {
            Debug.LogError("輸入值無效!");
            return;
        }

        Vector3 spawnPosition = GetSpawnPosition();

        GameObject newCarvingSystem = Instantiate(cubeCarvingSystemPrefab, spawnPosition, Quaternion.identity);

        if (parentObject != null)
            newCarvingSystem.transform.SetParent(parentObject);

        CubeCarvingSystem carvingSystem = newCarvingSystem.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            carvingSystem.SetParameters(cubeSize, gridSize, VoxelShape.Cube);
        }
        else
        {
            Debug.LogError("預製物上找不到CubeCarvingSystem組件!");
        }

        Debug.Log($"生成CubeCarvingSystem - CubeSize: {cubeSize}, GridSize: {gridSize}, Position: {spawnPosition}");
    }

    private float GetCubeScaleValue()
    {
        if (uiManager != null && uiManager.CubeScale != null && float.TryParse(uiManager.CubeScale.text, out float value))
            return value;
        return defaultCubeSize;
    }

    private int GetGridScaleValue()
    {
        if (uiManager != null && uiManager.GridScale != null && int.TryParse(uiManager.GridScale.text, out int value))
            return value;
        return defaultGridSize;
    }

    private Vector3 GetSpawnPosition()
    {
        if (targetCamera != null)
        {
            return targetCamera.transform.position + targetCamera.transform.forward * 1.5f;
        }
        else
        {
            Debug.LogWarning("未設定目標攝影機，使用預設位置");
            return Vector3.forward;
        }
    }
}