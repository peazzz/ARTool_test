using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;
using SimpleFileBrowser;

// 新增：OBJ物件的材質和UV管理組件
public class OBJMaterialManager : MonoBehaviour
{
    [Header("Materials (Auto-assigned from SculptFunction)")]
    public Material paintMaterial;
    public Material textureMaterial;

    [Header("Current State")]
    [SerializeField] private bool isInTextureMode = false;
    [SerializeField] private Color currentColor = Color.white;
    [SerializeField] private UVMode uvMode = UVMode.Continuous;

    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    private Texture2D currentTexture;
    private Material activeMaterial;
    private Mesh originalMesh;
    private Vector2[] originalUVs;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        if (!meshFilter) meshFilter = gameObject.AddComponent<MeshFilter>();

        AutoAssignMaterials();
        SetPaintMode();

        // 保存原始UV
        if (meshFilter.mesh != null)
        {
            originalMesh = meshFilter.mesh;
            originalUVs = originalMesh.uv;
        }
    }

    void Start()
    {
        if (!paintMaterial || !textureMaterial)
        {
            AutoAssignMaterials();
        }

        if (currentColor == Color.clear || currentColor == new Color(0, 0, 0, 0))
        {
            SculptFunction sculptFunction = FindObjectOfType<SculptFunction>();
            if (sculptFunction && sculptFunction.fcp)
            {
                currentColor = sculptFunction.fcp.color;
            }
            else
            {
                currentColor = Color.white;
            }
        }
    }

    void AutoAssignMaterials()
    {
        SculptFunction sculptFunction = FindObjectOfType<SculptFunction>();
        if (sculptFunction)
        {
            if (!paintMaterial && sculptFunction.ColorMaterial)
            {
                paintMaterial = sculptFunction.ColorMaterial;
            }

            if (!textureMaterial && sculptFunction.TextureMaterial)
            {
                textureMaterial = sculptFunction.TextureMaterial;
            }

            if (sculptFunction.fcp)
            {
                currentColor = sculptFunction.fcp.color;
            }
        }
    }

    public void SetPaintMode()
    {
        isInTextureMode = false;
        currentTexture = null;
        uvMode = UVMode.UnwrappedFaces;

        // 使用包裝UV模式
        ApplyUVMapping();

        if (paintMaterial)
        {
            StartCoroutine(ApplyPaintMaterialNextFrame());
        }
    }

    public void SetTextureMode(Texture2D texture)
    {
        if (!texture)
        {
            SetPaintMode();
            return;
        }

        isInTextureMode = true;
        currentTexture = texture;
        uvMode = UVMode.Continuous;

        // 使用連續UV模式
        ApplyUVMapping();

        if (textureMaterial)
        {
            StartCoroutine(ApplyTextureMaterialNextFrame());
        }
    }

    // 新增：UV映射處理
    private void ApplyUVMapping()
    {
        if (!meshFilter || !originalMesh) return;

        Mesh mesh = meshFilter.mesh;
        if (mesh == null) return;

        Vector2[] uvs = null;

        switch (uvMode)
        {
            case UVMode.Continuous:
                // 使用原始UV或生成連續UV
                uvs = GenerateContinuousUV(mesh);
                break;
            case UVMode.UnwrappedFaces:
                // 生成包裝式UV
                uvs = GenerateUnwrappedUV(mesh);
                break;
        }

        if (uvs != null && uvs.Length == mesh.vertexCount)
        {
            mesh.uv = uvs;
        }
    }

    // 新增：生成連續UV
    private Vector2[] GenerateContinuousUV(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = new Vector2[vertices.Length];

        // 如果有原始UV，使用原始UV
        if (originalUVs != null && originalUVs.Length == vertices.Length)
        {
            return originalUVs;
        }

        // 否則基於世界座標生成UV
        Bounds bounds = mesh.bounds;
        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];

            // 根據包圍盒映射UV
            float u = Mathf.InverseLerp(bounds.min.x, bounds.max.x, vertex.x);
            float v = Mathf.InverseLerp(bounds.min.z, bounds.max.z, vertex.z);

            uvs[i] = new Vector2(u, v);
        }

        return uvs;
    }

    // 新增：生成包裝式UV（模擬CubeCarvingSystem的UnwrappedFaces模式）
    private Vector2[] GenerateUnwrappedUV(Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        Vector2[] uvs = new Vector2[vertices.Length];

        if (normals == null || normals.Length != vertices.Length)
        {
            mesh.RecalculateNormals();
            normals = mesh.normals;
        }

        Bounds bounds = mesh.bounds;
        float uvPadding = 0.01f;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            Vector3 normal = normals[i];

            // 根據法線決定面向
            FaceDirection face = GetFaceFromNormal(normal);
            Vector2 baseUV = CalculateUVFromFace(vertex, face, bounds);

            // 應用包裝UV區域
            UVRegion region = GetUVRegionForFace(face, uvPadding);
            uvs[i] = region.offset + Vector2.Scale(baseUV, region.size);
        }

        return uvs;
    }

    // 新增：從法線獲取面向
    private FaceDirection GetFaceFromNormal(Vector3 normal)
    {
        Vector3 abs = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));

        if (abs.y >= abs.x && abs.y >= abs.z)
        {
            return normal.y > 0 ? FaceDirection.Up : FaceDirection.Down;
        }
        else if (abs.x >= abs.y && abs.x >= abs.z)
        {
            return normal.x > 0 ? FaceDirection.Right : FaceDirection.Left;
        }
        else
        {
            return normal.z > 0 ? FaceDirection.Forward : FaceDirection.Back;
        }
    }

    // 新增：根據面向計算UV
    private Vector2 CalculateUVFromFace(Vector3 vertex, FaceDirection face, Bounds bounds)
    {
        Vector3 normalizedVertex = new Vector3(
            Mathf.InverseLerp(bounds.min.x, bounds.max.x, vertex.x),
            Mathf.InverseLerp(bounds.min.y, bounds.max.y, vertex.y),
            Mathf.InverseLerp(bounds.min.z, bounds.max.z, vertex.z)
        );

        switch (face)
        {
            case FaceDirection.Up:
            case FaceDirection.Down:
                return new Vector2(normalizedVertex.x, normalizedVertex.z);

            case FaceDirection.Left:
            case FaceDirection.Right:
                return new Vector2(normalizedVertex.z, normalizedVertex.y);

            case FaceDirection.Forward:
            case FaceDirection.Back:
                return new Vector2(normalizedVertex.x, normalizedVertex.y);

            default:
                return Vector2.zero;
        }
    }

    // 新增：獲取面向的UV區域（模擬CubeCarvingSystem的布局）
    private UVRegion GetUVRegionForFace(FaceDirection face, float padding)
    {
        switch (face)
        {
            case FaceDirection.Up:
                return new UVRegion(new Vector2(0f + padding, 0f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            case FaceDirection.Down:
                return new UVRegion(new Vector2(1f / 3f + padding, 0f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            case FaceDirection.Forward:
                return new UVRegion(new Vector2(2f / 3f + padding, 0f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            case FaceDirection.Back:
                return new UVRegion(new Vector2(0f + padding, 1f / 2f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            case FaceDirection.Left:
                return new UVRegion(new Vector2(1f / 3f + padding, 1f / 2f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            case FaceDirection.Right:
                return new UVRegion(new Vector2(2f / 3f + padding, 1f / 2f + padding),
                                  new Vector2(1f / 3f - 2 * padding, 1f / 2f - 2 * padding));
            default:
                return new UVRegion(Vector2.zero, Vector2.one);
        }
    }

    // 新增：UV區域類
    private class UVRegion
    {
        public Vector2 offset, size;
        public UVRegion(Vector2 offset, Vector2 size)
        {
            this.offset = offset;
            this.size = size;
        }
    }

    IEnumerator ApplyPaintMaterialNextFrame()
    {
        yield return null;
        yield return null;

        if (paintMaterial && meshRenderer)
        {
            activeMaterial = new Material(paintMaterial);
            activeMaterial.name = $"{paintMaterial.name}_Paint_{gameObject.GetInstanceID()}";

            activeMaterial.color = currentColor;
            if (activeMaterial.HasProperty("_Color"))
            {
                activeMaterial.SetColor("_Color", currentColor);
            }

            if (activeMaterial.HasProperty("_MainTex"))
            {
                activeMaterial.mainTexture = null;
            }

            meshRenderer.material = activeMaterial;
        }
    }

    IEnumerator ApplyTextureMaterialNextFrame()
    {
        yield return null;
        yield return null;

        if (textureMaterial && meshRenderer && currentTexture)
        {
            activeMaterial = new Material(textureMaterial);
            activeMaterial.name = $"{textureMaterial.name}_Texture_{gameObject.GetInstanceID()}";

            if (activeMaterial.HasProperty("_MainTex"))
            {
                activeMaterial.mainTexture = currentTexture;
            }

            activeMaterial.color = currentColor;
            if (activeMaterial.HasProperty("_Color"))
            {
                activeMaterial.SetColor("_Color", currentColor);
            }

            meshRenderer.material = activeMaterial;
        }
    }

    public void OnTextureLoaded(Texture2D loadedTexture)
    {
        if (loadedTexture)
        {
            SetTextureMode(loadedTexture);
        }
        else
        {
            SetPaintMode();
        }
    }

    public void ClearTexture()
    {
        SetPaintMode();
    }

    public bool HasTexture() => isInTextureMode && currentTexture != null;

    public bool SupportsPainting() => !isInTextureMode;

    public Texture2D GetCurrentTexture() => currentTexture;

    public bool IsInTextureMode() => isInTextureMode;

    public UVMode GetUVMode() => uvMode;

    public void SetUVMode(UVMode newUVMode)
    {
        uvMode = newUVMode;
        ApplyUVMapping();
    }

    public void SetColor(Color color)
    {
        currentColor = color;

        if (meshRenderer && meshRenderer.material)
        {
            Material currentMat = meshRenderer.material;

            if (isInTextureMode)
            {
                currentMat.color = color;

                if (currentMat.HasProperty("_Color"))
                {
                    currentMat.SetColor("_Color", color);
                }
            }
            else
            {
                currentMat.color = color;

                if (currentMat.HasProperty("_Color"))
                {
                    currentMat.SetColor("_Color", color);
                }
            }
        }
    }

    public void RefreshMaterial()
    {
        if (isInTextureMode && currentTexture)
        {
            Color tempColor = currentColor;
            SetTextureMode(currentTexture);
            currentColor = tempColor;
            SetColor(tempColor);
        }
        else
        {
            SetPaintMode();
            SetColor(currentColor);
        }
    }

    public Color GetCurrentColor() => currentColor;

    public void CopyStateTo(OBJMaterialManager target)
    {
        if (!target) return;

        if (isInTextureMode && currentTexture)
        {
            target.SetTextureMode(currentTexture);
        }
        else
        {
            target.SetPaintMode();
            target.SetColor(currentColor);
        }
    }

    public void CopyStateFrom(OBJMaterialManager source)
    {
        if (!source) return;

        if (source.IsInTextureMode() && source.GetCurrentTexture())
        {
            SetTextureMode(source.GetCurrentTexture());
        }
        else
        {
            SetPaintMode();
            SetColor(source.GetCurrentColor());
        }
    }

    void OnDestroy()
    {
        if (activeMaterial && activeMaterial != paintMaterial && activeMaterial != textureMaterial)
        {
            DestroyImmediate(activeMaterial);
        }
    }
}