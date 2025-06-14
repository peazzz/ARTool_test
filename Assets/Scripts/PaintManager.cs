using UnityEngine;
using System.Collections.Generic;

public class PaintManager : MonoBehaviour
{
    [Header("Paint Settings")]
    public Color paintColor = Color.white;
    public float brushSize = 0.05f;
    public int textureSize = 512;

    [Header("Brush Settings")]
    public float brushSoftness = 0.1f;

    [Header("Debug")]
    public bool showDebugInfo = false;
    public bool createTestPattern = false;

    private Texture2D paintTexture;
    private Material materialInstance;
    private CubeCarvingSystem carvingSystem;
    private Renderer targetRenderer;

    private List<Texture2D> paintHistory = new List<Texture2D>();
    private int maxHistoryCount = 10;

    void Start()
    {
        InitializePaintSystem();
    }

    void InitializePaintSystem()
    {
        targetRenderer = GetComponent<Renderer>();
        carvingSystem = GetComponent<CubeCarvingSystem>();

        if (targetRenderer == null || carvingSystem == null)
        {
            Debug.LogError("PaintManager needs Renderer and CubeCarvingSystem components!");
            return;
        }

        CreatePaintTexture();
        CreateMaterialInstance();
    }

    void CreatePaintTexture()
    {
        paintTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);

        if (carvingSystem.GetUVMode() == UVMode.UnwrappedFaces)
        {
            InitializeUnwrappedTexture();
        }
        else
        {
            InitializeContinuousTexture();
        }

        if (createTestPattern)
        {
            CreateTestPattern();
        }

