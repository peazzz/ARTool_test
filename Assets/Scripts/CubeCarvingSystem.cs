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

public class CubeCarvingSystem : MonoBehaviour
{
    [Header("基本設定")]
    [SerializeField] private int gridSize = 10;
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private VoxelShape shapeType = VoxelShape.Cube;

    [Header("調試設定")]
    [SerializeField] private bool showDebugInfo = false;

    // 體素數據 - true 表示體素存在
    private bool[,,] voxels;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    // 雕刻工具追蹤
    private List<CubeCarvingTool> activeCarvingTools = new List<CubeCarvingTool>();

    // 添加Collider以支援點擊選擇
    private MeshCollider meshCollider;

    void Start()
    {
        SetupComponents();
        InitializeVoxels();
        GenerateMesh();
        FindCarvingTools();
    }

    void SetupComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // 添加MeshCollider以支援點擊選擇
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = gameObject.AddComponent<MeshCollider>();

        if (cubeMaterial != null)
            meshRenderer.material = cubeMaterial;

        // 設定物件層級（如果需要）
        if (gameObject.layer == 0) // 如果還沒設定層級
        {
            int sculptLayer = LayerMask.NameToLayer("SculptObject");
            if (sculptLayer != -1)
                gameObject.layer = sculptLayer;
        }
    }

    void InitializeVoxels()
    {
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

                        if (showDebugInfo)
                            Debug.Log($"Carved voxel at ({voxelPos.x}, {voxelPos.y}, {voxelPos.z})");
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
        List<Vector3> meshVertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector3> normals = new List<Vector3>();

        float voxelSize = cubeSize / gridSize;

        // 遍歷所有體素
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (voxels[x, y, z])
                    {
                        GenerateVoxelFaces(x, y, z, voxelSize, meshVertices, triangles, normals);
                    }
                }
            }
        }

        // 更新mesh
        if (mesh == null) mesh = new Mesh();
        mesh.Clear();
        mesh.vertices = meshVertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.normals = normals.ToArray();
        mesh.RecalculateBounds();

        meshFilter.mesh = mesh;

        // 更新MeshCollider
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null; // 先清空
            meshCollider.sharedMesh = mesh; // 重新指派
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
        cubeSize = newCubeSize;
        gridSize = newGridSize;
        shapeType = newShapeType;

        // 重新初始化體素和生成網格
        InitializeVoxels();
        GenerateMesh();

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
}