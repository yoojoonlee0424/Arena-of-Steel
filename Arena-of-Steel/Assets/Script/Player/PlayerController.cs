using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;
    [SerializeField] private Renderer[] firstPersonHiddenRenderers;

    [Header("이동 설정")]
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float rotationSpeed = 10f;

    [Header("달리기 설정")]
    [SerializeField] private float runSpeed = 6f;
    [SerializeField] private float staminaDrainRate = 15f;

    [Header("점프 설정")]
    [SerializeField] private float jumpPower = 2f;

    [Header("바닥 설정")]
    [SerializeField] private float gravity = -20f;

    [Header("대시 설정")]
    [SerializeField] private float dashSpeed = 16f;
    [SerializeField] private float dashDuration = 0.2f;
    [SerializeField] private float dashCooldown = 0.8f;
    [SerializeField] private float dashManaCost = 20f;
    [SerializeField] private int maxAirDashes = 1;

    private Vector2 input;
    private CharacterController controller;
    private InputSystem_Actions inputActions;
    private PlayerStamina playerStamina;
    private Vector3 moveVelocity;
    private float verticalVelocity;
    public bool isRunning { get; private set; }
    public bool isDashing { get; private set; }
    private float dashTimer;
    private float dashCooldownTimer;
    private int currentAirDashes;
    private Vector3 dashDirection;

    public InputSystem_Actions InputActions
    {
        get
        {
            if (inputActions == null)
                inputActions = new InputSystem_Actions();
            return inputActions;
        }
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerStamina = GetComponent<PlayerStamina>();
        if (inputActions == null)
            inputActions = new InputSystem_Actions();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraController == null)
            cameraController = GetComponent<CameraController>();
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();
    }

    private void Start()
    {
        SettingManager.Instance?.ApplyBindings(inputActions.asset);
        if (cameraController != null)
            UpdateMeshVisibility(cameraController.Mode);
    }

    private void OnEnable()
    {
        inputActions?.Player.Enable();
        if (inputActions != null)
            inputActions.Player.Dash.performed += OnDashPerformed;
        if (cameraController != null)
            cameraController.OnModeChanged += UpdateMeshVisibility;
        if (SettingManager.Instance != null)
            SettingManager.Instance.OnBindingsChanged += OnBindingsChanged;
    }

    private void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Dash.performed -= OnDashPerformed;
        inputActions?.Disable();
        if (cameraController != null)
            cameraController.OnModeChanged -= UpdateMeshVisibility;
        if (SettingManager.Instance != null)
            SettingManager.Instance.OnBindingsChanged -= OnBindingsChanged;
    }

    private void OnBindingsChanged()
    {
        SettingManager.Instance?.ApplyBindings(inputActions.asset);
    }

    public void SetInputActive(bool active)
    {
        if (active)
        {
            inputActions?.Player.Enable();
        }
        else
        {
            inputActions?.Player.Disable();
            moveVelocity = Vector3.zero;
            isDashing = false;
        }
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
        if (dashCooldownTimer > 0f) dashCooldownTimer -= Time.deltaTime;
        if (controller != null && controller.isGrounded) currentAirDashes = 0;

        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
        }

        if (inputActions != null && inputActions.Player.ToggleView.WasPressedThisFrame())
        {
            cameraController?.ToggleView();
        }

        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        if (isDashing)
        {
            moveVelocity = dashDirection * dashSpeed;
            return;
        }

        input = inputActions.Player.Move.ReadValue<Vector2>();
        input = Vector2.ClampMagnitude(input, 1f);

        bool wantRun = inputActions.Player.Sprint.IsPressed() && input.sqrMagnitude > 0.001f;
        isRunning = wantRun && playerStamina != null && playerStamina.UseStamina(staminaDrainRate * Time.deltaTime);

        float currentSpeed = isRunning ? runSpeed : walkSpeed;

        Vector3 camForward = cameraController != null ? cameraController.PlanarForward : transform.forward;
        Vector3 camRight = cameraController != null ? cameraController.PlanarRight : transform.right;
        Vector3 moveDirection = camForward * input.y + camRight * input.x;

        bool isThirdPerson = (cameraController == null || cameraController.Mode == ControlMode.ThirdPersonView);
        if (isThirdPerson && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        moveVelocity = moveDirection * currentSpeed;
    }

    private void HandleJump()
    {
        if (controller == null || !controller.enabled || isDashing) return;

        if (inputActions.Player.Jump.WasPressedThisFrame() && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpPower * -2f * gravity);
        }
    }

    private void ApplyGravity()
    {
        if (controller == null || !controller.enabled) return;

        if (isDashing)
        {
            verticalVelocity = 0f;
            controller.Move(moveVelocity * Time.deltaTime);
            return;
        }

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

    private void OnDashPerformed(UnityEngine.InputSystem.InputAction.CallbackContext _)
    {
        if (isDashing || dashCooldownTimer > 0f) return;
        if (controller != null && !controller.isGrounded && maxAirDashes > 0 && currentAirDashes >= maxAirDashes) return;
        if (playerStamina != null && !playerStamina.UseStamina(dashManaCost)) return;

        Vector3 camForward = cameraController != null ? cameraController.PlanarForward : transform.forward;
        Vector3 camRight = cameraController != null ? cameraController.PlanarRight : transform.right;

        Vector2 moveInput = inputActions != null ? inputActions.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        if (moveInput.sqrMagnitude > 0.001f)
        {
            dashDirection = (camForward * moveInput.y + camRight * moveInput.x).normalized;
        }
        else
        {
            dashDirection = camForward.sqrMagnitude > 0.001f ? camForward.normalized : transform.forward;
        }

        bool isThirdPerson = (cameraController == null || cameraController.Mode == ControlMode.ThirdPersonView);
        if (isThirdPerson && dashDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(dashDirection);
        }

        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        verticalVelocity = 0f;

        if (controller != null && !controller.isGrounded)
        {
            currentAirDashes++;
        }
    }

    private void UpdateMeshVisibility(ControlMode mode)
    {
        if (firstPersonHiddenRenderers == null) return;
        bool isFirstPerson = (mode == ControlMode.FirstPersonView);
        foreach (var r in firstPersonHiddenRenderers)
        {
            if (r != null)
                r.shadowCastingMode = isFirstPerson ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}