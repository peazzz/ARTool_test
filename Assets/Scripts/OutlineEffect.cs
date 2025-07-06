using UnityEngine;

public class OutlineEffect : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = Color.yellow;
    public float outlineWidth = 0.02f;
    public bool useOutline = true;
    [Range(8, 64)]
    public int circleSegments = 32;

    private LineRenderer[] outlineRenderers;
    private MeshFilter meshFilter;
    private Material outlineMaterial;

    void Start()
    {
        CreateOutlineMaterial();
        CreateOutline();
    }

    void CreateOutlineMaterial()
    {
        // 嘗試多種Shader，按優先級排序
        string[] shaderNames = new string[]
        {
            "Sprites/Default",           // Unity內建，iOS兼容性好
            "UI/Default",                // UI Shader，通常可靠
            "Mobile/Unlit (Supports Lightmap)", // 移動平台優化
            "Mobile/VertexLit",          // 移動平台基礎Shader
            "Unlit/Color",               // 原始Shader
            "Legacy Shaders/Unlit/Color" // 備用選項
        };

        outlineMaterial = null;
        
        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader != null)
            {
                outlineMaterial = new Material(shader);
                outlineMaterial.color = outlineColor;
                
                // 對於某些Shader，需要設置特殊屬性
                if (shaderName.Contains("Sprites") || shaderName.Contains("UI"))
                {
                    if (outlineMaterial.HasProperty("_MainTex"))
                    {
                        // 創建一個1x1的白色紋理
                        Texture2D whiteTexture = new Texture2D(1, 1);
                        whiteTexture.SetPixel(0, 0, Color.white);
                        whiteTexture.Apply();
                        outlineMaterial.mainTexture = whiteTexture;
                    }
                }
                
                Debug.Log($"OutlineEffect: 使用Shader: {shaderName}");
                break;
            }
        }

        // 如果所有Shader都找不到，創建一個基本材質
        if (outlineMaterial == null)
        {
            Debug.LogWarning("OutlineEffect: 無法找到合適的Shader，使用默認材質");
            outlineMaterial = new Material(Shader.Find("Standard"));
            outlineMaterial.color = outlineColor;
            // 設置為無光照模式
            if (outlineMaterial.HasProperty("_Mode"))
            {
                outlineMaterial.SetFloat("_Mode", 1); // Cutout mode
            }
        }

        // 設置渲染隊列以確保正確顯示
        outlineMaterial.renderQueue = 3000; // Transparent queue
    }

    void CreateOutline()
    {
        if (!useOutline || outlineMaterial == null) return;

        meshFilter = GetComponent<MeshFilter>();
        if (!meshFilter || !meshFilter.mesh) return;

        CreateBoundingBoxOutline();
    }

    void CreateBoundingBoxOutline()
    {
        Bounds bounds = meshFilter.mesh.bounds;

        // 創建12條線段組成立方體邊框
        outlineRenderers = new LineRenderer[12];

        Vector3[] corners = new Vector3[8];
        corners[0] = bounds.min;
        corners[1] = new Vector3(bounds.min.x, bounds.min.y, bounds.max.z);
        corners[2] = new Vector3(bounds.min.x, bounds.max.y, bounds.min.z);
        corners[3] = new Vector3(bounds.max.x, bounds.min.y, bounds.min.z);
        corners[4] = new Vector3(bounds.max.x, bounds.max.y, bounds.min.z);
        corners[5] = new Vector3(bounds.max.x, bounds.min.y, bounds.max.z);
        corners[6] = new Vector3(bounds.min.x, bounds.max.y, bounds.max.z);
        corners[7] = bounds.max;

        // 定義12條邊
        int[,] edges = new int[,] {
            {0,1}, {1,5}, {5,3}, {3,0}, // 底面
            {2,6}, {6,7}, {7,4}, {4,2}, // 頂面
            {0,2}, {1,6}, {5,7}, {3,4}  // 垂直邊
        };

        for (int i = 0; i < 12; i++)
        {
            GameObject lineObj = new GameObject($"OutlineLine_{i}");
            lineObj.transform.SetParent(transform);
            lineObj.transform.localPosition = Vector3.zero;
            lineObj.transform.localRotation = Quaternion.identity;
            lineObj.transform.localScale = Vector3.one;

            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            
            // 使用我們創建的材質
            lr.material = outlineMaterial;
            
            // iOS兼容性設置
            lr.startWidth = outlineWidth;
            lr.endWidth = outlineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.sortingOrder = 100; // 確保在其他物件前面顯示
            
            // 設置線段的兩個端點
            Vector3 start = corners[edges[i, 0]];
            Vector3 end = corners[edges[i, 1]];

            // 稍微擴大邊框，避免Z-fighting
            Vector3 center = bounds.center;
            float expansionFactor = 1f + outlineWidth * 2f;
            start = center + (start - center) * expansionFactor;
            end = center + (end - center) * expansionFactor;

            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            outlineRenderers[i] = lr;
            
            // 設置適當的層級
            lineObj.layer = gameObject.layer; // 使用與父物件相同的層級
        }
    }

    public void ToggleOutline(bool enable)
    {
        useOutline = enable;
        if (outlineRenderers != null)
        {
            foreach (LineRenderer lr in outlineRenderers)
            {
                if (lr) lr.gameObject.SetActive(enable);
            }
        }
    }

    public void SetOutlineColor(Color color)
    {
        outlineColor = color;
        
        // 更新材質顏色
        if (outlineMaterial != null)
        {
            outlineMaterial.color = color;
        }
        
        // 確保所有LineRenderer使用更新後的顏色
        if (outlineRenderers != null)
        {
            foreach (LineRenderer lr in outlineRenderers)
            {
                if (lr && lr.material)
                {
                    lr.material.color = color;
                }
            }
        }
    }

    public void SetOutlineWidth(float width)
    {
        outlineWidth = width;
        if (outlineRenderers != null)
        {
            foreach (LineRenderer lr in outlineRenderers)
            {
                if (lr)
                {
                    lr.startWidth = width;
                    lr.endWidth = width;
                }
            }
        }

        // 重新創建outline來更新大小
        if (useOutline)
        {
            DestroyOutline();
            CreateBoundingBoxOutline();
        }
    }

    void DestroyOutline()
    {
        if (outlineRenderers != null)
        {
            foreach (LineRenderer lr in outlineRenderers)
            {
                if (lr) DestroyImmediate(lr.gameObject);
            }
            outlineRenderers = null;
        }
    }

    void OnDestroy()
    {
        DestroyOutline();
        
        // 清理材質
        if (outlineMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(outlineMaterial);
            else
                DestroyImmediate(outlineMaterial);
        }
    }

    // 新增：強制重新創建outline（用於除錯）
    [ContextMenu("Recreate Outline")]
    public void RecreateOutline()
    {
        DestroyOutline();
        CreateOutlineMaterial();
        CreateOutline();
    }

    // 新增：檢查當前使用的Shader
    [ContextMenu("Debug Shader Info")]
    public void DebugShaderInfo()
    {
        if (outlineMaterial != null)
        {
            Debug.Log($"當前使用的Shader: {outlineMaterial.shader.name}");
            Debug.Log($"材質顏色: {outlineMaterial.color}");
        }
        else
        {
            Debug.Log("outline材質為null");
        }
    }
}