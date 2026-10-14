using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DebugSettingSection : SettingSection
{
    [SerializeField] private Button deleteSaveDataButton;
    [SerializeField] private Button deleteSettingsButton;
    [SerializeField] private Button deleteAllDataButton;
    [SerializeField] private Button reloadCurrentSceneButton;
    [SerializeField] private SettingSelectorControl sceneSelector;
    [SerializeField] private Button loadSceneButton;
    [SerializeField] private Button quitGameButton;

    private readonly List<string> loadableSceneNames = new List<string>();

    public override void Init(SettingManager manager)
    {
        if (deleteSaveDataButton != null) deleteSaveDataButton.onClick.AddListener(OnDeleteSaveDataClicked);
        if (deleteSettingsButton != null) deleteSettingsButton.onClick.AddListener(SaveManager.DeleteSettings);
        if (deleteAllDataButton != null) deleteAllDataButton.onClick.AddListener(OnDeleteAllDataClicked);
        if (reloadCurrentSceneButton != null) reloadCurrentSceneButton.onClick.AddListener(ReloadCurrentScene);
        if (loadSceneButton != null) loadSceneButton.onClick.AddListener(LoadSelectedScene);
        if (quitGameButton != null) quitGameButton.onClick.AddListener(QuitGame);

        if (sceneSelector != null)
        {
            var options = new List<string>();
            loadableSceneNames.Clear();
            int sceneCount = SceneManager.sceneCountInBuildSettings;

            for (int i = 0; i < sceneCount; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = Path.GetFileNameWithoutExtension(path);
                loadableSceneNames.Add(sceneName);
                options.Add(sceneName);
            }

            if (options.Count == 0)
            {
                string activeName = SceneManager.GetActiveScene().name;
                loadableSceneNames.Add(activeName);
                options.Add(activeName);
            }

            int defaultIndex = Mathf.Max(0, loadableSceneNames.IndexOf(SceneManager.GetActiveScene().name));
            sceneSelector.Init(options, defaultIndex);
        }
    }

    private void ReloadCurrentScene()
    {
        SceneLoader.Load(SceneManager.GetActiveScene().name);
    }

    private void OnDeleteSaveDataClicked()
    {
        ReloadCurrentScene();
    }

    private void OnDeleteAllDataClicked()
    {
        SaveManager.DeleteAllData();
        ReloadCurrentScene();
    }

    private void LoadSelectedScene()
    {
        if (sceneSelector == null) return;

        int index = sceneSelector.CurrentIndex;
        if (index >= 0 && index < loadableSceneNames.Count)
        {
            SceneLoader.Load(loadableSceneNames[index]);
        }
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
