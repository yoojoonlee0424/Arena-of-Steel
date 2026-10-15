using System;
using UnityEngine;

[Serializable]
public class GameSettingsData
{
    public int version = 1;

    public float masterVolume = 0.8f;
    public float bgmVolume = 0.8f;
    public float sfxVolume = 0.8f;
    public float uiVolume = 0.8f;

    public bool masterMute = false;
    public bool bgmMute = false;
    public bool sfxMute = false;
    public bool uiMute = false;

    public int resolutionWidth = 1920;
    public int resolutionHeight = 1080;
    public FullScreenMode fullScreenMode = FullScreenMode.FullScreenWindow;
    public int qualityLevel = 2;
    public int vSyncCount = 0;
    public int targetFrameRate = 60;

    public float mouseSensitivity = 1.0f;
    public bool invertY = true;

    public string keyBindingsJson = string.Empty;
}
