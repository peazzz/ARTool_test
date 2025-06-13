using System.Collections;
using UnityEngine;

public class DualMaterialManager : MonoBehaviour
{
    [Header("Materials (Auto-assigned from SculptFunction)")]
    public Material paintMaterial;    // 自動從 SculptFunction.ColorMaterial 獲取
    public Material textureMaterial; // 自動從 SculptFunction.TextureMaterial 獲取

    private CubeCarvingSystem carvingSystem;
    private MeshRenderer meshRenderer;
    private Texture2D currentTexture;
    private bool hasTexture = false;

    void Awake()
    {
        carvingSystem = GetComponent<CubeCarvingSystem>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // 自動從 SculptFunction 獲取材質
        AutoAssignMaterials();

        // 設定初始材質
        SetPaintMode();
    }

    void AutoAssignMaterials()
    {
        SculptFunction sculptFunction = FindObjectOfType<SculptFunction>();
        if (sculptFunction)
        {
            // 如果材質尚未設定，則自動獲取
            if (!paintMaterial && sculptFunction.ColorMaterial)
            {
                paintMaterial = sculptFunction.ColorMaterial;
            }

            if (!textureMaterial && sculptFunction.TextureMaterial)
            {
                textureMaterial = sculptFunction.TextureMaterial;
            }
        }
    }

    // 設定為上色模式 (UnwrappedFaces UV)
    public void SetPaintMode()
    {
        hasTexture = false;
        currentTexture = null;

        if (carvingSystem)
        {
            carvingSystem.SetUVMode(UVMode.UnwrappedFaces);
            if (paintMaterial)
            {
                StartCoroutine(ApplyMaterialNextFrame(paintMaterial));
            }
        }
    }

    // 設定為貼圖模式 (Continuous UV)  
    public void SetTextureMode(Texture2D texture)
    {
        hasTexture = true;
        currentTexture = texture;

        if (carvingSystem)
        {
            carvingSystem.SetUVMode(UVMode.Continuous);
            if (textureMaterial)
            {
                StartCoroutine(ApplyMaterialNextFrame(textureMaterial));
            }
        }
    }

    IEnumerator ApplyMaterialNextFrame(Material material)
    {
        yield return null; // 等待UV重新生成

        if (material && meshRenderer)
        {
            meshRenderer.material = new Material(material);

            // 如果有貼圖，應用到材質
            if (hasTexture && currentTexture && meshRenderer.material.HasProperty("_MainTex"))
            {
                meshRenderer.material.mainTexture = currentTexture;
            }
        }
    }

    // 接收圖片的方法 (供file browser調用)
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

    // 清除貼圖，回到上色模式
    public void ClearTexture()
    {
        SetPaintMode();
    }

    // 檢查當前是否有貼圖
    public bool HasTexture() => hasTexture;

    // 檢查是否支援繪畫 (只有上色模式支援)
    public bool SupportsPainting() => !hasTexture;

    // 獲取當前貼圖
    public Texture2D GetCurrentTexture() => currentTexture;

    // 設定材質顏色 (只在上色模式有效)
    public void SetColor(Color color)
    {
        var currentColor = color;

        if (meshRenderer && meshRenderer.material)
        {
            Material currentMat = meshRenderer.material;

            // 只設置材質的基礎顏色，不影響繪圖
            currentMat.color = color;

            if (currentMat.HasProperty("_Color"))
            {
                currentMat.SetColor("_Color", color);
            }

            // 不要覆蓋繪圖相關的屬性
        }
    }
}