using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum VoxelShape { Cube, Sphere, Capsule, Cylinder }
public enum UVMode { Continuous, UnwrappedFaces }
public enum FaceDirection { Up, Down, Left, Right, Forward, Back }

public struct ModelData
{
    public string filename, shapeType, timestamp;
    public Vector3 position, rotation, scale;
    public Color materialColor;
    public bool hasTexture;
    public string textureName;

    public ModelData(string filename, string shapeType, Vector3 position, Vector3 rotation, Vector3 scale, Color color, bool hasTexture = false, string textureName = "")
    {
        this.filename = filename;
        this.shapeType = shapeType;
        this.position = position;
        this.rotation = rotation;
        this.scale = scale;
        this.materialColor = color;
        this.hasTexture = hasTexture;
        this.textureName = textureName;
        this.timestamp = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}

public class CubeCarvingSystem : MonoBehaviour
{
    [System.Serializable]
    public struct ModelState
    {
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Color color;
        public bool hasTexture;
        public Texture2D texture;
        public VoxelShape shapeType;
        public int gridSize;
        public bool[,,] voxelData;
    }

    [SerializeField] private int gridSize = 10;
    [SerializeField] private float cubeSize = 1f;
    [SerializeField] private Material cubeMaterial;
    [SerializeField] private VoxelShape shapeType = VoxelShape.Cube;
    [SerializeField] private UVMode uvMode = UVMode.UnwrappedFaces;
    [SerializeField] private float uvPadding = 0.01f;
    [SerializeField] private float meshUpdateDelay = 0.1f;

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
    private float lastMeshUpdateTime = 0f;
    private bool carvingEnabled = true;

    private ModelState savedState;
    private bool hasSavedState = false;

    private class UVRegion
    {
        public Vector2 offset, size;
        public UVRegion(Vector2 offset, Vector2 size) { this.offset = offset; this.size = size; }
    }

    void Awake()
    {
        SetupComponents();
    }

    void Start()
    {
        int estimatedSize = gridSize * gridSize * 24;

        if (estimatedSize > reusableVertices.Count)
            reusableVertices.Capacity = estimatedSize;
        if (estimatedSize > reusableTriangles.Count)
            reusableTriangles.Capacity = estimatedSize * 6;
        if (estimatedSize > reusableNormals.Count)
            reusableNormals.Capacity = estimatedSize;
        if (estimatedSize > reusableUVs.Count)
            reusableUVs.Capacity = estimatedSize;

        InitializeVoxels();
        GenerateMesh();
        FindCarvingTools();
        SaveCurrentState();
    }

    void SetupComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        meshCollider = GetComponent<MeshCollider>();
        if (!meshCollider) meshCollider = gameObject.AddComponent<MeshCollider>();

        if (cubeMaterial && meshRenderer) meshRenderer.material = cubeMaterial;

        int sculptLayer = LayerMask.NameToLayer("SculptObject");
        if (sculptLayer != -1) gameObject.layer = sculptLayer;
    }

    void InitializeVoxels()
    {
        if (gridSize <= 0) gridSize = 10;
        voxels = new bool[gridSize, gridSize, gridSize];
        switch (shapeType)
        {
            case VoxelShape.Cube: InitializeCube(); break;
            case VoxelShape.Sphere: InitializeSphere(); break;
            case VoxelShape.Capsule: InitializeCapsule(); break;
            case VoxelShape.Cylinder: InitializeCylinder(); break;
        }
    }

    void InitializeCube()
    {
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                    voxels[x, y, z] = true;
    }