        paintTexture.Apply();
    }

    void InitializeUnwrappedTexture()
    {
        Color[] pixels = new Color[textureSize * textureSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        paintTexture.SetPixels(pixels);
    }

    void InitializeContinuousTexture()
    {
        Color[] pixels = new Color[textureSize * textureSize];

        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.clear;
        }

        paintTexture.SetPixels(pixels);
    }

    void CreateMaterialInstance()
    {
        if (targetRenderer.material != null)
        {
            materialInstance = new Material(targetRenderer.material);
            materialInstance.SetTexture("_PaintTexture", paintTexture);
            materialInstance.SetFloat("_PaintOpacity", 1.0f);
            targetRenderer.material = materialInstance;
        }
    }

    void CreateTestPattern()
    {
        if (carvingSystem.GetUVMode() != UVMode.UnwrappedFaces) return;

        Color[] testColors = new Color[]
        {
            Color.red,
            Color.green,
            Color.blue,
            Color.yellow,
            Color.cyan,
            Color.magenta
        };

        string[] faceNames = { "Up", "Down", "Forward", "Back", "Left", "Right" };

        for (int faceIndex = 0; faceIndex < 6; faceIndex++)
        {
            Vector2 regionOffset;
            Vector2 regionSize = new Vector2(1f / 3f, 1f / 2f);

            switch (faceIndex)
            {
                case 0: regionOffset = new Vector2(0f, 0f); break;
                case 1: regionOffset = new Vector2(1f / 3f, 0f); break;
                case 2: regionOffset = new Vector2(2f / 3f, 0f); break;
                case 3: regionOffset = new Vector2(0f, 1f / 2f); break;
                case 4: regionOffset = new Vector2(1f / 3f, 1f / 2f); break;
                case 5: regionOffset = new Vector2(2f / 3f, 1f / 2f); break;
                default: regionOffset = Vector2.zero; break;
            }

            int startX = Mathf.RoundToInt(regionOffset.x * textureSize);
            int startY = Mathf.RoundToInt(regionOffset.y * textureSize);
            int endX = Mathf.RoundToInt((regionOffset.x + regionSize.x) * textureSize);
            int endY = Mathf.RoundToInt((regionOffset.y + regionSize.y) * textureSize);

            for (int y = startY; y < endY && y < textureSize; y++)
            {
                for (int x = startX; x < endX && x < textureSize; x++)
                {
                    float localU = (float)(x - startX) / (endX - startX);
                    float localV = (float)(y - startY) / (endY - startY);

                    Color pixelColor = testColors[faceIndex];

                    if (Mathf.Abs(localU - 0.5f) < 0.02f || Mathf.Abs(localV - 0.5f) < 0.02f)
                    {
                        pixelColor = Color.white;
                    }

                    if (localU < 0.05f || localU > 0.95f || localV < 0.05f || localV > 0.95f)
                    {
                        pixelColor = Color.black;
                    }

                    pixelColor.a = 0.7f;
                    paintTexture.SetPixel(x, y, pixelColor);
                }
            }
        }

        if (showDebugInfo)
        {
            Debug.Log("Test pattern created for UV debugging");
        }
    }

    public void PaintAt(Vector3 worldPosition, Vector3 normal)
    {
        if (paintTexture == null || carvingSystem == null)
            return;

        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector3 localNormal = transform.InverseTransformDirection(normal);

        FaceDirection face = GetFaceDirectionFromLocalNormal(localNormal);

        Vector2 uv = CalculateUVFromLocalPosition(localPosition, face, localNormal);

        if (showDebugInfo)
        {
            Debug.Log($"Paint at World: {worldPosition}, Local: {localPosition}, Face: {face}, UV: {uv}, LocalNormal: {localNormal}");

            // 新增：詳細除錯資訊
            Vector3 cubeCenter = transform.position;
            Vector3 relativePos = worldPosition - cubeCenter;
            Debug.Log($"Cube Center: {cubeCenter}, Relative Position: {relativePos}");
            Debug.Log($"Hit Normal: {normal}, Local Normal: {localNormal}");
        }

        PaintAtUV(uv, face);
    }

    FaceDirection GetFaceDirectionFromLocalNormal(Vector3 localNormal)
    {
        Vector3 absNormal = new Vector3(Mathf.Abs(localNormal.x), Mathf.Abs(localNormal.y), Mathf.Abs(localNormal.z));

        if (absNormal.y > absNormal.x && absNormal.y > absNormal.z)
        {
            return localNormal.y > 0 ? FaceDirection.Up : FaceDirection.Down;
        }
        else if (absNormal.x > absNormal.y && absNormal.x > absNormal.z)
        {
            return localNormal.x > 0 ? FaceDirection.Right : FaceDirection.Left;
        }
        else
        {
            return localNormal.z > 0 ? FaceDirection.Forward : FaceDirection.Back;
        }
    }

    Vector2 CalculateUVFromLocalPosition(Vector3 localPos, FaceDirection face, Vector3 localNormal)
    {
        float cubeSize = carvingSystem.GetCubeSize();
        float halfSize = cubeSize * 0.5f;

        // 將局部座標正規化到 [0, 1] 範圍
        Vector3 normalizedPos = (localPos + Vector3.one * halfSize) / cubeSize;

        Vector2 faceUV = Vector2.zero;

        // 簡化版本：先不做任何翻轉，直接按最基本的邏輯計算
        switch (face)
        {
            case FaceDirection.Up:
            case FaceDirection.Down:
                faceUV = new Vector2(normalizedPos.x, normalizedPos.z);
                break;

            case FaceDirection.Forward:
            case FaceDirection.Back:
                faceUV = new Vector2(normalizedPos.x, normalizedPos.y);
                break;

            case FaceDirection.Right:
            case FaceDirection.Left:
                faceUV = new Vector2(normalizedPos.z, normalizedPos.y);
                break;
        }

        faceUV.x = Mathf.Clamp01(faceUV.x);
        faceUV.y = Mathf.Clamp01(faceUV.y);

        if (showDebugInfo)
        {
            Debug.Log($"SIMPLE VERSION - Face: {face}, LocalPos: {localPos}, NormalizedPos: {normalizedPos}, FaceUV: {faceUV}");
        }

        if (carvingSystem.GetUVMode() == UVMode.UnwrappedFaces)
        {
            return MapFaceUVToUnwrappedUV(faceUV, face);
        }
        else
        {
            return faceUV;
        }
    }

    Vector2 MapFaceUVToUnwrappedUV(Vector2 faceUV, FaceDirection face)
    {
        Vector2 regionOffset = Vector2.zero;
        Vector2 regionSize = new Vector2(1f / 3f, 1f / 2f);

        // 必須與 CubeCarvingSystem 中的 UV 區域分配完全一致
        switch (face)
        {
            case FaceDirection.Up:
                regionOffset = new Vector2(0f, 0f);
                break;
            case FaceDirection.Down:
                regionOffset = new Vector2(1f / 3f, 0f);
                break;
            case FaceDirection.Forward:
                regionOffset = new Vector2(2f / 3f, 0f);
                break;
            case FaceDirection.Back:
                regionOffset = new Vector2(0f, 1f / 2f);
                break;
            case FaceDirection.Left:
                regionOffset = new Vector2(1f / 3f, 1f / 2f);
                break;
            case FaceDirection.Right:
                regionOffset = new Vector2(2f / 3f, 1f / 2f);
                break;
        }

        Vector2 mappedUV = regionOffset + Vector2.Scale(faceUV, regionSize);

        mappedUV.x = Mathf.Clamp01(mappedUV.x);
        mappedUV.y = Mathf.Clamp01(mappedUV.y);

        return mappedUV;
    }

    void PaintAtUV(Vector2 uv, FaceDirection face)
    {
        SavePaintState();

        int pixelX = Mathf.RoundToInt(uv.x * (textureSize - 1));
        int pixelY = Mathf.RoundToInt(uv.y * (textureSize - 1));

        int brushRadius = Mathf.RoundToInt(brushSize * textureSize * 0.5f);
        brushRadius = Mathf.Max(1, brushRadius);

        for (int y = -brushRadius; y <= brushRadius; y++)
        {
            for (int x = -brushRadius; x <= brushRadius; x++)
            {
                int targetX = pixelX + x;
                int targetY = pixelY + y;

                if (targetX >= 0 && targetX < textureSize &&
                    targetY >= 0 && targetY < textureSize)
                {
                    float distance = Mathf.Sqrt(x * x + y * y);

                    if (distance <= brushRadius)
                    {
                        float strength = CalculateBrushStrength(distance, brushRadius);

                        Color currentColor = paintTexture.GetPixel(targetX, targetY);

                        Color blendedColor = Color.Lerp(currentColor, paintColor, strength * paintColor.a);
                        blendedColor.a = Mathf.Max(currentColor.a, strength * paintColor.a);

                        paintTexture.SetPixel(targetX, targetY, blendedColor);
                    }
                }
            }
        }

        paintTexture.Apply();
    }

    float CalculateBrushStrength(float distance, float brushRadius)
    {
        if (brushRadius <= 0) return 1f;

        float normalizedDistance = distance / brushRadius;

        float falloffStart = 1f - brushSoftness;

        if (normalizedDistance <= falloffStart)
        {
            return 1f;
        }
        else if (normalizedDistance <= 1f)
        {
            float falloffRange = brushSoftness;
            float falloffPosition = (normalizedDistance - falloffStart) / falloffRange;
            return 1f - Mathf.SmoothStep(0f, 1f, falloffPosition);
        }
        else
        {
            return 0f;
        }
    }

    void SavePaintState()
    {
        Texture2D snapshot = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        snapshot.SetPixels(paintTexture.GetPixels());
        snapshot.Apply();

        paintHistory.Add(snapshot);

        if (paintHistory.Count > maxHistoryCount)
        {
            if (paintHistory[0] != null)
                DestroyImmediate(paintHistory[0]);
            paintHistory.RemoveAt(0);
        }
    }

    public void Undo()
    {
        if (paintHistory.Count > 0)
        {
            Texture2D lastState = paintHistory[paintHistory.Count - 1];
            paintTexture.SetPixels(lastState.GetPixels());
            paintTexture.Apply();

            if (lastState != null)
                DestroyImmediate(lastState);
            paintHistory.RemoveAt(paintHistory.Count - 1);
        }
    }

    public void ClearPaint()
    {
        if (paintTexture != null)
        {
            Color[] clearPixels = new Color[textureSize * textureSize];
            for (int i = 0; i < clearPixels.Length; i++)
            {
                clearPixels[i] = Color.clear;
            }
            paintTexture.SetPixels(clearPixels);
            paintTexture.Apply();
        }

        foreach (Texture2D texture in paintHistory)
        {
            if (texture != null)
                DestroyImmediate(texture);
        }
        paintHistory.Clear();
    }

    void OnDestroy()
    {
        if (paintTexture != null)
        {
            DestroyImmediate(paintTexture);
        }

        if (materialInstance != null)
        {
            DestroyImmediate(materialInstance);
        }

        foreach (Texture2D texture in paintHistory)
        {
            if (texture != null)
                DestroyImmediate(texture);
        }
        paintHistory.Clear();
    }

    void OnDrawGizmos()
    {
        if (Application.isPlaying && paintTexture != null)
        {
        }
    }

    // 新增方法來支援 DrawFunction.cs
    public void SetPaintColor(Color color)
    {
        paintColor = color;
    }

    public void SetBrushSize(float size)
    {
        brushSize = size;
    }
}