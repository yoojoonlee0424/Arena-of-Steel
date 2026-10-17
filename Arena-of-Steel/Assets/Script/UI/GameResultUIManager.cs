using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameResultUIManager : MonoBehaviour
{
    public static bool IsGameOverOrClear { get; private set; }

    [Header("Game Over UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI gameOverInfoText;
    [SerializeField] private Button gameOverRestartButton;
    [SerializeField] private Button gameOverMainMenuButton;

    [Header("Game Clear UI")]
    [SerializeField] private GameObject gameClearPanel;
    [SerializeField] private TextMeshProUGUI gameClearTimeText;
    [SerializeField] private Button gameClearRestartButton;
    [SerializeField] private Button gameClearMainMenuButton;

    private void Awake()
    {
        IsGameOverOrClear = false;

        if (gameOverRestartButton != null) gameOverRestartButton.onClick.AddListener(RestartGame);
        if (gameOverMainMenuButton != null) gameOverMainMenuButton.onClick.AddListener(GoToMainMenu);
        if (gameClearRestartButton != null) gameClearRestartButton.onClick.AddListener(RestartGame);
        if (gameClearMainMenuButton != null) gameClearMainMenuButton.onClick.AddListener(GoToMainMenu);
    }

    private void Start()
    {
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameClearPanel != null) gameClearPanel.SetActive(false);
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDied += ShowGameOver;
        StageManager.OnGameClear += ShowGameClear;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDied -= ShowGameOver;
        StageManager.OnGameClear -= ShowGameClear;
    }

    public void ShowGameOver()
    {
        if (IsGameOverOrClear) return;
        IsGameOverOrClear = true;

        if (gameOverPanel != null) gameOverPanel.SetActive(true);

        if (gameOverInfoText != null && StageManager.Instance != null)
        {
            int wave = StageManager.Instance.currentWave + 1;
            gameOverInfoText.text = $"Wave: {wave}\nTime: {FormatTime(StageManager.Instance.ElapsedTime)}";
        }

        HandleGameEnd();
    }

    public void ShowGameClear()
    {
        if (IsGameOverOrClear) return;
        IsGameOverOrClear = true;

        if (gameClearPanel != null) gameClearPanel.SetActive(true);

        if (gameClearTimeText != null && StageManager.Instance != null)
        {
            gameClearTimeText.text = $"Clear Time: {FormatTime(StageManager.Instance.ElapsedTime)}";
        }

        HandleGameEnd();
    }

    public void RestartGame()
    {
        IsGameOverOrClear = false;
        Time.timeScale = 1f;
        SceneLoader.Load(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        IsGameOverOrClear = false;
        Time.timeScale = 1f;
        SceneLoader.Load(0);
    }

    private void HandleGameEnd()
    {
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var player = FindFirstObjectByType<PlayerController>();
        if (player != null) player.SetInputActive(false);

        var cam = FindFirstObjectByType<CameraController>();
        if (cam != null) cam.SetInputActive(false);
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        return $"{minutes:00}:{seconds:00}";
    }
}
