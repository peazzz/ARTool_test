using UnityEngine;
using System.Collections.Generic;

public class LineRendererPreciseRaycast : MonoBehaviour
{
    [SerializeField] private Camera arCamera;
    [SerializeField] private float lineDetectionThreshold = 0.1f;

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                CheckDeletableObjectHit(touch.position);
            }
        }

#if UNITY_EDITOR
        if (Input.GetMouseButtonDown(0))
        {
            CheckDeletableObjectHit(Input.mousePosition);
        }
#endif
    }

    void CheckDeletableObjectHit(Vector2 screenPosition)
    {
        Ray ray = arCamera.ScreenPointToRay(screenPosition);

        // 先檢查LineRenderer物件
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

        // 再檢查CubeCarvingSystem物件（沒有LineRenderer的）
        CubeCarvingSystem[] allCubeCarvingSystems = FindObjectsOfType<CubeCarvingSystem>();
        foreach (CubeCarvingSystem ccs in allCubeCarvingSystems)
        {
            // 跳過已經有LineRenderer的物件（避免重複檢測）
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
        // 使用Physics.Raycast檢測碰撞器
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            return hit.collider.gameObject == obj;
        }
        return false;
    }

    // ... 其他方法保持不變
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
}