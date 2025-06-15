using UnityEngine;
using System.Collections.Generic;

public class LineRendererPreciseRaycast : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private float lineDetectionThreshold = 0.1f;
    [SerializeField] private float particleDetectionRadius = 0.5f;

    [Header("範圍清除設定")]
    [SerializeField] private bool enableAreaClear = true;
    [SerializeField] private float areaClearRadius = 2f;
    [SerializeField] private GameObject areaClearIndicator;
    [SerializeField] private float longPressTime = 0.5f;
    [SerializeField] private float maxTouchMovement = 50f;

    private bool isLongPressing = false;
    private float pressStartTime;
    private Vector3 longPressPosition;
    private Vector2 pressStartScreenPosition;
    private bool hasTriggeredAreaClear = false;

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
#if UNITY_EDITOR
        HandleMouseInput();
#else
        HandleTouchInput();
#endif
    }

    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            StartPress(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0))
        {
            UpdatePress(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0))
        {
            EndPress(Input.mousePosition);
        }
    }

    void HandleTouchInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            switch (touch.phase)
            {
                case TouchPhase.Began:
                    StartPress(touch.position);
                    break;
                case TouchPhase.Stationary:
                case TouchPhase.Moved:
                    UpdatePress(touch.position);
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    EndPress(touch.position);
                    break;
            }
        }
    }

    void StartPress(Vector2 screenPosition)
    {
        pressStartTime = Time.time;
        isLongPressing = true;
        hasTriggeredAreaClear = false;
        pressStartScreenPosition = screenPosition;

        longPressPosition = GetWorldPositionFromScreen(screenPosition);

        if (enableAreaClear && areaClearIndicator)
        {
            areaClearIndicator.SetActive(false);
        }

        Debug.Log($"開始按壓於螢幕位置: {screenPosition}, 世界位置: {longPressPosition}");
    }

    Vector3 GetWorldPositionFromScreen(Vector2 screenPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            return hit.point;
        }

        return arCamera.transform.position + arCamera.transform.forward * 3f;
    }

    void UpdatePress(Vector2 screenPosition)
    {
        if (!isLongPressing || hasTriggeredAreaClear) return;

        float currentPressTime = Time.time - pressStartTime;

        float movementDistance = Vector2.Distance(screenPosition, pressStartScreenPosition);
        if (movementDistance > maxTouchMovement)
        {
            Debug.Log($"觸控移動距離太大: {movementDistance}, 取消長按");
            isLongPressing = false;
            return;
        }

        if (enableAreaClear && currentPressTime >= longPressTime)
        {
            if (areaClearIndicator)
            {
                areaClearIndicator.SetActive(true);
                areaClearIndicator.transform.position = longPressPosition;
                areaClearIndicator.transform.localScale = Vector3.one * areaClearRadius * 2f;
            }

            int deletedCount = PerformAreaClear(longPressPosition);
            hasTriggeredAreaClear = true;

            Debug.Log($"範圍清除執行於位置: {longPressPosition}，半徑: {areaClearRadius}，刪除了 {deletedCount} 個物件");
        }
    }

    void EndPress(Vector2 screenPosition)
    {
        if (!hasTriggeredAreaClear && isLongPressing)
        {
            float movementDistance = Vector2.Distance(screenPosition, pressStartScreenPosition);
            if (movementDistance <= maxTouchMovement)
            {
                CheckDeletableObjectHit(screenPosition);
            }
        }

        isLongPressing = false;
        hasTriggeredAreaClear = false;

        if (areaClearIndicator)
        {
            areaClearIndicator.SetActive(false);
        }
    }

    int PerformAreaClear(Vector3 centerPosition)
    {
        List<GameObject> objectsToDelete = new List<GameObject>();

        Debug.Log($"開始範圍清除，中心位置: {centerPosition}，半徑: {areaClearRadius}");

        LineRenderer[] allLineRenderers = FindObjectsOfType<LineRenderer>();
        Debug.Log($"找到 {allLineRenderers.Length} 個 LineRenderer");

        foreach (LineRenderer lr in allLineRenderers)
        {
            if (lr == null) continue;

            bool inRange = IsLineRendererInRange(lr, centerPosition, areaClearRadius);
            Debug.Log($"LineRenderer {lr.gameObject.name} 是否在範圍內: {inRange}");

            if (inRange)
            {
                objectsToDelete.Add(lr.gameObject);
            }
        }

        ParticleSystem[] allParticleSystems = FindObjectsOfType<ParticleSystem>();
        Debug.Log($"找到 {allParticleSystems.Length} 個 ParticleSystem");

        foreach (ParticleSystem ps in allParticleSystems)
        {
            if (ps == null) continue;

            float distance = Vector3.Distance(ps.transform.position, centerPosition);
            bool inRange = distance <= areaClearRadius;
            Debug.Log($"ParticleSystem {ps.gameObject.name} 距離: {distance:F2}, 是否在範圍內: {inRange}");

            if (inRange)
            {
                objectsToDelete.Add(ps.gameObject);
            }
        }

        int deletedCount = 0;
        foreach (GameObject obj in objectsToDelete)
        {
            if (obj != null)
            {
                Debug.Log($"刪除物件: {obj.name}");
                Destroy(obj);
                deletedCount++;
            }
        }

        return deletedCount;
    }

    bool IsLineRendererInRange(LineRenderer lineRenderer, Vector3 centerPosition, float radius)
    {
        if (lineRenderer.positionCount < 1) return false;

        Vector3[] positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(positions);

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 worldPos = lineRenderer.transform.TransformPoint(positions[i]);
            float distance = Vector3.Distance(worldPos, centerPosition);

            if (distance <= radius)
            {
                Debug.Log($"LineRenderer 頂點 {i} 在範圍內，距離: {distance:F2}");
                return true;
            }
        }

        for (int i = 0; i < positions.Length - 1; i++)
        {
            Vector3 lineStart = lineRenderer.transform.TransformPoint(positions[i]);
            Vector3 lineEnd = lineRenderer.transform.TransformPoint(positions[i + 1]);

            if (IsLineSegmentIntersectingSphere(lineStart, lineEnd, centerPosition, radius))
            {
                Debug.Log($"LineRenderer 線段 {i}-{i + 1} 穿過範圍");
                return true;
            }
        }

        Bounds bounds = lineRenderer.bounds;
        float boundsDistance = Vector3.Distance(bounds.center, centerPosition);
        float maxBoundsSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);

        if (boundsDistance <= radius + maxBoundsSize * 0.5f)
        {
            Debug.Log($"LineRenderer 包圍盒可能與範圍重疊");
            return true;
        }

        return false;
    }

    bool IsLineSegmentIntersectingSphere(Vector3 lineStart, Vector3 lineEnd, Vector3 sphereCenter, float sphereRadius)
    {
        Vector3 lineDirection = lineEnd - lineStart;
        Vector3 lineToSphere = sphereCenter - lineStart;

        float lineLength = lineDirection.magnitude;
        if (lineLength == 0) return Vector3.Distance(lineStart, sphereCenter) <= sphereRadius;

        lineDirection /= lineLength;

        float projectionLength = Vector3.Dot(lineToSphere, lineDirection);
        projectionLength = Mathf.Clamp(projectionLength, 0, lineLength);

        Vector3 closestPoint = lineStart + lineDirection * projectionLength;
        float distance = Vector3.Distance(closestPoint, sphereCenter);

        return distance <= sphereRadius;
    }

    void CheckDeletableObjectHit(Vector2 screenPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        LineRenderer[] allLineRenderers = FindObjectsOfType<LineRenderer>();
        foreach (LineRenderer lr in allLineRenderers)
        {
            if (IsRayHittingLineRenderer(ray, lr))
            {
                Destroy(lr.gameObject);
                Debug.Log($"刪除了LineRenderer物件: {lr.gameObject.name}");
                return;
            }
        }

        ParticleSystem[] allParticleSystems = FindObjectsOfType<ParticleSystem>();
        foreach (ParticleSystem ps in allParticleSystems)
        {
            if (IsRayHittingParticleSystem(ray, ps))
            {
                Destroy(ps.gameObject);
                Debug.Log($"刪除了ParticleSystem物件: {ps.gameObject.name}");
                return;
            }
        }

        CubeCarvingSystem[] allCubeCarvingSystems = FindObjectsOfType<CubeCarvingSystem>();
        foreach (CubeCarvingSystem ccs in allCubeCarvingSystems)
        {
            if (ccs.GetComponent<LineRenderer>() != null) continue;

            if (IsRayHittingObject(ray, ccs.gameObject))
            {
                Destroy(ccs.gameObject);
                Debug.Log($"刪除了CubeCarvingSystem物件: {ccs.gameObject.name}");
                return;
            }
        }
    }

    bool IsRayHittingObject(Ray ray, GameObject obj)
    {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            return hit.collider.gameObject == obj;
        }
        return false;
    }

    bool IsRayHittingParticleSystem(Ray ray, ParticleSystem particleSystem)
    {
        if (!particleSystem || !particleSystem.gameObject.activeInHierarchy) return false;

        Collider particleCollider = particleSystem.GetComponent<Collider>();
        if (particleCollider != null)
        {
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                return hit.collider == particleCollider;
            }
        }

        return IsRayNearParticleSystem(ray, particleSystem);
    }

    bool IsRayNearParticleSystem(Ray ray, ParticleSystem particleSystem)
    {
        Vector3 particlePosition = particleSystem.transform.position;

        Vector3 rayToParticle = particlePosition - ray.origin;
        Vector3 rayDirection = ray.direction.normalized;

        float projectionLength = Vector3.Dot(rayToParticle, rayDirection);

        if (projectionLength < 0) return false;

        Vector3 closestPointOnRay = ray.origin + rayDirection * projectionLength;
        float distanceToParticle = Vector3.Distance(closestPointOnRay, particlePosition);

        return distanceToParticle <= particleDetectionRadius;
    }

    bool IsRayHittingLineRenderer(Ray ray, LineRenderer lineRenderer)
    {
        if (lineRenderer.positionCount < 2) return false;

        Vector3[] positions = new Vector3[lineRenderer.positionCount];
        lineRenderer.GetPositions(positions);

        for (int i = 0; i < positions.Length - 1; i++)
        {
            Vector3 lineStart = lineRenderer.transform.TransformPoint(positions[i]);
            Vector3 lineEnd = lineRenderer.transform.TransformPoint(positions[i + 1]);

            if (IsRayIntersectingLineSegment(ray, lineStart, lineEnd, lineDetectionThreshold))
            {
                return true;
            }
        }

        return false;
    }

    bool IsRayIntersectingLineSegment(Ray ray, Vector3 lineStart, Vector3 lineEnd, float threshold)
    {
        Vector3 lineDirection = (lineEnd - lineStart).normalized;
        Vector3 rayToLineStart = lineStart - ray.origin;

        Vector3 crossProduct = Vector3.Cross(ray.direction, lineDirection);

        if (crossProduct.magnitude < 0.001f) return false;

        float t = Vector3.Cross(rayToLineStart, lineDirection).magnitude / crossProduct.magnitude;
        float u = Vector3.Cross(rayToLineStart, ray.direction).magnitude / crossProduct.magnitude;

        if (u >= 0 && u <= 1)
        {
            Vector3 intersectionPoint = ray.origin + t * ray.direction;
            float distance = Vector3.Distance(intersectionPoint, Vector3.Lerp(lineStart, lineEnd, u));

            return distance <= threshold;
        }

        return false;
    }

    public void SetAreaClearRadius(float radius)
    {
        areaClearRadius = Mathf.Max(0.1f, radius);
        Debug.Log($"設定範圍清除半徑為: {areaClearRadius}");
    }

    public void SetLongPressTime(float time)
    {
        longPressTime = Mathf.Max(0.1f, time);
        Debug.Log($"設定長按時間為: {longPressTime}");
    }

    public void ToggleAreaClear(bool enabled)
    {
        enableAreaClear = enabled;
        Debug.Log($"範圍清除功能: {(enabled ? "啟用" : "禁用")}");
    }

    void OnDrawGizmos()
    {
        if (enableAreaClear && isLongPressing && !hasTriggeredAreaClear)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(longPressPosition, areaClearRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(longPressPosition, 0.1f);
        }
    }
}