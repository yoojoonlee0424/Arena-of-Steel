using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace VoxelDestructionPro.Vox
{
    public class VoxClickToDestroy : MonoBehaviour
    {
        [Header("Input / Camera")]
        [Tooltip("Camera used for raycasting. If null, Camera.main will be used.")]
        public Camera cam;

        [Tooltip("If enabled, holding mouse means continuous fire, Shift + click means a single shot.")]
        public bool useShiftForSingleClick = true;

        [Header("Destruction")]
        [Tooltip("Destruction radius in world units.")]
        public float destructionRadius = 2f;
        public float damage = 25f;
        [Range(0f, 1f)]
        public float distanceFalloff = 0f;

        [Tooltip("If true, isolates loose fragments with physics.")]
        public bool autoIsolateFragments = true;

        [Tooltip("Explosion impulse applied to loose fragments.")]
        public float explosionForce = 5f;

        private void Awake()
        {
            if (cam == null)
                cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            Vector2 mousePos = Vector2.zero;
            bool fireNow = false;

#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            if (mouse == null) return;

            mousePos = mouse.position.ReadValue();
            var keyboard = Keyboard.current;
            bool oneClickMode = useShiftForSingleClick && keyboard != null && keyboard.leftShiftKey.isPressed;

            fireNow = (!oneClickMode && mouse.leftButton.isPressed) ||
                      (oneClickMode && mouse.leftButton.wasPressedThisFrame);
#else
            bool oneClickMode = useShiftForSingleClick && Input.GetKey(KeyCode.LeftShift);
            fireNow = (!oneClickMode && Input.GetMouseButton(0)) ||
                      (oneClickMode && Input.GetMouseButtonDown(0));
            mousePos = Input.mousePosition;
#endif

            if (!fireNow)
                return;

            Vector2 screenPoint = (Cursor.lockState == CursorLockMode.Locked)
                ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
                : mousePos;

            Ray ray = cam.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, 999f))
                return;

            VoxVoxelObject voxelObject = hit.transform.GetComponentInParent<VoxVoxelObject>();
            if (voxelObject == null)
                return;

            VoxelDamageArgs args = new VoxelDamageArgs
            {
                radius = destructionRadius,
                damage = damage,
                distanceFalloff = distanceFalloff,
                isolateFragments = autoIsolateFragments,
                explosionForce = explosionForce,
                fragmentLifetime = 8f,
                impactDirection = ray.direction
            };
            voxelObject.DamageSphere(hit.point, args);
        }
    }
}
