using System;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance;
    public StageData StageData;
    public float ElapsedTime;
    public int currentWave = 0;
    
    public static event Action OnWaveChanged;
    public static event Action OnGameClear;

    private bool isGameStarted = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        ElapsedTime = 0.0f;

        if (StageData != null && StageData.BGM != null)
        {
            AudioManager.Instance.PlayBGM(StageData.BGM);
        }

        StartGame();
    }

    private void Update()
    {
        ElapsedTime += Time.deltaTime;
    }

    public void StartGame()
    {
        if (StageData == null || StageData.waveDatas == null || StageData.waveDatas.Length == 0) return;

        currentWave = 0;
        if (StageData.StartWaitTime > 0f)
        {
            isGameStarted = false;
            WaveSpawner.Instance?.StartPreparationPhase(StageData.StartWaitTime);
        }
        else
        {
            isGameStarted = true;
            OnWaveChanged?.Invoke();
        }
    }

    public void NextWave()
    {
        if (StageData == null || StageData.waveDatas == null) return;

        if (!isGameStarted)
        {
            isGameStarted = true;
            OnWaveChanged?.Invoke();
            return;
        }

        if (currentWave + 1 < StageData.waveDatas.Length)
        {
            currentWave++;
            OnWaveChanged?.Invoke();
        }
        else
        {
            OnGameClear?.Invoke();
        }
    }
}