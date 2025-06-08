using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.NavigationMesh;

public class UIManager : MonoBehaviour
{
    [Header("UIWindow")]
    public RectTransform FounctionUI_RT;
    public RectTransform HandleArrow_RT;
    public GameObject BackButton;
    public GameObject GroundCheck;

    [Header("MainPanel")]
    public GameObject UIHome;
    public GameObject SculptPanel1;
    public GameObject SculptPanel2;

    [Header("SculptPanel2Content")]
    public GameObject ScalePage;
    public GameObject RotationPage;
    public GameObject OtherPage;
    public GameObject ColorPage;

    public Button ScalePageButton;
    public Button RotationPageButton;
    public Button OtherPageButton;
    public GameObject ColorPageButton;

    public GameObject AE, SP, RP, OP;

    [Header("SculptFunction")]
    public SculptFunction sculptFunction;

    [Header("Component")]
    public LightshipNavMeshRenderer lightshipNavMeshRenderer;

    private bool UI_on = false;
    public bool isInColorPage = false;
    public bool isGroundChecking = true;

    private readonly Vector3 UI_SHOW_POSITION = Vector3.zero;
    private readonly Vector3 UI_HIDE_POSITION = new Vector3(0, -400, 0);
    private readonly Vector3 ARROW_NORMAL_SCALE = new Vector3(0.6f, 0.6f, 0.6f);
    private readonly Vector3 ARROW_FLIPPED_SCALE = new Vector3(0.6f, -0.6f, 0.6f);

    void Start()
    {
        InitializeUI();
        SetupAllButtonEvents();
        GroundCheckFunction();
    }

    void Update()
    {
        if (ColorPage != null && ColorPage.activeInHierarchy)
        {
            ColorPageButton.GetComponent<Image>().color = sculptFunction.fcp.color;
        }
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
        ColorPageButton.GetComponent<Button>().onClick.AddListener(() => ColorPageSelect());
        GroundCheck.GetComponent<Button>().onClick.AddListener(() => GroundCheckFunction());
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

    public void OtherPageSelect()
    {
        ScalePage.SetActive(false);
        RotationPage.SetActive(false);
        OtherPage.SetActive(true);

        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    void ColorPageSelect()
    {
        isInColorPage = true;

        ColorPage.SetActive(true);
        OtherPage.SetActive(false);

        AE.SetActive(false);
        SP.SetActive(false);
        RP.SetActive(false);
        OP.SetActive(false);

        if (sculptFunction != null)
        {
            StartCoroutine(DelayedColorSync());
        }
    }

    void GroundCheckFunction()
    {
        isGroundChecking = !isGroundChecking;

        if (isGroundChecking)
        {
            lightshipNavMeshRenderer.enabled = true;
            GroundCheck.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        }
        else
        {
            lightshipNavMeshRenderer.enabled = false;
            GroundCheck.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        }
    }

    private IEnumerator DelayedColorSync()
    {
        yield return new WaitForEndOfFrame();
        sculptFunction.SyncCurrentModelColorToUI();
    }

    void BackToPanel2()
    {   
        AE.SetActive(true);
        SP.SetActive(true);
        RP.SetActive(true);
        OP.SetActive(true);
        ColorPage.SetActive(false);
        OtherPage.SetActive(true);
        isInColorPage = false;

        if (sculptFunction != null)
        {
            sculptFunction.SyncCurrentModelColorToUI();
        }
    }

    public void Back()
    {
        if (isInColorPage)
        {
            BackToPanel2();
        }
        else
        {
            SwitchToPanel(UIHome);
            SetPanelActive(BackButton, false);

            sculptFunction.OnBackButtonClicked();
        }
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

    public void SwitchToPanel(GameObject targetPanel)
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