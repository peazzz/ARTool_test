using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("功能選單")]
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    public GameObject BackButton;

    [Header("主要面板")]
    public GameObject UIHome;          // 主頁面板
    public GameObject SculptPanel1;    // 形狀選擇面板
    public GameObject SculptPanel2;    // 參數調整面板

    [Header("SculptPanel2 子頁面")]
    public GameObject ScalePage;
    public GameObject RotationPage;
    public GameObject OtherPage;
    public Button ScalePageButton;
    public Button RotationPageButton;
    public Button OtherPageButton;

    [Header("功能管理器")]
    public SculptFunction sculptFunction;

    private bool UI_on = false;

    // UI 位置常數
    private readonly Vector3 UI_SHOW_POSITION = Vector3.zero;
    private readonly Vector3 UI_HIDE_POSITION = new Vector3(0, -400, 0);
    private readonly Vector3 ARROW_NORMAL_SCALE = new Vector3(0.6f, 0.6f, 0.6f);
    private readonly Vector3 ARROW_FLIPPED_SCALE = new Vector3(0.6f, -0.6f, 0.6f);

    void Start()
    {
        InitializeUI();
        SetupAllButtonEvents();
    }

    void InitializeUI()
    {
        // 初始化 UI 狀態
        SetUIVisibility(false);

        // 設定初始面板狀態
        SetPanelActive(UIHome, true);
        SetPanelActive(SculptPanel1, false);
        SetPanelActive(SculptPanel2, false);
        SetPanelActive(BackButton, false);

        // 初始化參數值
        sculptFunction?.UpdateAllUIValues();
    }

    void SetupAllButtonEvents()
    {
        // 形狀選擇按鈕
        ScalePageButton?.onClick.AddListener(() => ScalePageSelect());
        RotationPageButton?.onClick.AddListener(() => RotationPageSelect());
        OtherPageButton?.onClick.AddListener(() => OtherPageSelect());
    }

    public void SculptButton()
    {
        // 切換到形狀選擇面板
        SwitchToPanel(SculptPanel1);
        SetPanelActive(BackButton, true);
        Debug.Log("進入形狀選擇面板");
    }

    public void FunctionUISwitch()
    {
        UI_on = !UI_on;
        SetUIVisibility(UI_on);
    }

    void ScalePageSelect()
    {
        ScalePage.SetActive(true);
        RotationPage.SetActive(false);
        OtherPage.SetActive(false);

        ScalePageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    void RotationPageSelect()
    {
        ScalePage.SetActive(false);
        RotationPage.SetActive(true);
        OtherPage.SetActive(false);

        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    void OtherPageSelect()
    {
        ScalePage.SetActive(false);
        RotationPage.SetActive(false);
        OtherPage.SetActive(true);

        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    public void Back()
    {
        // 返回主頁
        SwitchToPanel(UIHome);
        SetPanelActive(BackButton, false);
    }

    /// <summary>
    /// 外部調用：選擇物件進入編輯模式
    /// </summary>
    /// <param name="selectedObject">要編輯的物件</param>
    public void SelectObjectForEditing(GameObject selectedObject)
    {
        if (sculptFunction != null)
        {
            sculptFunction.SelectObject(selectedObject);
        }
    }

    #region 私有輔助方法

    /// <summary>
    /// 設定 UI 選單的顯示狀態
    /// </summary>
    /// <param name="show">是否顯示</param>
    private void SetUIVisibility(bool show)
    {
        if (FounctionUI_RT != null)
        {
            FounctionUI_RT.anchoredPosition = show ? UI_SHOW_POSITION : UI_HIDE_POSITION;
        }

        if (HandleArrow_RT != null)
        {
            HandleArrow_RT.localScale = show ? ARROW_FLIPPED_SCALE : ARROW_NORMAL_SCALE;
        }
    }

    /// <summary>
    /// 切換到指定面板，同時隱藏其他面板
    /// </summary>
    /// <param name="targetPanel">目標面板</param>
    private void SwitchToPanel(GameObject targetPanel)
    {
        SetPanelActive(UIHome, targetPanel == UIHome);
        SetPanelActive(SculptPanel1, targetPanel == SculptPanel1);
        SetPanelActive(SculptPanel2, targetPanel == SculptPanel2);
    }

    /// <summary>
    /// 設定面板的啟用狀態（包含 null 檢查）
    /// </summary>
    /// <param name="panel">面板物件</param>
    /// <param name="active">是否啟用</param>
    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    #endregion
}