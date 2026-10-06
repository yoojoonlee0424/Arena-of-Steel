using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerShooting : MonoBehaviour
{
    public WeaponController Gun;
    public bool isHoldingShoot = false;

    private PlayerInput defaultInput;

    


    private void Awake()
    {
        defaultInput = new PlayerInput();

        defaultInput.OnFoot.Shoot.performed += e => OnShoot();
        defaultInput.OnFoot.ShootRelease.performed += e => OnShootRelease();

        defaultInput.OnFoot.Reload.performed += e => OnReload();

        defaultInput.Enable();



    }

    void OnShoot()
    {
        isHoldingShoot=true;
        
    }

    void OnShootRelease()
    {
        isHoldingShoot = false;
    }

    void OnReload()
    {
        if(Gun != null)
        {
            Gun.TryReload();
            
        }
    }


    // Update is called once per frame
    void Update()
    {
        if (isHoldingShoot == true && Gun != null)
        {
            Gun.Shoot();
        }



    }
}
