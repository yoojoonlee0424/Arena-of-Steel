using System;
using UnityEngine;
using UnityEngine.UI;

public class SettingAudioSliderControl : SettingSliderControl
{
    [Header("오디오 음소거")]
    [SerializeField] private Toggle muteToggle;

    public bool IsMuted { get; private set; }
    public event Action<bool> onMuteChanged;

    public void Init(float initialValue, bool initialMute)
    {
        base.Init(initialValue);
        SetMute(initialMute, notify: false);
    }

    public void SetMute(bool muted, bool notify = true)
    {
        IsMuted = muted;
        if (muteToggle != null) muteToggle.SetIsOnWithoutNotify(muted);

        SetInteractable(!muted);

        if (notify)
        {
            onMuteChanged?.Invoke(IsMuted);
        }
    }

    public override void AutoFindComponents()
    {
        base.AutoFindComponents();
        if (muteToggle == null) muteToggle = GetComponentInChildren<Toggle>(true);
    }

    protected override void BindEvents()
    {
        base.BindEvents();
        if (muteToggle != null)
        {
            muteToggle.onValueChanged.AddListener(OnMuteToggleChanged);
        }
    }

    private void OnMuteToggleChanged(bool muted)
    {
        SetMute(muted, notify: true);
    }
}
