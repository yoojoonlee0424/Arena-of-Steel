using System;
using UnityEngine;

public enum ControlMode
{
    FirstPersonView,
    ThirdPersonView,
}

public class CameraController : MonoBehaviour
{
    [Header("카메라 설정")]
    [SerializeField] private ControlMode controlMode = ControlMode.FirstPersonView;
    [SerializeField] private Transform cameraTarget;
    [SerializeField] private Transform cameraTransform;

    [Header("회전 설정")]
    [SerializeField] private float topClamp = 70f;
    [SerializeField] private float bottomClamp = -70f;

    [Header("3인칭 설정")]
    [SerializeField] private Vector3 shoulderOffset = new Vector3(0.5f, 0f, 0f);
    [SerializeField] private float defaultDistance = 3f;
    [SerializeField] private float minDistance = 0.3f;
    [SerializeField] private float collisionRadius = 0.2f;
    [SerializeField] private LayerMask collisionLayers = ~0;

    [Header("전환 딜레이")]
    [SerializeField] private bool smoothTransition = true;
    [SerializeField] private float transitionDuration = 0.2f;

    public event Action<ControlMode> OnModeChanged;

    public ControlMode Mode => controlMode;
    public Vector3 PlanarForward => cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized : Vector3.forward;
    public Vector3 PlanarRight => cameraTransform != null ? Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized : Vector3.right;

    private InputSystem_Actions inputActions;
    private Transform playerBody;
    private float cameraYaw;
    private float cameraPitch;
    private float currentDistance;
    private Vector3 currentShoulderOffset;
    private float distVelocity;
    private Vector3 offsetVelocity;

    private void Awake()
    {
        inputActions = new InputSystem_Actions();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTarget != null)
        {
            cameraYaw = cameraTarget.eulerAngles.y;
            playerBody = cameraTarget.root;
        }

        currentDistance = (controlMode == ControlMode.FirstPersonView) ? 0f : defaultDistance;
        currentShoulderOffset = (controlMode == ControlMode.FirstPersonView) ? Vector3.zero : shoulderOffset;
    }

    private void OnEnable() => inputActions?.Player.Enable();
    private void OnDisable() => inputActions?.Disable();

    private void OnDestroy()
    {
        if (inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void LateUpdate()
    {
        if (cameraTarget == null) return;
        EnsureCameraTransform();
        if (cameraTransform == null) return;

        HandleRotation();
        HandlePosition();
    }

    private void HandleRotation()
    {
        if (inputActions == null) return;

        Vector2 lookInput = inputActions.Player.Look.ReadValue<Vector2>();
        float sens = CameraSensitivity.Instance != null ? CameraSensitivity.Instance.Sensitivity : 1f;
        bool invertY = CameraSensitivity.Instance != null && CameraSensitivity.Instance.InvertY;

        float deltaMultiplier = 0.1f * sens;
        cameraYaw += lookInput.x * deltaMultiplier;
        cameraPitch += (invertY ? lookInput.y : -lookInput.y) * deltaMultiplier;
        cameraPitch = Mathf.Clamp(cameraPitch, bottomClamp, topClamp);

        Quaternion lookRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        if (controlMode == ControlMode.FirstPersonView)
        {
            if (playerBody != null)
                playerBody.rotation = Quaternion.Euler(0f, cameraYaw, 0f);

            cameraTarget.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
        }
        else
        {
            cameraTarget.rotation = lookRotation;
        }

        cameraTransform.rotation = lookRotation;
    }

    private void HandlePosition()
    {
        bool isFirstPerson = (controlMode == ControlMode.FirstPersonView);
        float targetDist = isFirstPerson ? 0f : defaultDistance;
        Vector3 targetOffset = isFirstPerson ? Vector3.zero : shoulderOffset;

        if (smoothTransition)
        {
            currentDistance = Mathf.SmoothDamp(currentDistance, targetDist, ref distVelocity, transitionDuration);
            currentShoulderOffset = Vector3.SmoothDamp(currentShoulderOffset, targetOffset, ref offsetVelocity, transitionDuration);
        }
        else
        {
            currentDistance = targetDist;
            currentShoulderOffset = targetOffset;
        }

        if (isFirstPerson && (!smoothTransition || currentDistance < 0.05f))
        {
            cameraTransform.position = cameraTarget.position;
            return;
        }

        Vector3 shoulderPos = cameraTarget.position + cameraTarget.rotation * currentShoulderOffset;
        float finalDist = currentDistance;

        if (finalDist > minDistance)
        {
            Ray ray = new Ray(shoulderPos, -cameraTarget.forward);
            if (Physics.SphereCast(ray, collisionRadius, out RaycastHit hit, finalDist, collisionLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.root != cameraTarget.root)
                {
                    finalDist = Mathf.Clamp(hit.distance - collisionRadius, minDistance, finalDist);
                }
            }
        }

        cameraTransform.position = shoulderPos - cameraTarget.forward * finalDist;
    }

    private void EnsureCameraTransform()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public void SetTarget(Transform newTarget)
    {
        cameraTarget = newTarget;
        playerBody = cameraTarget != null ? cameraTarget.root : null;
        if (cameraTarget != null) cameraYaw = cameraTarget.eulerAngles.y;
    }

    public void ToggleView()
    {
        SetControlMode(controlMode == ControlMode.FirstPersonView ? ControlMode.ThirdPersonView : ControlMode.FirstPersonView);
    }

    public void SetControlMode(ControlMode mode)
    {
        controlMode = mode;
        if (!smoothTransition)
        {
            currentDistance = (controlMode == ControlMode.FirstPersonView) ? 0f : defaultDistance;
            currentShoulderOffset = (controlMode == ControlMode.FirstPersonView) ? Vector3.zero : shoulderOffset;
            distVelocity = 0f;
            offsetVelocity = Vector3.zero;
        }

        OnModeChanged?.Invoke(controlMode);
    }
}
