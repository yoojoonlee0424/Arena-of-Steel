using UnityEngine;
using UnityEngine.UI;

public class ControlSettingSection : SettingSection
{
    [SerializeField] private SettingSliderControl sensControl;
    [SerializeField] private Toggle invertYToggle;
    [SerializeField] private Button resetBindingsButton;

    private SettingKeyRebindSlot[] keySlots;
    private InputSystem_Actions inputActions;
    private SettingManager settingManager;

    public override void Init(SettingManager manager)
    {
        settingManager = manager;
        var settings = manager.CurrentSettings;

        if (sensControl != null)
        {
            sensControl.Init(settings.mouseSensitivity);
            sensControl.onValueChanged += manager.SetSens;
        }

        if (invertYToggle != null)
        {
            invertYToggle.SetIsOnWithoutNotify(settings.invertY);
            invertYToggle.onValueChanged.AddListener(manager.SetInvertY);
        }

        if (inputActions == null)
        {
            inputActions = new InputSystem_Actions();
        }

        manager.ApplyBindings(inputActions.asset);

        keySlots = GetComponentsInChildren<SettingKeyRebindSlot>(true);
        for (int i = 0; i < keySlots.Length; i++)
        {
            keySlots[i].Init(inputActions.asset);
        }

        manager.OnBindingsChanged -= RefreshKeySlots;
        manager.OnBindingsChanged += RefreshKeySlots;

        if (resetBindingsButton != null)
        {
            resetBindingsButton.onClick.RemoveListener(OnResetBindingsClicked);
            resetBindingsButton.onClick.AddListener(OnResetBindingsClicked);
        }
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        if (settingManager != null)
        {
            settingManager.OnBindingsChanged -= RefreshKeySlots;
        }

        if (resetBindingsButton != null)
        {
            resetBindingsButton.onClick.RemoveListener(OnResetBindingsClicked);
        }

        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void OnResetBindingsClicked()
    {
        if (settingManager != null && inputActions != null)
        {
            settingManager.ResetBindings(inputActions.asset);
        }
    }

    private void RefreshKeySlots()
    {
        if (keySlots == null) return;

        for (int i = 0; i < keySlots.Length; i++)
        {
            if (keySlots[i] != null)
            {
                keySlots[i].RefreshDisplay();
            }
        }
    }
}
