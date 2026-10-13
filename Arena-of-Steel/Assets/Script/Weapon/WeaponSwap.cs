using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponSwap : MonoBehaviour
{
    [SerializeField] private PlayerShooting PlayerShooting;

    public bool isMain = true;
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
        if (PlayerShooting != null && PlayerShooting.Gun2 != null && PlayerShooting.Gun2.isReloadng) return;

        isMain = true;
        if (weapon1 != null) weapon1.SetActive(true);
        if (weapon2 != null) weapon2.SetActive(false);
    }

    private void OnWeapon2Performed(InputAction.CallbackContext _)
    {
        if (PlayerShooting != null && PlayerShooting.Gun1 != null && PlayerShooting.Gun1.isReloadng) return;

        isMain = false;
        if (weapon1 != null) weapon1.SetActive(false);
        if (weapon2 != null) weapon2.SetActive(true);
    }
}
