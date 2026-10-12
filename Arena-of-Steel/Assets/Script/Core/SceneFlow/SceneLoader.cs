using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : MonoBehaviour
{
    private static readonly string[] PrefabPaths = { "Loading Canvas", "UI/Loading Canvas", "UI/LoadingScreen" };

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Slider progressBar;
    [SerializeField] private float fadeDuration = 0.3f;

    public static bool IsLoading { get; private set; }

    public static void Load(string sceneName)
    {
        if (IsLoading) return;

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning("[SceneLoader] Scene not in Build Settings: " + sceneName);
            return;
        }

        UIWindowManager.Instance?.CloseSettings();

        SceneLoader loader = null;
        for (int i = 0; i < PrefabPaths.Length; i++)
        {
            var prefab = Resources.Load<GameObject>(PrefabPaths[i]);
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                loader = instance.GetComponent<SceneLoader>() ?? instance.AddComponent<SceneLoader>();
                if (loader.canvasGroup == null) loader.canvasGroup = instance.GetComponentInChildren<CanvasGroup>();
                if (loader.progressBar == null) loader.progressBar = instance.GetComponentInChildren<Slider>();
                break;
            }
        }

        if (loader == null)
            loader = new GameObject(nameof(SceneLoader)).AddComponent<SceneLoader>();

        DontDestroyOnLoad(loader.gameObject);
        loader.StartCoroutine(loader.LoadRoutine(sceneName));
    }

    private IEnumerator LoadRoutine(string sceneName)
    {
        IsLoading = true;
        Time.timeScale = 1f;
        var prevPriority = Application.backgroundLoadingPriority;
        Application.backgroundLoadingPriority = ThreadPriority.High;

        SetProgress(0f);
        yield return FadeRoutine(0f, 1f);

        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone)
        {
            SetProgress(op.progress);
            yield return null;
        }

        yield return null;
        SetProgress(1f);

        yield return FadeRoutine(1f, 0f);

        Application.backgroundLoadingPriority = prevPriority;
        IsLoading = false;
        Destroy(gameObject);
    }

    private IEnumerator FadeRoutine(float from, float to)
    {
        if (canvasGroup == null) yield break;

        canvasGroup.blocksRaycasts = true;
        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
        canvasGroup.blocksRaycasts = to > 0f;
    }

    private void SetProgress(float value)
    {
        if (progressBar != null) progressBar.value = value;
    }

    private void OnDestroy()
    {
        if (IsLoading) IsLoading = false;
    }
}
