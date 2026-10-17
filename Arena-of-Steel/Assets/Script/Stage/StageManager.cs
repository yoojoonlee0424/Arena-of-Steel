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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);
    }

    private void Start()
    {
        ElapsedTime = 0.0f;
        OnWaveChanged?.Invoke();

        if (StageData != null && StageData.BGM != null)
        {
            AudioManager.Instance.PlayBGM(StageData.BGM);
        }
    }

    private void Update()
    {
        ElapsedTime += Time.deltaTime;
    }

    public void NextWave()
    {
        if (StageData == null || StageData.waveDatas == null) return;

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