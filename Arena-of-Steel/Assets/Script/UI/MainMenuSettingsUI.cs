using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuSettingsUI : MonoBehaviour
{
    [Serializable]
    public struct SettingTab
    {
        public string tabName;
        public Button tabButton;
        public GameObject panel;
    }

    [SerializeField] private GameObject settingsRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private List<SettingTab> tabs = new();

    private readonly HashSet<GameObject> initializedPanels = new();
    private int currentTabIndex = -1;

    public bool IsOpen => settingsRoot != null && settingsRoot.activeSelf;

    private void Awake()
    {
        if (settingsRoot == null) settingsRoot = gameObject;

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            int index = i;
            if (tabs[i].tabButton != null)
            {
                tabs[i].tabButton.onClick.AddListener(() => SwitchTab(index));
            }
        }
    }

    public void Open()
    {
        if (settingsRoot != null)
        {
            settingsRoot.SetActive(true);
        }

        SwitchTab(0);
    }

    public void Close()
    {
        AudioManager.Instance?.PlayCancel();

        if (settingsRoot != null)
        {
            settingsRoot.SetActive(false);
        }

        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.SaveImmediate();
        }
    }

    public void SwitchTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;

        if (currentTabIndex != index)
        {
            AudioManager.Instance?.PlayNavigate();
        }

        currentTabIndex = index;

        for (int i = 0; i < tabs.Count; i++)
        {
            GameObject panel = tabs[i].panel;
            if (panel == null) continue;

            bool isTarget = i == index;
            panel.SetActive(isTarget);

            if (isTarget && !initializedPanels.Contains(panel))
            {
                InitPanelSections(panel);
                initializedPanels.Add(panel);
            }
        }
    }

    private void InitPanelSections(GameObject panel)
    {
        SettingManager manager = SettingManager.Instance;
        if (manager == null || panel == null) return;

        SettingSection[] sections = panel.GetComponentsInChildren<SettingSection>(true);
        for (int i = 0; i < sections.Length; i++)
        {
            sections[i].Init(manager);
        }
    }
}
