using System;
using System.IO;
using UnityEngine;

public static class SaveManager
{
    private static readonly string SettingsPath = Path.Combine(Application.persistentDataPath, "settings.json");

    public static void SaveSettings(GameSettingsData data)
    {
        try
        {
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"SaveSettings 실패: {e.Message}");
        }
    }

    private const int CurrentSettingsVersion = 1;

    public static GameSettingsData LoadSettings()
    {
        if (!File.Exists(SettingsPath)) return new GameSettingsData();

        try
        {
            string json = File.ReadAllText(SettingsPath);
            var data = JsonUtility.FromJson<GameSettingsData>(json) ?? new GameSettingsData();
            return MigrateSettings(data);
        }
        catch (Exception e)
        {
            Debug.LogError($"LoadSettings 실패: {e.Message}");
            return new GameSettingsData();
        }
    }

    private static GameSettingsData MigrateSettings(GameSettingsData data)
    {
        if (data.version < CurrentSettingsVersion)
        {
            data.version = CurrentSettingsVersion;
            SaveSettings(data);
        }
        return data;
    }

    /// <summary>
    /// 환경설정 데이터(settings.json) 파일을 삭제합니다.
    /// </summary>
    public static void DeleteSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                File.Delete(SettingsPath);

            Debug.Log("[SaveManager] Settings data deleted successfully.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] DeleteSettings failed: {e.Message}");
        }
    }

    /// <summary>
    /// 세이브 파일과 설정 파일을 모두 삭제합니다.
    /// </summary>
    public static void DeleteAllData()
    {
        DeleteSettings();
    }
}
