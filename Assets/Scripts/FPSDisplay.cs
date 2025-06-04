using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;

public class FPSDisplay : MonoBehaviour
{
    // 在 Inspector 中把 FPSText 物件拖進來
    public Text fpsText;
    float deltaTime = 0.0f;
    ARSession arSession;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
        arSession = FindObjectOfType<ARSession>();
    }

    void Start()
    {
        if (arSession != null)
        {
            arSession.matchFrameRateRequested = false;
        }
        Application.targetFrameRate = 60;
    }

    void Update()
    {
        // deltaTime 用滑動平均來平滑 FPS 變化
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

        // 稍後在 FixedUpdate 也可以用，但 Update 頻率與畫面 fps 相同即可
    }

    void LateUpdate()
    {
        if (fpsText == null)
        {
            // 如果你忘了在 Inspector 連結，跳錯誤提示
            Debug.LogWarning("FPSDisplay.cs：請在 Inspector 裡把 FPSText 拖進來！");
            return;
        }

        // 計算 fps = 1 / deltaTime
        float fps = 1.0f / deltaTime;

        // 把小數點後保留 1～2 位顯示出來
        fpsText.text = string.Format("FPS: {0:0.}", fps);
    }
}
