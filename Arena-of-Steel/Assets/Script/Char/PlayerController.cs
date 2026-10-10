using UnityEngine;

public enum ControlMode
{
    FirstPersonView,
    ThirdPersonView,
}

public class PlayerController : MonoBehaviour
{
    [SerializeField] private Renderer[] firstPersonHiddenRenderers;
    private CameraModeController cameraModeController;

    [Header("카메라 설정")]
    [SerializeField] private ControlMode controlMode = ControlMode.FirstPersonView;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private float topClamp = 70f;
    [SerializeField] private float bottomClamp = -70f;
    private float cameraYaw;
    private float cameraPitch;

    [Header("이동 설정")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("점프 설정")]
    [SerializeField] private float jumpPower = 2f;

    [Header("바닥 설정")]
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private InputSystem_Actions inputActions;
    private Vector3 moveVelocity;
    private float verticalVelocity;

    public InputSystem_Actions InputActions => inputActions;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        inputActions = new InputSystem_Actions();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        cameraYaw = transform.eulerAngles.y;
        cameraPitch = 0f;

        cameraModeController = GetComponent<CameraModeController>();
        cameraModeController?.BindTrackingTarget(cameraTarget);
    }

    private void Start()
    {
        SetControlMode(controlMode);
    }

    private void OnEnable()
    {
        inputActions?.Player.Enable();
    }

    private void OnDisable()
    {
        inputActions?.Disable();
    }

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void Update()
    {
        if (inputActions != null && inputActions.Player.ToggleView.WasPressedThisFrame())
        {
            SetControlMode(controlMode == ControlMode.FirstPersonView ? ControlMode.ThirdPersonView : ControlMode.FirstPersonView);
        }

        HandleLook();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void HandleLook()
    {
        if (inputActions == null || cameraTarget == null) return;

        Vector2 lookInput = inputActions.Player.Look.ReadValue<Vector2>();
        float sens = CameraSensitivity.Instance != null ? CameraSensitivity.Instance.Sensitivity : 1f;
        bool invertY = CameraSensitivity.Instance != null && CameraSensitivity.Instance.InvertY;

        float deltaMultiplier = 0.1f * sens;
        cameraYaw += lookInput.x * deltaMultiplier;
        cameraPitch += (invertY ? lookInput.y : -lookInput.y) * deltaMultiplier;
        cameraPitch = Mathf.Clamp(cameraPitch, bottomClamp, topClamp);

        if (controlMode == ControlMode.FirstPersonView)
        {
            transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
            cameraTarget.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
        else
        {
            cameraTarget.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        }
    }

    private void HandleMovement()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        input = Vector2.ClampMagnitude(input, 1f);

        bool isRunning = inputActions.Player.Sprint.IsPressed();
        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        Transform camRef = cameraTarget != null ? cameraTarget : transform;
        Vector3 camForward = Vector3.ProjectOnPlane(camRef.forward, Vector3.up).normalized;
        Vector3 camRight = Vector3.ProjectOnPlane(camRef.right, Vector3.up).normalized;
        Vector3 moveDirection = camForward * input.y + camRight * input.x;

        if (controlMode == ControlMode.ThirdPersonView && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        moveVelocity = moveDirection * currentSpeed;
    }

    private void HandleJump()
    {
        if (controller == null || !controller.enabled) return;

        if (inputActions.Player.Jump.WasPressedThisFrame() && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpPower * -2f * gravity);
        }
    }

    private void ApplyGravity()
    {
        if (controller == null || !controller.enabled) return;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 finalMovement = moveVelocity + Vector3.up * verticalVelocity;
        controller.Move(finalMovement * Time.deltaTime);
    }

    public void SetControlMode(ControlMode newMode)
    {
        controlMode = newMode;
        cameraModeController?.SetCameraMode(controlMode);
        UpdateMeshVisibility(controlMode == ControlMode.FirstPersonView);
    }

    private void UpdateMeshVisibility(bool isFirstPerson)
    {
        if (firstPersonHiddenRenderers == null) return;
        foreach (var r in firstPersonHiddenRenderers)
        {
            if (r != null)
                r.shadowCastingMode = isFirstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}