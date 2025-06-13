using System.Collections;
using UnityEngine;

public class PaintManager : MonoBehaviour
{
    public Color paintColor = Color.red;
    public float brushSize = 0.05f;
    public int textureSize = 1024;

    private Texture2D paintTexture;
    private bool hasPaintData = false;
    private CubeCarvingSystem carvingSystem;
    private MeshRenderer meshRenderer;
    private bool needsTextureUpdate = false;

    void Awake()
    {
        carvingSystem = GetComponent<CubeCarvingSystem>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();
        InitializePaintTexture();
    }

    void Update()
    {
        if (needsTextureUpdate)
        {
            UpdatePaintTexture();
            needsTextureUpdate = false;
        }
    }

    void InitializePaintTexture()
    {
        paintTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] clearPixels = new Color[textureSize * textureSize];
        for (int i = 0; i < clearPixels.Length; i++)
            clearPixels[i] = Color.clear;
        paintTexture.SetPixels(clearPixels);
        paintTexture.Apply();
        paintTexture.filterMode = FilterMode.Bilinear;
        paintTexture.wrapMode = TextureWrapMode.Clamp;
    }

    public void PaintAt(Vector3 worldPosition, Vector3 normal)
    {
        Vector2 uv = WorldToUV(worldPosition, normal);
        if (uv.x >= 0 && uv.x <= 1 && uv.y >= 0 && uv.y <= 1)
        {
            PaintAtUV(uv);
            hasPaintData = true;
        }
    }

    Vector2 WorldToUV(Vector3 worldPosition, Vector3 normal)
    {
        if (!carvingSystem) return Vector2.zero;
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        return CalculateUnwrappedUV(localPosition, normal);
    }

    Vector2 CalculateUnwrappedUV(Vector3 localPos, Vector3 normal)
    {
        float cubeSize = carvingSystem.GetCubeSize();
        Vector3 normalizedPos = (localPos + Vector3.one * cubeSize * 0.5f) / cubeSize;
        FaceDirection face = carvingSystem.GetFaceDirection(normal);

        Vector2 baseUV = Vector2.zero;
        Vector2 offset = Vector2.zero;
        Vector2 scale = new Vector2(1f / 3f, 1f / 2f);

        switch (face)
        {
            case FaceDirection.Up:
                baseUV = new Vector2(normalizedPos.x, normalizedPos.z);
                offset = new Vector2(0f, 0f);
                break;
            case FaceDirection.Down:
                baseUV = new Vector2(normalizedPos.x, normalizedPos.z);
                offset = new Vector2(1f / 3f, 0f);
                break;
            case FaceDirection.Forward:
                baseUV = new Vector2(normalizedPos.x, normalizedPos.y);
                offset = new Vector2(2f / 3f, 0f);
                break;
            case FaceDirection.Back:
                baseUV = new Vector2(normalizedPos.x, normalizedPos.y);
                offset = new Vector2(0f, 1f / 2f);
                break;
            case FaceDirection.Left:
                baseUV = new Vector2(normalizedPos.z, normalizedPos.y);
                offset = new Vector2(1f / 3f, 1f / 2f);
                break;
            case FaceDirection.Right:
                baseUV = new Vector2(normalizedPos.z, normalizedPos.y);
                offset = new Vector2(2f / 3f, 1f / 2f);
                break;
        }

        return offset + Vector2.Scale(baseUV, scale);
    }

    void PaintAtUV(Vector2 uv)
    {
        int x = Mathf.RoundToInt(uv.x * (textureSize - 1));
        int y = Mathf.RoundToInt(uv.y * (textureSize - 1));
        int brushRadius = Mathf.Max(Mathf.RoundToInt(brushSize * textureSize * 0.5f), 1);
        Color[] pixels = paintTexture.GetPixels();

        for (int py = -brushRadius; py <= brushRadius; py++)
        {
            for (int px = -brushRadius; px <= brushRadius; px++)
            {
                int pixelX = x + px;
                int pixelY = y + py;

                if (pixelX >= 0 && pixelX < textureSize && pixelY >= 0 && pixelY < textureSize)
                {
                    float distance = Mathf.Sqrt(px * px + py * py);
                    if (distance <= brushRadius)
                    {
                        int pixelIndex = pixelY * textureSize + pixelX;
                        pixels[pixelIndex] = paintColor;
                    }
                }
            }
        }

        paintTexture.SetPixels(pixels);
        needsTextureUpdate = true;
    }

    void UpdatePaintTexture()
    {
        if (paintTexture)
        {
            paintTexture.Apply();
            if (meshRenderer?.material)
            {
                Material mat = meshRenderer.material;

                // 設置繪圖紋理
                if (mat.HasProperty("_PaintTexture"))
                    mat.SetTexture("_PaintTexture", paintTexture);
                if (mat.HasProperty("_PaintOpacity"))
                    mat.SetFloat("_PaintOpacity", 1.0f);

                // 移除這些行，不要覆蓋材質的基礎顏色
                // mat.color = paintColor;
                // if (mat.HasProperty("_Color"))
                //     mat.SetColor("_Color", paintColor);
            }
        }
    }

    public bool HasPaintData() => hasPaintData;

    public void ClearPaint()
    {
        if (paintTexture)
        {
            Color[] clearPixels = new Color[textureSize * textureSize];
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = Color.clear;
            paintTexture.SetPixels(clearPixels);
            paintTexture.Apply();
            hasPaintData = false;
            if (meshRenderer?.material?.HasProperty("_PaintTexture") == true)
                meshRenderer.material.SetTexture("_PaintTexture", paintTexture);
        }
    }

    public void SetBrushSize(float size) => brushSize = Mathf.Clamp(size, 0.001f, 0.5f);
    public void SetPaintColor(Color color) => paintColor = color;
    public Texture2D GetPaintTexture() => paintTexture;

    public void SetTextureSize(int size)
    {
        textureSize = Mathf.ClosestPowerOfTwo(Mathf.Clamp(size, 256, 2048));
        InitializePaintTexture();
    }

    void OnDestroy()
    {
        if (paintTexture) DestroyImmediate(paintTexture);
    }
}