using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("UIWindow")]
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    public GameObject BackButton;

    [Header("MainPanel")]
    public GameObject UIHome;
    public GameObject SculptPanel1;
    public GameObject SculptPanel2;

    [Header("SculptPanel2Content")]
    public GameObject ScalePage;
    public GameObject RotationPage;
    public GameObject OtherPage;
    public Button ScalePageButton;
    public Button RotationPageButton;
    public Button OtherPageButton;

    [Header("SculptFunction")]
    public SculptFunction sculptFunction;

    private bool UI_on = false;

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
        SetUIVisibility(false);

        SetPanelActive(UIHome, true);
        SetPanelActive(SculptPanel1, false);
        SetPanelActive(SculptPanel2, false);
        SetPanelActive(BackButton, false);

        sculptFunction?.UpdateAllUIValues();
    }

    void SetupAllButtonEvents()
    {
        ScalePageButton?.onClick.AddListener(() => ScalePageSelect());
        RotationPageButton?.onClick.AddListener(() => RotationPageSelect());
        OtherPageButton?.onClick.AddListener(() => OtherPageSelect());
    }

    public void SculptButton()
    {
        SwitchToPanel(SculptPanel1);
        SetPanelActive(BackButton, true);
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
        SwitchToPanel(UIHome);
        SetPanelActive(BackButton, false);
    }

    public void SelectObjectForEditing(GameObject selectedObject)
    {
        if (sculptFunction != null)
        {
            sculptFunction.SelectObject(selectedObject);
        }
    }

    #region AuxiliaryFunction

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

    private void SwitchToPanel(GameObject targetPanel)
    {
        SetPanelActive(UIHome, targetPanel == UIHome);
        SetPanelActive(SculptPanel1, targetPanel == SculptPanel1);
        SetPanelActive(SculptPanel2, targetPanel == SculptPanel2);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel != null)
        {
            panel.SetActive(active);
        }
    }

    #endregion
}