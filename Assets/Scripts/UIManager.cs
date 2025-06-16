using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.NavigationMesh;

public class UIManager : MonoBehaviour
{
    public RectTransform FounctionUI_RT, HandleArrow_RT;
    public GameObject BackButton, FounctionUI, ClearModeButton, ClearModeObject, ClearModeHint, LoadButton;
    public GameObject UIHome, SculptPanel1, SculptPanel2, DrawPanel1, DrawPanel2, BrushPanel, BrushPanel2D;
    public GameObject ScalePage, RotationPage, OtherPage, ColorPage1;
    public Button ScalePageButton, RotationPageButton, OtherPageButton;
    public GameObject ColorPageButtonForSculpt, GroundCheck, AE, SP, RP, OP;
    public GameObject BasicEditPage, BasicEditPage2D, ColorPage2, ColorPage3, ColorPageButtonForDraw, ColorPageButtonForDraw2D;
    public SculptFunction sculptFunction;
    public DrawFunction drawFunction;
    public Canvas2DManager canvas2DManager;
    public LightshipNavMeshRenderer lightshipNavMeshRenderer;
    public bool UI_on = false, inSculpt, inDraw, isInColorPage = false, isGroundChecking = true;
    private bool ClearMode;
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
        if (ColorPage1?.activeInHierarchy == true)
            ColorPageButtonForSculpt.GetComponent<Image>().color = sculptFunction.fcp.color;
        if (ColorPage2?.activeInHierarchy == true)
            ColorPageButtonForDraw.GetComponent<Image>().color = drawFunction.fcp.color;
        if (ColorPage3?.activeInHierarchy == true)
            ColorPageButtonForDraw2D.GetComponent<Image>().color = drawFunction.fcp.color;
    }

    void InitializeUI()
    {
        SetUIVisibility(false);
        SetPanelActive(UIHome, true);
        SetPanelActive(SculptPanel1, false);
        SetPanelActive(SculptPanel2, false);
        SetPanelActive(BackButton, false);
        sculptFunction?.UpdateAllUIValues();
        ClearModeButton.SetActive(true);
    }

    void SetupAllButtonEvents()
    {
        ScalePageButton?.onClick.AddListener(() => ScalePageSelect());
        RotationPageButton?.onClick.AddListener(() => RotationPageSelect());
        OtherPageButton?.onClick.AddListener(() => OtherPageSelect());
        ColorPageButtonForSculpt.GetComponent<Button>().onClick.AddListener(() => ColorPageSelectForSculpt());
        ColorPageButtonForDraw.GetComponent<Button>().onClick.AddListener(() => ColorPageSelectForDraw());
        ColorPageButtonForDraw2D.GetComponent<Button>().onClick.AddListener(() => ColorPageSelectForDraw2D());
        GroundCheck.GetComponent<Button>().onClick.AddListener(() => GroundCheckFunction());
        ClearModeButton.GetComponent<Button>().onClick.AddListener(() => ClearModeSwitch());
    }

    public void ClearModeSwitch()
    {
        ClearMode = !ClearMode;
        if (ClearMode)
        {
            FounctionUI.SetActive(false);
            ClearModeButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
            ClearModeObject.SetActive(true);
            ClearModeHint.SetActive(true);
        }
        else
        {
            FounctionUI.SetActive(true);
            ClearModeButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
            ClearModeObject.SetActive(false);
            ClearModeHint.SetActive(false);
        }
    }

    public void SculptButton()
    {
        inSculpt = true;
        SwitchToPanel(SculptPanel1);
        SetPanelActive(BackButton, true);
        ClearModeButton.SetActive(false);
    }

    public void DrawButton()
    {
        inDraw = true;
        SwitchToPanel(DrawPanel1);
        SetPanelActive(BackButton, true);
        ClearModeButton.SetActive(false);
    }

    public void FunctionUISwitch()
    {
        UI_on = !UI_on;
        SetUIVisibility(UI_on);
    }

    public void ScalePageSelect()
    {
        ScalePage.SetActive(true); RotationPage.SetActive(false); OtherPage.SetActive(false);
        ScalePageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    void RotationPageSelect()
    {
        ScalePage.SetActive(false); RotationPage.SetActive(true); OtherPage.SetActive(false);
        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    public void OtherPageSelect()
    {
        ScalePage.SetActive(false); RotationPage.SetActive(false); OtherPage.SetActive(true);
        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        OtherPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    public void ColorPageSelectForSculpt()
    {
        isInColorPage = true;
        ColorPage1.SetActive(true); OtherPage.SetActive(false);
        if (inSculpt)
        {
            AE.SetActive(false); SP.SetActive(false); RP.SetActive(false); OP.SetActive(false);
            if (sculptFunction) StartCoroutine(DelayedColorSync());
        }
    }

    public void ColorPageSelectForDraw()
    {
        isInColorPage = true;
        ColorPage2.SetActive(true); BasicEditPage.SetActive(false);
    }

    public void ColorPageSelectForDraw2D()
    {
        isInColorPage = true;
        ColorPage3.SetActive(true); BasicEditPage2D.SetActive(false);
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

    public void BackToPanel2()
    {
        AE.SetActive(true); SP.SetActive(true); RP.SetActive(true); OP.SetActive(true);
        ColorPage1.SetActive(false);
        isInColorPage = false;

        OtherPageSelect();

        if (sculptFunction) sculptFunction.SyncCurrentModelColorToUI();
    }

    public void Back()
    {
        if (inSculpt)
        {
            if (!sculptFunction.isEditingExistingObject)
            {
                if (isInColorPage)
                {
                    ColorPageButtonForSculpt.GetComponent<Image>().color = sculptFunction.fcp.color;
                    BackToPanel2();
                }
                else
                {
                    SwitchToPanel(UIHome); SetPanelActive(BackButton, false);
                    sculptFunction.OnBackButtonClicked(); inSculpt = false;
                    lightshipNavMeshRenderer.enabled = false; ClearModeButton.SetActive(true);
                }
            }
            else
            {
                if (isInColorPage) BackToPanel2();
                else
                {
                    sculptFunction.CancelEditChanges();
                    SwitchToPanel(UIHome); SetPanelActive(BackButton, false);
                    inSculpt = false; lightshipNavMeshRenderer.enabled = false; ClearModeButton.SetActive(true);
                }
            }
        }
        else if (inDraw)
        {
            if (DrawPanel1.activeSelf)
            {
                SwitchToPanel(UIHome); SetPanelActive(BackButton, false); ClearModeButton.SetActive(true);
            }
            else if (BrushPanel.activeSelf)
            {
                if (isInColorPage)
                {
                    ColorPageButtonForDraw.GetComponent<Image>().color = drawFunction.fcp.color;
                    BasicEditPage.SetActive(true); ColorPage2.SetActive(false); isInColorPage = false;
                }
                else
                {
                    SwitchToPanel(DrawPanel1); drawFunction.DrawPanel.SetActive(false);
                    drawFunction.TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
                    drawFunction.waitingForSecondPoint = false;
                    if (drawFunction.tempLineRenderer) { Destroy(drawFunction.tempLineRenderer.gameObject); drawFunction.tempLineRenderer = null; }
                }
            }
            else if (canvas2DManager.Canvas2D.activeSelf)
            {
                drawFunction.Warning.SetActive(true); drawFunction.WarningText_2DPaint.SetActive(true);
                drawFunction.LeaveButton.SetActive(true); drawFunction.CancelButton.SetActive(true);
            }
            else if (canvas2DManager.Canvas2D_Tablet.activeSelf)
            {
                drawFunction.Warning.SetActive(true); drawFunction.WarningText_2DPaint.SetActive(true);
                drawFunction.LeaveButton.SetActive(true); drawFunction.CancelButton.SetActive(true);
            }
        }
    }

    public void SelectObjectForEditing(GameObject selectedObject)
    {
        if (sculptFunction) sculptFunction.SelectObject(selectedObject);
    }

    public void SetUIVisibility(bool show)
    {
        if (FounctionUI_RT) FounctionUI_RT.anchoredPosition = show ? UI_SHOW_POSITION : UI_HIDE_POSITION;
        if (HandleArrow_RT) HandleArrow_RT.localScale = show ? ARROW_FLIPPED_SCALE : ARROW_NORMAL_SCALE;
    }

    public void SwitchToPanel(GameObject targetPanel)
    {
        SetPanelActive(UIHome, targetPanel == UIHome);
        SetPanelActive(SculptPanel1, targetPanel == SculptPanel1);
        SetPanelActive(SculptPanel2, targetPanel == SculptPanel2);
        SetPanelActive(DrawPanel1, targetPanel == DrawPanel1);
        SetPanelActive(DrawPanel2, targetPanel == DrawPanel2);
        SetPanelActive(BrushPanel, targetPanel == BrushPanel);
        SetPanelActive(BrushPanel2D, targetPanel == BrushPanel2D);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel) panel.SetActive(active);
    }
}