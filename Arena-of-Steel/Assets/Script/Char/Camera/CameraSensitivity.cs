using UnityEngine;

public class CameraSensitivity : MonoBehaviour
{
    [Header("민감도 설정")]
    [SerializeField] private float sensitivity = 1f;
    [SerializeField] private bool invertY = false;

    public static CameraSensitivity Instance { get; private set; }

    public float Sensitivity
    {
        get => sensitivity;
        set => sensitivity = Mathf.Max(0.01f, value);
    }

    public bool InvertY
    {
        get => invertY;
        set => invertY = value;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (SettingManager.Instance != null && SettingManager.Instance.CurrentSettings != null)
        {
            var s = SettingManager.Instance.CurrentSettings;
            SetSensitivity(s.mouseSensitivity, s.invertY);
        }
    }

    public void SetSensitivity(float sensitivity, bool invertY)
    {
        Sensitivity = sensitivity;
        InvertY = invertY;
    }
}