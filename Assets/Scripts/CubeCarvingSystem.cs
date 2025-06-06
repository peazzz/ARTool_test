using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelShape
{
    Cube,      // 正方體
    Sphere,    // 圓體
    Capsule,   // 膠囊體
    Cylinder   // 圓柱體
}

public struct ModelData
{
    public string filename;
    public string shapeType;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public string timestamp;

    // 添加構造函數方便創建
    public ModelData(string filename, string shapeType, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        this.filename = filename;
        this.shapeType = shapeType;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
        this.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class CubeCarvingSystem : MonoBehaviour
{
    [Header("基本設定")]
    [SerializeField] private int gridSize = 10;
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private VoxelShape shapeType = VoxelShape.Cube;

    [Header("調試設定")]
    [SerializeField] private bool showDebugInfo = false;

    [Header("簡單效能優化")]
    [SerializeField] private float meshUpdateDelay = 0.1f;  // Mesh 更新延遲
    private float lastMeshUpdateTime = 0f;  // 記錄上次更新時間

    private List<Vector3> reusableVertices = new List<Vector3>();
    private List<int> reusableTriangles = new List<int>();
    private List<Vector3> reusableNormals = new List<Vector3>();

    // 體素數據 - true 表示體素存在
    private bool[,,] voxels;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // 雕刻工具追蹤
    private List<CubeCarvingTool> activeCarvingTools = new List<CubeCarvingTool>();

    // 添加Collider以支援點擊選擇
    private MeshCollider meshCollider;

    // 組件初始化標記
    private bool componentsInitialized = false;

    void Awake()
    {
        // 在Awake中確保組件初始化
        EnsureComponentsInitialized();
    }

    void Start()
    {
        EnsureComponentsInitialized();

        int estimatedSize = gridSize * gridSize * 24;
        reusableVertices.Capacity = estimatedSize;
        reusableTriangles.Capacity = estimatedSize * 6;
        reusableNormals.Capacity = estimatedSize;

        InitializeVoxels();
        GenerateMesh();
        FindCarvingTools();
    }

    void EnsureComponentsInitialized()
    {
        if (componentsInitialized) return;

        SetupComponents();
        componentsInitialized = true;
    }

    void SetupComponents()
    {
        // 確保MeshFilter組件存在
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            if (showDebugInfo)
                Debug.Log("Added MeshFilter component");
        }

        // 確保MeshRenderer組件存在
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (showDebugInfo)
                Debug.Log("Added MeshRenderer component");
        }

        // 添加MeshCollider以支援點擊選擇
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            if (showDebugInfo)
                Debug.Log("Added MeshCollider component");
        }

        // 設定材質
        if (cubeMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = cubeMaterial;
        }

        // 設定物件層級（如果需要）
        if (gameObject.layer == 0) // 如果還沒設定層級
        {
            int sculptLayer = LayerMask.NameToLayer("SculptObject");
            if (sculptLayer != -1)
                gameObject.layer = sculptLayer;
        }

