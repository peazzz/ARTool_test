using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelCube : MonoBehaviour
{
    [Header("Voxel 分割設定")]
    public int splitCount = 10;

    [Header("URP 材質設定 (Render Face 設為 Both)")]
    public Material urpMaterial;

    private Mesh mesh;
    private float voxelSize;
    private Vector3 cubeSize;

    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector3> normals;  // 添加法線列表
    private List<Vector2> uvs;      // 添加UV列表
    private List<int> triangleVoxelIndices;

    // 記錄所有trigger cell，方便後續 Cleanup
    private List<GameObject> triggerCells = new List<GameObject>();

    void Start()
    {
        GenerateMesh();

        // 設定 MeshFilter & Renderer
        var mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        if (mr != null && urpMaterial != null)
        {
            mr.material = urpMaterial;
            // 關閉陰影投射和接收
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        SetupVoxelTriggers();
    }

    void GenerateMesh()
    {
        cubeSize = transform.localScale;
        voxelSize = cubeSize.x / splitCount;

        vertices = new List<Vector3>();
        triangles = new List<int>();
        normals = new List<Vector3>();
        uvs = new List<Vector2>();
        triangleVoxelIndices = new List<int>();

        // 為每個voxel生成6個面（立方體的6個面）
        for (int y = 0; y < splitCount; y++)
            for (int x = 0; x < splitCount; x++)
                for (int z = 0; z < splitCount; z++)
                {
                    int vi = y * splitCount * splitCount + x * splitCount + z;
                    Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize - cubeSize * 0.5f;

                    CreateVoxelFaces(center, voxelSize, vi);
                }

        // 建立 Mesh
        mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    void CreateVoxelFaces(Vector3 center, float size, int voxelIndex)
    {
        float halfSize = size * 0.5f;

        // 定義立方體的8個頂點
        Vector3[] cubeVertices = new Vector3[8]
        {
            center + new Vector3(-halfSize, -halfSize, -halfSize), // 0: left-bottom-back
            center + new Vector3( halfSize, -halfSize, -halfSize), // 1: right-bottom-back
            center + new Vector3( halfSize,  halfSize, -halfSize), // 2: right-top-back
            center + new Vector3(-halfSize,  halfSize, -halfSize), // 3: left-top-back
            center + new Vector3(-halfSize, -halfSize,  halfSize), // 4: left-bottom-front
            center + new Vector3( halfSize, -halfSize,  halfSize), // 5: right-bottom-front
            center + new Vector3( halfSize,  halfSize,  halfSize), // 6: right-top-front
            center + new Vector3(-halfSize,  halfSize,  halfSize)  // 7: left-top-front
        };

        // 定義立方體6個面的頂點索引和法線
        int[,] faceIndices = new int[6, 4]
        {
            {0, 1, 2, 3}, // Back face
            {5, 4, 7, 6}, // Front face
            {4, 0, 3, 7}, // Left face
            {1, 5, 6, 2}, // Right face
            {3, 2, 6, 7}, // Top face
            {4, 5, 1, 0}  // Bottom face
        };

        Vector3[] faceNormals = new Vector3[6]
        {
            Vector3.back,    // Back face
            Vector3.forward, // Front face
            Vector3.left,    // Left face
            Vector3.right,   // Right face
            Vector3.up,      // Top face
            Vector3.down     // Bottom face
        };

        // 為每個面創建兩個三角形
        for (int face = 0; face < 6; face++)
        {
            int startVertex = vertices.Count;

            // 添加4個頂點
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = cubeVertices[faceIndices[face, i]];
                vertices.Add(vertex);
                normals.Add(faceNormals[face]);

                // 計算基於整個大方塊的UV座標 (註解掉，改用每個voxel獨立的UV)
                // Vector2 uv = CalculateGlobalUV(vertex, face);
                // uvs.Add(uv);

                // 每個voxel使用完整的紋理 (0-1 UV座標)
                Vector2 localUV = GetLocalFaceUV(i);
                uvs.Add(localUV);
            }

            // 添加兩個三角形（順時針方向）
            // 第一個三角形: 0-1-2
            triangles.Add(startVertex + 0);
            triangles.Add(startVertex + 1);
            triangles.Add(startVertex + 2);
            triangleVoxelIndices.Add(voxelIndex);

            // 第二個三角形: 0-2-3
            triangles.Add(startVertex + 0);
            triangles.Add(startVertex + 2);
            triangles.Add(startVertex + 3);
            triangleVoxelIndices.Add(voxelIndex);
        }
    }

    Vector2 GetLocalFaceUV(int vertexIndex)
    {
        // 每個面的4個頂點使用標準的0-1 UV座標
        Vector2[] faceUVs = new Vector2[4]
        {
            new Vector2(0, 0), // bottom-left
            new Vector2(1, 0), // bottom-right
            new Vector2(1, 1), // top-right
            new Vector2(0, 1)  // top-left
        };

        return faceUVs[vertexIndex];
    }

    // 註解掉全域UV計算方法，保留以備後用
    /*
    Vector2 CalculateGlobalUV(Vector3 worldPos, int faceIndex)
    {
        // 將世界座標轉換為相對於整個大方塊的座標 (0-1範圍)
        Vector3 relativePos = (worldPos + cubeSize * 0.5f);
        
        switch (faceIndex)
        {
            case 0: // Back face (-Z)
                return new Vector2(1.0f - relativePos.x / cubeSize.x, relativePos.y / cubeSize.y);
            case 1: // Front face (+Z)
                return new Vector2(relativePos.x / cubeSize.x, relativePos.y / cubeSize.y);
            case 2: // Left face (-X)
                return new Vector2(relativePos.z / cubeSize.z, relativePos.y / cubeSize.y);
            case 3: // Right face (+X)
                return new Vector2(1.0f - relativePos.z / cubeSize.z, relativePos.y / cubeSize.y);
            case 4: // Top face (+Y)
                return new Vector2(relativePos.x / cubeSize.x, relativePos.z / cubeSize.z);
            case 5: // Bottom face (-Y)
                return new Vector2(relativePos.x / cubeSize.x, 1.0f - relativePos.z / cubeSize.z);
            default:
                return Vector2.zero;
        }
    }
    */

    public void RemoveVoxelByIndex(int vi)
    {
        // 記錄要移除的三角形和對應的頂點
        List<int> trianglesToRemove = new List<int>();

        // 找出要移除的三角形
        for (int i = triangleVoxelIndices.Count - 1; i >= 0; i--)
        {
            if (triangleVoxelIndices[i] == vi)
            {
                trianglesToRemove.Add(i);
            }
        }

        Debug.Log($"Voxel #{vi} removing {trianglesToRemove.Count} triangles");

        // 從後往前移除三角形，避免索引錯亂
        foreach (int triangleIndex in trianglesToRemove)
        {
            // 移除三角形的3個頂點索引
            triangles.RemoveRange(triangleIndex * 3, 3);
            triangleVoxelIndices.RemoveAt(triangleIndex);
        }

        // 更新 Mesh - 不重新計算法線，保持原有的法線
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        // 重要：不呼叫 RecalculateNormals()，保持手動設定的法線
    }

    void SetupVoxelTriggers()
    {
        cubeSize = transform.localScale;
        voxelSize = cubeSize.x / splitCount;

        // 為每個 voxel 建立一個子物件，帶 Trigger
        for (int y = 0; y < splitCount; y++)
            for (int x = 0; x < splitCount; x++)
                for (int z = 0; z < splitCount; z++)
                {
                    int vi = y * splitCount * splitCount + x * splitCount + z;
                    Vector3 centerLocal =
                        (new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize)
                        - cubeSize * 0.5f;

                    // 建子物件
                    GameObject cell = new GameObject($"VT_{vi}");
                    cell.transform.SetParent(transform, false);
                    cell.transform.localPosition = centerLocal;
                    cell.transform.localRotation = Quaternion.identity;

                    // 加 BoxCollider 作為 Trigger
                    var bc = cell.AddComponent<BoxCollider>();
                    bc.size = Vector3.one * voxelSize;
                    bc.isTrigger = true;

                    // 加腳本，OnTrigger 時呼叫父物件方法
                    var tv = cell.AddComponent<VoxelTrigger>();
                    tv.owner = this;
                    tv.voxelIndex = vi;

                    triggerCells.Add(cell);
                }
    }
}