using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.Rendering.Universal;

public class ARLightManager : MonoBehaviour
{
    [Header("References")]
    public ARCameraManager arCameraManager;
    public Light directionalLight;
    
    [Header("Settings")]
    [Range(0.1f, 3.0f)]
    public float lightIntensityMultiplier = 1.0f;
    
    [Range(0.1f, 2.0f)]
    public float ambientIntensityMultiplier = 0.5f;
    
    public bool debugMode = false;
    
    void Start()
    {
        // 自動找到AR Camera Manager
        if (arCameraManager == null)
            arCameraManager = FindObjectOfType<ARCameraManager>();
            
        // 自動找到場景中的定向光源
        if (directionalLight == null)
            directionalLight = RenderSettings.sun;
            
        // 啟用光線估計
        if (arCameraManager != null)
        {
            arCameraManager.requestedLightEstimation = 
                LightEstimation.AmbientIntensity | 
                LightEstimation.AmbientColor |
                LightEstimation.MainLightDirection |
                LightEstimation.MainLightIntensity;
                
            arCameraManager.frameReceived += OnCameraFrameReceived;
        }
    }
    
    void OnDestroy()
    {
        if (arCameraManager != null)
            arCameraManager.frameReceived -= OnCameraFrameReceived;
    }
    
    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        var lightEstimation = eventArgs.lightEstimation;
        
        // 更新環境光亮度
        if (lightEstimation.averageBrightness.HasValue)
        {
            var brightness = lightEstimation.averageBrightness.Value;
            RenderSettings.ambientIntensity = brightness * ambientIntensityMultiplier;
            
            if (debugMode)
                Debug.Log($"環境光亮度: {brightness}");
        }
        
        // 更新環境光顏色
        if (lightEstimation.averageColorTemperature.HasValue)
        {
            var colorTemp = lightEstimation.averageColorTemperature.Value;
            var color = Mathf.CorrelatedColorTemperatureToRGB(colorTemp);
            RenderSettings.ambientSkyColor = color;
            
            if (debugMode)
                Debug.Log($"色溫: {colorTemp}K");
        }
        
        // 更新主光源方向
        if (directionalLight != null && lightEstimation.mainLightDirection.HasValue)
        {
            var lightDirection = lightEstimation.mainLightDirection.Value;
            directionalLight.transform.rotation = Quaternion.LookRotation(lightDirection);
            
            if (debugMode)
                Debug.Log($"主光源方向: {lightDirection}");
        }
        
        // 更新主光源強度
        if (directionalLight != null && lightEstimation.mainLightIntensityLumens.HasValue)
        {
            var intensity = lightEstimation.mainLightIntensityLumens.Value;
            // 將流明轉換為Unity的光強度值 (簡化版)
            directionalLight.intensity = Mathf.Clamp(intensity / 1000f * lightIntensityMultiplier, 0.1f, 3.0f);
            
            if (debugMode)
                Debug.Log($"主光源強度: {intensity} 流明");
        }
        
        // 更新主光源顏色
        if (directionalLight != null && lightEstimation.mainLightColor.HasValue)
        {
            directionalLight.color = lightEstimation.mainLightColor.Value;
            
            if (debugMode)
                Debug.Log($"主光源顏色: {lightEstimation.mainLightColor.Value}");
        }
    }
}