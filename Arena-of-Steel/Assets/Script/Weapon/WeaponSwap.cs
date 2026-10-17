using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwap : MonoBehaviour
{
    public static event System.Action<GunSO, int> OnWeaponChanged;
    public static event System.Action<bool> OnSlot2Unlocked;

    [SerializeField] private PlayerShooting PlayerShooting;

    public bool isMain = true;
    public bool hasWeapon2 = false;
    public GameObject weapon1;
    public GameObject weapon2;

    private InputSystem_Actions inputActions;
    private bool ownsInputActions;

    private void Awake()
    {
        PlayerShooting = GetComponent<PlayerShooting>();
        if (weapon2 != null) weapon2.SetActive(false);

        var pc = GetComponent<PlayerController>();
        if (pc != null)
        {
            inputActions = pc.InputActions;
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
        OnSlot2Unlocked?.Invoke(hasWeapon2);
        if (PlayerShooting != null && PlayerShooting.Gun1 != null)
        {
            OnWeaponChanged?.Invoke(PlayerShooting.Gun1.GunSO, 1);
        }
    }

    private void OnEnable()
    {
        if (inputActions == null) return;
        inputActions.Player.Weapon1.performed += OnWeapon1Performed;
        inputActions.Player.Weapon2.performed += OnWeapon2Performed;
    }

    private void OnDisable()
    {
        if (inputActions == null) return;
        inputActions.Player.Weapon1.performed -= OnWeapon1Performed;
        inputActions.Player.Weapon2.performed -= OnWeapon2Performed;
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

    private void OnWeapon1Performed(InputAction.CallbackContext _)
    {
        SwitchToWeapon(1);
    }

    private void OnWeapon2Performed(InputAction.CallbackContext _)
    {
        SwitchToWeapon(2);
    }

    public void SwitchToWeapon(int slot)
    {
        if (slot == 1)
        {
            if (PlayerShooting != null && PlayerShooting.Gun2 != null && PlayerShooting.Gun2.isReloadng) return;
            isMain = true;
            if (weapon1 != null) weapon1.SetActive(true);
            if (weapon2 != null) weapon2.SetActive(false);
            if (PlayerShooting != null && PlayerShooting.Gun1 != null)
            {
                PlayerShooting.Gun1.NotifyAmmo();
                OnWeaponChanged?.Invoke(PlayerShooting.Gun1.GunSO, 1);
            }
        }
        else if (slot == 2 && hasWeapon2)
        {
            if (PlayerShooting != null && PlayerShooting.Gun1 != null && PlayerShooting.Gun1.isReloadng) return;
            isMain = false;
            if (weapon1 != null) weapon1.SetActive(false);
            if (weapon2 != null) weapon2.SetActive(true);
            if (PlayerShooting != null && PlayerShooting.Gun2 != null)
            {
                PlayerShooting.Gun2.NotifyAmmo();
                OnWeaponChanged?.Invoke(PlayerShooting.Gun2.GunSO, 2);
            }
        }
    }

    public void EquipWeapon(GunSO newGun, Vector3 dropPosition = default, Quaternion dropRotation = default)
    {
        if (newGun == null) return;

        if (!hasWeapon2)
        {
            hasWeapon2 = true;
            OnSlot2Unlocked?.Invoke(true);

            if (PlayerShooting != null && PlayerShooting.Gun2 != null)
            {
                PlayerShooting.Gun2.SetGunSO(newGun);
                if (PlayerShooting.Aiming2 != null) PlayerShooting.Aiming2.GunSO = newGun;
            }
            SwitchToWeapon(2);
        }
        else
        {
            int targetSlot = isMain ? 1 : 2;
            WeaponController targetGun = isMain ? PlayerShooting.Gun1 : PlayerShooting.Gun2;
            Aiming targetAiming = isMain ? PlayerShooting.Aiming1 : PlayerShooting.Aiming2;

            GunSO oldGun = targetGun != null ? targetGun.GunSO : null;

            if (targetGun != null) targetGun.SetGunSO(newGun);
            if (targetAiming != null) targetAiming.GunSO = newGun;
            OnWeaponChanged?.Invoke(newGun, targetSlot);

            if (oldGun != null && oldGun != newGun)
            {
                Vector3 spawnPos = dropPosition != default ? dropPosition : transform.position + transform.forward * 1.5f;
                Quaternion spawnRot = dropRotation != default ? dropRotation : Quaternion.identity;
                WeaponPickup.Spawn(oldGun, spawnPos, spawnRot);
            }
        }
    }
}
