using UnityEngine;

public class AnimationController : MonoBehaviour
{
    [SerializeField] private float dampTime = 0.4f;
    private CharacterController controller;
    private PlayerController playerController;

    private float horizontalSpeed;

    public Animator animator;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller = GetComponent<CharacterController>();
        playerController = GetComponent<PlayerController>();
    }

    // Update is called once per frame
    void Update()
    {

        if (controller != null && playerController.isDashing == false)
        {
            Vector3 horizontalVelocity = new Vector3(controller.velocity.x, 0, controller.velocity.z);
            horizontalSpeed = horizontalVelocity.magnitude;

            animator.SetFloat("Speed", horizontalSpeed, dampTime, Time.deltaTime);

        }
        
    }
}
