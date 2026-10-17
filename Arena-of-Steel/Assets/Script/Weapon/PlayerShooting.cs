using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;

public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private Rig Rig1;
    [SerializeField] private Rig Rig2;
    [SerializeField] private WeaponSwap WeaponSwap;

    public WeaponController Gun1;
    public WeaponController Gun2;
    public Aiming Aiming1;
    public Aiming Aiming2;
    public bool isHoldingShoot;

    private PlayerController playerController;
    private bool isSprint => playerController != null && playerController.isRunning;
    private bool isAim;
    private InputSystem_Actions inputActions;
    private bool ownsInputActions;

    private void Awake()
    {
        if (WeaponSwap == null)
            WeaponSwap = GetComponent<WeaponSwap>();

        playerController = GetComponent<PlayerController>();
        if (playerController != null)
        {
            inputActions = playerController.InputActions;
        }
        else
        {
            inputActions = new InputSystem_Actions();
            inputActions.Player.Enable();
            ownsInputActions = true;
        }
    }

    private void Start()
    {
        if (Aiming1 != null && Gun1 != null) Aiming1.GunSO = Gun1.GunSO;
        if (Aiming2 != null && Gun2 != null) Aiming2.GunSO = Gun2.GunSO;
    }

    private void OnEnable()
    {
        if (inputActions == null) return;
        inputActions.Player.Attack.started += OnAttackStarted;
        inputActions.Player.Attack.canceled += OnAttackCanceled;
        inputActions.Player.Reload.performed += OnReloadPerformed;
        inputActions.Player.Aim.performed += OnAimPerformed;
    }

    private void OnDisable()
    {
        if (inputActions == null) return;
        inputActions.Player.Attack.started -= OnAttackStarted;
        inputActions.Player.Attack.canceled -= OnAttackCanceled;
        inputActions.Player.Reload.performed -= OnReloadPerformed;
        inputActions.Player.Aim.performed -= OnAimPerformed;
        isHoldingShoot = false;
    }

    private void OnDestroy()
    {
        if (ownsInputActions && inputActions != null)
        {
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }
    }

    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        if (isSprint) return;
        isHoldingShoot = true;
    }

    private void OnAttackCanceled(InputAction.CallbackContext _)
    {
        isHoldingShoot = false;
    }

    private void OnReloadPerformed(InputAction.CallbackContext _)
    {
        if (isSprint || isAim) return;

        if (Gun1 != null && WeaponSwap != null && WeaponSwap.isMain)
            Gun1.TryReload();

        if (Gun2 != null && WeaponSwap != null && !WeaponSwap.isMain)
            Gun2.TryReload();
    }

    private void OnAimPerformed(InputAction.CallbackContext _)
    {
        if (Gun1 != null && Gun1.isReloadng) return;
        if (Gun2 != null && Gun2.isReloadng) return;
        if (isSprint) return;

        if (Aiming1 != null) Aiming1.UpdateAiming();
        if (Aiming2 != null) Aiming2.UpdateAiming();
        isAim = !isAim;
        if (Gun1 != null) Gun1.isAim();
        if (Gun2 != null) Gun2.isAim();
    }

    private void Update()
    {
        if (isHoldingShoot && Gun1 != null && WeaponSwap != null && WeaponSwap.isMain)
        {
            Gun1.Shoot();
        }

        if (isHoldingShoot && Gun2 != null && WeaponSwap != null && !WeaponSwap.isMain)
        {
            Gun2.Shoot();
        }

        if (isAim && isSprint)
        {
            if (Aiming1 != null) Aiming1.UpdateAiming();
            if (Aiming2 != null) Aiming2.UpdateAiming();
            isAim = false;
            if (Gun1 != null) Gun1.isAim();
            if (Gun2 != null) Gun2.isAim();
        }

        if (Rig1 != null && Gun1 != null)
        {
            Rig1.weight = Gun1.isReloadng ? 0 : 1;
        }

        if (Rig2 != null && Gun2 != null)
        {
            Rig2.weight = Gun2.isReloadng ? 0 : 1;
        }
    }
}
