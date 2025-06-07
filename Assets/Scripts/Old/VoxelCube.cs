using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VoxelCube : MonoBehaviour
{
    public int splitCount = 10;

    public Material urpMaterial;

    private Mesh mesh;
    private float voxelSize;
    private Vector3 cubeSize;

    private List<Vector3> vertices;
    private List<int> triangles;
    private List<Vector3> normals;
    private List<Vector2> uvs;
    private List<int> triangleVoxelIndices;

    private List<GameObject> triggerCells = new List<GameObject>();

    void Start()
    {
        GenerateMesh();

        var mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
        var mr = GetComponent<MeshRenderer>();
        if (mr != null && urpMaterial != null)
        {
            mr.material = urpMaterial;
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

        for (int y = 0; y < splitCount; y++)
            for (int x = 0; x < splitCount; x++)
                for (int z = 0; z < splitCount; z++)
                {
                    int vi = y * splitCount * splitCount + x * splitCount + z;
                    Vector3 center = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize - cubeSize * 0.5f;

                    CreateVoxelFaces(center, voxelSize, vi);
                }

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

        for (int face = 0; face < 6; face++)
        {
            int startVertex = vertices.Count;
            for (int i = 0; i < 4; i++)
            {
                Vector3 vertex = cubeVertices[faceIndices[face, i]];
                vertices.Add(vertex);
                normals.Add(faceNormals[face]);
                Vector2 localUV = GetLocalFaceUV(i);
                uvs.Add(localUV);
            }

            triangles.Add(startVertex + 0);
            triangles.Add(startVertex + 1);
            triangles.Add(startVertex + 2);
            triangleVoxelIndices.Add(voxelIndex);

            triangles.Add(startVertex + 0);
            triangles.Add(startVertex + 2);
            triangles.Add(startVertex + 3);
            triangleVoxelIndices.Add(voxelIndex);
        }
    }

    Vector2 GetLocalFaceUV(int vertexIndex)
    {
        Vector2[] faceUVs = new Vector2[4]
        {
            new Vector2(0, 0), // bottom-left
            new Vector2(1, 0), // bottom-right
            new Vector2(1, 1), // top-right
            new Vector2(0, 1)  // top-left
        };

        return faceUVs[vertexIndex];
    }

    public void RemoveVoxelByIndex(int vi)
    {
        List<int> trianglesToRemove = new List<int>();

        for (int i = triangleVoxelIndices.Count - 1; i >= 0; i--)
        {
            if (triangleVoxelIndices[i] == vi)
            {
                trianglesToRemove.Add(i);
            }
        }

        foreach (int triangleIndex in trianglesToRemove)
        {
            triangles.RemoveRange(triangleIndex * 3, 3);
            triangleVoxelIndices.RemoveAt(triangleIndex);
        }

        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
    }

    void SetupVoxelTriggers()
    {
        cubeSize = transform.localScale;
        voxelSize = cubeSize.x / splitCount;

        for (int y = 0; y < splitCount; y++)
            for (int x = 0; x < splitCount; x++)
                for (int z = 0; z < splitCount; z++)
                {
                    int vi = y * splitCount * splitCount + x * splitCount + z;
                    Vector3 centerLocal =
                        (new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * voxelSize)
                        - cubeSize * 0.5f;

                    GameObject cell = new GameObject($"VT_{vi}");
                    cell.transform.SetParent(transform, false);
                    cell.transform.localPosition = centerLocal;
                    cell.transform.localRotation = Quaternion.identity;

                    var bc = cell.AddComponent<BoxCollider>();
                    bc.size = Vector3.one * voxelSize;
                    bc.isTrigger = true;

                    var tv = cell.AddComponent<VoxelTrigger>();
                    tv.owner = this;
                    tv.voxelIndex = vi;

                    triggerCells.Add(cell);
                }
    }
}