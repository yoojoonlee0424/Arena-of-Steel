using UnityEngine;

public class Aiming : MonoBehaviour
{
    [Header("Weapon / Camera")]
    [SerializeField] private Transform WeaponADSLayer;
    [SerializeField] private Camera _camera;

    [Header("Follow Objects")]
    [SerializeField] private Transform additionalObject1;
    [SerializeField] private Transform additionalObject2;

    [Header("Weapon Data")]
    [SerializeField] private GunSO gunSO;

    public GunSO GunSO
    {
        get => gunSO;
        set => gunSO = value;
    }

    [Header("State")]
    [SerializeField] private bool IsAiming = false;

    private float smoothTime => gunSO != null ? gunSO.aimSmoothTime : 20f;
    private float offsetX => gunSO != null ? gunSO.aimOffsetX : 0f;
    private float offsetY => gunSO != null ? gunSO.aimOffsetY : 0.02f;
    private float offsetZ => gunSO != null ? gunSO.aimOffsetZ : 0f;

    private Vector3 originalWeaponPosition;
    private Vector3 lastWeaponLocalPosition;

    private void Start()
    {
        originalWeaponPosition = WeaponADSLayer.localPosition;
        lastWeaponLocalPosition = originalWeaponPosition;
    }

    private void Update()
    {
        HandleAiming();
        UpdateFollowObjects();
    }

    private void HandleAiming()
    {

        if (IsAiming)
        {
            Vector3 targetScreenPosition = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);

            float distanceFromCamera = Vector3.Distance(WeaponADSLayer.position, _camera.transform.position);
            Vector3 targetWorldPosition = _camera.ScreenToWorldPoint(new Vector3(targetScreenPosition.x, targetScreenPosition.y, distanceFromCamera));

            Vector3 targetLocalPosition = WeaponADSLayer.parent.InverseTransformPoint(targetWorldPosition);

            targetLocalPosition += new Vector3(offsetX, offsetY, offsetZ);
            targetLocalPosition.z = offsetZ;
            WeaponADSLayer.localPosition = Vector3.Lerp(WeaponADSLayer.localPosition, targetLocalPosition, Time.deltaTime * smoothTime);
        }
        else
        {
            WeaponADSLayer.localPosition = Vector3.Lerp(WeaponADSLayer.localPosition, originalWeaponPosition, Time.deltaTime * smoothTime);
        }


    }

    private void UpdateFollowObjects()
    {
        if (WeaponADSLayer == null) return;

        Vector3 deltaPosition = WeaponADSLayer.localPosition - lastWeaponLocalPosition;

        if (IsAiming)
        {
            if (additionalObject1 != null)
            {
                additionalObject1.localPosition += deltaPosition;
            }

            if (additionalObject2 != null)
            {
                additionalObject2.localPosition += deltaPosition;
            }
        }
        else
        {
            if (additionalObject1 != null)
            {
                additionalObject1.localPosition += deltaPosition;
            }

            if (additionalObject2 != null)
            {
                additionalObject2.localPosition += deltaPosition;
            }
        }

        lastWeaponLocalPosition = WeaponADSLayer.localPosition;
    }

    public void UpdateAiming()
    {
        IsAiming = !IsAiming;
    }
}

