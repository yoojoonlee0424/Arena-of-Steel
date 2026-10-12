using UnityEngine;

public class AudioSettingSection : SettingSection
{
    [SerializeField] private SettingAudioSliderControl masterControl;
    [SerializeField] private SettingAudioSliderControl bgmControl;
    [SerializeField] private SettingAudioSliderControl sfxControl;
    [SerializeField] private SettingAudioSliderControl uiControl;

    public override void Init(SettingManager manager)
    {
        var settings = manager.CurrentSettings;

        if (masterControl != null)
        {
            masterControl.Init(settings.masterVolume, settings.masterMute);
            masterControl.onValueChanged += manager.SetMasterVol;
            masterControl.onMuteChanged += manager.SetMasterMute;
        }

        if (bgmControl != null)
        {
            bgmControl.Init(settings.bgmVolume, settings.bgmMute);
            bgmControl.onValueChanged += manager.SetBGMVol;
            bgmControl.onMuteChanged += manager.SetBGMMute;
        }

        if (sfxControl != null)
        {
            sfxControl.Init(settings.sfxVolume, settings.sfxMute);
            sfxControl.onValueChanged += manager.SetSFXVol;
            sfxControl.onMuteChanged += manager.SetSFXMute;
        }

        if (uiControl != null)
        {
            uiControl.Init(settings.uiVolume, settings.uiMute);
            uiControl.onValueChanged += manager.SetUIVol;
            uiControl.onMuteChanged += manager.SetUIMute;
        }
    }
}
