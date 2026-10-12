using UnityEngine;

public class WeaponSwap : MonoBehaviour
{
    [SerializeField] private PlayerShooting PlayerShooting;

    public bool isMain = true;

    public GameObject weapon1;
    public GameObject weapon2;

    private void Awake()
    {
        PlayerShooting = GetComponent<PlayerShooting>();
        weapon2.SetActive(false);
    }


    void OnWeapon1()
    {
        if (PlayerShooting.Gun2.isReloadng)
        {
            return;
        }
        isMain = true;
        weapon1.SetActive(true);
        weapon2.SetActive(false);
    }

    void OnWeapon2()
    {
        if (PlayerShooting.Gun1.isReloadng)
        {
            return;
        }
        isMain =false;
        weapon1.SetActive(false);
        weapon2.SetActive(true);
    }
}
