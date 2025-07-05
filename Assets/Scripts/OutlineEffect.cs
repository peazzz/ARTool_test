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

    void Start()
    {
        CreateOutline();
    }

    void CreateOutline()
    {
        if (!useOutline) return;

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
            lr.material = new Material(Shader.Find("Unlit/Color"));
            lr.material.color = outlineColor;
            lr.startWidth = outlineWidth;
            lr.endWidth = outlineWidth;
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // 設置線段的兩個端點
            Vector3 start = corners[edges[i, 0]];
            Vector3 end = corners[edges[i, 1]];

            // 稍微擴大邊框
            Vector3 center = bounds.center;
            start = center + (start - center) * (1f + outlineWidth);
            end = center + (end - center) * (1f + outlineWidth);

            lr.SetPosition(0, start);
            lr.SetPosition(1, end);

            outlineRenderers[i] = lr;
            lineObj.layer = 31;
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
        if (outlineRenderers != null)
        {
            foreach (LineRenderer lr in outlineRenderers)
            {
                if (lr && lr.material)
                    lr.material.color = color;
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
    }
}