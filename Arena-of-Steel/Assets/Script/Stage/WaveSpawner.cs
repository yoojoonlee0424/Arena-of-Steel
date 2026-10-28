using System;
using UnityEngine;
using UnityEngine.AI;

public enum WaveState
{
    Idle,
    Preparation,
    Combat,
    Reward
}

[Serializable]
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
    public static event Action<int> OnPrepTimerChanged;
    public static event Action OnPrepFinished;
    public static event Action<WaveState> OnWaveStateChanged;

    public static WaveSpawner Instance { get; private set; }
    public WaveState CurrentState { get; private set; } = WaveState.Idle;

    public Transform[] defaultSpawnPoints;
    public WaveSpawnGroup[] waveSpawnPoints;
    public Transform player;

    private WaveData currentWaveData;
    private int currentWaveIndex = 0;
    private float spawnTimer = 0f;
    private int spawnedCount = 0;
    private int aliveCount = 0;
    private int remainingCount = 0;
    private float prepTimer = 0f;
    private int lastPrepSeconds = -1;

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

    private void SetState(WaveState newState)
    {
        CurrentState = newState;
        OnWaveStateChanged?.Invoke(newState);
    }

    private void HandleWaveChanged()
    {
        if (StageManager.Instance == null || StageManager.Instance.StageData == null) return;

        var stageData = StageManager.Instance.StageData;
        currentWaveIndex = StageManager.Instance.currentWave;

        if (stageData.waveDatas == null || currentWaveIndex < 0 || currentWaveIndex >= stageData.waveDatas.Length)
        {
            SetState(WaveState.Idle);
            return;
        }

        currentWaveData = stageData.waveDatas[currentWaveIndex];
        spawnedCount = 0;
        aliveCount = 0;
        spawnTimer = 0f;
        remainingCount = currentWaveData.totalEnemyCount;
        SetState(WaveState.Combat);

        OnRemainingEnemiesChanged?.Invoke(aliveCount);

        if (currentWaveData.totalEnemyCount <= 0)
        {
            SetState(WaveState.Idle);
            StageManager.Instance.NextWave();
        }
    }

    private void Update()
    {
        switch (CurrentState)
        {
            case WaveState.Preparation:
                UpdatePreparation();
                break;
            case WaveState.Combat:
                UpdateCombat();
                break;
        }
    }

    private void UpdatePreparation()
    {
        prepTimer -= Time.deltaTime;
        int currentSeconds = Mathf.CeilToInt(Mathf.Max(0f, prepTimer));

        if (currentSeconds != lastPrepSeconds)
        {
            lastPrepSeconds = currentSeconds;
            OnPrepTimerChanged?.Invoke(currentSeconds);
        }

        if (prepTimer <= 0f)
        {
            EndPreparation();
        }
    }

    private void UpdateCombat()
    {
        if (currentWaveData == null) return;

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
        if (CurrentState != WaveState.Combat || currentWaveData == null) return;

        aliveCount--;
        if (aliveCount < 0) aliveCount = 0;

        remainingCount--;
        if (remainingCount < 0) remainingCount = 0;

        OnRemainingEnemiesChanged?.Invoke(aliveCount);

        if (spawnedCount >= currentWaveData.totalEnemyCount && aliveCount == 0)
        {
            if (StageManager.Instance != null && StageManager.Instance.StageData != null &&
                currentWaveIndex + 1 >= StageManager.Instance.StageData.waveDatas.Length)
            {
                SetState(WaveState.Idle);
                StageManager.Instance.NextWave();
            }
            else if (StageManager.Instance != null && StageManager.Instance.StageData != null)
            {
                SetState(WaveState.Reward);
                var rewards = StageManager.Instance.StageData.GetRewardsForWave(currentWaveIndex);
                OnWaveRewardRequested?.Invoke(rewards);
            }
            else
            {
                SetState(WaveState.Idle);
                StageManager.Instance?.NextWave();
            }
        }
    }

    public void StartPreparationPhase(float customDuration = -1f)
    {
        float duration = customDuration >= 0f ? customDuration : (currentWaveData != null ? currentWaveData.prepTime : 10f);
        if (duration <= 0f)
        {
            EndPreparation();
            return;
        }

        SetState(WaveState.Preparation);
        prepTimer = duration;
        lastPrepSeconds = Mathf.CeilToInt(prepTimer);
        OnPrepTimerChanged?.Invoke(lastPrepSeconds);
    }

    public void EndPreparation()
    {
        SetState(WaveState.Idle);
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