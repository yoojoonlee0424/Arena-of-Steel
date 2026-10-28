using UnityEngine;

[System.Serializable]
public struct EnemySpawnEntry
{
    public EnemyData enemyData;
    [Range(0f, 100f)] public float spawnWeight;
}

[System.Serializable]
public class WaveData
{
    public int totalEnemyCount;
    public int maxAliveCount;
    public float SpawnInterval;
    public float prepTime = 10f;
    public EnemySpawnEntry[] enemySpawnEntries;
    public WaveRewardSO[] rewardPool;
}

[CreateAssetMenu(fileName = "StageData", menuName = "StageData")]
public class StageData : ScriptableObject
{
    public string StageName = "New Stage";
    public float StartWaitTime = 0f;
    public WaveData[] waveDatas;
    public WaveRewardSO[] defaultRewardPool;
    public AudioClip BGM;

    public WaveRewardSO[] GetRewardsForWave(int waveIndex)
    {
        if (waveDatas != null && waveIndex >= 0 && waveIndex < waveDatas.Length)
        {
            var pool = waveDatas[waveIndex].rewardPool;
            if (pool != null && pool.Length > 0) return pool;
        }
        return defaultRewardPool;
    }

    public EnemyData GetRandomEnemy(int waveIndex)
    {
        if (waveDatas == null || waveIndex < 0 || waveIndex >= waveDatas.Length) return null;

        var entries = waveDatas[waveIndex].enemySpawnEntries;
        if (entries == null || entries.Length == 0) return null;

        float totalWeight = 0f;
        for (int i = 0; i < entries.Length; i++)
        {
            totalWeight += entries[i].spawnWeight;
        }

        if (totalWeight <= 0f) return entries[0].enemyData;

        float randomVal = Random.Range(0f, totalWeight);
        float currentSum = 0f;

        for (int i = 0; i < entries.Length; i++)
        {
            currentSum += entries[i].spawnWeight;
            if (randomVal <= currentSum)
            {
                return entries[i].enemyData;
            }
        }

        return entries[0].enemyData;
    }
}