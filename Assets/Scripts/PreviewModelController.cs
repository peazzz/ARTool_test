using UnityEngine;
using Niantic.Lightship.AR.NavigationMesh;

/// <summary>
/// PreviewModelController
/// 改編自 LightshipNavMeshSample，將物件放置在相機面前的地面位置
/// 使用現有的地面檢測系統
/// </summary>
public class PreviewModelController : MonoBehaviour
{
    [Header("位置設定")]
    [SerializeField] private Camera _camera;
    [SerializeField] private float distanceFromCamera = 1.5f;     // 物件距離相機的距離
    [SerializeField] private float groundDetectionRange = 10f;    // 地面檢測範圍
    [SerializeField] private LightshipNavMeshManager _navmeshManager;

    [Header("備用位置設定")]
    [SerializeField] private bool useFixedPositionWhenNoGround = true;  // 沒有地面時使用固定位置
    [SerializeField] private float fixedHeightOffset = -0.5f;           // 相對於相機的固定高度偏移

    [Header("調試設定")]
    [SerializeField] private bool showDebugInfo = false;

    private bool isPositioned = false; // 標記是否已經定位完成

    void Start()
    {
        // 獲取主相機
        if (_camera == null)
            _camera = Camera.main;

        if (_camera == null)
            _camera = FindObjectOfType<Camera>();

        // 立即設定模型位置
        PlaceObjectInFrontOfCamera();

        if (showDebugInfo)
            Debug.Log($"PreviewModelController 初始化完成 - 模型位置: {transform.position}");
    }

    void Update()
    {
        // 如果還沒有定位完成，繼續嘗試定位
        if (!isPositioned && _camera != null)
        {
            PlaceObjectInFrontOfCamera();
        }
    }

    private void PlaceObjectInFrontOfCamera()
    {
        if (_camera == null) return;

        // 從相機中心點向前方發射射線
        Vector3 cameraPosition = _camera.transform.position;
        Vector3 cameraForward = _camera.transform.forward;

        // 計算相機前方指定距離的位置
        Vector3 targetPosition = cameraPosition + cameraForward * distanceFromCamera;

        // 從目標位置向下發射射線，尋找地面
        Ray downwardRay = new Ray(targetPosition, Vector3.down);
        RaycastHit hit;

        // 使用你現有的地面檢測系統
        if (Physics.Raycast(downwardRay, out hit, groundDetectionRange))
        {
            // 將物件放置在檢測到的地面位置
            transform.position = hit.point;
            isPositioned = true; // 標記為已定位完成

            if (showDebugInfo)
                Debug.Log($"物件已放置在地面位置: {hit.point}，命中物件: {hit.collider.name}");
        }
        else
        {
            // 沒有檢測到地面時的處理
            if (useFixedPositionWhenNoGround)
            {
                // 使用相機前方的固定位置（加上高度偏移）
                Vector3 fixedPosition = targetPosition + Vector3.up * fixedHeightOffset;
                transform.position = fixedPosition;

                if (showDebugInfo)
                    Debug.Log($"未檢測到地面，使用相機前方固定位置: {fixedPosition}");
            }
            else
            {
                // 使用原本的目標位置
                transform.position = targetPosition;

                if (showDebugInfo)
                    Debug.Log($"未檢測到地面，使用計算的目標位置: {targetPosition}");
            }

            isPositioned = true;
        }
    }

    // 設定可視化（保留原有功能，適用於有NavMesh的情況）
    public void SetVisualization(bool isVisualizationOn)
    {
        if (_navmeshManager != null)
        {
            // 控制navmesh的渲染
            var renderer = _navmeshManager.GetComponent<LightshipNavMeshRenderer>();
            if (renderer != null)
                renderer.enabled = isVisualizationOn;
        }
    }

    // 公開方法供外部使用
    public void SetCamera(Camera camera)
    {
        _camera = camera;

        // 重新定位（如果還沒固定位置）
        if (!isPositioned)
        {
            PlaceObjectInFrontOfCamera();
        }
    }

    public void SetDistanceFromCamera(float distance)
    {
        distanceFromCamera = distance;

        // 重新定位（如果還沒固定位置）
        if (!isPositioned)
        {
            PlaceObjectInFrontOfCamera();
        }
    }

    public void SetGroundDetectionRange(float range)
    {
        groundDetectionRange = range;

        if (showDebugInfo)
            Debug.Log($"設定地面檢測範圍: {range}");
    }

    public void SetFixedPositionMode(bool useFixed, float heightOffset = -0.5f)
    {
        useFixedPositionWhenNoGround = useFixed;
        fixedHeightOffset = heightOffset;

        if (showDebugInfo)
            Debug.Log($"固定位置模式: {useFixed}, 高度偏移: {heightOffset}");
    }

    public void SetNavMeshManager(LightshipNavMeshManager navmeshManager)
    {
        _navmeshManager = navmeshManager;
    }

    public Vector3 GetCurrentPosition()
    {
        return transform.position;
    }

    public bool IsPositioned()
    {
        return isPositioned;
    }

    // 強制重新定位
    public void ForceReposition()
    {
        isPositioned = false;
        PlaceObjectInFrontOfCamera();

        if (showDebugInfo)
            Debug.Log("強制重新定位物件");
    }

    void OnDrawGizmos()
    {
        if (_camera == null) return;

        // 繪製相機到目標位置的射線（藍色）
        Gizmos.color = Color.blue;
        Vector3 cameraPos = _camera.transform.position;
        Vector3 targetPos = cameraPos + _camera.transform.forward * distanceFromCamera;
        Gizmos.DrawLine(cameraPos, targetPos);

        // 繪製向下的地面檢測射線（紅色）
        Gizmos.color = Color.red;
        Gizmos.DrawLine(targetPos, targetPos + Vector3.down * groundDetectionRange);

        // 繪製物件當前位置（綠色）
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.1f);

        // 繪製目標位置標記（黃色）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(targetPos, Vector3.one * 0.1f);

        // 繪製備用固定位置（橙色）
        if (useFixedPositionWhenNoGround)
        {
            Gizmos.color = Color.magenta;
            Vector3 fixedPos = targetPos + Vector3.up * fixedHeightOffset;
            Gizmos.DrawWireCube(fixedPos, Vector3.one * 0.15f);
        }
    }
}