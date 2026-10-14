using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class SettingUIManager : MonoBehaviour
{
    public static SettingUIManager Instance { get; private set; }

    [Serializable]
    public struct MenuEntry
    {
        public string title;
        public Button button;
        public GameObject panel;
        [Tooltip("개발 빌드/에디터 전용")]
        public bool debugOnly;
    }

    [Header("메인 요소")]
    [SerializeField] private GameObject settingPanel;

    [Header("인디케이터")]
    [SerializeField] private RectTransform arrow;
    [SerializeField] private float spacing = 20f;
    [SerializeField] private float moveSpeed = 20f;

    [Header("메뉴 항목")]
    [SerializeField] private List<MenuEntry> menuList = new List<MenuEntry>();

    private readonly Vector3[] corners = new Vector3[4];
    private int activeEntryIndex = -1;
    private GameObject lastSelected;
    private GameObject lastMenuButton;
    private Coroutine arrowCoroutine;

    public bool IsOpen
    {
        get { return settingPanel != null && settingPanel.activeSelf; }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(this);
            return;
        }

        if (settingPanel == null)
        {
            Transform panelTransform = transform.Find("Setting Panel");
            if (panelTransform != null)
            {
                settingPanel = panelTransform.gameObject;
            }
        }

        for (int i = 0; i < menuList.Count; i++)
        {
            Button btn = menuList[i].button;
            if (btn != null)
            {
                BindButtonEvents(btn, i);
            }
        }

        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
    }

    private void Start()
    {
        Warmup();
        ApplyDebugVisibility();

        SettingManager manager = SettingManager.Instance;
        if (manager == null || settingPanel == null) return;

        SettingSection[] sections = settingPanel.GetComponentsInChildren<SettingSection>(true);
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i].Init(manager);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        StopArrow();
    }

    public void Open()
    {
        gameObject.SetActive(true);
        if (settingPanel != null) settingPanel.SetActive(true);

        StopArrow();

        if (menuList.Count > 0 && menuList[0].button != null)
        {
            GameObject firstButton = menuList[0].button.gameObject;
            lastSelected = firstButton;
            lastMenuButton = firstButton;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(firstButton);
            }

            StartCoroutine(CoInitArrowPosition(firstButton));
            OpenSubPanel(0);
        }
    }

    public void Close()
    {
        HideAllSubPanels();
        if (settingPanel == null) return;

        settingPanel.SetActive(false);

        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.SaveImmediate();
        }
    }

    public bool OnBackPressed()
    {
        if (activeEntryIndex < 0) return false;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCancel();
        }

        CloseAllSubPanels();
        return true;
    }

    public void OpenSubPanel(int index)
    {
        SetActiveSubPanel(index);

        GameObject targetPanel = menuList[index].panel;
        if (targetPanel != null)
        {
            Selectable firstSelectable = targetPanel.GetComponentInChildren<Selectable>();
            if (firstSelectable != null)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
                }
                lastSelected = firstSelectable.gameObject;
            }
        }
    }

    public void CloseAllSubPanels()
    {
        HideAllSubPanels();

        if (lastMenuButton != null && lastMenuButton.activeInHierarchy)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(lastMenuButton);
            }
            lastSelected = lastMenuButton;
            MoveArrowTo(lastMenuButton);
        }
    }

    private void Warmup()
    {
        if (settingPanel == null) return;

        CanvasGroup cg = settingPanel.GetComponent<CanvasGroup>();
        bool addedCg = false;
        if (cg == null)
        {
            cg = settingPanel.AddComponent<CanvasGroup>();
            addedCg = true;
        }

        float prevAlpha = cg.alpha;
        bool prevInteractable = cg.interactable;
        bool prevBlocksRaycasts = cg.blocksRaycasts;

        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        settingPanel.SetActive(true);
        for (int i = 0; i < menuList.Count; i++)
        {
            if (menuList[i].panel != null)
            {
                menuList[i].panel.SetActive(true);
            }
        }

        Canvas.ForceUpdateCanvases();
        LayoutGroup[] layouts = settingPanel.GetComponentsInChildren<LayoutGroup>(true);
        for (int i = 0; i < layouts.Length; i++)
        {
            if (layouts[i].gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layouts[i].transform);
            }
        }
        Canvas.ForceUpdateCanvases();

        HideAllSubPanels();
        settingPanel.SetActive(false);

        cg.alpha = prevAlpha;
        cg.interactable = prevInteractable;
        cg.blocksRaycasts = prevBlocksRaycasts;

        if (addedCg)
        {
            Destroy(cg);
        }
    }

    private void ApplyDebugVisibility()
    {
        bool isDebug = Debug.isDebugBuild || Application.isEditor;
        if (isDebug) return;

        for (int i = 0; i < menuList.Count; i++)
        {
            MenuEntry entry = menuList[i];
            if (!entry.debugOnly) continue;

            if (entry.button != null) entry.button.gameObject.SetActive(false);
            if (entry.panel != null) entry.panel.SetActive(false);
        }
    }

    private void SetActiveSubPanel(int index)
    {
        activeEntryIndex = index;
        for (int i = 0; i < menuList.Count; i++)
        {
            if (menuList[i].panel != null)
            {
                menuList[i].panel.SetActive(i == index);
            }
        }
    }

    private void HideAllSubPanels()
    {
        SetActiveSubPanel(-1);
    }

    private void BindButtonEvents(Button button, int index)
    {
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => OnEntryClicked(index));

        EventTrigger trigger = button.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = button.gameObject.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear();

        EventTrigger.Entry enterEntry = new EventTrigger.Entry();
        enterEntry.eventID = EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener(_ => SelectMenu(index));
        trigger.triggers.Add(enterEntry);

        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback.AddListener(_ => OnEntryClicked(index));
        trigger.triggers.Add(clickEntry);
    }

    private void SelectMenu(int index)
    {
        if (index < 0 || index >= menuList.Count) return;
        GameObject buttonGo = menuList[index].button != null ? menuList[index].button.gameObject : null;
        if (buttonGo == null || lastSelected == buttonGo) return;

        AudioManager.Instance?.PlayNavigate();

        lastSelected = buttonGo;
        lastMenuButton = buttonGo;
        MoveArrowTo(buttonGo);
    }

    private void OnEntryClicked(int index)
    {
        if (index < 0 || index >= menuList.Count) return;

        MenuEntry entry = menuList[index];
        if (entry.panel == null)
        {
            AudioManager.Instance?.PlaySubmit();
            Close();
            return;
        }

        if (activeEntryIndex == index)
        {
            return;
        }

        AudioManager.Instance?.PlaySubmit();

        if (entry.button != null)
        {
            lastMenuButton = entry.button.gameObject;
            MoveArrowTo(lastMenuButton);
        }

        OpenSubPanel(index);
    }

    private IEnumerator CoInitArrowPosition(GameObject target)
    {
        yield return null;
        if (arrow != null && target != null)
        {
            arrow.localPosition = GetArrowPosition(target);
        }
    }

    private void StopArrow()
    {
        if (arrowCoroutine != null)
        {
            StopCoroutine(arrowCoroutine);
            arrowCoroutine = null;
        }
    }

    private void MoveArrowTo(GameObject target)
    {
        if (arrow == null || target == null) return;

        StopArrow();
        arrowCoroutine = StartCoroutine(MoveArrow(GetArrowPosition(target)));
    }

    private IEnumerator MoveArrow(Vector3 targetLocalPos)
    {
        while (arrow != null && Vector3.Distance(arrow.localPosition, targetLocalPos) > 0.1f)
        {
            arrow.localPosition = Vector3.Lerp(arrow.localPosition, targetLocalPos, Time.unscaledDeltaTime * moveSpeed);
            yield return null;
        }

        if (arrow != null)
        {
            arrow.localPosition = targetLocalPos;
        }

        arrowCoroutine = null;
    }

    private Vector3 GetArrowPosition(GameObject target)
    {
        if (target == null || arrow == null) return Vector3.zero;

        RectTransform targetRect = target.GetComponent<RectTransform>();
        if (targetRect == null) return arrow.localPosition;

        targetRect.GetWorldCorners(corners);
        Vector3 leftCenterWorld = (corners[0] + corners[1]) * 0.5f;

        float arrowWidth = arrow.rect.width * arrow.lossyScale.x;
        float arrowHeight = arrow.rect.height * arrow.lossyScale.y;

        float pivotToRightEdge = (1f - arrow.pivot.x) * arrowWidth;
        float pivotToCenterY = (arrow.pivot.y - 0.5f) * arrowHeight;

        Vector3 finalWorldPos = leftCenterWorld
            - targetRect.right * (spacing + pivotToRightEdge)
            + targetRect.up * pivotToCenterY;

        return arrow.parent.InverseTransformPoint(finalWorldPos);
    }
}
