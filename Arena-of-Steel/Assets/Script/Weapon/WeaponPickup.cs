using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponPickup : MonoBehaviour
{
    public static event System.Action<string> OnPromptChanged;
    private static WeaponPickup activePickup;

    public GunSO gunData;
    [SerializeField] private float pickupRadius = 2.5f;

    private int spawnFrame;
    private static Transform playerTransform;
    private static WeaponSwap playerWeaponSwap;
    private static InputSystem_Actions inputActions;

    public void Init(GunSO gun)
    {
        if (gun != null) gunData = gun;
    }

    private void Awake()
    {
        spawnFrame = Time.frameCount;
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.isTrigger = true;
        }
    }

    private void Update()
    {
        if (Time.frameCount == spawnFrame) return;

        if (playerTransform == null || playerWeaponSwap == null)
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null) return;

            playerTransform = playerObj.transform;
            playerWeaponSwap = playerObj.GetComponentInParent<WeaponSwap>();
            var pc = playerObj.GetComponentInParent<PlayerController>();
            if (pc != null)
            {
                inputActions = pc.InputActions;
            }
        }

        float dist = Vector3.Distance(transform.position, playerTransform.position);
        if (dist <= pickupRadius)
        {
            if (activePickup == null || activePickup == this || dist < Vector3.Distance(activePickup.transform.position, playerTransform.position))
            {
                if (activePickup != this)
                {
                    activePickup = this;
                    UpdatePrompt();
                }

                bool interactPressed = inputActions != null
                    ? inputActions.Player.Interact.WasPressedThisFrame()
                    : (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

                if (interactPressed)
                {
                    Pickup();
                }
            }
        }
        else if (activePickup == this)
        {
            activePickup = null;
            OnPromptChanged?.Invoke(string.Empty);
        }
    }

    private void UpdatePrompt()
    {
        string key = GetInteractKeyName();
        string gunName = gunData != null ? gunData.name : "Weapon";
        OnPromptChanged?.Invoke($"[{key}] Get {gunName}");
    }

    private string GetInteractKeyName()
    {
        if (inputActions != null && inputActions.Player.Interact != null)
        {
            var action = inputActions.Player.Interact;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];
                if (!string.IsNullOrEmpty(b.groups) && b.groups.Contains("Keyboard"))
                {
                    string display = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions);
                    if (!string.IsNullOrEmpty(display)) return display;
                }
            }

            string fallback = action.GetBindingDisplayString(InputBinding.DisplayStringOptions.DontIncludeInteractions);
            if (!string.IsNullOrEmpty(fallback)) return fallback;
        }
        return "E";
    }

    private void Pickup()
    {
        if (playerWeaponSwap != null && gunData != null)
        {
            playerWeaponSwap.EquipWeapon(gunData, transform.position, transform.rotation);
        }

        if (activePickup == this)
        {
            activePickup = null;
            OnPromptChanged?.Invoke(string.Empty);
        }

        Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (activePickup == this)
        {
            activePickup = null;
            OnPromptChanged?.Invoke(string.Empty);
        }
    }

    private void OnDestroy()
    {
        if (activePickup == this)
        {
            activePickup = null;
            OnPromptChanged?.Invoke(string.Empty);
        }
    }

    public static WeaponPickup Spawn(GunSO gun, Vector3 position, Quaternion rotation)
    {
        if (gun == null || gun.pickupPrefab == null) return null;

        if (UnityEngine.AI.NavMesh.SamplePosition(position, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            position = hit.position;
        }

        GameObject obj = Instantiate(gun.pickupPrefab, position, rotation);
        WeaponPickup pickup = obj.GetComponent<WeaponPickup>();
        if (pickup != null)
        {
            pickup.Init(gun);
        }
        return pickup;
    }
}
