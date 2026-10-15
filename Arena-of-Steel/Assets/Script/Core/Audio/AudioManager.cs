using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("오디오 믹서")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private AudioMixerGroup ambienceMixerGroup;

    [Header("오디오 소스")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource ambienceSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource sfxSource2D;

    [Header("3D SFX 풀")]
    [SerializeField] private int sfxPoolSize = 8;
    [SerializeField] private int maxSfxPoolSize = 16;
    private readonly List<AudioSource> sfx3DPool = new();
    private Transform poolRoot;
    private int sfxPoolIndex;

    [Header("UI 클립")]
    [SerializeField] private AudioClip navClip;
    [SerializeField] private AudioClip submitClip;
    [SerializeField] private AudioClip cancelClip;

    private Coroutine bgmFadeCoroutine;
    private Coroutine ambFadeCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (transform.parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            InitSources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void InitSources()
    {
        if (sfxSource2D == null) sfxSource2D = CreateSource("SFX_2D", false, 0f, sfxMixerGroup);
        if (ambienceSource == null) ambienceSource = CreateSource("Ambience", true, 0f, ambienceMixerGroup != null ? ambienceMixerGroup : bgmMixerGroup);

        poolRoot = new GameObject("SFX_3D_Pool").transform;
        poolRoot.SetParent(transform, false);

        for (int i = 0; i < sfxPoolSize; i++)
        {
            var source = CreateSource($"SFX_3D_{i + 1}", false, 1f, sfxMixerGroup);
            source.transform.SetParent(poolRoot, false);
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            sfx3DPool.Add(source);
        }
    }

    private AudioSource CreateSource(string sourceName, bool loop, float spatialBlend, AudioMixerGroup mixerGroup = null)
    {
        var go = new GameObject(sourceName);
        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = spatialBlend;
        if (mixerGroup != null) source.outputAudioMixerGroup = mixerGroup;
        return source;
    }

    private AudioSource GetAvailable3DSource()
    {
        for (int i = 0; i < sfx3DPool.Count; i++)
        {
            if (sfx3DPool[i] != null && !sfx3DPool[i].isPlaying)
                return sfx3DPool[i];
        }

        if (sfx3DPool.Count < maxSfxPoolSize)
        {
            var newSource = CreateSource($"SFX_3D_{sfx3DPool.Count + 1}", false, 1f, sfxMixerGroup);
            if (poolRoot != null) newSource.transform.SetParent(poolRoot, false);
            newSource.rolloffMode = AudioRolloffMode.Logarithmic;
            sfx3DPool.Add(newSource);
            return newSource;
        }

        if (sfx3DPool.Count == 0) return null;

        var source = sfx3DPool[sfxPoolIndex];
        sfxPoolIndex = (sfxPoolIndex + 1) % sfx3DPool.Count;
        return source;
    }

    public void PlayNavigate() => PlayUISound(navClip);
    public void PlaySubmit() => PlayUISound(submitClip);
    public void PlayCancel() => PlayUISound(cancelClip);

    public void PlayUISound(AudioClip clip, float volume = 1f)
    {
        if (!IsUIAudible() || clip == null || uiSource == null) return;
        uiSource.PlayOneShot(clip, volume);
    }

    public void PlaySFX2D(AudioClip clip, float volume = 1f, float minPitch = 1f, float maxPitch = 1f)
    {
        if (!IsSFXAudible() || clip == null || sfxSource2D == null) return;
        sfxSource2D.pitch = (minPitch != maxPitch) ? Random.Range(minPitch, maxPitch) : minPitch;
        sfxSource2D.PlayOneShot(clip, volume);
    }

    public void PlaySFXAt(AudioClip clip, Vector3 position, float volume = 1f, float minDistance = 1f, float maxDistance = 25f, float minPitch = 1f, float maxPitch = 1f)
    {
        if (!IsSFXAudible() || clip == null) return;

        var source = GetAvailable3DSource();
        if (source == null) return;
        source.transform.position = position;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.pitch = (minPitch != maxPitch) ? Random.Range(minPitch, maxPitch) : minPitch;
        source.volume = volume;
        source.clip = clip;
        source.Play();
    }

    private bool IsUIAudible()
    {
        if (SettingManager.Instance == null || SettingManager.Instance.CurrentSettings == null) return true;
        var s = SettingManager.Instance.CurrentSettings;
        return !s.masterMute && !s.uiMute && s.masterVolume > 0.0001f && s.uiVolume > 0.0001f;
    }

    private bool IsSFXAudible()
    {
        if (SettingManager.Instance == null || SettingManager.Instance.CurrentSettings == null) return true;
        var s = SettingManager.Instance.CurrentSettings;
        return !s.masterMute && !s.sfxMute && s.masterVolume > 0.0001f && s.sfxVolume > 0.0001f;
    }

    public void PlayBGM(AudioClip clip, bool loop = true)
    {
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        if (bgmSource == null) return;

        if (clip == null)
        {
            bgmSource.Stop();
            return;
        }

        bgmSource.clip = clip;
        bgmSource.loop = loop;
        bgmSource.volume = 1f;
        bgmSource.Play();
    }

    public void FadeBGM(AudioClip newClip, float duration = 1.5f, bool loop = true)
    {
        if (bgmSource == null) return;
        if (bgmFadeCoroutine != null) StopCoroutine(bgmFadeCoroutine);
        bgmFadeCoroutine = StartCoroutine(CoFadeAudioSource(bgmSource, newClip, duration, loop));
    }

    public void PlayAmbience(AudioClip clip, float duration = 1.5f)
    {
        if (ambienceSource == null) return;
        if (ambFadeCoroutine != null) StopCoroutine(ambFadeCoroutine);
        ambFadeCoroutine = StartCoroutine(CoFadeAudioSource(ambienceSource, clip, duration, true));
    }

    public void StopAmbience(float duration = 1.5f)
    {
        if (ambienceSource == null) return;
        if (ambFadeCoroutine != null) StopCoroutine(ambFadeCoroutine);
        ambFadeCoroutine = StartCoroutine(CoFadeAudioSource(ambienceSource, null, duration, true));
    }

    private IEnumerator CoFadeAudioSource(AudioSource source, AudioClip newClip, float duration, bool loop)
    {
        float halfDuration = Mathf.Max(duration * 0.5f, 0.01f);
        float startVol = source.volume;

        if (source.isPlaying && source.clip != null)
        {
            for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(startVol, 0f, t / halfDuration);
                yield return null;
            }
            source.volume = 0f;
            source.Stop();
        }

        if (newClip != null)
        {
            source.clip = newClip;
            source.loop = loop;
            source.Play();

            for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
            {
                source.volume = Mathf.Lerp(0f, 1f, t / halfDuration);
                yield return null;
            }
            source.volume = 1f;
        }
    }

    public void SetMixerVolume(string paramName, float linearVolume)
    {
        if (audioMixer == null || string.IsNullOrEmpty(paramName)) return;
        float dB = linearVolume <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp(linearVolume, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(paramName, dB);
    }
}