        if (showDebugInfo)
            Debug.Log("Components setup completed");
    }

    void InitializeVoxels()
    {
        if (gridSize <= 0)
        {
            Debug.LogError("Grid size must be greater than 0!");
            gridSize = 10; // 設定預設值
        }

        voxels = new bool[gridSize, gridSize, gridSize];

        // 根據形狀類型初始化體素
        switch (shapeType)
        {
            case VoxelShape.Cube:
                InitializeCube();
                break;
            case VoxelShape.Sphere:
                InitializeSphere();
                break;
            case VoxelShape.Capsule:
                InitializeCapsule();
                break;
            case VoxelShape.Cylinder:
                InitializeCylinder();
                break;
        }

        if (showDebugInfo)
            Debug.Log($"Voxel grid initialized as {shapeType}: {gridSize}x{gridSize}x{gridSize}");
    }

    void InitializeCube()
    {
        // 原始的正方體初始化
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    voxels[x, y, z] = true;
                }
            }
        }
    }

    void InitializeSphere()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.4f; // 稍微小一點避免邊界問題

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    float distance = Vector3.Distance(pos, center);
                    voxels[x, y, z] = distance <= radius;
                }
            }
        }
    }

    void InitializeCapsule()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.3f;
        float halfHeight = gridSize * 0.3f; // 膠囊的圓柱部分高度的一半

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    bool isInside = false;

                    // 中間的圓柱部分
                    if (pos.y >= center.y - halfHeight && pos.y <= center.y + halfHeight)
                    {
                        Vector2 xzPos = new Vector2(pos.x, pos.z);
                        Vector2 xzCenter = new Vector2(center.x, center.z);
                        float xzDistance = Vector2.Distance(xzPos, xzCenter);
                        isInside = xzDistance <= radius;
                    }
                    // 上半球
                    else if (pos.y > center.y + halfHeight)
                    {
                        Vector3 sphereCenter = new Vector3(center.x, center.y + halfHeight, center.z);
                        float distance = Vector3.Distance(pos, sphereCenter);
                        isInside = distance <= radius;
                    }
                    // 下半球
                    else if (pos.y < center.y - halfHeight)
                    {
                        Vector3 sphereCenter = new Vector3(center.x, center.y - halfHeight, center.z);
                        float distance = Vector3.Distance(pos, sphereCenter);
                        isInside = distance <= radius;
                    }

                    voxels[x, y, z] = isInside;
                }
            }
        }
    }

    void InitializeCylinder()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.3f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);

                    // 計算在XZ平面上與中心的距離
                    Vector2 xzPos = new Vector2(pos.x, pos.z);
                    Vector2 xzCenter = new Vector2(center.x, center.z);
                    float xzDistance = Vector2.Distance(xzPos, xzCenter);

                    voxels[x, y, z] = xzDistance <= radius;
                }
            }
        }
    }

    void FindCarvingTools()
    {
        CubeCarvingTool[] tools = FindObjectsOfType<CubeCarvingTool>();
        activeCarvingTools.Clear();
        activeCarvingTools.AddRange(tools);

        if (showDebugInfo)
            Debug.Log($"Found {activeCarvingTools.Count} carving tools");
    }

    void Update()
    {
        CheckCarvingCollisions();
    }

    void CheckCarvingCollisions()
    {
        if (Time.time - lastMeshUpdateTime < meshUpdateDelay)
            return;

        bool meshNeedsUpdate = false;

        if (activeCarvingTools.Count == 0)
            FindCarvingTools();

        foreach (CubeCarvingTool tool in activeCarvingTools)
        {
            if (tool != null && tool.IsActive())
            {
                if (ProcessCarvingTool(tool))
                    meshNeedsUpdate = true;
            }
        }

        if (meshNeedsUpdate)
        {
            GenerateMesh();
            if (showDebugInfo)
                Debug.Log("Mesh updated!");
        }
    }

    bool ProcessCarvingTool(CubeCarvingTool tool)
    {
        bool modified = false;
        Vector3[] toolPoints = tool.GetCarvingPoints();

        if (toolPoints == null || toolPoints.Length == 0)
            return false;

        foreach (Vector3 toolPoint in toolPoints)
        {
            Vector3 localPoint = transform.InverseTransformPoint(toolPoint);

            // 檢查是否在基本體內
            Vector3 halfSize = Vector3.one * cubeSize * 0.5f;
            if (localPoint.x >= -halfSize.x && localPoint.x <= halfSize.x &&
                localPoint.y >= -halfSize.y && localPoint.y <= halfSize.y &&
                localPoint.z >= -halfSize.z && localPoint.z <= halfSize.z)
            {
                Vector3Int voxelPos = WorldToVoxel(localPoint);

                // 檢查體素座標是否有效
                if (voxelPos.x >= 0 && voxelPos.x < gridSize &&
                    voxelPos.y >= 0 && voxelPos.y < gridSize &&
                    voxelPos.z >= 0 && voxelPos.z < gridSize)
                {
                    // 移除體素
                    if (voxels[voxelPos.x, voxelPos.y, voxelPos.z])
                    {
                        voxels[voxelPos.x, voxelPos.y, voxelPos.z] = false;
                        modified = true;
                    }
                }
            }
        }

        return modified;
    }

    Vector3Int WorldToVoxel(Vector3 localPos)
    {
        Vector3 normalizedPos = (localPos + Vector3.one * cubeSize * 0.5f) / cubeSize;
        return new Vector3Int(
            Mathf.FloorToInt(normalizedPos.x * gridSize),
            Mathf.FloorToInt(normalizedPos.y * gridSize),
            Mathf.FloorToInt(normalizedPos.z * gridSize)
        );
    }

    Vector3 VoxelToWorld(int x, int y, int z)
    {
        float voxelSize = cubeSize / gridSize;
        Vector3 offset = Vector3.one * (-cubeSize * 0.5f);
        return offset + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
    }

    void GenerateMesh()
    {
        // 確保組件已初始化
        EnsureComponentsInitialized();

        // 再次檢查 meshFilter 是否存在
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter is null! Cannot generate mesh.");
            return;
        }

        // 清空重複使用容器（不創建新的）
        reusableVertices.Clear();
        reusableTriangles.Clear();
        reusableNormals.Clear();

        float voxelSize = cubeSize / gridSize;

        // 遍歷所有 Voxel
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (voxels[x, y, z])
                    {
                        GenerateVoxelFaces(x, y, z, voxelSize, reusableVertices, reusableTriangles, reusableNormals);
                    }
                }
            }
        }

        // 更新 mesh
        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = $"VoxelMesh_{shapeType}";
        }

        mesh.Clear();

        if (reusableVertices.Count > 0)
        {
            mesh.vertices = reusableVertices.ToArray();
            mesh.triangles = reusableTriangles.ToArray();
            mesh.normals = reusableNormals.ToArray();
            mesh.RecalculateBounds();
        }

        // 安全地設定 mesh
        try
        {
            meshFilter.mesh = mesh;

            // 修復 MeshCollider - 確保正確設定 Mesh
            if (meshCollider != null && reusableVertices.Count > 0)
            {
                // 重要：需要先清空再設定新的 mesh
                meshCollider.sharedMesh = null;

                // 等待一幀讓物理系統處理
                StartCoroutine(UpdateMeshColliderNextFrame());
            }

            if (showDebugInfo)
                Debug.Log($"優化後的 mesh 已生成，包含 {reusableVertices.Count} 個頂點");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"設定 mesh 時發生錯誤: {e.Message}");
        }
    }

    // 新增協程來延遲更新 MeshCollider
    private IEnumerator UpdateMeshColliderNextFrame()
    {
        yield return null; // 等待一幀

        if (meshCollider != null && mesh != null)
        {
            meshCollider.sharedMesh = mesh;

            // 確保 MeshCollider 不是 Trigger（除非你特意要設為 Trigger）
            meshCollider.isTrigger = false;

            // 對於複雜的 mesh，可能需要設為 convex
            // 但這會簡化碰撞形狀，你可以根據需要調整
            // meshCollider.convex = true;

            if (showDebugInfo)
            {
                Debug.Log($"MeshCollider 已更新，Mesh: {mesh.name}, 頂點數: {mesh.vertexCount}");
            }
        }
    }

    void GenerateVoxelFaces(int x, int y, int z, float voxelSize, List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals)
    {
        Vector3 voxelPos = VoxelToWorld(x, y, z);

        // 定義體素的8個頂點
        Vector3[] vertices = new Vector3[]
        {
            voxelPos + new Vector3(0, 0, 0),
            voxelPos + new Vector3(voxelSize, 0, 0),
            voxelPos + new Vector3(0, voxelSize, 0),
            voxelPos + new Vector3(voxelSize, voxelSize, 0),
            voxelPos + new Vector3(0, 0, voxelSize),
            voxelPos + new Vector3(voxelSize, 0, voxelSize),
            voxelPos + new Vector3(0, voxelSize, voxelSize),
            voxelPos + new Vector3(voxelSize, voxelSize, voxelSize)
        };

        // 檢查每個面是否需要繪製(只有相鄰體素不存在時才繪製)

        // 下面 (Y-)
        if (y == 0 || !voxels[x, y - 1, z])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[0], vertices[1], vertices[5], vertices[4] },
                Vector3.down);
        }

        // 上面 (Y+)
        if (y == gridSize - 1 || !voxels[x, y + 1, z])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[2], vertices[6], vertices[7], vertices[3] },
                Vector3.up);
        }

        // 前面 (Z+)
        if (z == gridSize - 1 || !voxels[x, y, z + 1])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[4], vertices[5], vertices[7], vertices[6] },
                Vector3.forward);
        }

        // 後面 (Z-)
        if (z == 0 || !voxels[x, y, z - 1])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[1], vertices[0], vertices[2], vertices[3] },
                Vector3.back);
        }

        // 右面 (X+)
        if (x == gridSize - 1 || !voxels[x + 1, y, z])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[1], vertices[3], vertices[7], vertices[5] },
                Vector3.right);
        }

        // 左面 (X-)
        if (x == 0 || !voxels[x - 1, y, z])
        {
            AddQuad(meshVertices, triangles, normals,
                new Vector3[] { vertices[0], vertices[4], vertices[6], vertices[2] },
                Vector3.left);
        }
    }

    void AddQuad(List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals, Vector3[] quadVertices, Vector3 normal)
    {
        int startIndex = meshVertices.Count;
        meshVertices.AddRange(quadVertices);

        for (int i = 0; i < 4; i++)
            normals.Add(normal);

        triangles.AddRange(new int[] {
            startIndex, startIndex + 1, startIndex + 2,
            startIndex, startIndex + 2, startIndex + 3
        });
    }

    void OnDrawGizmos()
    {
        if (!showDebugInfo || voxels == null) return;

        float voxelSize = cubeSize / gridSize;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (voxels[x, y, z])
                    {
                        Gizmos.color = Color.green;
                        Vector3 worldPos = transform.TransformPoint(VoxelToWorld(x, y, z) + Vector3.one * voxelSize * 0.5f);
                        Gizmos.DrawWireCube(worldPos, Vector3.one * voxelSize * 0.9f);
                    }
                }
            }
        }
    }

    // 公開方法供外部動態調整參數
    public void SetParameters(float newCubeSize, int newGridSize, VoxelShape newShapeType)
    {
        // 確保組件已初始化
        EnsureComponentsInitialized();

        // 驗證參數
        if (newCubeSize <= 0)
        {
            Debug.LogWarning("CubeSize must be greater than 0, using default value 1");
            newCubeSize = 1f;
        }

        if (newGridSize <= 0)
        {
            Debug.LogWarning("GridSize must be greater than 0, using default value 10");
            newGridSize = 10;
        }

        cubeSize = newCubeSize;
        gridSize = newGridSize;
        shapeType = newShapeType;

        // 重新初始化體素和生成網格
        InitializeVoxels();
        GenerateMesh();

        // 通知 ModelStat 更新數據
        NotifyModelStatUpdate();

        if (showDebugInfo)
            Debug.Log($"CubeCarvingSystem參數已更新 - CubeSize: {cubeSize}, GridSize: {gridSize}, Shape: {shapeType}");
    }

    // 保持原有的SetParameters方法以保持向後兼容
    public void SetParameters(float newCubeSize, int newGridSize)
    {
        SetParameters(newCubeSize, newGridSize, VoxelShape.Cube);
    }

    // 獲取當前參數的公開方法
    public float GetCubeSize()
    {
        return cubeSize;
    }

    public int GetGridSize()
    {
        return gridSize;
    }

    public VoxelShape GetShapeType()
    {
        return shapeType;
    }

    // 重置體素到初始狀態
    public void ResetVoxels()
    {
        InitializeVoxels();
        GenerateMesh();
        Debug.Log($"體素已重置為初始{shapeType}形狀");
    }

    /// <summary>
    /// 獲取當前模型的完整數據
    /// </summary>
    /// <returns>包含當前模型所有信息的 ModelData</returns>
    public ModelData GetCurrentModelData()
    {
        return new ModelData
        {
            filename = gameObject.name,
            shapeType = shapeType.ToString(),
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        };
    }

    /// <summary>
    /// 通知 ModelStat 組件更新數據
    /// </summary>
    public void NotifyModelStatUpdate()
    {
        ModelStat modelStat = GetComponent<ModelStat>();
        if (modelStat != null)
        {
            ModelData currentData = GetCurrentModelData();
            modelStat.SetModelData(currentData);

            if (showDebugInfo)
            {
                Debug.Log($"已通知 ModelStat 更新數據: {gameObject.name}");
            }
        }
    }
}