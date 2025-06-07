using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class FPSDisplay : MonoBehaviour
{
    public Text fpsText;
    float deltaTime = 0.0f;
    ARSession arSession;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 90;
        arSession = FindObjectOfType<ARSession>();
    }

    void Start()
    {
        if (arSession != null)
        {
            arSession.matchFrameRateRequested = false;
        }
        Application.targetFrameRate = 90;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
    }

    void LateUpdate()
    {
        if (fpsText == null)
        {
            return;
        }

        float fps = 1.0f / deltaTime;
        fpsText.text = string.Format("FPS: {0:0.}", fps);
    }
}
