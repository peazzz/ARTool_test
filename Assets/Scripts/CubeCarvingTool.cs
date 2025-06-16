using UnityEngine;

public class CubeCarvingTool : MonoBehaviour
{
    [SerializeField] private Vector3 toolSize = new Vector3(0.2f, 0.1f, 0.2f);
    [SerializeField] private int samplingDensity = 4;

    [SerializeField] private bool showToolBounds = true;
    [SerializeField] private Color toolColor = Color.blue;
    [SerializeField] private bool showCarvingPoints = true;

    private Vector3[] carvingPoints;

    void Start()
    {
        GenerateCarvingPoints();
    }

    void GenerateCarvingPoints()
    {
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
                    float normalizedX = (float)x / (samplingDensity - 1);
                    float normalizedY = (float)y / (samplingDensity - 1);
                    float normalizedZ = (float)z / (samplingDensity - 1);

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
        samplingDensity = Mathf.Max(2, density);
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
}