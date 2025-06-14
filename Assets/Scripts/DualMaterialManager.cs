using System.Collections;
using UnityEngine;

public class DualMaterialManager : MonoBehaviour
{
    [Header("Materials (Auto-assigned from SculptFunction)")]
    public Material paintMaterial;
    public Material textureMaterial;

    [Header("Current State")]
    [SerializeField] private bool isInTextureMode = false;
    [SerializeField] private Color currentColor = Color.white;

    private CubeCarvingSystem carvingSystem;
    private MeshRenderer meshRenderer;
    private Texture2D currentTexture;
    private Material activeMaterial;

    void Awake()
    {
        carvingSystem = GetComponent<CubeCarvingSystem>();
        meshRenderer = GetComponent<MeshRenderer>();
        if (!meshRenderer) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        AutoAssignMaterials();
        SetPaintMode();
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

        if (carvingSystem)
        {
            carvingSystem.SetUVMode(UVMode.UnwrappedFaces);
        }

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

        if (carvingSystem)
        {
            carvingSystem.SetUVMode(UVMode.Continuous);
        }

        if (textureMaterial)
        {
            StartCoroutine(ApplyTextureMaterialNextFrame());
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

    public void CopyStateTo(DualMaterialManager target)
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

    public void CopyStateFrom(DualMaterialManager source)
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

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    public void LogCurrentState()
    {
    }
}