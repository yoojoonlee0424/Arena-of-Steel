using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    [Header("메인 메뉴 버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private MainMenuSettingsUI settingsUI;

    [Header("씬 선택 패널")]
    [SerializeField] private GameObject sceneSelectPanel;
    [SerializeField] private Transform sceneButtonContainer;
    [SerializeField] private Button sceneButtonPrefab;
    [SerializeField] private Button closeSceneSelectButton;
    [SerializeField] private bool excludeCurrentScene = true;

    private InputSystem_Actions inputActions;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();
        BindMainButtons();
    }

    private void Start()
    {
        UnlockCursor();
        InitSceneButtons();
        if (sceneSelectPanel != null)
        {
            sceneSelectPanel.SetActive(false);
        }
        if (settingsUI != null && settingsUI.IsOpen)
        {
            settingsUI.Close();
        }
    }

    private void OnEnable()
    {
        inputActions.UI.Enable();
        inputActions.UI.Previous.performed += OnBackPressed;
        UnlockCursor();
    }

    private void OnDisable()
    {
        inputActions.UI.Previous.performed -= OnBackPressed;
        inputActions.UI.Disable();
    }

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void BindMainButtons()
    {
        if (startButton != null) startButton.onClick.AddListener(OpenSceneSelect);
        if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
        if (quitButton != null) quitButton.onClick.AddListener(QuitGame);
        if (closeSceneSelectButton != null) closeSceneSelectButton.onClick.AddListener(CloseSceneSelect);
    }

    private void InitSceneButtons()
    {
        if (sceneButtonContainer == null || sceneButtonPrefab == null) return;

        foreach (Transform child in sceneButtonContainer)
        {
            Destroy(child.gameObject);
        }

        int sceneCount = SceneManager.sceneCountInBuildSettings;
        string currentScene = SceneManager.GetActiveScene().name;

        for (int i = 0; i < sceneCount; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            string sceneName = Path.GetFileNameWithoutExtension(path);

            if (string.IsNullOrEmpty(sceneName)) continue;
            if (excludeCurrentScene && sceneName == currentScene) continue;

            Button btn = Instantiate(sceneButtonPrefab, sceneButtonContainer);
            btn.gameObject.SetActive(true);

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = sceneName;

            string targetScene = sceneName;
            btn.onClick.AddListener(() =>
            {
                AudioManager.Instance?.PlaySubmit();
                SceneLoader.Load(targetScene);
            });
        }
    }

    public void OpenSceneSelect()
    {
        AudioManager.Instance?.PlaySubmit();
        if (sceneSelectPanel != null) sceneSelectPanel.SetActive(true);
    }

    public void CloseSceneSelect()
    {
        AudioManager.Instance?.PlayCancel();
        if (sceneSelectPanel != null) sceneSelectPanel.SetActive(false);
    }

    public void OpenSettings()
    {
        AudioManager.Instance?.PlaySubmit();

        if (settingsUI != null)
        {
            settingsUI.Open();
        }
        else if (SettingUIManager.Instance != null)
        {
            SettingUIManager.Instance.Open();
        }
        else if (UIWindowManager.Instance != null)
        {
            UIWindowManager.Instance.OpenSettings();
        }

        UnlockCursor();
    }

    private void OnBackPressed(InputAction.CallbackContext _)
    {
        if (settingsUI != null && settingsUI.IsOpen)
        {
            settingsUI.Close();
            return;
        }

        if (SettingUIManager.Instance != null && SettingUIManager.Instance.IsOpen) return;

        if (sceneSelectPanel != null && sceneSelectPanel.activeSelf)
        {
            CloseSceneSelect();
        }
    }

    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void QuitGame()
    {
        AudioManager.Instance?.PlaySubmit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
