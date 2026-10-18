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

    [Header("웅크리기 설정")]
    [SerializeField] private float crouchSpeed = 1.5f;
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float crouchCameraHeight = 0.0f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    [SerializeField] private bool toggleCrouch = false;
    [SerializeField] private LayerMask ceilingLayers = ~0;

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
    public bool isCrouching { get; private set; }
    private bool wantCrouch;
    private float standingHeight = 2.0f;
    private Vector3 standingCenter = Vector3.zero;
    private float standingCameraLocalY = 0.6f;
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

        if (controller != null)
        {
            standingHeight = controller.height;
            standingCenter = controller.center;
        }

        Transform camTarget = cameraController != null ? cameraController.CameraTarget : null;
        if (camTarget != null)
        {
            standingCameraLocalY = camTarget.localPosition.y;
        }
    }

    private void Start()
    {
        SettingManager.Instance?.ApplyBindings(inputActions.asset);
        if (SettingManager.Instance != null)
            toggleCrouch = SettingManager.Instance.CurrentSettings.toggleCrouch;
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
        {
            SettingManager.Instance.OnBindingsChanged += OnBindingsChanged;
            SettingManager.Instance.OnToggleCrouchChanged += OnToggleCrouchChanged;
        }
    }

    private void OnDisable()
    {
        if (inputActions != null)
            inputActions.Player.Dash.performed -= OnDashPerformed;
        inputActions?.Disable();
        if (cameraController != null)
            cameraController.OnModeChanged -= UpdateMeshVisibility;
        if (SettingManager.Instance != null)
        {
            SettingManager.Instance.OnBindingsChanged -= OnBindingsChanged;
            SettingManager.Instance.OnToggleCrouchChanged -= OnToggleCrouchChanged;
        }
    }

    private void OnBindingsChanged()
    {
        SettingManager.Instance?.ApplyBindings(inputActions.asset);
    }

    private void OnToggleCrouchChanged(bool toggle)
    {
        toggleCrouch = toggle;
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
            wantCrouch = false;
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

        HandleCrouch();
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    /// <summary>
    /// 머리 위에 장애물이 없어 일어설 수 있는지 검사합니다.
    /// </summary>
    public bool CanStandUp()
    {
        if (controller == null) return true;

        float radius = controller.radius * 0.9f;
        Vector3 feet = transform.position + standingCenter - Vector3.up * (standingHeight * 0.5f);
        Vector3 bottom = feet + Vector3.up * radius;
        Vector3 top = feet + Vector3.up * (standingHeight - radius);

        Collider[] hits = Physics.OverlapCapsule(bottom, top, radius, ceilingLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform.root != transform.root)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 웅크리기 입력 처리 및 캐릭터 컨트롤러, 카메라 높이 보간을 수행합니다.
    /// </summary>
    private void HandleCrouch()
    {
        if (inputActions == null || controller == null) return;

        if (toggleCrouch)
        {
            if (inputActions.Player.Crouch.WasPressedThisFrame())
                wantCrouch = !wantCrouch;
        }
        else
        {
            wantCrouch = inputActions.Player.Crouch.IsPressed();
        }

        if (wantCrouch)
        {
            isCrouching = true;
        }
        else if (isCrouching && CanStandUp())
        {
            isCrouching = false;
        }

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        if (Mathf.Abs(controller.height - targetHeight) > 0.001f)
        {
            float newHeight = Mathf.MoveTowards(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);
            controller.height = newHeight;
            controller.center = new Vector3(standingCenter.x, standingCenter.y - (standingHeight - newHeight) * 0.5f, standingCenter.z);
        }

        Transform camTarget = cameraController != null ? cameraController.CameraTarget : null;
        if (camTarget != null)
        {
            float targetCamY = isCrouching ? crouchCameraHeight : standingCameraLocalY;
            Vector3 lp = camTarget.localPosition;
            if (Mathf.Abs(lp.y - targetCamY) > 0.001f)
            {
                lp.y = Mathf.MoveTowards(lp.y, targetCamY, crouchTransitionSpeed * Time.deltaTime);
                camTarget.localPosition = lp;
            }
        }
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
        if (wantRun && isCrouching && CanStandUp())
        {
            wantCrouch = false;
            isCrouching = false;
        }

        isRunning = !isCrouching && wantRun && playerStamina != null && playerStamina.UseStamina(staminaDrainRate * Time.deltaTime);

        float currentSpeed = isCrouching ? crouchSpeed : (isRunning ? runSpeed : walkSpeed);

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

        if (inputActions.Player.Jump.WasPressedThisFrame())
        {
            if (isCrouching)
            {
                if (CanStandUp())
                {
                    wantCrouch = false;
                    isCrouching = false;
                }
                return;
            }

            if (controller.isGrounded)
            {
                verticalVelocity = Mathf.Sqrt(jumpPower * -2f * gravity);
            }
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

        if (isCrouching && CanStandUp())
        {
            wantCrouch = false;
            isCrouching = false;
        }

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