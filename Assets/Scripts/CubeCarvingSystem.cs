using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelShape
{
    Cube,
    Sphere,
    Capsule,
    Cylinder
}

public enum UVMode
{
    Continuous,      // 連續 UV（原本的方式）
    UnwrappedFaces  // 展開的獨立面 UV
}

public enum FaceDirection
{
    Up, Down, Left, Right, Forward, Back
}

public struct ModelData
{
    public string filename;
    public string shapeType;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale;
    public Color materialColor;
    public string timestamp;

    public ModelData(string filename, string shapeType, Vector3 position, Vector3 rotation, Vector3 scale, Color color)
    {
        this.filename = filename;
        this.shapeType = shapeType;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
        this.materialColor = color;
        this.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class CubeCarvingSystem : MonoBehaviour
{
    [Header("BasicSetting")]
    [SerializeField] private int gridSize = 10;
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private VoxelShape shapeType = VoxelShape.Cube;

    [Header("UV Mapping")]
    [SerializeField] private UVMode uvMode = UVMode.UnwrappedFaces;
    [SerializeField] private float uvPadding = 0.01f; // UV 區域之間的間隔

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;
    [SerializeField] private bool showUVDebug = false;
    [SerializeField] private float meshUpdateDelay = 0.1f;
    private float lastMeshUpdateTime = 0f;

    private List<Vector3> reusableVertices = new List<Vector3>();
    private List<int> reusableTriangles = new List<int>();
    private List<Vector3> reusableNormals = new List<Vector3>();
    private List<Vector2> reusableUVs = new List<Vector2>();

    private bool[,,] voxels;
    private Mesh mesh;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private List<CubeCarvingTool> activeCarvingTools = new List<CubeCarvingTool>();
    private MeshCollider meshCollider;
    private bool componentsInitialized = false;

    // UV 展開配置
    private class UVRegion
    {
        public Vector2 offset;
        public Vector2 size;
        
        public UVRegion(Vector2 offset, Vector2 size)
        {
            this.offset = offset;
            this.size = size;
        }
    }

    void Awake()
    {
        EnsureComponentsInitialized();
    }

    void Start()
    {
        EnsureComponentsInitialized();

        int estimatedSize = gridSize * gridSize * 24;
        reusableVertices.Capacity = estimatedSize;
        reusableTriangles.Capacity = estimatedSize * 6;
        reusableNormals.Capacity = estimatedSize;
        reusableUVs.Capacity = estimatedSize;

        InitializeVoxels();
        GenerateMesh();
        FindCarvingTools();
        
        if (showUVDebug)
        {
            CreateUVDebugTexture();
        }
    }

    void CreateUVDebugTexture()
    {
        // 創建一個調試紋理來視覺化 UV 佈局
        Texture2D debugTexture = new Texture2D(1024, 1024, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[1024 * 1024];
        
        // 填充背景
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.gray;
        }
        
        // 繪製 UV 區域
        // 上面 - 紅色
        DrawUVRegion(pixels, 1024, 0f, 0f, 1f/3f, 1f/2f, Color.red);
        // 下面 - 綠色
        DrawUVRegion(pixels, 1024, 1f/3f, 0f, 1f/3f, 1f/2f, Color.green);
        // 前面 - 藍色
        DrawUVRegion(pixels, 1024, 2f/3f, 0f, 1f/3f, 1f/2f, Color.blue);
        // 後面 - 黃色
        DrawUVRegion(pixels, 1024, 0f, 1f/2f, 1f/3f, 1f/2f, Color.yellow);
        // 左面 - 青色
        DrawUVRegion(pixels, 1024, 1f/3f, 1f/2f, 1f/3f, 1f/2f, Color.cyan);
        // 右面 - 洋紅色
        DrawUVRegion(pixels, 1024, 2f/3f, 1f/2f, 1f/3f, 1f/2f, Color.magenta);
        
        debugTexture.SetPixels(pixels);
        debugTexture.Apply();
        
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.mainTexture = debugTexture;
        }
    }

    void DrawUVRegion(Color[] pixels, int textureSize, float u, float v, float width, float height, Color color)
    {
        int startX = Mathf.RoundToInt(u * textureSize);
        int startY = Mathf.RoundToInt(v * textureSize);
        int endX = Mathf.RoundToInt((u + width) * textureSize);
        int endY = Mathf.RoundToInt((v + height) * textureSize);
        
        for (int y = startY; y < endY && y < textureSize; y++)
        {
            for (int x = startX; x < endX && x < textureSize; x++)
            {
                pixels[y * textureSize + x] = color;
            }
        }
    }

    void EnsureComponentsInitialized()
    {
        if (componentsInitialized) return;

        SetupComponents();
        componentsInitialized = true;
    }

    void SetupComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
            if (showDebugInfo)
                Debug.Log("Added MeshFilter component");
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            if (showDebugInfo)
                Debug.Log("Added MeshRenderer component");
        }

        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
            if (showDebugInfo)
                Debug.Log("Added MeshCollider component");
        }

        if (cubeMaterial != null && meshRenderer != null)
        {
            meshRenderer.material = cubeMaterial;
        }

        if (gameObject.layer == 0)
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
            gridSize = 10;
        }

        voxels = new bool[gridSize, gridSize, gridSize];

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
        float radius = gridSize * 0.4f;

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
        float halfHeight = gridSize * 0.3f;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    bool isInside = false;

                    if (pos.y >= center.y - halfHeight && pos.y <= center.y + halfHeight)
                    {
                        Vector2 xzPos = new Vector2(pos.x, pos.z);
                        Vector2 xzCenter = new Vector2(center.x, center.z);
                        float xzDistance = Vector2.Distance(xzPos, xzCenter);
                        isInside = xzDistance <= radius;
                    }
                    else if (pos.y > center.y + halfHeight)
                    {
                        Vector3 sphereCenter = new Vector3(center.x, center.y + halfHeight, center.z);
                        float distance = Vector3.Distance(pos, sphereCenter);
                        isInside = distance <= radius;
                    }
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

            Vector3 halfSize = Vector3.one * cubeSize * 0.5f;
            if (localPoint.x >= -halfSize.x && localPoint.x <= halfSize.x &&
                localPoint.y >= -halfSize.y && localPoint.y <= halfSize.y &&
                localPoint.z >= -halfSize.z && localPoint.z <= halfSize.z)
            {
                Vector3Int voxelPos = WorldToVoxel(localPoint);

                if (voxelPos.x >= 0 && voxelPos.x < gridSize &&
                    voxelPos.y >= 0 && voxelPos.y < gridSize &&
                    voxelPos.z >= 0 && voxelPos.z < gridSize)
                {
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
        EnsureComponentsInitialized();
        if (meshFilter == null)
        {
            Debug.LogError("MeshFilter is null! Cannot generate mesh.");
            return;
        }

        reusableVertices.Clear();
        reusableTriangles.Clear();
        reusableNormals.Clear();
        reusableUVs.Clear();

        float voxelSize = cubeSize / gridSize;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (voxels[x, y, z])
                    {
                        GenerateVoxelFaces(x, y, z, voxelSize, reusableVertices, reusableTriangles, reusableNormals, reusableUVs);
                    }
                }
            }
        }

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
            mesh.uv = reusableUVs.ToArray();
            mesh.RecalculateBounds();
        }

        try
        {
            meshFilter.mesh = mesh;

            if (meshCollider != null && reusableVertices.Count > 0)
            {
                meshCollider.sharedMesh = null;
                StartCoroutine(UpdateMeshColliderNextFrame());
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error: {e.Message}");
        }
    }

    private IEnumerator UpdateMeshColliderNextFrame()
    {
        yield return null;

        if (meshCollider != null && mesh != null)
        {
            meshCollider.sharedMesh = mesh;
            meshCollider.isTrigger = false;
        }
    }

    void GenerateVoxelFaces(int x, int y, int z, float voxelSize, List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs)
    {
        Vector3 voxelPos = VoxelToWorld(x, y, z);

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

        if (y == 0 || !voxels[x, y - 1, z])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[0], vertices[1], vertices[5], vertices[4] },
                Vector3.down, x, y, z, FaceDirection.Down);
        }

        if (y == gridSize - 1 || !voxels[x, y + 1, z])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[6], vertices[7], vertices[3], vertices[2] },
                Vector3.up, x, y, z, FaceDirection.Up);
        }

        if (z == gridSize - 1 || !voxels[x, y, z + 1])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[4], vertices[5], vertices[7], vertices[6] },
                Vector3.forward, x, y, z, FaceDirection.Forward);
        }

        if (z == 0 || !voxels[x, y, z - 1])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[1], vertices[0], vertices[2], vertices[3] },
                Vector3.back, x, y, z, FaceDirection.Back);
        }

        if (x == gridSize - 1 || !voxels[x + 1, y, z])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[1], vertices[3], vertices[7], vertices[5] },
                Vector3.right, x, y, z, FaceDirection.Right);
        }

        if (x == 0 || !voxels[x - 1, y, z])
        {
            AddQuadWithUV(meshVertices, triangles, normals, uvs,
                new Vector3[] { vertices[0], vertices[4], vertices[6], vertices[2] },
                Vector3.left, x, y, z, FaceDirection.Left);
        }
    }

    void AddQuadWithUV(List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs,
        Vector3[] quadVertices, Vector3 normal, int voxelX, int voxelY, int voxelZ, FaceDirection face)
    {
        int startIndex = meshVertices.Count;
        meshVertices.AddRange(quadVertices);

        for (int i = 0; i < 4; i++)
            normals.Add(normal);

        // 根據 UV 模式計算 UV 座標
        Vector2[] quadUVs;
        if (uvMode == UVMode.UnwrappedFaces)
        {
            quadUVs = CalculateUnwrappedUV(voxelX, voxelY, voxelZ, face);
        }
        else
        {
            quadUVs = CalculateContinuousUV(voxelX, voxelY, voxelZ, face);
        }
        
        uvs.AddRange(quadUVs);

        triangles.AddRange(new int[] {
            startIndex, startIndex + 1, startIndex + 2,
            startIndex, startIndex + 2, startIndex + 3
        });
    }

    Vector2[] CalculateUnwrappedUV(int x, int y, int z, FaceDirection face)
    {
        UVRegion region;
        float pad = uvPadding;

        switch (face)
        {
            case FaceDirection.Up:
                region = new UVRegion(new Vector2(0f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            case FaceDirection.Down:
                region = new UVRegion(new Vector2(1f / 3f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            case FaceDirection.Forward:
                region = new UVRegion(new Vector2(2f / 3f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            case FaceDirection.Back:
                region = new UVRegion(new Vector2(0f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            case FaceDirection.Left:
                region = new UVRegion(new Vector2(1f / 3f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            case FaceDirection.Right:
                region = new UVRegion(new Vector2(2f / 3f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad));
                break;
            default:
                region = new UVRegion(Vector2.zero, Vector2.one);
                break;
        }

        Vector2[] faceUVs = GetBaseFaceUVs(x, y, z, face);

        for (int i = 0; i < 4; i++)
        {
            faceUVs[i] = region.offset + Vector2.Scale(faceUVs[i], region.size);
        }

        return faceUVs;
    }

    Vector2[] GetBaseFaceUVs(int x, int y, int z, FaceDirection face)
    {
        float u0, v0, u1, v1;

        switch (face)
        {
            case FaceDirection.Down:
                u0 = (float)x / gridSize;
                v0 = (float)z / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(z + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1)
            };

            case FaceDirection.Up:
                u0 = (float)x / gridSize;
                v0 = (float)z / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(z + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0)
            };

            case FaceDirection.Forward:
                u0 = (float)x / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1)
            };

            case FaceDirection.Back:
                u0 = (float)x / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u1, v0), new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1)
            };

            case FaceDirection.Right:
                u0 = (float)z / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(z + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0)
            };

            case FaceDirection.Left:
                u0 = (float)z / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(z + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                return new Vector2[] {
                new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1)
            };

            default:
                return new Vector2[] {
                Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero
            };
        }
    }

    Vector2[] CalculateContinuousUV(int x, int y, int z, FaceDirection face)
    {
        // 原本的連續 UV 映射方法
        float u0, v0, u1, v1;
        
        switch (face)
        {
            case FaceDirection.Up:
            case FaceDirection.Down:
                u0 = (float)x / gridSize;
                v0 = (float)z / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(z + 1) / gridSize;
                break;
                
            case FaceDirection.Forward:
            case FaceDirection.Back:
                u0 = (float)x / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(x + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                break;
                
            case FaceDirection.Left:
            case FaceDirection.Right:
                u0 = (float)z / gridSize;
                v0 = (float)y / gridSize;
                u1 = (float)(z + 1) / gridSize;
                v1 = (float)(y + 1) / gridSize;
                break;
                
            default:
                u0 = v0 = 0;
                u1 = v1 = 1;
                break;
        }

        return new Vector2[]
        {
            new Vector2(u0, v0),
            new Vector2(u1, v0),
            new Vector2(u1, v1),
            new Vector2(u0, v1)
        };
    }

    // 提供方法讓 PaintManager 知道使用的是哪種 UV 模式
    public UVMode GetUVMode()
    {
        return uvMode;
    }

    // 提供方法讓 PaintManager 可以根據法線判斷是哪個面
    public FaceDirection GetFaceDirection(Vector3 normal)
    {
        Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        
        if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
        {
            return normal.y > 0 ? FaceDirection.Up : FaceDirection.Down;
        }
        else if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
        {
            return normal.x > 0 ? FaceDirection.Right : FaceDirection.Left;
        }
        else
        {
            return normal.z > 0 ? FaceDirection.Forward : FaceDirection.Back;
        }
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

    public void SetParameters(float newCubeSize, int newGridSize, VoxelShape newShapeType)
    {
        EnsureComponentsInitialized();

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

        InitializeVoxels();
        GenerateMesh();

        NotifyModelStatUpdate();
    }

    public void SetParameters(float newCubeSize, int newGridSize)
    {
        SetParameters(newCubeSize, newGridSize, VoxelShape.Cube);
    }

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

    public void ResetVoxels()
    {
        InitializeVoxels();
        GenerateMesh();
    }

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

    public void NotifyModelStatUpdate()
    {
        ModelStat modelStat = GetComponent<ModelStat>();
        if (modelStat != null)
        {
            ModelData currentData = GetCurrentModelData();
            modelStat.SetModelData(currentData);
        }
    }
}