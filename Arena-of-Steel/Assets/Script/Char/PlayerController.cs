using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private CameraController cameraController;
    [SerializeField] private Renderer[] firstPersonHiddenRenderers;

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

        if (cameraController == null)
            cameraController = GetComponent<CameraController>();
        if (cameraController == null)
            cameraController = FindAnyObjectByType<CameraController>();
    }

    private void Start()
    {
        if (cameraController != null)
            UpdateMeshVisibility(cameraController.Mode);
    }

    private void OnEnable()
    {
        inputActions?.Player.Enable();
        if (cameraController != null)
            cameraController.OnModeChanged += UpdateMeshVisibility;
    }

    private void OnDisable()
    {
        inputActions?.Disable();
        if (cameraController != null)
            cameraController.OnModeChanged -= UpdateMeshVisibility;
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
            cameraController?.ToggleView();
        }

        HandleMovement();
        HandleJump();
        ApplyGravity();
    }

    private void HandleMovement()
    {
        Vector2 input = inputActions.Player.Move.ReadValue<Vector2>();
        input = Vector2.ClampMagnitude(input, 1f);

        bool isRunning = inputActions.Player.Sprint.IsPressed();
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