    void InitializeSphere()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.4f;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    voxels[x, y, z] = Vector3.Distance(pos, center) <= radius;
                }
    }

    void InitializeCapsule()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.3f;
        float halfHeight = gridSize * 0.3f;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    bool isInside = false;
                    if (pos.y >= center.y - halfHeight && pos.y <= center.y + halfHeight)
                    {
                        Vector2 xzPos = new Vector2(pos.x, pos.z);
                        Vector2 xzCenter = new Vector2(center.x, center.z);
                        isInside = Vector2.Distance(xzPos, xzCenter) <= radius;
                    }
                    else if (pos.y > center.y + halfHeight)
                    {
                        Vector3 sphereCenter = new Vector3(center.x, center.y + halfHeight, center.z);
                        isInside = Vector3.Distance(pos, sphereCenter) <= radius;
                    }
                    else if (pos.y < center.y - halfHeight)
                    {
                        Vector3 sphereCenter = new Vector3(center.x, center.y - halfHeight, center.z);
                        isInside = Vector3.Distance(pos, sphereCenter) <= radius;
                    }
                    voxels[x, y, z] = isInside;
                }
    }

    void InitializeCylinder()
    {
        Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
        float radius = gridSize * 0.3f;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                {
                    Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                    Vector2 xzPos = new Vector2(pos.x, pos.z);
                    Vector2 xzCenter = new Vector2(center.x, center.z);
                    voxels[x, y, z] = Vector2.Distance(xzPos, xzCenter) <= radius;
                }
    }

    void FindCarvingTools()
    {
        activeCarvingTools.Clear();
        activeCarvingTools.AddRange(FindObjectsOfType<CubeCarvingTool>());
    }

    void Update()
    {
        CheckCarvingCollisions();
    }

    void CheckCarvingCollisions()
    {
        if (!carvingEnabled || Time.time - lastMeshUpdateTime < meshUpdateDelay) return;
        bool meshNeedsUpdate = false;
        if (activeCarvingTools.Count == 0) FindCarvingTools();
        foreach (CubeCarvingTool tool in activeCarvingTools)
            if (tool && tool.IsActive() && ProcessCarvingTool(tool))
                meshNeedsUpdate = true;
        if (meshNeedsUpdate) GenerateMesh();
    }

    bool ProcessCarvingTool(CubeCarvingTool tool)
    {
        bool modified = false;
        Vector3[] toolPoints = tool.GetCarvingPoints();
        if (toolPoints?.Length == 0) return false;
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
                    voxelPos.z >= 0 && voxelPos.z < gridSize &&
                    voxels[voxelPos.x, voxelPos.y, voxelPos.z])
                {
                    voxels[voxelPos.x, voxelPos.y, voxelPos.z] = false;
                    modified = true;
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
            Mathf.FloorToInt(normalizedPos.z * gridSize));
    }

    Vector3 VoxelToWorld(int x, int y, int z)
    {
        float voxelSize = cubeSize / gridSize;
        Vector3 offset = Vector3.one * (-cubeSize * 0.5f);
        return offset + new Vector3(x * voxelSize, y * voxelSize, z * voxelSize);
    }

    void GenerateMesh()
    {
        reusableVertices.Clear();
        reusableTriangles.Clear();
        reusableNormals.Clear();
        reusableUVs.Clear();
        float voxelSize = cubeSize / gridSize;

        int processedVoxels = 0;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                    if (voxels[x, y, z])
                    {
                        GenerateVoxelFaces(x, y, z, voxelSize, reusableVertices, reusableTriangles, reusableNormals, reusableUVs);
                        processedVoxels++;
                    }

        if (!mesh) mesh = new Mesh { name = $"VoxelMesh_{shapeType}" };
        mesh.Clear();

        if (reusableVertices.Count > 0)
        {
            mesh.vertices = reusableVertices.ToArray();
            mesh.triangles = reusableTriangles.ToArray();
            mesh.normals = reusableNormals.ToArray();
            mesh.uv = reusableUVs.ToArray();
            mesh.RecalculateBounds();
        }

        meshFilter.mesh = mesh;

        if (meshRenderer)
        {
            meshRenderer.enabled = false;
            meshRenderer.enabled = true;
        }

        if (meshCollider && reusableVertices.Count > 0)
        {
            meshCollider.sharedMesh = null;
            StartCoroutine(UpdateMeshColliderNextFrame());
        }

        lastMeshUpdateTime = Time.time;
    }

    IEnumerator UpdateMeshColliderNextFrame()
    {
        yield return null;
        meshCollider.sharedMesh = mesh;
        meshCollider.isTrigger = false;
    }

    void GenerateVoxelFaces(int x, int y, int z, float voxelSize, List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs)
    {
        Vector3 voxelPos = VoxelToWorld(x, y, z);
        Vector3[] vertices = new Vector3[] {
            voxelPos + new Vector3(0, 0, 0), voxelPos + new Vector3(voxelSize, 0, 0),
            voxelPos + new Vector3(0, voxelSize, 0), voxelPos + new Vector3(voxelSize, voxelSize, 0),
            voxelPos + new Vector3(0, 0, voxelSize), voxelPos + new Vector3(voxelSize, 0, voxelSize),
            voxelPos + new Vector3(0, voxelSize, voxelSize), voxelPos + new Vector3(voxelSize, voxelSize, voxelSize)
        };
        if (y == 0 || !voxels[x, y - 1, z])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[0], vertices[1], vertices[5], vertices[4] }, Vector3.down, x, y, z, FaceDirection.Down);
        if (y == gridSize - 1 || !voxels[x, y + 1, z])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[6], vertices[7], vertices[3], vertices[2] }, Vector3.up, x, y, z, FaceDirection.Up);
        if (z == gridSize - 1 || !voxels[x, y, z + 1])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[4], vertices[5], vertices[7], vertices[6] }, Vector3.forward, x, y, z, FaceDirection.Forward);
        if (z == 0 || !voxels[x, y, z - 1])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[1], vertices[0], vertices[2], vertices[3] }, Vector3.back, x, y, z, FaceDirection.Back);
        if (x == gridSize - 1 || !voxels[x + 1, y, z])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[1], vertices[3], vertices[7], vertices[5] }, Vector3.right, x, y, z, FaceDirection.Right);
        if (x == 0 || !voxels[x - 1, y, z])
            AddQuadWithUV(meshVertices, triangles, normals, uvs, new Vector3[] { vertices[0], vertices[4], vertices[6], vertices[2] }, Vector3.left, x, y, z, FaceDirection.Left);
    }

    void AddQuadWithUV(List<Vector3> meshVertices, List<int> triangles, List<Vector3> normals, List<Vector2> uvs, Vector3[] quadVertices, Vector3 normal, int voxelX, int voxelY, int voxelZ, FaceDirection face)
    {
        int startIndex = meshVertices.Count;
        meshVertices.AddRange(quadVertices);
        for (int i = 0; i < 4; i++) normals.Add(normal);
        Vector2[] quadUVs = uvMode == UVMode.UnwrappedFaces ? CalculateUnwrappedUV(voxelX, voxelY, voxelZ, face) : CalculateContinuousUV(voxelX, voxelY, voxelZ, face);
        uvs.AddRange(quadUVs);
        triangles.AddRange(new int[] { startIndex, startIndex + 1, startIndex + 2, startIndex, startIndex + 2, startIndex + 3 });
    }

    Vector2[] CalculateUnwrappedUV(int x, int y, int z, FaceDirection face)
    {
        UVRegion region;
        float pad = uvPadding;
        switch (face)
        {
            case FaceDirection.Up: region = new UVRegion(new Vector2(0f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            case FaceDirection.Down: region = new UVRegion(new Vector2(1f / 3f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            case FaceDirection.Forward: region = new UVRegion(new Vector2(2f / 3f + pad, 0f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            case FaceDirection.Back: region = new UVRegion(new Vector2(0f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            case FaceDirection.Left: region = new UVRegion(new Vector2(1f / 3f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            case FaceDirection.Right: region = new UVRegion(new Vector2(2f / 3f + pad, 1f / 2f + pad), new Vector2(1f / 3f - 2 * pad, 1f / 2f - 2 * pad)); break;
            default: region = new UVRegion(Vector2.zero, Vector2.one); break;
        }
        Vector2[] faceUVs = GetBaseFaceUVs(x, y, z, face);
        for (int i = 0; i < 4; i++) faceUVs[i] = region.offset + Vector2.Scale(faceUVs[i], region.size);
        return faceUVs;
    }

    Vector2[] GetBaseFaceUVs(int x, int y, int z, FaceDirection face)
    {
        float u0, v0, u1, v1;
        switch (face)
        {
            case FaceDirection.Down:
                u0 = (float)x / gridSize; v0 = (float)z / gridSize; u1 = (float)(x + 1) / gridSize; v1 = (float)(z + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            case FaceDirection.Up:
                u0 = (float)x / gridSize; v0 = (float)z / gridSize; u1 = (float)(x + 1) / gridSize; v1 = (float)(z + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0) };
            case FaceDirection.Forward:
                u0 = (float)x / gridSize; v0 = (float)y / gridSize; u1 = (float)(x + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            case FaceDirection.Back:
                u0 = (float)x / gridSize; v0 = (float)y / gridSize; u1 = (float)(x + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u1, v0), new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1) };
            case FaceDirection.Right:
                u0 = (float)z / gridSize; v0 = (float)y / gridSize; u1 = (float)(z + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };
            case FaceDirection.Left:
                u0 = (float)z / gridSize; v0 = (float)y / gridSize; u1 = (float)(z + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };
            default: return new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
        }
    }

    Vector2[] CalculateContinuousUV(int x, int y, int z, FaceDirection face)
    {
        float u0, v0, u1, v1;

        switch (face)
        {
            case FaceDirection.Up:
                u0 = (float)x / gridSize; v0 = (float)z / gridSize;
                u1 = (float)(x + 1) / gridSize; v1 = (float)(z + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0), new Vector2(u0, v0) };

            case FaceDirection.Down:
                u0 = (float)x / gridSize; v0 = (float)z / gridSize;
                u1 = (float)(x + 1) / gridSize; v1 = (float)(z + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };

            case FaceDirection.Forward:
                u0 = (float)x / gridSize; v0 = (float)y / gridSize;
                u1 = (float)(x + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };

            case FaceDirection.Back:
                u0 = (float)x / gridSize; v0 = (float)y / gridSize;
                u1 = (float)(x + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u1, v0), new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1) };

            case FaceDirection.Right:
                u0 = (float)z / gridSize; v0 = (float)y / gridSize;
                u1 = (float)(z + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) };

            case FaceDirection.Left:
                u0 = (float)z / gridSize; v0 = (float)y / gridSize;
                u1 = (float)(z + 1) / gridSize; v1 = (float)(y + 1) / gridSize;
                return new Vector2[] { new Vector2(u0, v0), new Vector2(u1, v0), new Vector2(u1, v1), new Vector2(u0, v1) };

            default:
                return new Vector2[] { Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero };
        }
    }

    public Vector2 WorldPointToUV(Vector3 worldPoint, out FaceDirection hitFace)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 normalizedPoint = localPoint / cubeSize;
        Vector3 uvPoint = normalizedPoint + Vector3.one * 0.5f;
        hitFace = GetFaceFromLocalPoint(localPoint);
        Vector2 uv = CalculateUVFromFace(uvPoint, hitFace);
        return uv;
    }

    private FaceDirection GetFaceFromLocalPoint(Vector3 localPoint)
    {
        Vector3 abs = new Vector3(Mathf.Abs(localPoint.x), Mathf.Abs(localPoint.y), Mathf.Abs(localPoint.z));

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return localPoint.y > 0 ? FaceDirection.Up : FaceDirection.Down;
        }
        else if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return localPoint.x > 0 ? FaceDirection.Right : FaceDirection.Left;
        }
        else
        {
            return localPoint.z > 0 ? FaceDirection.Forward : FaceDirection.Back;
        }
    }

    private Vector2 CalculateUVFromFace(Vector3 uvPoint, FaceDirection face)
    {
        switch (face)
        {
            case FaceDirection.Up:
            case FaceDirection.Down:
                return new Vector2(uvPoint.x, uvPoint.z);

            case FaceDirection.Left:
            case FaceDirection.Right:
                return new Vector2(uvPoint.z, uvPoint.y);

            case FaceDirection.Forward:
            case FaceDirection.Back:
                return new Vector2(uvPoint.x, uvPoint.y);

            default:
                return Vector2.zero;
        }
    }

    public Vector2 GetUnwrappedUV(Vector3 worldPoint)
    {
        FaceDirection face;
        Vector2 baseUV = WorldPointToUV(worldPoint, out face);

        if (uvMode == UVMode.Continuous)
        {
            return baseUV;
        }
        else
        {
            return ConvertToUnwrappedUV(baseUV, face);
        }
    }

    private Vector2 ConvertToUnwrappedUV(Vector2 baseUV, FaceDirection face)
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

        return region.offset + Vector2.Scale(baseUV, region.size);
    }

    public bool IsPointOnSurface(Vector3 worldPoint, float tolerance = 0.01f)
    {
        Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
        Vector3 abs = new Vector3(Mathf.Abs(localPoint.x), Mathf.Abs(localPoint.y), Mathf.Abs(localPoint.z));
        float halfSize = cubeSize * 0.5f;

        return (Mathf.Abs(abs.x - halfSize) < tolerance ||
                Mathf.Abs(abs.y - halfSize) < tolerance ||
                Mathf.Abs(abs.z - halfSize) < tolerance) &&
               abs.x <= halfSize + tolerance &&
               abs.y <= halfSize + tolerance &&
               abs.z <= halfSize + tolerance;
    }

    public UVMode GetUVMode() => uvMode;

    public FaceDirection GetFaceDirection(Vector3 normal)
    {
        Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
        if (absNormal.y > absNormal.x && absNormal.y > absNormal.z) return normal.y > 0 ? FaceDirection.Up : FaceDirection.Down;
        if (absNormal.x > absNormal.y && absNormal.x > absNormal.z) return normal.x > 0 ? FaceDirection.Right : FaceDirection.Left;
        return normal.z > 0 ? FaceDirection.Forward : FaceDirection.Back;
    }

    public void SetUVMode(UVMode newUVMode) { uvMode = newUVMode; GenerateMesh(); }
    public void SetCarvingEnabled(bool enabled) => carvingEnabled = enabled;
    public float GetCubeSize() => cubeSize;
    public int GetGridSize() => gridSize;
    public VoxelShape GetShapeType() => shapeType;

    public void SetParameters(float newCubeSize, int newGridSize, VoxelShape newShapeType)
    {
        if (newCubeSize <= 0) newCubeSize = 1f;
        if (newGridSize <= 0) newGridSize = 10;
        cubeSize = newCubeSize;
        gridSize = newGridSize;
        shapeType = newShapeType;
        InitializeVoxels();
        GenerateMesh();
        NotifyModelStatUpdate();
    }

    public void SetParameters(float newCubeSize, int newGridSize) => SetParameters(newCubeSize, newGridSize, VoxelShape.Cube);
    public void ResetVoxels() { InitializeVoxels(); GenerateMesh(); }

    public ModelData GetCurrentModelData()
    {
        DualMaterialManager dualManager = GetComponent<DualMaterialManager>();
        bool hasTexture = false;
        string textureName = "";
        Color currentColor = Color.white;

        if (dualManager)
        {
            hasTexture = dualManager.IsInTextureMode();
            if (hasTexture && dualManager.GetCurrentTexture())
                textureName = dualManager.GetCurrentTexture().name;
            currentColor = dualManager.GetCurrentColor();
        }
        else
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer && renderer.material)
                currentColor = renderer.material.color;
        }

        return new ModelData(
            gameObject.name,
            shapeType.ToString(),
            transform.position,
            transform.eulerAngles,
            transform.localScale,
            currentColor,
            hasTexture,
            textureName
        );
    }

    public void NotifyModelStatUpdate()
    {
        ModelStat modelStat = GetComponent<ModelStat>();
        if (modelStat)
        {
            ModelData currentData = GetCurrentModelData();
            modelStat.SetModelData(currentData);
        }
    }

    public ModelState GetCurrentModelState()
    {
        DualMaterialManager dualManager = GetComponent<DualMaterialManager>();

        ModelState state = new ModelState
        {
            position = transform.position,
            rotation = transform.eulerAngles,
            scale = transform.localScale,
            color = Color.white,
            hasTexture = false,
            texture = null,
            shapeType = shapeType,
            gridSize = gridSize,
            voxelData = CloneVoxelArray()
        };

        if (dualManager)
        {
            state.hasTexture = dualManager.IsInTextureMode();
            state.texture = dualManager.GetCurrentTexture();
            state.color = dualManager.GetCurrentColor();
        }
        else
        {
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer && renderer.material)
                state.color = renderer.material.color;
        }

        return state;
    }

    public void UpdateModelInfo(Vector3 position, Vector3 rotation, Vector3 scale, Color color, bool hasTexture = false, Texture2D texture = null)
    {
        transform.position = position;
        transform.rotation = Quaternion.Euler(rotation);
        transform.localScale = scale;

        DualMaterialManager dualManager = GetComponent<DualMaterialManager>();
        if (dualManager)
        {
            if (hasTexture && texture)
            {
                dualManager.SetTextureMode(texture);
                dualManager.SetColor(color);
            }
            else
            {
                dualManager.SetPaintMode();
                dualManager.SetColor(color);
            }
        }

        NotifyModelStatUpdate();
    }

    public void SaveCurrentState()
    {
        savedState = GetCurrentModelState();
        hasSavedState = true;
    }

    public void CommitCurrentState()
    {
        SaveCurrentState();
    }

    public void RevertToSavedState()
    {
        if (!hasSavedState) return;

        transform.position = savedState.position;
        transform.rotation = Quaternion.Euler(savedState.rotation);
        transform.localScale = savedState.scale;

        shapeType = savedState.shapeType;
        gridSize = savedState.gridSize;

        if (savedState.voxelData != null)
        {
            voxels = CloneVoxelArray(savedState.voxelData);
            GenerateMesh();
        }

        DualMaterialManager dualManager = GetComponent<DualMaterialManager>();
        if (dualManager)
        {
            if (savedState.hasTexture && savedState.texture)
            {
                dualManager.SetTextureMode(savedState.texture);
                dualManager.SetColor(savedState.color);
            }
            else
            {
                dualManager.SetPaintMode();
                dualManager.SetColor(savedState.color);
            }
        }

        NotifyModelStatUpdate();
    }

    private bool[,,] CloneVoxelArray(bool[,,] source = null)
    {
        bool[,,] sourceArray = source ?? voxels;
        if (sourceArray == null) return null;

        int xSize = sourceArray.GetLength(0);
        int ySize = sourceArray.GetLength(1);
        int zSize = sourceArray.GetLength(2);

        bool[,,] clone = new bool[xSize, ySize, zSize];

        for (int x = 0; x < xSize; x++)
            for (int y = 0; y < ySize; y++)
                for (int z = 0; z < zSize; z++)
                    clone[x, y, z] = sourceArray[x, y, z];

        return clone;
    }


    public void SetVoxelData(bool[,,] newVoxelData)
    {
        if (newVoxelData == null)
        {
            return;
        }

        int xSize = newVoxelData.GetLength(0);
        int ySize = newVoxelData.GetLength(1);
        int zSize = newVoxelData.GetLength(2);

        reusableVertices.Clear();
        reusableTriangles.Clear();
        reusableNormals.Clear();
        reusableUVs.Clear();

        if (xSize != gridSize || ySize != gridSize || zSize != gridSize)
        {
            gridSize = Mathf.Max(xSize, ySize, zSize);

            int newEstimatedSize = gridSize * gridSize * 24;
            reusableVertices.Capacity = Mathf.Max(reusableVertices.Count, newEstimatedSize);
            reusableTriangles.Capacity = Mathf.Max(reusableTriangles.Count, newEstimatedSize * 6);
            reusableNormals.Capacity = Mathf.Max(reusableNormals.Count, newEstimatedSize);
            reusableUVs.Capacity = Mathf.Max(reusableUVs.Count, newEstimatedSize);
        }

        voxels = CloneVoxelArray(newVoxelData);

        GenerateMesh();

        if (meshCollider && mesh)
        {
            meshCollider.sharedMesh = null;
            StartCoroutine(UpdateMeshColliderDelayed());
        }

        NotifyModelStatUpdate();

        if (meshRenderer && meshFilter)
        {
            meshFilter.mesh = mesh;
            meshRenderer.enabled = false;
            meshRenderer.enabled = true;
        }

        int activeVoxels = 0;
        for (int x = 0; x < gridSize; x++)
            for (int y = 0; y < gridSize; y++)
                for (int z = 0; z < gridSize; z++)
                    if (voxels[x, y, z]) activeVoxels++;
    }

    private IEnumerator UpdateMeshColliderDelayed()
    {
        yield return null;
        yield return null;

        if (meshCollider && mesh)
        {
            meshCollider.sharedMesh = mesh;
            meshCollider.isTrigger = false;
        }
    }

    public bool[,,] GetVoxelData()
    {
        return CloneVoxelArray(voxels);
    }

    public bool HasCarvingMarks()
    {
        if (voxels == null) return false;

        int totalVoxels = gridSize * gridSize * gridSize;
        int activeVoxels = 0;

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                for (int z = 0; z < gridSize; z++)
                {
                    if (voxels[x, y, z])
                        activeVoxels++;
                }
            }
        }

        return activeVoxels < GetExpectedVoxelCount();
    }

    private int GetExpectedVoxelCount()
    {
        switch (shapeType)
        {
            case VoxelShape.Cube:
                return gridSize * gridSize * gridSize;

            case VoxelShape.Sphere:
                Vector3 center = new Vector3(gridSize * 0.5f, gridSize * 0.5f, gridSize * 0.5f);
                float radius = gridSize * 0.4f;
                int count = 0;
                for (int x = 0; x < gridSize; x++)
                    for (int y = 0; y < gridSize; y++)
                        for (int z = 0; z < gridSize; z++)
                        {
                            Vector3 pos = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                            if (Vector3.Distance(pos, center) <= radius)
                                count++;
                        }
                return count;

            case VoxelShape.Capsule:
            case VoxelShape.Cylinder:
                return Mathf.RoundToInt(gridSize * gridSize * gridSize * 0.7f);

            default:
                return gridSize * gridSize * gridSize;
        }
    }
}