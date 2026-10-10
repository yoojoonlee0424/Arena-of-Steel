using System.Collections;
using UnityEngine;



public class WeaponController : MonoBehaviour
{
    public GunSO GunSO;
    /*
    public float reloadTime = 1f;
    public float fireRate = 0.15f;
    public int magsize = 30;

    public GameObject bullet;*/
    public Transform bulletSpawnPoint;

    public Animator anime;
    public Animator WepAnime;

    [SerializeField] private Recoil Recoil;

    private int currentAmmo;
    private bool isReloadng = false;
    private float nextTimeToFire = 0f;

    private bool isAiming;




    private void Start()
    {
        Recoil = FindAnyObjectByType<Recoil>();

        currentAmmo = GunSO.magsize;

    }


    private void Update()
    {

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

        if(Recoil != null)
        {
            float recoilMult = isAiming ? 0.6f : 1f;
            Recoil.AddRecoil(recoilMult);
        }

        nextTimeToFire = Time.time + GunSO.fireRate;
        currentAmmo--;

        anime.SetTrigger("Shooting");
        WepAnime.SetTrigger("Shooting");

        Instantiate(GunSO.bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        Instantiate(GunSO.weaponFlash, bulletSpawnPoint.position, bulletSpawnPoint.rotation);

    }


    IEnumerator Reload()
    {
        isReloadng = true;

        float halfReload = GunSO.reloadTime / 2f;
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

        


        currentAmmo = GunSO.magsize;
        isReloadng = false;

    }

    public void TryReload()
    {
        if (isReloadng)
        {
            return;
        }
        if(currentAmmo == GunSO.magsize)
        {
            return ;
        }


        StartCoroutine(Reload());
    }
}
