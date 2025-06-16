using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DeviceType
{
    Phone,
    Tablet
}

public class DeviceDetector : MonoBehaviour
{
    public static DeviceDetector Instance;

    [Header("Device Detection Settings")]
    public float tabletMinDiagonal = 7.0f;
    public float tabletMaxAspectRatio = 1.7f;

    private DeviceType currentDeviceType;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            DetectDeviceType();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void DetectDeviceType()
    {
        bool isTabletBySize = IsTabletBySize();
        bool isTabletByRatio = IsTabletByAspectRatio();

        if ((isTabletBySize && isTabletByRatio) ||
            (GetScreenDiagonalInches() >= 9.0f))
        {
            currentDeviceType = DeviceType.Tablet;
        }
        else
        {
            currentDeviceType = DeviceType.Phone;
        }
    }

    bool IsTabletBySize()
    {
        return GetScreenDiagonalInches() >= tabletMinDiagonal;
    }

    bool IsTabletByAspectRatio()
    {
        float aspectRatio = (float)Screen.width / Screen.height;
        return aspectRatio < tabletMaxAspectRatio;
    }

    float GetScreenDiagonalInches()
    {
        float dpi = Screen.dpi > 0 ? Screen.dpi : 160;
        float diagonal = Mathf.Sqrt(Screen.width * Screen.width + Screen.height * Screen.height);
        return diagonal / dpi;
    }

    public DeviceType GetCurrentDeviceType()
    {
        return currentDeviceType;
    }

    public bool IsTablet()
    {
        return currentDeviceType == DeviceType.Tablet;
    }

    public bool IsPhone()
    {
        return currentDeviceType == DeviceType.Phone;
    }
}
