using UnityEngine;

public class OutlineEffect : MonoBehaviour
{
    [Header("Outline Settings")]
    public Color outlineColor = Color.black;
    public float outlineWidth = 0.03f;
    public bool useOutline = true;
    
    private GameObject outlineObject;
    private MeshRenderer originalRenderer;
    private Material outlineMaterial;
    
    void Start()
    {
        CreateOutline();
    }
    
    void CreateOutline()
    {
        if (!useOutline) return;
        
        originalRenderer = GetComponent<MeshRenderer>();
        if (!originalRenderer) return;
        
        // 創建outline材質
        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineMaterial.color = outlineColor;
        
        // 創建outline物件
        outlineObject = new GameObject(gameObject.name + "_Outline");
        outlineObject.transform.SetParent(transform);
        outlineObject.transform.localPosition = Vector3.zero;
        outlineObject.transform.localRotation = Quaternion.identity;
        outlineObject.transform.localScale = Vector3.one * (1f + outlineWidth);
        
        // 複製mesh
        MeshFilter originalMeshFilter = GetComponent<MeshFilter>();
        if (originalMeshFilter)
        {
            MeshFilter outlineMeshFilter = outlineObject.AddComponent<MeshFilter>();
            outlineMeshFilter.mesh = originalMeshFilter.mesh;
        }
        
        // 設置renderer
        MeshRenderer outlineRenderer = outlineObject.AddComponent<MeshRenderer>();
        outlineRenderer.material = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        
        // 確保outline在原物件後面渲染
        outlineRenderer.sortingOrder = originalRenderer.sortingOrder - 1;
    }
    
    public void SetOutlineColor(Color color)
    {
        outlineColor = color;
        if (outlineMaterial)
            outlineMaterial.color = color;
    }
    
    public void SetOutlineWidth(float width)
    {
        outlineWidth = width;
        if (outlineObject)
            outlineObject.transform.localScale = Vector3.one * (1f + width);
    }
    
    public void ToggleOutline(bool enable)
    {
        useOutline = enable;
        if (outlineObject)
            outlineObject.SetActive(enable);
    }
    
    void OnDestroy()
    {
        if (outlineObject)
            DestroyImmediate(outlineObject);
        if (outlineMaterial)
            DestroyImmediate(outlineMaterial);
    }
}