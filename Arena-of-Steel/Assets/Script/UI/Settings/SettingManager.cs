using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SettingManager : MonoBehaviour
{
    public static SettingManager Instance { get; private set; }

    [Header("오디오 믹서")]
    [SerializeField] private AudioMixer audioMixer;

    public GameSettingsData CurrentSettings { get; private set; }

    public event Action OnBindingsChanged;
    private InputActionRebindingExtensions.RebindingOperation rebindOp;
    private Coroutine saveCoroutine;
    private bool isDirty;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
        }
        else
        {
            Destroy(this);
            return;
        }

        CurrentSettings = SaveManager.LoadSettings();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyAll();
    }

    private void Start()
    {
        ApplyAll();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SaveImmediate();

        if (Instance == this)
        {
            Instance = null;
        }

        if (rebindOp != null)
        {
            rebindOp.Cancel();
            rebindOp.Dispose();
            rebindOp = null;
        }
    }

    public void ApplyAll()
    {
        ApplyAudio();
        ApplyGraphic();
        ApplyCamera();
    }

    public void ApplyAudio()
    {
        var s = CurrentSettings;
        SetMixerVolume("Master", s.masterMute ? 0f : s.masterVolume);
        SetMixerVolume("BGM", GetChannelVolume(s.bgmVolume, s.bgmMute));
        SetMixerVolume("Ambience", GetChannelVolume(s.bgmVolume, s.bgmMute));
        SetMixerVolume("SFX", GetChannelVolume(s.sfxVolume, s.sfxMute));
        SetMixerVolume("UI", GetChannelVolume(s.uiVolume, s.uiMute));
    }

    public void ApplyGraphic()
    {
        var s = CurrentSettings;
        Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, s.fullScreenMode);
        QualitySettings.SetQualityLevel(s.qualityLevel, true);
        QualitySettings.vSyncCount = s.vSyncCount;
        Application.targetFrameRate = s.targetFrameRate;
    }

    public void ApplyCamera()
    {
        var camSens = FindFirstObjectByType<CameraSensitivity>();
        if (camSens != null)
        {
            camSens.SetSensitivity(CurrentSettings.mouseSensitivity, CurrentSettings.invertY);
        }
    }

    public void SetMasterVol(float val)
    {
        CurrentSettings.masterVolume = val;
        ApplyAudio();
        Save();
    }

    public void SetMasterMute(bool mute)
    {
        CurrentSettings.masterMute = mute;
        ApplyAudio();
        Save();
    }

    public void SetBGMVol(float val)
    {
        CurrentSettings.bgmVolume = val;
        ApplyAudio();
        Save();
    }

    public void SetBGMMute(bool mute)
    {
        CurrentSettings.bgmMute = mute;
        ApplyAudio();
        Save();
    }

    public void SetSFXVol(float val)
    {
        CurrentSettings.sfxVolume = val;
        ApplyAudio();
        Save();
    }

    public void SetSFXMute(bool mute)
    {
        CurrentSettings.sfxMute = mute;
        ApplyAudio();
        Save();
    }

    public void SetUIVol(float val)
    {
        CurrentSettings.uiVolume = val;
        ApplyAudio();
        Save();
    }

    public void SetUIMute(bool mute)
    {
        CurrentSettings.uiMute = mute;
        ApplyAudio();
        Save();
    }

    public void SetRes(int width, int height)
    {
        CurrentSettings.resolutionWidth = width;
        CurrentSettings.resolutionHeight = height;
        ApplyGraphic();
        Save();
    }

    public void SetFullScreen(FullScreenMode mode)
    {
        CurrentSettings.fullScreenMode = mode;
        ApplyGraphic();
        Save();
    }

    public void SetQuality(int level)
    {
        CurrentSettings.qualityLevel = level;
        ApplyGraphic();
        Save();
    }

    public void SetVSync(int count)
    {
        CurrentSettings.vSyncCount = count;
        ApplyGraphic();
        Save();
    }

    public void SetFPS(int fps)
    {
        CurrentSettings.targetFrameRate = fps;
        ApplyGraphic();
        Save();
    }

    public void SetSens(float val)
    {
        CurrentSettings.mouseSensitivity = val;
        ApplyCamera();
        Save();
    }

    public void SetInvertY(bool invert)
    {
        CurrentSettings.invertY = invert;
        ApplyCamera();
        Save();
    }

    public void ApplyBindings(InputActionAsset asset)
    {
        if (asset == null) return;
        asset.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(CurrentSettings.keyBindingsJson))
        {
            asset.LoadBindingOverridesFromJson(CurrentSettings.keyBindingsJson);
        }
    }

    public void SaveBindings(InputActionAsset asset)
    {
        if (asset == null) return;
        CurrentSettings.keyBindingsJson = asset.SaveBindingOverridesAsJson();
        Save();
        OnBindingsChanged?.Invoke();
    }

    public void ResetBindings(InputActionAsset asset)
    {
        if (asset == null) return;
        asset.RemoveAllBindingOverrides();
        SaveBindings(asset);
    }

    public void Rebind(InputAction action, int bindingIndex, Action onFinished)
    {
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count) return;
        if (action.bindings[bindingIndex].isComposite)
        {
            Debug.LogWarning($"[SettingManager] Cannot rebind composite header '{action.name}' at index {bindingIndex}.");
            return;
        }

        action.Disable();
        rebindOp?.Cancel();

        rebindOp = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Pointer>/delta")
            .WithControlsExcluding("<Mouse>/scroll")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnMatchWaitForAnother(0.1f)
            .OnComplete(op => FinishRebind(op, action, true, onFinished))
            .OnCancel(op => FinishRebind(op, action, false, onFinished));

        rebindOp.Start();
    }

    public void Save()
    {
        isDirty = true;

        if (saveCoroutine != null)
        {
            StopCoroutine(saveCoroutine);
        }

        saveCoroutine = StartCoroutine(CoDelayedSave());
    }

    public void SaveImmediate()
    {
        if (saveCoroutine != null)
        {
            StopCoroutine(saveCoroutine);
            saveCoroutine = null;
        }

        if (isDirty)
        {
            isDirty = false;
            SaveManager.SaveSettings(CurrentSettings);
        }
    }

    private IEnumerator CoDelayedSave()
    {
        yield return new WaitForSecondsRealtime(0.5f);
        saveCoroutine = null;

        if (isDirty)
        {
            isDirty = false;
            SaveManager.SaveSettings(CurrentSettings);
        }
    }

    private void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, InputAction action, bool completed, Action onFinished)
    {
        action.Enable();
        op.Dispose();
        rebindOp = null;

        if (completed && action.actionMap != null)
        {
            SaveBindings(action.actionMap.asset);
        }

        onFinished?.Invoke();
    }

    private float GetChannelVolume(float volume, bool mute)
    {
        if (CurrentSettings.masterMute || mute) return 0f;
        return volume;
    }

    private void SetMixerVolume(string paramName, float linearVolume)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMixerVolume(paramName, linearVolume);
            return;
        }

        if (audioMixer != null)
        {
            float dB = linearVolume <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp(linearVolume, 0.0001f, 1f)) * 20f;
            audioMixer.SetFloat(paramName, dB);
        }
    }
}