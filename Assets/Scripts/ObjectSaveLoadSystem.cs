using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using SimpleFileBrowser;

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
        public string voxelData;
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
            public string textureData;
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
    public Camera cam;

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
        SetupFileBrowser();
    }

    private void SetupButtonEvents()
    {
        if (saveButton != null)
            saveButton.onClick.AddListener(SaveCurrentSelectedObject);

        if (loadButton != null)
            loadButton.onClick.AddListener(ShowLoadFileDialog);
    }

    private void SetupFileBrowser()
    {
        FileBrowser.SetFilters(true, new FileBrowser.Filter("JSON Files", ".json"));

        string defaultPath = GetObjectsSavePath();
        if (Directory.Exists(defaultPath))
        {
            FileBrowser.AddQuickLink("ARTool Objects", defaultPath, null);
        }

        FileBrowser.ShowHiddenFiles = false;
        FileBrowser.SingleClickMode = false;
    }

    public void ShowLoadFileDialog()
    {
        string initialPath = GetObjectsSavePath();

        FileBrowser.ShowLoadDialog(
            onSuccess: (paths) => {
                if (paths.Length > 0)
                {
                    LoadObjectFromFile(paths[0]);
                }
            },
            onCancel: () => {

            },
            pickMode: FileBrowser.PickMode.Files,
            allowMultiSelection: false,
            initialPath: initialPath,
            initialFilename: null,
            title: "Select Object",
            loadButtonText: "Load"
        );
    }

    private void CreateSaveDirectory()
    {
        try
        {
            string objectsPath = GetObjectsSavePath();
            if (!Directory.Exists(objectsPath))
            {
                Directory.CreateDirectory(objectsPath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.Message}");
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
            ShowSaveStatus("Error", false);
            return;
        }

        GameObject selectedObject = sculptFunction.currentSelectedObject;
        if (selectedObject == null)
        {
            ShowSaveStatus("Select Object", false);
            return;
        }

        StartCoroutine(SaveObjectCoroutine(selectedObject));
    }

    private IEnumerator SaveObjectCoroutine(GameObject targetObject)
    {
        ShowSaveStatus("Save...", true);

        try
        {
            SavedObjectData saveData = CollectObjectData(targetObject);

            string jsonData = JsonUtility.ToJson(saveData, true);

            string fileName = GenerateUniqueFileName(saveData.objectInfo.name);
            string filePath = Path.Combine(GetObjectsSavePath(), fileName);

            File.WriteAllText(filePath, jsonData);

            ShowSaveStatus($"{fileName}", true);
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("Fall", false);
            Debug.LogError($"{e.Message}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    private SavedObjectData CollectObjectData(GameObject targetObject)
    {
        SavedObjectData saveData = new SavedObjectData();

        saveData.objectInfo.name = targetObject.name;
        saveData.objectInfo.id = GenerateObjectId();
        saveData.objectInfo.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        saveData.objectInfo.objectType = "CubeCarvingSystem";

        saveData.transform.position = targetObject.transform.position;
        saveData.transform.rotation = targetObject.transform.eulerAngles;
        saveData.transform.scale = targetObject.transform.localScale;

        CubeCarvingSystem carvingSystem = targetObject.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            saveData.geometry.shapeType = carvingSystem.GetShapeType().ToString();
            saveData.geometry.gridSize = carvingSystem.GetGridSize();
            saveData.geometry.cubeSize = carvingSystem.GetCubeSize();
            saveData.geometry.uvMode = carvingSystem.GetUVMode().ToString();

            CollectCarvingData(carvingSystem, saveData.carvingData);
        }

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

            if (modelState.voxelData != null)
            {
                int activeVoxels = 0;
                int totalVoxels = modelState.voxelData.GetLength(0) * modelState.voxelData.GetLength(1) * modelState.voxelData.GetLength(2);
                for (int x = 0; x < modelState.voxelData.GetLength(0); x++)
                    for (int y = 0; y < modelState.voxelData.GetLength(1); y++)
                        for (int z = 0; z < modelState.voxelData.GetLength(2); z++)
                            if (modelState.voxelData[x, y, z]) activeVoxels++;

                byte[] compressedData = CompressVoxelData(modelState.voxelData);
                carvingData.voxelData = System.Convert.ToBase64String(compressedData);
            }
        }
        catch (System.Exception e)
        {
            carvingData.hasCarving = false;
        }
    }

    private byte[] CompressVoxelData(bool[,,] voxelData)
    {
        int xSize = voxelData.GetLength(0);
        int ySize = voxelData.GetLength(1);
        int zSize = voxelData.GetLength(2);

        int totalVoxels = xSize * ySize * zSize;
        int byteCount = (totalVoxels + 7) / 8;
        byte[] compressed = new byte[byteCount + 12];

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
            materialData.materialType = "StandardMaterial";
            materialData.shaderName = renderer.material.shader.name;
            materialData.colorTint = new SavedObjectData.MaterialData.ColorProperty(renderer.material.color);
        }
    }

    private string GenerateObjectId()
    {
        return "obj_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + UnityEngine.Random.Range(1000, 9999);
    }

    private string GenerateUniqueFileName(string baseName)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{baseName}_{timestamp}.json";

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

    public void OpenLoadInterface()
    {

    }

    public void LoadObjectFromFile(string filePath)
    {
        StartCoroutine(LoadObjectCoroutine(filePath));
    }

    private IEnumerator LoadObjectCoroutine(string filePath)
    {
        ShowSaveStatus("Load...", true);

        if (!File.Exists(filePath))
        {
            ShowSaveStatus("no Object", false);
            yield return new WaitForSeconds(2f);
            HideSaveStatus();
            yield break;
        }

        LoadResult result = LoadObjectData(filePath);

        if (result.success && result.loadData != null)
        {
            yield return StartCoroutine(CreateAndValidateObject(result.loadData));
        }
        else
        {
            ShowSaveStatus(result.errorMessage ?? "Fall", false);
            if (enableDebugLogs && !string.IsNullOrEmpty(result.errorMessage))
                Debug.LogError($"{result.errorMessage}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

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

    private IEnumerator CreateAndValidateObject(SavedObjectData loadData)
    {
        GameObject newObject = CreateObjectFromData(loadData);

        if (newObject != null)
        {
            yield return new WaitForSeconds(0.5f);

            CubeCarvingSystem carvingSystem = newObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                bool hasCarving = carvingSystem.HasCarvingMarks();
                MeshFilter meshFilter = newObject.GetComponent<MeshFilter>();
                int vertexCount = meshFilter?.mesh?.vertexCount ?? 0;

                if (loadData.carvingData.hasCarving && !hasCarving)
                {
                    yield return StartCoroutine(ForceRestoreCarving(carvingSystem, loadData.carvingData));
                }
            }

            ShowSaveStatus($"{loadData.objectInfo.name}", true);
        }
        else
        {
            ShowSaveStatus("Fall", false);
        }
    }

    private GameObject CreateObjectFromData(SavedObjectData loadData)
    {
        try
        {
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

            if (spawnParent != null)
                newObject.transform.SetParent(spawnParent);

            newObject.name = loadData.objectInfo.name;
            newObject.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
            newObject.transform.rotation = Quaternion.Euler(loadData.transform.rotation);
            newObject.transform.localScale = loadData.transform.scale;

            CubeCarvingSystem carvingSystem = newObject.GetComponent<CubeCarvingSystem>();
            if (carvingSystem != null)
            {
                VoxelShape shapeType = (VoxelShape)System.Enum.Parse(typeof(VoxelShape), loadData.geometry.shapeType);

                carvingSystem.SetParameters(loadData.geometry.cubeSize, loadData.geometry.gridSize, shapeType);

                StartCoroutine(RestoreCarvingDataDelayed(carvingSystem, loadData.carvingData));
            }

            RestoreMaterialData(newObject, loadData.materials);

            int sculptLayer = LayerMask.NameToLayer("SculptObject");
            if (sculptLayer != -1)
                newObject.layer = sculptLayer;
            newObject.tag = "SculptObject";

            return newObject;
        }
        catch (System.Exception e)
        {
            return null;
        }
    }

    private IEnumerator RestoreCarvingDataDelayed(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        yield return null;
        yield return null;

        RestoreCarvingData(carvingSystem, carvingData);
    }

    private void RestoreCarvingData(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        if (string.IsNullOrEmpty(carvingData.voxelData))
        {
            return;
        }

        var restoreResult = ProcessCarvingDataRestore(carvingData);

        if (restoreResult.success)
        {
            SetVoxelDataToSystem(carvingSystem, restoreResult.voxelData);
        }
    }

    private RestoreResult ProcessCarvingDataRestore(SavedObjectData.CarvingData carvingData)
    {
        try
        {
            byte[] compressedData = System.Convert.FromBase64String(carvingData.voxelData);
            bool[,,] voxelData = DecompressVoxelData(compressedData);

            int activeVoxels = 0;
            int totalVoxels = voxelData.GetLength(0) * voxelData.GetLength(1) * voxelData.GetLength(2);
            for (int x = 0; x < voxelData.GetLength(0); x++)
                for (int y = 0; y < voxelData.GetLength(1); y++)
                    for (int z = 0; z < voxelData.GetLength(2); z++)
                        if (voxelData[x, y, z]) activeVoxels++;

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
        carvingSystem.SetVoxelData(voxelData);

        StartCoroutine(EnsureMeshUpdateMultipleTimes(carvingSystem));
    }

    private IEnumerator EnsureMeshUpdateMultipleTimes(CubeCarvingSystem carvingSystem)
    {
        yield return null;
        yield return null;

        var generateMeshMethod = typeof(CubeCarvingSystem).GetMethod("GenerateMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (generateMeshMethod != null)
        {
            generateMeshMethod.Invoke(carvingSystem, null);
        }

        yield return null;

        CheckFinalVoxelState(carvingSystem);
    }

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
        }
    }

    private IEnumerator ForceRestoreCarving(CubeCarvingSystem carvingSystem, SavedObjectData.CarvingData carvingData)
    {
        var restoreResult = ProcessCarvingDataRestore(carvingData);

        if (restoreResult.success)
        {
            carvingSystem.SetVoxelData(restoreResult.voxelData);

            yield return null;
            yield return null;

            var generateMeshMethod = typeof(CubeCarvingSystem).GetMethod("GenerateMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            generateMeshMethod?.Invoke(carvingSystem, null);

            yield return null;

            bool finalCheck = carvingSystem.HasCarvingMarks();
        }
    }

    private void RestoreMaterialData(GameObject targetObject, SavedObjectData.MaterialData materialData)
    {
        try
        {
            DualMaterialManager dualManager = targetObject.GetComponent<DualMaterialManager>();
            if (dualManager == null)
                dualManager = targetObject.AddComponent<DualMaterialManager>();

            if (materialData.colorTint != null)
            {
                Color color = materialData.colorTint.ToColor();
                dualManager.SetColor(color);
            }

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
        }
        catch (System.Exception e)
        {
            Debug.LogError($"{e.Message}");
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
    }

    private void HideSaveStatus()
    {
        if (saveStatusText != null)
        {
            saveStatusText.gameObject.SetActive(false);
        }
    }

    public void ManualSaveObject()
    {
        SaveCurrentSelectedObject();
    }

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
            Debug.LogError($"{e.Message}");
        }

        return new string[0];
    }
}