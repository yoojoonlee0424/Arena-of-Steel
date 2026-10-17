using System.Collections;
using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public GunSO GunSO;
    public Transform bulletSpawnPoint;
    public Animator anime;
    public Animator WepAnime;

    [SerializeField] private Recoil Recoil;

    public static event System.Action<WeaponController, int, int> OnAmmoChanged;

    public bool isReloadng = false;
    private int currentAmmo;
    private float nextTimeToFire = 0f;
    private bool isAiming = false;

    public int CurrentAmmo => currentAmmo;
    public int MaxAmmo => GunSO != null ? GunSO.magsize : 0;

    private void Start()
    {
        if (GunSO != null) currentAmmo = GunSO.magsize;
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);
    }

    public void SetGunSO(GunSO newGun)
    {
        GunSO = newGun;
        if (GunSO != null) currentAmmo = GunSO.magsize;
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);
    }

    public void NotifyAmmo()
    {
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);
    }

    public void isAim()
    {
        isAiming = !isAiming;
    }

    public void Shoot()
    {
        if (isReloadng || Time.time < nextTimeToFire)
        {
            return;
        }

        if (currentAmmo <= 0 && !isAiming)
        {
            StartCoroutine(Reload());
        }

        if (currentAmmo <= 0)
        {
            return;
        }

        if (Recoil != null)
        {
            float recoilMult = isAiming ? 0.6f : 1f;
            Recoil.AddRecoil(recoilMult);
        }

        nextTimeToFire = Time.time + GunSO.fireRate;
        currentAmmo--;
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);

        anime.SetTrigger("Shooting");
        WepAnime.SetTrigger("Shooting");

        GameObject bulletObj = Instantiate(GunSO.bullet, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        Bullet bulletComp = bulletObj.GetComponent<Bullet>();
        if (bulletComp != null)
        {
            bulletComp.Init(GunSO.damage, gameObject);
        }

        if (GunSO.weaponFlash != null)
        {
            Instantiate(GunSO.weaponFlash, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        }
    }

    private IEnumerator Reload()
    {
        isReloadng = true;
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);
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
        OnAmmoChanged?.Invoke(this, currentAmmo, MaxAmmo);
    }

    public void TryReload()
    {
        if (isReloadng || currentAmmo == GunSO.magsize)
        {
            return;
        }

        StartCoroutine(Reload());
    }
}