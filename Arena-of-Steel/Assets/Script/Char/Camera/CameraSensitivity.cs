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
}