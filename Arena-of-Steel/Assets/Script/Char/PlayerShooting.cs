using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerShooting : MonoBehaviour
{
    public WeaponController Gun;
    public bool isHoldingShoot = false;

    private PlayerInput defaultInput;

    private bool isSprint = false;


    private void Awake()
    {
        defaultInput = new PlayerInput();

        /*defaultInput.OnFoot.Shoot.performed += e => OnShoot();
        defaultInput.OnFoot.ShootRelease.performed += e => OnShootRelease();

        defaultInput.OnFoot.Reload.performed += e => OnReload();

        defaultInput.OnFoot.Sprint.performed += e => OnSprint();
        defaultInput.OnFoot.SprintReleased.performed += e => OnSprintReleased();

        defaultInput.Enable();*/



    }

    void OnShoot()
    {
        if (isSprint)
            return;




        isHoldingShoot=true;
        
    }

    void OnShootRelease()
    {
        if(isSprint)
            return ;

        isHoldingShoot = false;
    }

    void OnReload()
    {
        if(isSprint)
            return ;

        if(Gun != null)
        {
            Gun.TryReload();
            
        }
    }

    void OnSprint()
    {
        isSprint = true;
    }

    public void OnSprintReleased()
    {
        isSprint = false;
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
