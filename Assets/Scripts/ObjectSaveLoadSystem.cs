using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

[System.Serializable]
public class SavedObjectData
{
    [System.Serializable]
    public class ObjectInfo
    {
        public string name;
        public string id;
        public string timestamp;
        public string version = "1.0";
        public string objectType;
    }

    [System.Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
    }

    [System.Serializable]
    public class GeometryData
    {
        public string shapeType;
        public int gridSize;
        public float cubeSize;
        public string uvMode;
    }

    [System.Serializable]
    public class CarvingData
    {
        public bool hasCarving;
        public string originalShape;
        public int[] gridDimensions = new int[3];
        public string voxelData; // Base64 編碼的壓縮體素資料
        public string compressionMethod = "BitPacking";
    }

    [System.Serializable]
    public class MaterialData
    {
        public string materialType;
        public string shaderName;
        public ColorProperty colorTint;
        public TextureProperty paintTexture;
        public ColorProperty albedoColor;
        public TextureProperty albedoTexture;

        [System.Serializable]
        public class ColorProperty
        {
            public float r, g, b, a;
            public ColorProperty() { }
            public ColorProperty(Color color)
            {
                r = color.r; g = color.g; b = color.b; a = color.a;
            }
            public Color ToColor() => new Color(r, g, b, a);
        }

        [System.Serializable]
        public class TextureProperty
        {
            public bool hasTexture;
            public string textureData; // Base64 編碼的貼圖資料
            public int width, height;
            public float opacity = 1f;
        }
    }

    public ObjectInfo objectInfo = new ObjectInfo();
    public TransformData transform = new TransformData();
    public GeometryData geometry = new GeometryData();
    public CarvingData carvingData = new CarvingData();
    public MaterialData materials = new MaterialData();
}

public class ObjectSaveLoadSystem : MonoBehaviour
{
    [Header("UI References")]
    public Button saveButton;
    public Button loadButton;
    public SculptFunction sculptFunction;
    public UIManager uiManager;

    [Header("Save Settings")]
    public bool autoSaveOnFinish = true;
    public string saveFileName = "ARTool_Object";
    public bool showSaveDialog = true;
    public Text saveStatusText;

    [Header("Load Settings")]
    public Transform spawnParent;
    public GameObject cubeCarvingSystemPrefab;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    // 內部結果類別
    [System.Serializable]
    private class LoadResult
    {
        public bool success;
        public SavedObjectData loadData;
        public string errorMessage;
    }

    [System.Serializable]
    private class RestoreResult
    {
        public bool success;
        public bool[,,] voxelData;
        public string errorMessage;
    }

    private void Start()
    {
        SetupButtonEvents();
        CreateSaveDirectory();
    }

