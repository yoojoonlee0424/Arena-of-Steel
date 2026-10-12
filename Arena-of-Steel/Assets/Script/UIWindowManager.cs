using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class UIWindowManager : MonoBehaviour
{
    public static UIWindowManager Instance { get; private set; }

    [Header("설정 UI")]
    [SerializeField] private SettingUIManager settingUI;
    [SerializeField] private Button settingButton;

    public bool IsOpen => ActiveSettingUI != null && ActiveSettingUI.IsOpen;
    private SettingUIManager ActiveSettingUI => SettingUIManager.Instance != null ? SettingUIManager.Instance : settingUI;

    private InputSystem_Actions inputActions;

    private int lastEscapeFrame = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(this); return; }

        if (settingUI == null) settingUI = FindFirstObjectByType<SettingUIManager>(FindObjectsInactive.Include);

        inputActions = new InputSystem_Actions();
    }

    private void Start()
    {
        if (settingButton != null)
        {
            settingButton.onClick.AddListener(ToggleSettings);
        }
    }

    public void ToggleSettings()
    {
        if (IsOpen)
        {
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }
    
    private void OnEnable()
    {
        inputActions.UI.Enable();
        inputActions.UI.Previous.performed += OnEscapePerformed;
    }

    private void OnDisable()
    {
        inputActions.UI.Previous.performed -= OnEscapePerformed;
        inputActions.UI.Disable();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void OnEscapePerformed(InputAction.CallbackContext _) => HandleEscape();

    public void HandleEscape()
    {
        if (Time.frameCount == lastEscapeFrame) return;
        lastEscapeFrame = Time.frameCount;
        if (IsOpen)
        {
            if (ActiveSettingUI != null && ActiveSettingUI.OnBackPressed()) return;
            CloseSettings();
        }
        else
        {
            OpenSettings();
        }
    }

    public void OpenSettings()
    {
        if (ActiveSettingUI != null) ActiveSettingUI.Open();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetPlayerControl(false);
    }

    public void CloseSettings()
    {
        if (ActiveSettingUI != null) ActiveSettingUI.Close();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetPlayerControl(true);
    }

    public void CloseAllTabs(bool animatePanel = false) => CloseSettings();

    private void SetPlayerControl(bool active)
    {
        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputActive(active);

        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.SetInputActive(active);
    }
}
