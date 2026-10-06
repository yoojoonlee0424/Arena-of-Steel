using System.Collections;
using UnityEngine;
using static PlayerModel;



public class WeaponController : MonoBehaviour
{
    private PlayerController characterController;


    [Header("¼³Á¤")]
    public WeaponSettingsModel WeaponSet;

    bool isInitialised;

    Vector3 newWeaponRotation;
    Vector3 newWeaponRotationVelocity;

    Vector3 targetWeaponRotation;
    Vector3 targetWeaponRotationVelocity;

    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magsize = 30;

    public GameObject bullet;
    public Transform bulletSpawnPoint;

    public GameObject weaponFlash;

    private int currentAmmo;
    private bool isReloadng = false;
    private float nextTimeToFire = 0f;


    public Animator anime;
    public Animator WepAnime;



    private void Start()
    {
        newWeaponRotation = transform.localRotation.eulerAngles;


        currentAmmo = magsize;

    }


    public void Initialise(PlayerController CharacterController)
    {
        characterController = CharacterController;
        isInitialised = true;
    }


    private void Update()
    {
        if (!isInitialised)
        {
            return;
        }

        targetWeaponRotation.y += WeaponSet.SwayAmount * characterController.input_View.x * Time.deltaTime;
        targetWeaponRotation.x += WeaponSet.SwayAmount * characterController.input_View.y * Time.deltaTime;

        targetWeaponRotation.x = Mathf.Clamp(targetWeaponRotation.x, -WeaponSet.SwayClampX, WeaponSet.SwayClampX);
        targetWeaponRotation.x = Mathf.Clamp(targetWeaponRotation.y, -WeaponSet.SwayClampY, WeaponSet.SwayClampY);

        targetWeaponRotation = Vector3.SmoothDamp(targetWeaponRotation, Vector3.zero, ref targetWeaponRotationVelocity, WeaponSet.SwayResetSmoothing);

        newWeaponRotation = Vector3.SmoothDamp(newWeaponRotation, targetWeaponRotation, ref newWeaponRotationVelocity, WeaponSet.SwaySmoothing);


        transform.localRotation = Quaternion.Euler(newWeaponRotation);



    }


    public void Shoot()
    {
        if (isReloadng)
        {
            return;
        }

        if (Time.time < nextTimeToFire)
        {
            return;
        }

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
        }

        nextTimeToFire = Time.time + fireRate;
        currentAmmo--;

        anime.SetTrigger("Shooting");
        WepAnime.SetTrigger("Shooting");

        anime.SetBool("isWalk", false);

        Instantiate(bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        Instantiate(weaponFlash, bulletSpawnPoint.position, bulletSpawnPoint.rotation);

    }


    IEnumerator Reload()
    {
        isReloadng = true;

        float halfReload = reloadTime / 2f;
        float t = 0f;

        anime.SetTrigger("Reload");
        WepAnime.SetTrigger("Reload");


        while (t < halfReload)
        {
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;

        while (t < halfReload)
        {
            t += Time.deltaTime;
            yield return null;
        }

        


        currentAmmo = magsize;
        isReloadng = false;

    }

    public void TryReload()
    {
        if (isReloadng)
        {
            return;
        }
        if(currentAmmo == magsize)
        {
            return ;
        }


        StartCoroutine(Reload());
    }
}
