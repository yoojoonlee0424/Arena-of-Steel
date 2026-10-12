using System;
using System.Collections.Generic;
using UnityEngine;

public class GraphicSettingSection : SettingSection
{
    [Serializable]
    private struct ScreenModeOption
    {
        public string name;
        public FullScreenMode mode;

        public ScreenModeOption(string name, FullScreenMode mode)
        {
            this.name = name;
            this.mode = mode;
        }
    }

    [Serializable]
    private struct ResOption
    {
        public int width;
        public int height;

        public ResOption(int width, int height)
        {
            this.width = width;
            this.height = height;
        }
    }

    [SerializeField] private SettingSelectorControl resSelector;
    [SerializeField] private SettingSelectorControl screenModeSelector;
    [SerializeField] private SettingSelectorControl qualitySelector;
    [SerializeField] private SettingSelectorControl vsyncSelector;
    [SerializeField] private SettingSelectorControl fpsSelector;

    private static readonly ScreenModeOption[] ScreenModes = new ScreenModeOption[]
    {
        new ScreenModeOption("Borderless Window", FullScreenMode.FullScreenWindow),
        new ScreenModeOption("Fullscreen", FullScreenMode.ExclusiveFullScreen),
        new ScreenModeOption("Windowed", FullScreenMode.Windowed)
    };

    private static readonly int[] FpsLimits = new int[] { 30, 60, 120, 144, 240, -1 };

    private readonly List<ResOption> supportedResolutions = new List<ResOption>();

    public override void Init(SettingManager manager)
    {
        var settings = manager.CurrentSettings;

        InitResolutionSelector(manager, settings);
        InitScreenModeSelector(manager, settings);
        InitQualitySelector(manager, settings);
        InitVSyncSelector(manager, settings);
        InitFpsSelector(manager, settings);
    }

    private void InitResolutionSelector(SettingManager manager, GameSettingsData settings)
    {
        if (resSelector == null) return;

        supportedResolutions.Clear();
        var addedSet = new HashSet<string>();
        var resTextList = new List<string>();

        foreach (var r in Screen.resolutions)
        {
            if (r.width < 1024 || r.height < 600) continue;

            string key = r.width + "x" + r.height;
            if (addedSet.Add(key))
            {
                supportedResolutions.Add(new ResOption(r.width, r.height));
            }
        }

        if (supportedResolutions.Count == 0)
        {
            supportedResolutions.Add(new ResOption(1920, 1080));
            supportedResolutions.Add(new ResOption(1280, 720));
        }

        int selectedIndex = 0;
        for (int i = 0; i < supportedResolutions.Count; i++)
        {
            var res = supportedResolutions[i];
            resTextList.Add(res.width + " x " + res.height);

            if (res.width == settings.resolutionWidth && res.height == settings.resolutionHeight)
            {
                selectedIndex = i;
            }
        }

        resSelector.Init(resTextList, selectedIndex);
        resSelector.onIndexChanged += index =>
        {
            if (index >= 0 && index < supportedResolutions.Count)
            {
                var target = supportedResolutions[index];
                manager.SetRes(target.width, target.height);
            }
        };
    }

    private void InitScreenModeSelector(SettingManager manager, GameSettingsData settings)
    {
        if (screenModeSelector == null) return;

        var modeNames = new List<string>();
        int selectedIndex = 0;

        for (int i = 0; i < ScreenModes.Length; i++)
        {
            modeNames.Add(ScreenModes[i].name);
            if (ScreenModes[i].mode == settings.fullScreenMode)
            {
                selectedIndex = i;
            }
        }

        screenModeSelector.Init(modeNames, selectedIndex);
        screenModeSelector.onIndexChanged += index =>
        {
            if (index >= 0 && index < ScreenModes.Length)
            {
                manager.SetFullScreen(ScreenModes[index].mode);
            }
        };
    }

    private void InitQualitySelector(SettingManager manager, GameSettingsData settings)
    {
        if (qualitySelector == null) return;

        var qualityNames = new List<string>(QualitySettings.names);
        int currentLevel = Mathf.Clamp(settings.qualityLevel, 0, qualityNames.Count - 1);

        qualitySelector.Init(qualityNames, currentLevel);
        qualitySelector.onIndexChanged += manager.SetQuality;
    }

    private void InitVSyncSelector(SettingManager manager, GameSettingsData settings)
    {
        if (vsyncSelector == null) return;

        var options = new List<string>() { "Off", "On" };
        int selectedIndex = settings.vSyncCount > 0 ? 1 : 0;

        vsyncSelector.Init(options, selectedIndex);
        vsyncSelector.onIndexChanged += manager.SetVSync;
    }

    private void InitFpsSelector(SettingManager manager, GameSettingsData settings)
    {
        if (fpsSelector == null) return;

        var fpsOptions = new List<string>();
        int selectedIndex = 1;

        for (int i = 0; i < FpsLimits.Length; i++)
        {
            int limit = FpsLimits[i];
            fpsOptions.Add(limit < 0 ? "Unlimited" : limit.ToString());

            if (limit == settings.targetFrameRate)
            {
                selectedIndex = i;
            }
        }

        fpsSelector.Init(fpsOptions, selectedIndex);
        fpsSelector.onIndexChanged += index =>
        {
            if (index >= 0 && index < FpsLimits.Length)
            {
                manager.SetFPS(FpsLimits[index]);
            }
        };
    }
}
