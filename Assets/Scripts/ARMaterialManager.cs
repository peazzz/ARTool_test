using UnityEngine;

public class ARMaterialManager : MonoBehaviour
{
    [Header("AR Light Compatibility")]
    [SerializeField] private bool maintainARLightCompatibility = true;
    [SerializeField] private float arLightRefreshRate = 0.1f; // 每0.1秒強制刷新一次
    
    private Material materialInstance;
    private Renderer targetRenderer;
    private PaintManager paintManager;
    private float lastRefreshTime;
    
    // 追蹤上次的光照狀態，檢測變化
    private Color lastAmbientColor;
    private float lastAmbientIntensity;
    private Color lastLightColor;
    private float lastLightIntensity;
    
    void Start()
    {
        targetRenderer = GetComponent<Renderer>();
        paintManager = GetComponent<PaintManager>();
        
        if (maintainARLightCompatibility)
        {
            InitializeARCompatibleMaterial();
            CacheLightingState();
        }
    }
    
    void Update()
    {
        if (maintainARLightCompatibility && materialInstance != null)
        {
            // 定期檢查光照變化並強制更新材質
            if (Time.time - lastRefreshTime > arLightRefreshRate)
            {
                CheckAndUpdateLighting();
                lastRefreshTime = Time.time;
            }
        }
    }
    
    void InitializeARCompatibleMaterial()
    {
        if (targetRenderer != null && targetRenderer.material != null)
        {
            // 創建材質實例但保持AR兼容性
            materialInstance = new Material(targetRenderer.material);
            
            // 設置AR光照倍增器為1.0（讓shader接收完整的AR光照）
            if (materialInstance.HasProperty("_ARLightMultiplier"))
            {
                materialInstance.SetFloat("_ARLightMultiplier", 1.0f);
            }
            
            // 應用材質實例
            targetRenderer.material = materialInstance;
            
            Debug.Log("AR兼容材質已初始化");
        }
    }
    
    void CacheLightingState()
    {
        lastAmbientColor = RenderSettings.ambientSkyColor;
        lastAmbientIntensity = RenderSettings.ambientIntensity;
        
        Light mainLight = RenderSettings.sun;
        if (mainLight != null)
        {
            lastLightColor = mainLight.color;
            lastLightIntensity = mainLight.intensity;
        }
    }
    
    void CheckAndUpdateLighting()
    {
        bool lightingChanged = false;
        
        // 檢查環境光變化
        if (RenderSettings.ambientSkyColor != lastAmbientColor ||
            RenderSettings.ambientIntensity != lastAmbientIntensity)
        {
            lightingChanged = true;
        }
        
        // 檢查主光源變化
        Light mainLight = RenderSettings.sun;
        if (mainLight != null)
        {
            if (mainLight.color != lastLightColor ||
                mainLight.intensity != lastLightIntensity)
            {
                lightingChanged = true;
            }
        }
        
        if (lightingChanged)
        {
            ForceShaderRefresh();
            CacheLightingState();
        }
    }
    
    void ForceShaderRefresh()
    {
        if (materialInstance != null)
        {
            // 強制shader重新計算光照
            if (materialInstance.HasProperty("_ForceRefresh"))
            {
                float currentRefresh = materialInstance.GetFloat("_ForceRefresh");
                materialInstance.SetFloat("_ForceRefresh", currentRefresh + 0.001f);
            }
            
            // 確保AR光照倍增器保持正確值
            if (materialInstance.HasProperty("_ARLightMultiplier"))
            {
                materialInstance.SetFloat("_ARLightMultiplier", 1.0f);
            }
            
            Debug.Log("強制刷新shader以應用AR光照變化");
        }
    }
    
    // 給PaintManager調用的方法
    public void UpdatePaintTexture(Texture2D paintTexture)
    {
        if (materialInstance != null && paintTexture != null)
        {
            materialInstance.SetTexture("_PaintTexture", paintTexture);
            materialInstance.SetFloat("_PaintOpacity", 1.0f);
            
            // 確保AR光照兼容性不受影響
            if (maintainARLightCompatibility)
            {
                ForceShaderRefresh();
            }
        }
    }
    
    // 手動強制更新材質以接收最新AR光照
    public void ForceARLightingUpdate()
    {
        if (materialInstance != null)
        {
            ForceShaderRefresh();
            Debug.Log("手動強制更新AR光照");
        }
    }
    
    // 切換AR光照兼容性
    public void SetARLightCompatibility(bool enabled)
    {
        maintainARLightCompatibility = enabled;
        
        if (enabled)
        {
            CacheLightingState();
            ForceShaderRefresh();
        }
    }
    
    void OnDestroy()
    {
        if (materialInstance != null)
        {
            DestroyImmediate(materialInstance);
        }
    }
}