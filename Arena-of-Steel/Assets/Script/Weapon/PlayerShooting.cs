using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class PlayerShooting : MonoBehaviour
{
    [SerializeField] private Rig Rig1;
    [SerializeField] private Rig Rig2;

    [SerializeField] private WeaponSwap WeaponSwap;

    public WeaponController Gun1;
    public WeaponController Gun2;
    public Aiming Aiming1;
    public Aiming Aiming2;
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

        //Aiming = GetComponent<Aiming>();
        
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

        if(Gun1 != null && WeaponSwap.isMain == true)
        {
            Gun1.TryReload();
            
        }

        if (Gun2 != null && WeaponSwap.isMain == false)
        {
            Gun2.TryReload();

        }
    }

    void OnAim()
    {
        if(Gun1.isReloadng) return;
        if(Gun2.isReloadng) return;
        if(isSprint) return;

        Aiming1.UpdateAiming();
        Aiming2.UpdateAiming();
        isAim = !isAim;
        Gun1.isAim();
        Gun2.isAim();
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
        if (isHoldingShoot == true && Gun1 != null && WeaponSwap.isMain == true)
        {
            Gun1.Shoot();
        }

        if (isHoldingShoot == true && Gun2 != null && WeaponSwap.isMain == false)
        {
            Gun2.Shoot();
        }

        if (isAim && isSprint)
        {
            Aiming1.UpdateAiming();
            Aiming2.UpdateAiming();
            isAim = false;
        }

        if (Gun1.isReloadng)
        {
            Rig1.weight = 0;
        }
        else
        {
            Rig1.weight = 1;
        }

        if (Gun2.isReloadng)
        {
            Rig2.weight = 0;
        }
        else
        {
            Rig2.weight = 1;
        }
    }
}
