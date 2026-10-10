using UnityEngine;
using Unity.Cinemachine;

public class CameraModeController : MonoBehaviour
{
    [Header("시네머신 카메라")]
    [SerializeField] private CinemachineCamera firstPersonCamera;
    [SerializeField] private CinemachineCamera thirdPersonCamera;

    private void Awake()
    {
        FindCameras();
    }

    private void FindCameras()
    {
        if (firstPersonCamera != null && thirdPersonCamera != null) return;

        var cameras = FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None);
        foreach (var cam in cameras)
        {
            if (cam == null) continue;
            string camName = cam.gameObject.name.ToLower();
            if (firstPersonCamera == null && camName.Contains("first"))
                firstPersonCamera = cam;
            else if (thirdPersonCamera == null && camName.Contains("third"))
                thirdPersonCamera = cam;
        }
    }

    public void BindTrackingTarget(Transform target)
    {
        if (target == null) return;
        if (firstPersonCamera != null) firstPersonCamera.Target.TrackingTarget = target;
        if (thirdPersonCamera != null) thirdPersonCamera.Target.TrackingTarget = target;
    }

    public void SetCameraMode(ControlMode mode)
    {
        bool isFirstPerson = mode == ControlMode.FirstPersonView;

        if (firstPersonCamera != null)
            firstPersonCamera.Priority.Value = isFirstPerson ? 10 : 0;

        if (thirdPersonCamera != null)
            thirdPersonCamera.Priority.Value = isFirstPerson ? 0 : 10;
    }
}
