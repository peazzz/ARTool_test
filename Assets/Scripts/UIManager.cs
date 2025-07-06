using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Niantic.Lightship.AR.NavigationMesh;

public class UIManager : MonoBehaviour
{
    public RectTransform FounctionUI_RT, HandleArrow_RT;
    public GameObject BackButton, FounctionUI, ClearModeButton, ClearModeObject, ClearModeHint, NewSculptButton, SculptModeHint, LoadButton, MaterialButton;
    public GameObject UIHome, NewSculptPanel, SculptPanel1, SculptPanel2, DrawPanel1, DrawPanel2, BrushPanel, BrushPanel2D, MaterialPanel;
    public GameObject ScalePage, RotationPage, ColorPage1;
    public Button ScalePageButton, RotationPageButton;
    public GameObject ColorPageButtonForSculpt, GroundCheck, AE, SP, RP;
    public GameObject BasicEditPage, BasicEditPage2D, ColorPage2, ColorPage3, ColorPageButtonForDraw, ColorPageButtonForDraw2D_Phone, ColorPageButtonForDraw2D_Tablet, MaterialColor, MaterialPenColor;
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
    public bool inSculptMode = false;
    public GameObject SelectObjectHint;

    public GameObject ColorPicker;

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

        ColorImageChange();
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
        ColorPageButtonForSculpt.GetComponent<Button>().onClick.AddListener(() => ColorPageSelectForSculpt());
        ColorPageButtonForDraw.GetComponent<Button>().onClick.AddListener(() => ColorPageSelectForDraw());
        GroundCheck.GetComponent<Button>().onClick.AddListener(() => GroundCheckFunction());
        ClearModeButton.GetComponent<Button>().onClick.AddListener(() => ClearModeSwitch());
        NewSculptButton.GetComponent<Button>().onClick.AddListener(() => NewSculpt());
        MaterialButton.GetComponent<Button>().onClick.AddListener(() => MaterialButtonSelect());
    }

    public void NewSculpt()
    {
        inSculptMode = true;
        
        SwitchToPanel(NewSculptPanel);
        SculptModeHint.SetActive(true);
        SetPanelActive(BackButton, true);
        ClearModeButton.SetActive(false);
      
        if (sculptFunction)
        {
            sculptFunction.EnterSculptMode();
        }
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

    public void MaterialButtonSelect()
    {
        drawFunction.TextureMode = true;
        FounctionUI.SetActive(false);
        SelectObjectHint.SetActive(true);

        SwitchToPanel(MaterialPanel);
        SetPanelActive(BackButton, true);
    }

    public void FunctionUISwitch()
    {
        UI_on = !UI_on;
        SetUIVisibility(UI_on);
    }

    public void ScalePageSelect()
    {
        ScalePage.SetActive(true); RotationPage.SetActive(false);
        ScalePageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
    }

    void RotationPageSelect()
    {
        ScalePage.SetActive(false); RotationPage.SetActive(true);
        ScalePageButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
        RotationPageButton.GetComponent<Image>().color = new Color(143f / 255f, 255f / 255f, 196f / 255f);
    }

    public void ColorPageSelectForSculpt()
    {
        isInColorPage = true;
        ColorPage1.SetActive(true);
        sculptFunction.GenerateButtonObj.SetActive(false);
        if (inSculpt)
        {
            AE.SetActive(false); SP.SetActive(false); RP.SetActive(false);
            if (sculptFunction) StartCoroutine(DelayedColorSync());
        }
    }

    public void ColorPageSelectForDraw()
    {
        //isInColorPage = true;
        //ColorPage2.SetActive(true); BasicEditPage.SetActive(false);
        //drawFunction.FinishButtonObj.SetActive(false);

        ColorPicker.SetActive(true);
    }

    void ColorImageChange()
    {
        ColorPageButtonForDraw.GetComponent<Image>().color = drawFunction.fcp.color;
        ColorPageButtonForDraw2D_Phone.GetComponent<Image>().color = canvas2DManager.fcp.color;
        ColorPageButtonForDraw2D_Tablet.GetComponent<Image>().color = canvas2DManager.fcp_Tablet.color;
        MaterialPenColor.GetComponent<Image>().color = sculptFunction.fcp_pen.color;

        MaterialColor.GetComponent<Image>().color = sculptFunction.fcp.color;
    }

    public void ColorPageSelectForDraw2D()
    {
        //isInColorPage = true;
        //ColorPage3.SetActive(true); BasicEditPage2D.SetActive(false);

        ColorPicker.SetActive(true);
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
        AE.SetActive(true); SP.SetActive(true); RP.SetActive(true);
        ColorPage1.SetActive(false);
        isInColorPage = false;


        if (sculptFunction) sculptFunction.SyncCurrentModelColorToUI();
    }

    public void Back()
    {
        if (inSculptMode)
        {
            inSculptMode = false;
            if (sculptFunction)
            {
                sculptFunction.ExitSculptMode();
            }
            SwitchToPanel(UIHome);
            SetPanelActive(BackButton, false);
            ClearModeButton.SetActive(true);
            SculptModeHint.SetActive(false);
        }
        else if (inSculpt)
        {
            if (!sculptFunction.isEditingExistingObject)
            {
                if (isInColorPage)
                {
                    ColorPageButtonForSculpt.GetComponent<Image>().color = sculptFunction.fcp.color;
                    BackToPanel2();
                    sculptFunction.GenerateButtonObj.SetActive(true);
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
                if (isInColorPage)
                {
                    BackToPanel2();
                    sculptFunction.GenerateButtonObj.SetActive(true);
                }
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
                    drawFunction.FinishButtonObj.SetActive(true);
                }
                else
                {
                    drawFunction.ClearAll.SetActive(false);
                    SetPanelActive(BackButton, false); ClearModeButton.SetActive(true);
                    SwitchToPanel(UIHome); drawFunction.DrawPanel.SetActive(false);
                    drawFunction.TwoPointActionButton.GetComponent<Image>().color = new Color(128f / 255f, 128f / 255f, 128f / 255f);
                    drawFunction.waitingForSecondPoint = false;
                    if (drawFunction.tempLineRenderer) { Destroy(drawFunction.tempLineRenderer.gameObject); drawFunction.tempLineRenderer = null; }
                    inDraw = false;
                }
            }
        }
        else if(drawFunction.TextureMode && sculptFunction.currentSelectedObject != null)
        {
            SwitchToPanel(UIHome);
            SetPanelActive(BackButton, false);
            FounctionUI.SetActive(true);
            SelectObjectHint.SetActive(false);
            sculptFunction.SetObjectGlow(sculptFunction.currentSelectedObject, false);
            sculptFunction.currentSelectedObject = null;
            drawFunction.TextureMode = false;
        }
    }

    public void Canvas2DPainting_Exit()
    {
        drawFunction.Warning.SetActive(true); drawFunction.WarningText_2DPaint.SetActive(true);
        drawFunction.LeaveButton.SetActive(true); drawFunction.CancelButton.SetActive(true);
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
        SetPanelActive(NewSculptPanel, targetPanel == NewSculptPanel);
        SetPanelActive(SculptPanel1, targetPanel == SculptPanel1);
        SetPanelActive(SculptPanel2, targetPanel == SculptPanel2);
        SetPanelActive(DrawPanel1, targetPanel == DrawPanel1);
        SetPanelActive(DrawPanel2, targetPanel == DrawPanel2);
        SetPanelActive(BrushPanel, targetPanel == BrushPanel);
        SetPanelActive(BrushPanel2D, targetPanel == BrushPanel2D);
        SetPanelActive(MaterialPanel, targetPanel == MaterialPanel);
    }

    private void SetPanelActive(GameObject panel, bool active)
    {
        if (panel) panel.SetActive(active);
    }
}