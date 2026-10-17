using System;
using UnityEngine;
using UnityEngine.AI;

[System.Serializable]
public class WaveSpawnGroup
{
    [Tooltip("웨이브 번호 (1부터 시작, 예: 3웨이브는 3)")]
    public int waveNumber = 1;
    public Transform[] points;
}

public class WaveSpawner : MonoBehaviour
{
    public static event Action<int> OnRemainingEnemiesChanged;
    public static event Action<WaveRewardSO[]> OnWaveRewardRequested;
    public static event Action<float> OnPrepTimerChanged;
    public static event Action OnPrepFinished;

    public static WaveSpawner Instance { get; private set; }

    public Transform[] defaultSpawnPoints;
    public WaveSpawnGroup[] waveSpawnPoints;
    public Transform player;

    private WaveData currentWaveData;
    private int currentWaveIndex = 0;
    private float spawnTimer = 0f;
    private int spawnedCount = 0;
    private int aliveCount = 0;
    private int remainingCount = 0;
    private bool isWaveActive = false;
    private bool isPrepActive = false;
    private float prepTimer = 0f;

    private void Awake()
    {
        Instance = this;
        if (player == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    private void OnEnable()
    {
        EnemyBase.OnAnyEnemyDied += HandleEnemyDied;
        StageManager.OnWaveChanged += HandleWaveChanged;
    }

    private void OnDisable()
    {
        EnemyBase.OnAnyEnemyDied -= HandleEnemyDied;
        StageManager.OnWaveChanged -= HandleWaveChanged;
    }

    private void HandleWaveChanged()
    {
        if (StageManager.Instance == null || StageManager.Instance.StageData == null) return;

        var stageData = StageManager.Instance.StageData;
        currentWaveIndex = StageManager.Instance.currentWave;

        if (stageData.waveDatas == null || currentWaveIndex < 0 || currentWaveIndex >= stageData.waveDatas.Length)
        {
            isWaveActive = false;
            return;
        }

        currentWaveData = stageData.waveDatas[currentWaveIndex];
        spawnedCount = 0;
        aliveCount = 0;
        spawnTimer = 0f;
        isPrepActive = false;
        remainingCount = currentWaveData.totalEnemyCount;
        isWaveActive = true;

        OnRemainingEnemiesChanged?.Invoke(aliveCount);

        if (currentWaveData.totalEnemyCount <= 0)
        {
            isWaveActive = false;
            StageManager.Instance.NextWave();
        }
    }

    private void Update()
    {
        if (isPrepActive)
        {
            prepTimer -= Time.deltaTime;
            OnPrepTimerChanged?.Invoke(Mathf.Max(0f, prepTimer));
            if (prepTimer <= 0f)
            {
                EndPreparation();
            }
            return;
        }

        if (!isWaveActive || currentWaveData == null) return;

        if (spawnedCount < currentWaveData.totalEnemyCount)
        {
            bool canSpawn = currentWaveData.maxAliveCount <= 0 || aliveCount < currentWaveData.maxAliveCount;
            if (canSpawn)
            {
                spawnTimer += Time.deltaTime;
                if (spawnTimer >= currentWaveData.SpawnInterval)
                {
                    spawnTimer = 0f;
                    if (SpawnEnemy())
                    {
                        spawnedCount++;
                        aliveCount++;
                        OnRemainingEnemiesChanged?.Invoke(aliveCount);
                    }
                }
            }
        }
    }

    private void HandleEnemyDied(EnemyBase enemy)
    {
        if (!isWaveActive || currentWaveData == null) return;

        aliveCount--;
        if (aliveCount < 0) aliveCount = 0;

        remainingCount--;
        if (remainingCount < 0) remainingCount = 0;

        OnRemainingEnemiesChanged?.Invoke(aliveCount);

        if (spawnedCount >= currentWaveData.totalEnemyCount && aliveCount == 0)
        {
            isWaveActive = false;

            if (StageManager.Instance != null && StageManager.Instance.StageData != null &&
                currentWaveIndex + 1 >= StageManager.Instance.StageData.waveDatas.Length)
            {
                StageManager.Instance.NextWave();
            }
            else if (StageManager.Instance != null && StageManager.Instance.StageData != null)
            {
                var rewards = StageManager.Instance.StageData.GetRewardsForWave(currentWaveIndex);
                OnWaveRewardRequested?.Invoke(rewards);
            }
            else
            {
                StageManager.Instance?.NextWave();
            }
        }
    }

    public void StartPreparationPhase()
    {
        float duration = currentWaveData != null ? currentWaveData.prepTime : 10f;
        if (duration <= 0f)
        {
            EndPreparation();
            return;
        }

        isPrepActive = true;
        prepTimer = duration;
        OnPrepTimerChanged?.Invoke(prepTimer);
    }

    public void EndPreparation()
    {
        isPrepActive = false;
        OnPrepFinished?.Invoke();
        if (StageManager.Instance != null)
        {
            StageManager.Instance.NextWave();
        }
    }

    private bool SpawnEnemy()
    {
        EnemyData data = StageManager.Instance.StageData.GetRandomEnemy(currentWaveIndex);
        if (data == null || data.prefab == null) return false;

        Transform spawnPoint = GetSpawnPoint(currentWaveIndex);
        Vector3 spawnPos = spawnPoint.position;

        if (NavMesh.SamplePosition(spawnPos, out NavMeshHit hit, 3f, NavMesh.AllAreas))
        {
            spawnPos = hit.position;
        }

        GameObject enemyObj = Instantiate(data.prefab, spawnPos, spawnPoint.rotation);
        EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();
        if (enemy != null)
        {
            enemy.enemyData = data;
            enemy.ApplyEnemyData();
            if (player != null)
            {
                enemy.SetTarget(player);
            }
        }
        return true;
    }

    private Transform GetSpawnPoint(int waveIndex)
    {
        int currentWaveNum = waveIndex + 1;
        if (waveSpawnPoints != null)
        {
            for (int i = 0; i < waveSpawnPoints.Length; i++)
            {
                var group = waveSpawnPoints[i];
                if (group != null && (group.waveNumber == currentWaveNum || (group.waveNumber == 0 && i == waveIndex)))
                {
                    if (group.points != null && group.points.Length > 0)
                    {
                        Transform valid = GetRandomValidPoint(group.points);
                        if (valid != null) return valid;
                    }
                }
            }
        }

        if (defaultSpawnPoints != null && defaultSpawnPoints.Length > 0)
        {
            Transform valid = GetRandomValidPoint(defaultSpawnPoints);
            if (valid != null) return valid;
        }

        return transform;
    }

    private Transform GetRandomValidPoint(Transform[] points)
    {
        int startIndex = UnityEngine.Random.Range(0, points.Length);
        for (int i = 0; i < points.Length; i++)
        {
            int index = (startIndex + i) % points.Length;
            if (points[index] != null)
            {
                return points[index];
            }
        }
        return null;
    }
}