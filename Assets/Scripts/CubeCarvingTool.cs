using UnityEngine;

public class CubeCarvingTool : MonoBehaviour
{
    [SerializeField] private Vector3 toolSize = new Vector3(0.2f, 0.1f, 0.2f);
    [SerializeField] private int samplingDensity = 4;
    [SerializeField] private bool showToolBounds = true;
    [SerializeField] private Color toolColor = Color.blue;
    [SerializeField] private bool showCarvingPoints = true;
    [SerializeField] private int carveDepth = 1;

    private Vector3[] carvingPoints;

    void Start()
    {
        GenerateCarvingPoints();
    }

    void GenerateCarvingPoints()
    {
        if (samplingDensity <= 0) samplingDensity = 1;

        // 3D採樣：samplingDensity^3 個點
        int totalPoints = samplingDensity * samplingDensity * samplingDensity;
        carvingPoints = new Vector3[totalPoints];

        Vector3 halfSize = toolSize * 0.5f;
        int index = 0;

        for (int x = 0; x < samplingDensity; x++)
        {
            for (int y = 0; y < samplingDensity; y++)
            {
                for (int z = 0; z < samplingDensity; z++)
                {
                    // 3D均勻分佈計算
                    float normalizedX, normalizedY, normalizedZ;

                    if (samplingDensity == 1)
                    {
                        normalizedX = 0.5f;
                        normalizedY = 0.5f;
                        normalizedZ = 0.5f;
                    }
                    else
                    {
                        // 在每個3D網格單元的中心採樣
                        normalizedX = (x + 0.5f) / samplingDensity;
                        normalizedY = (y + 0.5f) / samplingDensity;
                        normalizedZ = (z + 0.5f) / samplingDensity;
                    }

                    Vector3 localPoint = new Vector3(
                        Mathf.Lerp(-halfSize.x, halfSize.x, normalizedX),
                        Mathf.Lerp(-halfSize.y, halfSize.y, normalizedY),
                        Mathf.Lerp(-halfSize.z, halfSize.z, normalizedZ)
                    );

                    carvingPoints[index] = localPoint;
                    index++;
                }
            }
        }
    }

    public bool IsActive()
    {
        return true;
    }

    public Vector3[] GetCarvingPoints()
    {
        if (carvingPoints == null)
            GenerateCarvingPoints();

        Vector3[] worldPoints = new Vector3[carvingPoints.Length];
        for (int i = 0; i < carvingPoints.Length; i++)
        {
            worldPoints[i] = transform.TransformPoint(carvingPoints[i]);
        }
        return worldPoints;
    }

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, toolSize);
    }

    public void SetToolSize(Vector3 size)
    {
        toolSize = size;
        GenerateCarvingPoints();
    }

    public void SetSamplingDensity(int density)
    {
        samplingDensity = Mathf.Max(1, density); // 修正：允許最小值為1
        GenerateCarvingPoints();
    }

    void OnDrawGizmos()
    {
        if (!showToolBounds) return;

        Gizmos.color = toolColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, toolSize);

        Gizmos.color = new Color(toolColor.r, toolColor.g, toolColor.b, 0.2f);
        Gizmos.DrawCube(Vector3.zero, toolSize);

        Gizmos.matrix = Matrix4x4.identity;

        if (showCarvingPoints && carvingPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 point in carvingPoints)
            {
                Vector3 worldPoint = transform.TransformPoint(point);
                Gizmos.DrawSphere(worldPoint, 0.005f);
            }
        }
    }

    public void SetCarveDepth(int depth)
    {
        carveDepth = Mathf.Clamp(depth, 1, 5);
    }

    public int GetCarveDepth()
    {
        return carveDepth;
    }
}