    private void SetupButtonEvents()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveCurrentSelectedObject);

        if (loadButton != null)
            loadButton.onClick.AddListener(OpenLoadInterface);
    }

    private void CreateSaveDirectory()
    {
        try
        {
            string objectsPath = GetObjectsSavePath();
            if (!Directory.Exists(objectsPath))
            {
                Directory.CreateDirectory(objectsPath);
                if (enableDebugLogs)
                    Debug.Log($"建立儲存目錄: {objectsPath}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"建立儲存目錄失敗: {e.Message}");
        }
    }

    private string GetObjectsSavePath()
    {
        string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documentsPath, "ARTool", "Object");
    }

    /// <summary>
    /// 儲存當前選擇的物件
    /// </summary>
    public void SaveCurrentSelectedObject()
    {
        if (sculptFunction == null)
        {
            ShowSaveStatus("錯誤：找不到 SculptFunction", false);
            return;
        }

        GameObject selectedObject = sculptFunction.currentSelectedObject;
        if (selectedObject == null)
        {
            ShowSaveStatus("請先選擇一個物件", false);
            return;
        }

        StartCoroutine(SaveObjectCoroutine(selectedObject));
    }

    private IEnumerator SaveObjectCoroutine(GameObject targetObject)
    {
        ShowSaveStatus("正在儲存物件...", true);

        try
        {
            // 收集物件資料
            SavedObjectData saveData = CollectObjectData(targetObject);

            // 轉換為 JSON
            string jsonData = JsonUtility.ToJson(saveData, true);

            // 產生唯一檔名
            string fileName = GenerateUniqueFileName(saveData.objectInfo.name);
            string filePath = Path.Combine(GetObjectsSavePath(), fileName);

            // 寫入檔案
            File.WriteAllText(filePath, jsonData);

            ShowSaveStatus($"物件已儲存：{fileName}", true);

            if (enableDebugLogs)
                Debug.Log($"物件儲存成功：{filePath}");
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("儲存失敗", false);
            Debug.LogError($"儲存物件時發生錯誤：{e.Message}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    private SavedObjectData CollectObjectData(GameObject targetObject)
    {
        SavedObjectData saveData = new SavedObjectData();

        // 基本物件資訊
        saveData.objectInfo.name = targetObject.name;
        saveData.objectInfo.id = GenerateObjectId();
        saveData.objectInfo.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        saveData.objectInfo.objectType = "CubeCarvingSystem";

        // Transform 資料
        saveData.transform.position = targetObject.transform.position;
        saveData.transform.rotation = targetObject.transform.eulerAngles;
        saveData.transform.scale = targetObject.transform.localScale;

        // CubeCarvingSystem 資料
        CubeCarvingSystem carvingSystem = targetObject.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            saveData.geometry.shapeType = carvingSystem.GetShapeType().ToString();
            saveData.geometry.gridSize = carvingSystem.GetGridSize();
            saveData.geometry.cubeSize = carvingSystem.GetCubeSize();
            saveData.geometry.uvMode = carvingSystem.GetUVMode().ToString();

            // 收集雕刻資料
            CollectCarvingData(carvingSystem, saveData.carvingData);
        }

        // Material 資料
        CollectMaterialData(targetObject, saveData.materials);

        return saveData;
    }

    private void CollectCarvingData(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        try
        {
            CubeCarvingSystem.ModelState modelState = carvingSystem.GetCurrentModelState();

            carvingData.hasCarving = true;
            carvingData.originalShape = modelState.shapeType.ToString();
            carvingData.gridDimensions[0] = modelState.gridSize;
            carvingData.gridDimensions[1] = modelState.gridSize;
            carvingData.gridDimensions[2] = modelState.gridSize;

            // 壓縮並編碼 voxel 資料
            if (modelState.voxelData != null)
            {
                Debug.Log($"儲存 voxel 資料，維度: {modelState.voxelData.GetLength(0)}x{modelState.voxelData.GetLength(1)}x{modelState.voxelData.GetLength(2)}");

                // 計算有多少 voxel 是 true（即未被雕刻）
                int activeVoxels = 0;
                int totalVoxels = modelState.voxelData.GetLength(0) * modelState.voxelData.GetLength(1) * modelState.voxelData.GetLength(2);
                for (int x = 0; x < modelState.voxelData.GetLength(0); x++)
                    for (int y = 0; y < modelState.voxelData.GetLength(1); y++)
                        for (int z = 0; z < modelState.voxelData.GetLength(2); z++)
                            if (modelState.voxelData[x, y, z]) activeVoxels++;

                Debug.Log($"活躍 voxels: {activeVoxels}/{totalVoxels}, 雕刻率: {((totalVoxels - activeVoxels) / (float)totalVoxels * 100):F1}%");

                byte[] compressedData = CompressVoxelData(modelState.voxelData);
                carvingData.voxelData = System.Convert.ToBase64String(compressedData);
            }
            else
            {
                Debug.LogWarning("modelState.voxelData 是 null！");
            }

            if (enableDebugLogs)
                Debug.Log($"收集雕刻資料完成，Grid: {carvingData.gridDimensions[0]}x{carvingData.gridDimensions[1]}x{carvingData.gridDimensions[2]}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"收集雕刻資料時發生錯誤：{e.Message}");
            carvingData.hasCarving = false;
        }
    }

    private byte[] CompressVoxelData(bool[,,] voxelData)
    {
        // 簡單的位元壓縮
        int xSize = voxelData.GetLength(0);
        int ySize = voxelData.GetLength(1);
        int zSize = voxelData.GetLength(2);

        int totalVoxels = xSize * ySize * zSize;
        int byteCount = (totalVoxels + 7) / 8; // 向上取整
        byte[] compressed = new byte[byteCount + 12]; // +12 for dimensions

        // 儲存維度資訊
        BitConverter.GetBytes(xSize).CopyTo(compressed, 0);
        BitConverter.GetBytes(ySize).CopyTo(compressed, 4);
        BitConverter.GetBytes(zSize).CopyTo(compressed, 8);

        int bitIndex = 0;
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                for (int z = 0; z < zSize; z++)
                {
                    if (voxelData[x, y, z])
                    {
                        int byteIndex = 12 + (bitIndex / 8);
                        int bitOffset = bitIndex % 8;
                        compressed[byteIndex] |= (byte)(1 << bitOffset);
                    }
                    bitIndex++;
                }
            }
        }

        return compressed;
    }

    private void CollectMaterialData(GameObject targetObject, SavedObjectData.MaterialData materialData)
    {
        DualMaterialManager dualManager = targetObject.GetComponent<DualMaterialManager>();
        MeshRenderer renderer = targetObject.GetComponent<MeshRenderer>();

        if (dualManager != null)
        {
            // 使用 DualMaterialManager 的資料
            materialData.materialType = dualManager.IsInTextureMode() ? "TextureMaterial" : "ColorMaterial";
            materialData.colorTint = new SavedObjectData.MaterialData.ColorProperty(dualManager.GetCurrentColor());

            if (dualManager.IsInTextureMode())
            {
                Texture2D currentTexture = dualManager.GetCurrentTexture();
                if (currentTexture != null)
                {
                    materialData.paintTexture = new SavedObjectData.MaterialData.TextureProperty();
                    materialData.paintTexture.hasTexture = true;
                    materialData.paintTexture.width = currentTexture.width;
                    materialData.paintTexture.height = currentTexture.height;
                    materialData.paintTexture.textureData = System.Convert.ToBase64String(currentTexture.EncodeToPNG());
                }
            }
        }
        else if (renderer != null && renderer.material != null)
        {
            // 使用基本 Material 資料
            materialData.materialType = "StandardMaterial";
            materialData.shaderName = renderer.material.shader.name;
            materialData.colorTint = new SavedObjectData.MaterialData.ColorProperty(renderer.material.color);
        }

        if (enableDebugLogs)
            Debug.Log($"收集材質資料完成，類型：{materialData.materialType}");
    }

    private string GenerateObjectId()
    {
        return "obj_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    private string GenerateUniqueFileName(string baseName)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{baseName}_{timestamp}.json";

        // 確保檔名唯一
        string fullPath = Path.Combine(GetObjectsSavePath(), fileName);
        int counter = 1;
        while (File.Exists(fullPath))
        {
            fileName = $"{baseName}_{timestamp}_{counter}.json";
            fullPath = Path.Combine(GetObjectsSavePath(), fileName);
            counter++;
        }

        return fileName;
    }

    /// <summary>
    /// 開啟載入介面（你會自己實現搜尋介面）
    /// </summary>
    public void OpenLoadInterface()
    {
        // 這裡你可以實現檔案選擇介面
        // 當選擇檔案後，呼叫 LoadObjectFromFile(filePath)

        if (enableDebugLogs)
            Debug.Log("開啟載入介面");
    }

    public void LoadTestObject()
    {
        string filePath = @"C:\Users\User\Documents\ARTool\Object\CubeCarvingSystem_Cube_Final_20250615_050615.json";
        LoadObjectFromFile(filePath);
    }

    /// <summary>
    /// 從檔案載入物件到場景中
    /// </summary>
    /// <param name="filePath">完整的檔案路徑</param>
    public void LoadObjectFromFile(string filePath)
    {
        StartCoroutine(LoadObjectCoroutine(filePath));
    }

    // 修正後的載入協程（避免 yield return 在 try-catch 中的問題）
    private IEnumerator LoadObjectCoroutine(string filePath)
    {
        ShowSaveStatus("正在載入物件...", true);

        // 先檢查檔案是否存在
        if (!File.Exists(filePath))
        {
            ShowSaveStatus("檔案不存在", false);
            yield return new WaitForSeconds(2f);
            HideSaveStatus();
            yield break;
        }

        // 分離 try-catch 和 yield return
        LoadResult result = LoadObjectData(filePath);

        if (result.success && result.loadData != null)
        {
            // 創建物件（這裡可以安全使用 yield）
            yield return StartCoroutine(CreateAndValidateObject(result.loadData));
        }
        else
        {
            ShowSaveStatus(result.errorMessage ?? "載入失敗", false);
            if (enableDebugLogs && !string.IsNullOrEmpty(result.errorMessage))
                Debug.LogError($"載入物件時發生錯誤：{result.errorMessage}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    // 分離的載入數據方法（不使用協程）
    private LoadResult LoadObjectData(string filePath)
    {
        try
        {
            string jsonData = File.ReadAllText(filePath);
            SavedObjectData loadData = JsonUtility.FromJson<SavedObjectData>(jsonData);

            return new LoadResult
            {
                success = true,
                loadData = loadData,
                errorMessage = null
            };
        }
        catch (System.Exception e)
        {
            return new LoadResult
            {
                success = false,
                loadData = null,
                errorMessage = e.Message
            };
        }
    }

    // 創建和驗證物件的協程
    private IEnumerator CreateAndValidateObject(SavedObjectData loadData)
    {
        GameObject newObject = CreateObjectFromData(loadData);

        if (newObject != null)
        {
            // 等待物件完全載入
            yield return new WaitForSeconds(0.5f);

            // 驗證載入結果
            CubeCarvingSystem carvingSystem = newObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                bool hasCarving = carvingSystem.HasCarvingMarks();
                MeshFilter meshFilter = newObject.GetComponent<MeshFilter>();
                int vertexCount = meshFilter?.mesh?.vertexCount ?? 0;

                Debug.Log($"載入驗證 - 物件: {newObject.name}, 有雕刻: {hasCarving}, 頂點數: {vertexCount}");

                if (loadData.carvingData.hasCarving && !hasCarving)
                {
                    Debug.LogWarning("警告：應該有雕刻痕跡但未正確載入！");

                    // 嘗試再次恢復雕刻數據
                    yield return StartCoroutine(ForceRestoreCarving(carvingSystem, loadData.carvingData));
                }
            }

            ShowSaveStatus($"載入成功：{loadData.objectInfo.name}", true);

            if (enableDebugLogs)
                Debug.Log($"物件載入成功：{newObject.name}");
        }
        else
        {
            ShowSaveStatus("創建物件失敗", false);
        }
    }

    private GameObject CreateObjectFromData(SavedObjectData loadData)
    {
        try
        {
            // 創建基本物件
            GameObject newObject;
            if (cubeCarvingSystemPrefab != null)
            {
                newObject = Instantiate(cubeCarvingSystemPrefab);
            }
            else
            {
                newObject = new GameObject("LoadedObject");
                newObject.AddComponent<CubeCarvingSystem>();
            }

            // 設定父物件
            if (spawnParent != null)
                newObject.transform.SetParent(spawnParent);

            // 設定基本屬性
            newObject.name = loadData.objectInfo.name;
            newObject.transform.position = loadData.transform.position;
            newObject.transform.rotation = Quaternion.Euler(loadData.transform.rotation);
            newObject.transform.localScale = loadData.transform.scale;

            // 設定 CubeCarvingSystem
            CubeCarvingSystem carvingSystem = newObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                // 解析形狀
                VoxelShape shapeType = (VoxelShape)System.Enum.Parse(typeof(VoxelShape), loadData.geometry.shapeType);

                // 首先設定基本參數（這會初始化基本形狀）
                carvingSystem.SetParameters(loadData.geometry.cubeSize, loadData.geometry.gridSize, shapeType);

                // 等待一幀後再恢復雕刻數據
                StartCoroutine(RestoreCarvingDataDelayed(carvingSystem, loadData.carvingData));
            }

            // 恢復材質
            RestoreMaterialData(newObject, loadData.materials);

            // 設定圖層和標籤
            int sculptLayer = LayerMask.NameToLayer("SculptObject");
            if (sculptLayer != -1)
                newObject.layer = sculptLayer;
            newObject.tag = "SculptObject";

            return newObject;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"創建物件時發生錯誤：{e.Message}");
            return null;
        }
    }

    // 延遲恢復雕刻數據的協程
    private IEnumerator RestoreCarvingDataDelayed(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        // 等待基本初始化完成
        yield return null;
        yield return null;

        // 現在恢復雕刻數據
        RestoreCarvingData(carvingSystem, carvingData);
    }

    private void RestoreCarvingData(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        if (string.IsNullOrEmpty(carvingData.voxelData))
        {
            Debug.LogWarning("carvingData.voxelData 是空的！");
            return;
        }

        // 處理數據恢復，但將可能出錯的部分分離
        var restoreResult = ProcessCarvingDataRestore(carvingData);

        if (restoreResult.success)
        {
            // 使用 CubeCarvingSystem 的公開方法設定 voxel 數據
            SetVoxelDataToSystem(carvingSystem, restoreResult.voxelData);

            if (enableDebugLogs)
                Debug.Log("雕刻數據恢復完成");
        }
        else
        {
            Debug.LogError($"恢復雕刻數據時發生錯誤：{restoreResult.errorMessage}");
        }
    }

    // 分離的數據處理方法
    private RestoreResult ProcessCarvingDataRestore(SavedObjectData.CarvingData carvingData)
    {
        try
        {
            // 解壓縮 voxel 數據
            byte[] compressedData = System.Convert.FromBase64String(carvingData.voxelData);
            bool[,,] voxelData = DecompressVoxelData(compressedData);

            Debug.Log($"載入 voxel 數據，維度: {voxelData.GetLength(0)}x{voxelData.GetLength(1)}x{voxelData.GetLength(2)}");

            // 計算載入的雕刻狀況
            int activeVoxels = 0;
            int totalVoxels = voxelData.GetLength(0) * voxelData.GetLength(1) * voxelData.GetLength(2);
            for (int x = 0; x < voxelData.GetLength(0); x++)
                for (int y = 0; y < voxelData.GetLength(1); y++)
                    for (int z = 0; z < voxelData.GetLength(2); z++)
                        if (voxelData[x, y, z]) activeVoxels++;

            Debug.Log($"載入的活躍 voxels: {activeVoxels}/{totalVoxels}, 雕刻率: {((totalVoxels - activeVoxels) / (float)totalVoxels * 100):F1}%");

            return new RestoreResult
            {
                success = true,
                voxelData = voxelData,
                errorMessage = null
            };
        }
        catch (System.Exception e)
        {
            return new RestoreResult
            {
                success = false,
                voxelData = null,
                errorMessage = e.Message
            };
        }
    }

    private bool[,,] DecompressVoxelData(byte[] compressedData)
    {
        // 讀取維度資訊
        int xSize = BitConverter.ToInt32(compressedData, 0);
        int ySize = BitConverter.ToInt32(compressedData, 4);
        int zSize = BitConverter.ToInt32(compressedData, 8);

        bool[,,] voxelData = new bool[xSize, ySize, zSize];

        int bitIndex = 0;
        for (int x = 0; x < xSize; x++)
        {
            for (int y = 0; y < ySize; y++)
            {
                for (int z = 0; z < zSize; z++)
                {
                    int byteIndex = 12 + (bitIndex / 8);
                    int bitOffset = bitIndex % 8;

                    if (byteIndex < compressedData.Length)
                    {
                        voxelData[x, y, z] = (compressedData[byteIndex] & (1 << bitOffset)) != 0;
                    }
                    bitIndex++;
                }
            }
        }

        return voxelData;
    }

    private void SetVoxelDataToSystem(CubeCarvingSystem carvingSystem, bool[,,] voxelData)
    {
        // 直接調用 SetVoxelData 方法
        carvingSystem.SetVoxelData(voxelData);

        // 等待兩幀後再次確保網格更新
        StartCoroutine(EnsureMeshUpdateMultipleTimes(carvingSystem));

        if (enableDebugLogs)
            Debug.Log("已使用 SetVoxelData 方法設定雕刻數據");
    }

    private IEnumerator EnsureMeshUpdateMultipleTimes(CubeCarvingSystem carvingSystem)
    {
        // 等待一幀
        yield return null;

        // 再等待一幀，確保所有初始化完成
        yield return null;

        // 使用反射強制重新生成網格（作為備用方案）
        var generateMeshMethod = typeof(CubeCarvingSystem).GetMethod("GenerateMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (generateMeshMethod != null)
        {
            generateMeshMethod.Invoke(carvingSystem, null);
            Debug.Log("通過反射強制重新生成網格");
        }

        // 再等待一幀
        yield return null;

        // 檢查並記錄最終狀態
        CheckFinalVoxelState(carvingSystem);
    }

    // 分離的狀態檢查方法
    private void CheckFinalVoxelState(CubeCarvingSystem carvingSystem)
    {
        bool[,,] currentVoxels = carvingSystem.GetVoxelData();
        if (currentVoxels != null)
        {
            int activeCount = 0;
            int total = currentVoxels.GetLength(0) * currentVoxels.GetLength(1) * currentVoxels.GetLength(2);

            for (int x = 0; x < currentVoxels.GetLength(0); x++)
                for (int y = 0; y < currentVoxels.GetLength(1); y++)
                    for (int z = 0; z < currentVoxels.GetLength(2); z++)
                        if (currentVoxels[x, y, z]) activeCount++;

            Debug.Log($"最終確認 - 活躍 voxels: {activeCount}/{total}");
            Debug.Log($"是否有雕刻痕跡: {carvingSystem.HasCarvingMarks()}");
        }
    }

    private IEnumerator ForceRestoreCarving(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        Debug.Log("嘗試強制恢復雕刻數據...");

        var restoreResult = ProcessCarvingDataRestore(carvingData);

        if (restoreResult.success)
        {
            // 再次設定 voxel 數據
            carvingSystem.SetVoxelData(restoreResult.voxelData);

            yield return null;
            yield return null;

            // 強制重新生成網格
            var generateMeshMethod = typeof(CubeCarvingSystem).GetMethod("GenerateMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            generateMeshMethod?.Invoke(carvingSystem, null);

            yield return null;

            // 最終驗證
            bool finalCheck = carvingSystem.HasCarvingMarks();
            Debug.Log($"強制恢復後的雕刻狀態：{finalCheck}");
        }
        else
        {
            Debug.LogError($"強制恢復雕刻時發生錯誤：{restoreResult.errorMessage}");
        }
    }

    private void RestoreMaterialData(GameObject targetObject, SavedObjectData.MaterialData materialData)
    {
        try
        {
            // 確保有 DualMaterialManager
            DualMaterialManager dualManager = targetObject.GetComponent<DualMaterialManager>();
            if (dualManager == null)
                dualManager = targetObject.AddComponent<DualMaterialManager>();

            // 恢復顏色
            if (materialData.colorTint != null)
            {
                Color color = materialData.colorTint.ToColor();
                dualManager.SetColor(color);
            }

            // 恢復貼圖
            if (materialData.paintTexture != null && materialData.paintTexture.hasTexture
                && !string.IsNullOrEmpty(materialData.paintTexture.textureData))
            {
                byte[] textureBytes = System.Convert.FromBase64String(materialData.paintTexture.textureData);
                Texture2D texture = new Texture2D(materialData.paintTexture.width, materialData.paintTexture.height);
                texture.LoadImage(textureBytes);

                dualManager.SetTextureMode(texture);
            }
            else
            {
                dualManager.SetPaintMode();
            }

            if (enableDebugLogs)
                Debug.Log("材質資料恢復完成");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"恢復材質資料時發生錯誤：{e.Message}");
        }
    }

    private void ShowSaveStatus(string message, bool isProgress)
    {
        if (saveStatusText != null)
        {
            saveStatusText.text = message;
            saveStatusText.color = isProgress ? Color.blue : Color.red;
            saveStatusText.gameObject.SetActive(true);
        }

        if (enableDebugLogs)
            Debug.Log($"儲存狀況：{message}");
    }

    private void HideSaveStatus()
    {
        if (saveStatusText != null)
        {
            saveStatusText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 手動呼叫儲存（可以繫結到按鈕）
    /// </summary>
    public void ManualSaveObject()
    {
        SaveCurrentSelectedObject();
    }

    /// <summary>
    /// 取得所有已儲存的物件檔案
    /// </summary>
    public string[] GetSavedObjectFiles()
    {
        try
        {
            string objectsPath = GetObjectsSavePath();
            if (Directory.Exists(objectsPath))
            {
                return Directory.GetFiles(objectsPath, "*.json");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"取得儲存檔案時發生錯誤：{e.Message}");
        }

        return new string[0];
    }
}