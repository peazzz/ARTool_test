using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using SimpleFileBrowser;
using UnityEditor;

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
    public Button saveButton2;
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

    [Header("Voxelization Settings")]
    public int voxelResolution = 32;
    public float voxelPadding = 0.1f;
    public bool enableVoxelization = true;
    public VoxelizationMethod voxelMethod = VoxelizationMethod.Raycast;
    public float targetObjectSize = 2.0f;

    [Header("Debug")]
    public bool enableDebugLogs = true;

    public enum VoxelizationMethod
    {
        Raycast,
        PointSampling,
        SurfaceDistance
    }

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

    [System.Serializable]
    private class VoxelizationResult
    {
        public bool success;
        public bool[,,] voxelData;
        public Vector3 voxelSize;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
        public int totalVoxels;
        public int activeVoxels;
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
            loadButton.onClick.AddListener(ShowUnifiedLoadFileDialog);

        if (saveButton2 != null)
            saveButton2.onClick.AddListener(SaveCurrentSelectedObjectAsOBJ);
    }

    private void SetupFileBrowser()
    {
        FileBrowser.SetFilters(true,
            new FileBrowser.Filter("JSON Files", ".json"),
            new FileBrowser.Filter("OBJ Files", ".obj"),
            new FileBrowser.Filter("All Supported", ".json", ".obj")); //設定檔案篩選

        string defaultPath = GetObjectsSavePath(); //設定儲存路徑
        CreateSaveDirectory(); //路徑不存在則建立路徑

        if (Directory.Exists(defaultPath))
        {
            FileBrowser.AddQuickLink("ARTool Objects", defaultPath, null); //新增路徑位置至快速連結
        }

#if UNITY_IOS
        FileBrowser.ShowHiddenFiles = false;
        FileBrowser.SingleClickMode = true;
#else
        FileBrowser.ShowHiddenFiles = false;
        FileBrowser.SingleClickMode = false;
#endif
    }

    public void ShowUnifiedLoadFileDialog()
    {
        string initialPath = GetObjectsSavePath();

        FileBrowser.SetFilters(true,
            new FileBrowser.Filter("All Supported", ".json", ".obj"),
            new FileBrowser.Filter("JSON Files", ".json"),
            new FileBrowser.Filter("OBJ Files", ".obj"));

        FileBrowser.ShowLoadDialog(
            onSuccess: (paths) => {
                if (paths.Length > 0)
                {
                    LoadFileBasedOnExtension(paths[0]);
                }
            },
            onCancel: () => {

            },
            pickMode: FileBrowser.PickMode.Files,
            allowMultiSelection: false,
            initialPath: initialPath,
            initialFilename: null,
            title: "Select Object File",
            loadButtonText: "Load"
        );
    }

    private void LoadFileBasedOnExtension(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLower();

        switch (extension)
        {
            case ".json":
                LoadObjectFromFile(filePath);
                break;
            case ".obj":
                LoadOBJFromFile(filePath);
                break;
            default:
                ShowSaveStatus("Unsupported Format", false);
                StartCoroutine(HideStatusAfterDelay());
                break;
        }
    }

    private IEnumerator HideStatusAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    public void ShowLoadFileDialog()
    {
        ShowUnifiedLoadFileDialog();
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
#if UNITY_IOS && !UNITY_EDITOR
        string basePath = Application.persistentDataPath;
#elif UNITY_ANDROID && !UNITY_EDITOR
        string basePath = Application.persistentDataPath;
#else
        string basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
#endif

        return Path.Combine(basePath, "ARTool", "Object");
    }

    private bool CheckFilePermissions()
    {
#if UNITY_IOS && !UNITY_EDITOR
        string testPath = Application.persistentDataPath;
        return Directory.Exists(testPath) || CanCreateDirectory(testPath);
#else
        return true;
#endif
    }

    private bool CanCreateDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

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

    public void SaveCurrentSelectedObjectAsOBJ()
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

        StartCoroutine(SaveObjectAsOBJCoroutine(selectedObject));
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

            if (File.Exists(filePath))
            {
                ShowSaveStatus($"{fileName}", true);
            }
            else
            {
                ShowSaveStatus("Fall", false);
            }
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("Fall", false);
            Debug.LogError($"{e.Message}");
        }

        yield return new WaitForSeconds(2f);
        HideSaveStatus();
    }

    private IEnumerator SaveObjectAsOBJCoroutine(GameObject targetObject)
    {
        ShowSaveStatus("Save OBJ...", true);

        try
        {
            string objContent = GenerateOBJContent(targetObject);
            string fileName = GenerateUniqueOBJFileName(targetObject.name);
            string filePath = Path.Combine(GetObjectsSavePath(), fileName);

            File.WriteAllText(filePath, objContent);

            if (File.Exists(filePath))
            {
                ShowSaveStatus($"OBJ: {fileName}", true);
                if (enableDebugLogs)
                    Debug.Log($"OBJ file saved: {filePath}");
            }
            else
            {
                ShowSaveStatus("OBJ Fall", false);
            }
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("OBJ Fall", false);
            Debug.LogError($"Failed to save OBJ: {e.Message}");
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

    private string GenerateOBJContent(GameObject targetObject)
    {
        MeshFilter meshFilter = targetObject.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
        {
            throw new System.Exception("No mesh found on target object");
        }

        Mesh mesh = meshFilter.mesh;
        Transform transform = targetObject.transform;

        System.Text.StringBuilder objContent = new System.Text.StringBuilder();

        objContent.AppendLine($"# OBJ file exported from ARTool");
        objContent.AppendLine($"# Object: {targetObject.name}");
        objContent.AppendLine($"# Exported: {System.DateTime.Now}");
        objContent.AppendLine($"# Vertices: {mesh.vertexCount}");
        objContent.AppendLine($"# Triangles: {mesh.triangles.Length / 3}");
        objContent.AppendLine();

        Vector3[] vertices = mesh.vertices;
        foreach (Vector3 vertex in vertices)
        {
            Vector3 worldVertex = transform.TransformPoint(vertex);
            objContent.AppendLine($"v {worldVertex.x:F6} {worldVertex.y:F6} {worldVertex.z:F6}");
        }
        objContent.AppendLine();

        if (mesh.normals != null && mesh.normals.Length > 0)
        {
            Vector3[] normals = mesh.normals;
            foreach (Vector3 normal in normals)
            {
                Vector3 worldNormal = transform.TransformDirection(normal).normalized;
                objContent.AppendLine($"vn {worldNormal.x:F6} {worldNormal.y:F6} {worldNormal.z:F6}");
            }
            objContent.AppendLine();
        }

        if (mesh.uv != null && mesh.uv.Length > 0)
        {
            Vector2[] uvs = mesh.uv;
            foreach (Vector2 uv in uvs)
            {
                objContent.AppendLine($"vt {uv.x:F6} {uv.y:F6}");
            }
            objContent.AppendLine();
        }

        int[] triangles = mesh.triangles;
        bool hasNormals = mesh.normals != null && mesh.normals.Length > 0;
        bool hasUVs = mesh.uv != null && mesh.uv.Length > 0;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i] + 1;
            int v2 = triangles[i + 1] + 1;
            int v3 = triangles[i + 2] + 1;

            if (hasUVs && hasNormals)
            {
                objContent.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
            }
            else if (hasUVs)
            {
                objContent.AppendLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
            }
            else if (hasNormals)
            {
                objContent.AppendLine($"f {v1}//{v1} {v2}//{v2} {v3}//{v3}");
            }
            else
            {
                objContent.AppendLine($"f {v1} {v2} {v3}");
            }
        }

        return objContent.ToString();
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

    private string GenerateUniqueOBJFileName(string baseName)
    {
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"{baseName}_{timestamp}.obj";

        string fullPath = Path.Combine(GetObjectsSavePath(), fileName);
        int counter = 1;
        while (File.Exists(fullPath))
        {
            fileName = $"{baseName}_{timestamp}_{counter}.obj";
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

    public void LoadOBJFromFile(string filePath)
    {
        StartCoroutine(LoadOBJCoroutine(filePath));
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

    private IEnumerator LoadOBJCoroutine(string filePath)
    {
        ShowSaveStatus("Load OBJ...", true);

        if (!File.Exists(filePath))
        {
            ShowSaveStatus("No OBJ File", false);
            yield return new WaitForSeconds(2f);
            HideSaveStatus();
            yield break;
        }

        try
        {
            string objContent = File.ReadAllText(filePath);
            Mesh mesh = ParseOBJContent(objContent);

            if (mesh != null)
            {
                GameObject newObject = CreateGameObjectFromMesh(mesh, Path.GetFileNameWithoutExtension(filePath));

                if (newObject != null)
                {
                    ShowSaveStatus($"OBJ: {Path.GetFileName(filePath)}", true);
                    if (enableDebugLogs)
                        Debug.Log($"OBJ loaded successfully: {filePath}");
                }
                else
                {
                    ShowSaveStatus("OBJ Create Failed", false);
                }
            }
            else
            {
                ShowSaveStatus("OBJ Parse Failed", false);
            }
        }
        catch (System.Exception e)
        {
            ShowSaveStatus("OBJ Load Failed", false);
            Debug.LogError($"Failed to load OBJ: {e.Message}");
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

    private Mesh ParseOBJContent(string objContent)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        string[] lines = objContent.Split('\n');

        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                continue;

            string[] parts = trimmedLine.Split(new char[] { ' ', '\t' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                continue;

            switch (parts[0])
            {
                case "v":
                    if (parts.Length >= 4)
                    {
                        float x = ParseFloat(parts[1]);
                        float y = ParseFloat(parts[2]);
                        float z = ParseFloat(parts[3]);
                        vertices.Add(new Vector3(x, y, z));
                    }
                    break;

                case "vn":
                    if (parts.Length >= 4)
                    {
                        float x = ParseFloat(parts[1]);
                        float y = ParseFloat(parts[2]);
                        float z = ParseFloat(parts[3]);
                        normals.Add(new Vector3(x, y, z));
                    }
                    break;

                case "vt":
                    if (parts.Length >= 3)
                    {
                        float u = ParseFloat(parts[1]);
                        float v = ParseFloat(parts[2]);
                        uvs.Add(new Vector2(u, v));
                    }
                    break;

                case "f":
                    ParseFace(parts, triangles, vertices.Count);
                    break;
            }
        }

        if (vertices.Count == 0 || triangles.Count == 0)
        {
            Debug.LogError("Invalid OBJ file: No vertices or faces found");
            return null;
        }

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();

        if (triangles.Count > 0)
            mesh.triangles = triangles.ToArray();

        if (normals.Count == vertices.Count)
            mesh.normals = normals.ToArray();
        else
            mesh.RecalculateNormals();

        if (uvs.Count == vertices.Count)
            mesh.uv = uvs.ToArray();

        mesh.RecalculateBounds();

        return mesh;
    }

    private void ParseFace(string[] parts, List<int> triangles, int vertexCount)
    {
        List<int> faceIndices = new List<int>();

        for (int i = 1; i < parts.Length; i++)
        {
            string[] indices = parts[i].Split('/');
            if (indices.Length > 0)
            {
                int vertexIndex = ParseInt(indices[0]) - 1;

                if (vertexIndex >= 0 && vertexIndex < vertexCount)
                {
                    faceIndices.Add(vertexIndex);
                }
            }
        }

        for (int i = 1; i < faceIndices.Count - 1; i++)
        {
            triangles.Add(faceIndices[0]);
            triangles.Add(faceIndices[i]);
            triangles.Add(faceIndices[i + 1]);
        }
    }

    private float ParseFloat(string value)
    {
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }
        return 0f;
    }

    private int ParseInt(string value)
    {
        if (int.TryParse(value, out int result))
        {
            return result;
        }
        return 0;
    }

    private GameObject CreateGameObjectFromMesh(Mesh mesh, string objectName)
    {
        try
        {
            if (enableVoxelization)
            {
                return CreateVoxelizedObject(mesh, objectName);
            }
            else
            {
                return CreateSimpleGameObjectFromMesh(mesh, objectName);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to create GameObject from mesh: {e.Message}");
            return null;
        }
    }

    private GameObject CreateVoxelizedObject(Mesh mesh, string objectName)
    {
        VoxelizationResult voxelResult = VoxelizeMesh(mesh);

        if (!voxelResult.success)
        {
            Debug.LogError($"Voxelization failed: {voxelResult.errorMessage}");
            return CreateSimpleGameObjectFromMesh(mesh, objectName);
        }

        GameObject newObject;
        if (cubeCarvingSystemPrefab != null)
        {
            newObject = Instantiate(cubeCarvingSystemPrefab);
        }
        else
        {
            newObject = new GameObject($"Voxelized_{objectName}");
            newObject.AddComponent<CubeCarvingSystem>();
        }

        if (spawnParent != null)
            newObject.transform.SetParent(spawnParent);

        newObject.name = $"Voxelized_{objectName}";
        newObject.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
        newObject.transform.rotation = Quaternion.identity;

        CubeCarvingSystem carvingSystem = newObject.GetComponent<CubeCarvingSystem>();
        if (carvingSystem != null)
        {
            float cubeSize = targetObjectSize;
            carvingSystem.SetParameters(cubeSize, voxelResolution, VoxelShape.Cube);
            StartCoroutine(ApplyVoxelDataDelayed(carvingSystem, voxelResult.voxelData));
        }

        DualMaterialManager dualManager = newObject.GetComponent<DualMaterialManager>();
        if (!dualManager)
            dualManager = newObject.AddComponent<DualMaterialManager>();

        SculptFunction sculptFunction = FindObjectOfType<SculptFunction>();
        if (sculptFunction)
        {
            if (sculptFunction.ColorMaterial)
            {
                dualManager.paintMaterial = sculptFunction.ColorMaterial;
            }

            if (sculptFunction.TextureMaterial)
            {
                dualManager.textureMaterial = sculptFunction.TextureMaterial;
            }

            if (sculptFunction.fcp)
            {
                dualManager.SetColor(sculptFunction.fcp.color);
            }
        }

        dualManager.SetPaintMode();

        int sculptLayer = LayerMask.NameToLayer("SculptObject");
        if (sculptLayer != -1)
            newObject.layer = sculptLayer;
        newObject.tag = "SculptObject";

        ModelStat modelStat = newObject.GetComponent<ModelStat>();
        if (!modelStat)
            modelStat = newObject.AddComponent<ModelStat>();

        if (modelStat)
        {
            ModelData modelData = new ModelData(
                objectName,
                "VoxelizedMesh",
                newObject.transform.position,
                newObject.transform.eulerAngles,
                newObject.transform.localScale,
                dualManager.GetCurrentColor(),
                false,
                ""
            );
            modelStat.SetModelData(modelData);
        }

        return newObject;
    }

    private IEnumerator ApplyVoxelDataDelayed(CubeCarvingSystem carvingSystem, bool[,,] voxelData)
    {
        yield return null;
        yield return null;

        carvingSystem.SetVoxelData(voxelData);

        yield return null;

        var generateMeshMethod = typeof(CubeCarvingSystem).GetMethod("GenerateMesh",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        generateMeshMethod?.Invoke(carvingSystem, null);

        if (enableDebugLogs)
            Debug.Log("Voxel data applied and mesh regenerated");
    }

    private GameObject CreateSimpleGameObjectFromMesh(Mesh mesh, string objectName)
    {
        GameObject newObject = new GameObject($"Imported_{objectName}");

        if (spawnParent != null)
            newObject.transform.SetParent(spawnParent);

        newObject.transform.position = cam.transform.position + cam.transform.forward * 1.5f;
        newObject.transform.rotation = Quaternion.identity;

        float meshSize = Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z);
        float scaleFactor = targetObjectSize / meshSize;
        newObject.transform.localScale = Vector3.one * scaleFactor;

        MeshFilter meshFilter = newObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = newObject.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = newObject.AddComponent<MeshCollider>();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = mesh;

        // 改用新的 OBJMaterialManager
        OBJMaterialManager objMaterialManager = newObject.AddComponent<OBJMaterialManager>();

        SculptFunction sculptFunction = FindObjectOfType<SculptFunction>();
        if (sculptFunction)
        {
            if (sculptFunction.ColorMaterial)
            {
                objMaterialManager.paintMaterial = sculptFunction.ColorMaterial;
            }

            if (sculptFunction.TextureMaterial)
            {
                objMaterialManager.textureMaterial = sculptFunction.TextureMaterial;
            }

            if (sculptFunction.fcp)
            {
                objMaterialManager.SetColor(sculptFunction.fcp.color);
            }
        }

        objMaterialManager.SetPaintMode();

        int sculptLayer = LayerMask.NameToLayer("SculptObject");
        if (sculptLayer != -1)
            newObject.layer = sculptLayer;
        newObject.tag = "SculptObject";

        ModelStat modelStat = newObject.AddComponent<ModelStat>();
        if (modelStat)
        {
            ModelData modelData = new ModelData(
                objectName,
                "ImportedMesh",
                newObject.transform.position,
                newObject.transform.eulerAngles,
                newObject.transform.localScale,
                objMaterialManager.GetCurrentColor(),
                false,
                ""
            );
            modelStat.SetModelData(modelData);
        }

        return newObject;
    }

    private VoxelizationResult VoxelizeMesh(Mesh mesh)
    {
        VoxelizationResult result = new VoxelizationResult();

        try
        {
            Bounds bounds = mesh.bounds;
            Vector3 boundsCenter = bounds.center;
            Vector3 boundsSize = bounds.size;

            boundsSize += Vector3.one * voxelPadding;

            float voxelSize = Mathf.Max(boundsSize.x, boundsSize.y, boundsSize.z) / voxelResolution;
            Vector3 voxelSizeVec = Vector3.one * voxelSize; 

            bool[,,] voxelData = new bool[voxelResolution, voxelResolution, voxelResolution];

            Vector3 startPos = boundsCenter - boundsSize * 0.5f;

            int activeVoxels = 0;
            int totalVoxels = voxelResolution * voxelResolution * voxelResolution;

            switch (voxelMethod)
            {
                case VoxelizationMethod.Raycast:
                    activeVoxels = VoxelizeWithRaycast(mesh, voxelData, startPos, voxelSize);
                    break;
                case VoxelizationMethod.PointSampling:
                    activeVoxels = VoxelizeWithPointSampling(mesh, voxelData, startPos, voxelSize);
                    break;
                case VoxelizationMethod.SurfaceDistance:
                    activeVoxels = VoxelizeWithSurfaceDistance(mesh, voxelData, startPos, voxelSize);
                    break;
            }

            result.success = activeVoxels > 0;
            result.voxelData = voxelData;
            result.voxelSize = voxelSizeVec;
            result.boundsCenter = boundsCenter;
            result.boundsSize = boundsSize;
            result.totalVoxels = totalVoxels;
            result.activeVoxels = activeVoxels;

            if (activeVoxels == 0)
            {
                result.errorMessage = "No voxels were activated during voxelization";
            }
        }
        catch (System.Exception e)
        {
            result.success = false;
            result.errorMessage = e.Message;
        }

        return result;
    }

    private int VoxelizeWithRaycast(Mesh mesh, bool[,,] voxelData, Vector3 startPos, float voxelSize)
    {
        int activeVoxels = 0;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        for (int x = 0; x < voxelResolution; x++)
        {
            for (int y = 0; y < voxelResolution; y++)
            {
                for (int z = 0; z < voxelResolution; z++)
                {
                    Vector3 voxelCenter = startPos + new Vector3(
                        (x + 0.5f) * voxelSize,
                        (y + 0.5f) * voxelSize,
                        (z + 0.5f) * voxelSize
                    );

                    if (IsPointInsideMesh(voxelCenter, vertices, triangles))
                    {
                        voxelData[x, y, z] = true;
                        activeVoxels++;
                    }
                }
            }
        }

        return activeVoxels;
    }

    private int VoxelizeWithPointSampling(Mesh mesh, bool[,,] voxelData, Vector3 startPos, float voxelSize)
    {
        int activeVoxels = 0;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        int sampleCount = 8;
        float sampleStep = voxelSize / 2f;

        for (int x = 0; x < voxelResolution; x++)
        {
            for (int y = 0; y < voxelResolution; y++)
            {
                for (int z = 0; z < voxelResolution; z++)
                {
                    Vector3 voxelMin = startPos + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);

                    int insideCount = 0;
                    for (int sx = 0; sx < 2; sx++)
                    {
                        for (int sy = 0; sy < 2; sy++)
                        {
                            for (int sz = 0; sz < 2; sz++)
                            {
                                Vector3 samplePoint = voxelMin + new Vector3(
                                    sx * sampleStep + sampleStep * 0.5f,
                                    sy * sampleStep + sampleStep * 0.5f,
                                    sz * sampleStep + sampleStep * 0.5f
                                );

                                if (IsPointInsideMesh(samplePoint, vertices, triangles))
                                {
                                    insideCount++;
                                }
                            }
                        }
                    }

                    if (insideCount >= sampleCount / 2)
                    {
                        voxelData[x, y, z] = true;
                        activeVoxels++;
                    }
                }
            }
        }

        return activeVoxels;
    }

    private int VoxelizeWithSurfaceDistance(Mesh mesh, bool[,,] voxelData, Vector3 startPos, float voxelSize)
    {
        int activeVoxels = 0;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        float threshold = voxelSize * 0.5f;

        for (int x = 0; x < voxelResolution; x++)
        {
            for (int y = 0; y < voxelResolution; y++)
            {
                for (int z = 0; z < voxelResolution; z++)
                {
                    Vector3 voxelCenter = startPos + new Vector3(
                        (x + 0.5f) * voxelSize,
                        (y + 0.5f) * voxelSize,
                        (z + 0.5f) * voxelSize
                    );

                    float distanceToSurface = GetDistanceToMeshSurface(voxelCenter, vertices, triangles);

                    if (distanceToSurface <= threshold)
                    {
                        voxelData[x, y, z] = true;
                        activeVoxels++;
                    }
                }
            }
        }

        return activeVoxels;
    }

    private bool IsPointInsideMesh(Vector3 point, Vector3[] vertices, int[] triangles)
    {
        int intersectionCount = 0;
        Vector3 rayDirection = Vector3.right;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            if (RayTriangleIntersection(point, rayDirection, v0, v1, v2))
            {
                intersectionCount++;
            }
        }

        return (intersectionCount % 2) == 1;
    }

    private bool RayTriangleIntersection(Vector3 rayOrigin, Vector3 rayDirection, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        const float EPSILON = 0.0000001f;

        Vector3 edge1 = v1 - v0;
        Vector3 edge2 = v2 - v0;
        Vector3 h = Vector3.Cross(rayDirection, edge2);
        float a = Vector3.Dot(edge1, h);

        if (a > -EPSILON && a < EPSILON)
            return false;

        float f = 1.0f / a;
        Vector3 s = rayOrigin - v0;
        float u = f * Vector3.Dot(s, h);

        if (u < 0.0f || u > 1.0f)
            return false;

        Vector3 q = Vector3.Cross(s, edge1);
        float v = f * Vector3.Dot(rayDirection, q);

        if (v < 0.0f || u + v > 1.0f)
            return false;

        float t = f * Vector3.Dot(edge2, q);

        return t > EPSILON;
    }

    private float GetDistanceToMeshSurface(Vector3 point, Vector3[] vertices, int[] triangles)
    {
        float minDistance = float.MaxValue;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 v0 = vertices[triangles[i]];
            Vector3 v1 = vertices[triangles[i + 1]];
            Vector3 v2 = vertices[triangles[i + 2]];

            float distance = DistancePointToTriangle(point, v0, v1, v2);
            minDistance = Mathf.Min(minDistance, distance);
        }

        return minDistance;
    }

    private float DistancePointToTriangle(Vector3 point, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = point - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return Vector3.Distance(a, point);

        Vector3 bp = point - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return Vector3.Distance(b, point);

        Vector3 cp = point - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return Vector3.Distance(c, point);

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return Vector3.Distance(point, a + v * ab);
        }

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return Vector3.Distance(point, a + w * ac);
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return Vector3.Distance(point, b + w * (c - b));
        }

        float denom = 1f / (va + vb + vc);
        float v2 = vb * denom;
        float w2 = vc * denom;
        return Vector3.Distance(point, a + ab * v2 + ac * w2);
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

    public void ManualSaveObjectAsOBJ()
    {
        SaveCurrentSelectedObjectAsOBJ();
    }

    public void ManualLoadFile()
    {
        ShowUnifiedLoadFileDialog();
    }

    public void ManualLoadOBJ()
    {
        ShowUnifiedLoadFileDialog();
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

    public void DebugListFiles()
    {
        string path = GetObjectsSavePath();
        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path, "*.json");
            Debug.Log($"Found {files.Length} JSON files:");
            foreach (string file in files)
            {
                Debug.Log($"- {Path.GetFileName(file)}");
            }
        }
        else
        {
            Debug.Log($"Directory does not exist: {path}");
        }
    }

    public void SaveMeshAsAsset(string meshName = null)
    {
        if (sculptFunction.currentSelectedObject == null)
        {
            return;
        }

        var meshFilter = sculptFunction.currentSelectedObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            return;
        }

        var mesh = meshFilter.mesh;
        if (mesh == null || mesh.vertexCount == 0)
        {
            return;
        }

        if (string.IsNullOrEmpty(meshName))
            meshName = $"SculptedMesh_{sculptFunction.currentSelectedObject.name}_{System.DateTime.Now:yyyyMMdd_HHmmss}";

        string meshPath = $"Assets/SculptedMeshes/{meshName}.asset";
        string directory = System.IO.Path.GetDirectoryName(meshPath);
        
        if (!System.IO.Directory.Exists(directory))
            System.IO.Directory.CreateDirectory(directory);

        Mesh meshCopy = new Mesh();
        meshCopy.name = meshName;

        meshCopy.vertices = mesh.vertices;
        meshCopy.triangles = mesh.triangles;
        meshCopy.normals = mesh.normals;
        meshCopy.uv = mesh.uv;
        meshCopy.bounds = mesh.bounds;

        AssetDatabase.CreateAsset(meshCopy, meshPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        MeshFilter currentMeshFilter = GetComponent<MeshFilter>();
        if (currentMeshFilter)
        {
            currentMeshFilter.mesh = meshCopy;
        }
    }

    public void SaveCurrentMesh()
    {
        SaveMeshAsAsset();
    }
}