using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private Rig Rig;

    public WeaponController Gun;
    public Aiming Aiming;
    public bool isHoldingShoot = false;

    private PlayerInput defaultInput;

    private bool isSprint = false;
    private bool isAim = false;



    private void Awake()
    {


        //defaultInput = new PlayerInput();

        /*defaultInput.OnFoot.Shoot.performed += e => OnShoot();
        defaultInput.OnFoot.ShootRelease.performed += e => OnShootRelease();

        defaultInput.OnFoot.Reload.performed += e => OnReload();

        defaultInput.OnFoot.Sprint.performed += e => OnSprint();
        defaultInput.OnFoot.SprintReleased.performed += e => OnSprintReleased();

        defaultInput.Enable();*/

        Aiming = GetComponent<Aiming>();
        
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

        if (isAim)
            return;

        if(Gun != null)
        {
            Gun.TryReload();
            
        }
    }

    void OnAim()
    {
        if(Gun.isReloadng) return;
        if(isSprint) return;

        Aiming.UpdateAiming();
        isAim = !isAim;
        Gun.isAim();
    }

    //단순 확인용
    void OnSprint()
    {
        isSprint = true;
    }

    void OnSprintReleased()
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

        if (isAim && isSprint)
        {
            Aiming.UpdateAiming();
            isAim = false;
        }

        if (Gun.isReloadng)
        {
            Rig.weight = 0;
        }
        else
        {
            Rig.weight = 1; 
        }
    }
}
