using UnityEngine;
using static PlayerModel;

public class PlayerController : MonoBehaviour
{
    private CharacterController characterController;

    private PlayerInput defaultInput;

    public Vector2 input_Movement;
    [HideInInspector]
    public Vector2 input_View;


    private Vector3 newCamRotation;
    private Vector3 newCharactorRotation;

    [Header("Ref")]
    public Transform camHolder;
    public Transform feetTransfrom;

    [Header("설정")]
    public PlayerSettingsModel playerSet;

    public float viewClampYmin= -70;
    public float viewClampYmax= 80;

    public LayerMask playerMask;

    [Header("중력")]
    public float gravityAmount;
    public float gravityMin;
    private float playerGravity;

    public Vector3 jumpingForce;
    private Vector3 jumpingForceVelocity;

    [Header("자세")]
    public PlayerStance playerStance;

    public float playerStanceSmoothing;

    public CharacterStance PlayerStandStance;
    public CharacterStance PlayerCrouchStance;
    public CharacterStance PlayerProneStance;

    private float stanceCheckForError = 0.05f;

    private float cameraHeight;
    private float cameraHeightVelocity;

    private Vector3 stanceCapsuleCenterVelocity;
    private float stanceCapsuleHeightVelocity;

    private bool isSprinting;

    private Vector3 newMovementSpeed;
    private Vector3 newMovementSpeedVelocity;


    [Header("Weapon")]
    public WeaponController currentWeapon;

    public Animator anime;


    private void Awake()
    {
        defaultInput = new PlayerInput();

        defaultInput.OnFoot.Movement.performed += e => input_Movement = e.ReadValue<Vector2>();
        defaultInput.OnFoot.View.performed += e => input_View = e.ReadValue<Vector2>();
        defaultInput.OnFoot.Jump.performed += e => Jump();

        defaultInput.OnFoot.Crouch.performed += e => Crouch();
        defaultInput.OnFoot.Prone.performed += e => Prone();

        defaultInput.OnFoot.Sprint.performed += e => ToggleSprint();
        defaultInput.OnFoot.SprintReleased.performed += e => StopSprint();

        defaultInput.Enable();


        newCamRotation = camHolder.localRotation.eulerAngles;
        newCharactorRotation = transform.localRotation.eulerAngles;

        characterController = GetComponent<CharacterController>();

        cameraHeight = camHolder.localPosition.y;


        if(currentWeapon)
        {
            currentWeapon.Initialise(this);
        }



        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;




    }

    private void Update()
    {
        CalculateView();
        CalculateMovement();
        CalculateJump();
        CalculateStance();







        if (!isSprinting)
        {
            if (Mathf.Abs(input_Movement.x) > 0 || Mathf.Abs(input_Movement.y) > 0)
            {
                anime.SetBool("isWalk", true);
            }
            else if (input_Movement.x == 0 || input_Movement.y == 0)
            {
                anime.SetBool("isWalk", false);
            }
        }


        if(isSprinting)
        {
            anime.SetBool("Running", true);
        }
        else
        {
            anime.SetBool("Running", false);
        }

    }


    private void CalculateView()
    {

        newCharactorRotation.y += playerSet.ViewXSensitivity * input_View.x * Time.deltaTime;
        transform.localRotation = Quaternion.Euler(newCharactorRotation);


        newCamRotation.x += playerSet.ViewYSensitivity * input_View.y * Time.deltaTime;

        newCamRotation.x = Mathf.Clamp(newCamRotation.x, viewClampYmin, viewClampYmax);

        camHolder.localRotation = Quaternion.Euler(newCamRotation);

        


    }


    private void CalculateMovement()
    {

        if(input_Movement.y <= 0.2f)
        {
            isSprinting = false;
        }




        var verticalSpeed = playerSet.WalkingFowardSpeed;
        var horizontalSpeed = playerSet.WalkingStrafeSpeed;

        if(isSprinting)
        {
            verticalSpeed = playerSet.RunningFowardSpeed;
            horizontalSpeed = playerSet.RunningStrafeSpeed;
        }

        if(!characterController.isGrounded)
        {
            playerSet.SpeedEffector = playerSet.FallingSpeedEffector;
        }
        else if(playerStance == PlayerStance.Crouching)
        {
            playerSet.SpeedEffector = playerSet.CrouchSpeedEffector;
        }
        else if (playerStance == PlayerStance.Prone)
        {
            playerSet.SpeedEffector = playerSet.ProneSpeedEffector;
        }
        else
        {
            playerSet.SpeedEffector = 1;
        }

        verticalSpeed *= playerSet.SpeedEffector;
        horizontalSpeed *= playerSet.SpeedEffector;



        


        newMovementSpeed = Vector3.SmoothDamp(newMovementSpeed,
            new Vector3(horizontalSpeed * input_Movement.x * Time.deltaTime, 0, verticalSpeed * input_Movement.y * Time.deltaTime), 
            ref newMovementSpeedVelocity, characterController.isGrounded ? playerSet.MovementSmoothing : playerSet.FallingSmoothing);

        var MovementSpeed = transform.TransformDirection(newMovementSpeed);


        if (playerGravity > gravityMin)
        {
            playerGravity -= gravityAmount * Time.deltaTime;
        }

        

        if(playerGravity < -0.1f && characterController.isGrounded)
        {
            playerGravity = -0.1f;
        }
     

        MovementSpeed.y += playerGravity;

        MovementSpeed += jumpingForce * Time.deltaTime;





        characterController.Move(MovementSpeed);

    }

    private void CalculateJump()
    {
        jumpingForce = Vector3.SmoothDamp(jumpingForce,Vector3.zero, ref jumpingForceVelocity, playerSet.JumpingFalloff);
    }



    private void CalculateStance()
    {
        var currentStance = PlayerStandStance;

        if (playerStance == PlayerStance.Crouching)
        {
            currentStance = PlayerCrouchStance;
        }
        else if(playerStance == PlayerStance.Prone)
        {
            currentStance = PlayerProneStance;
        }


        cameraHeight = Mathf.SmoothDamp(camHolder.localPosition.y, currentStance.CameraHeight, ref cameraHeightVelocity, playerStanceSmoothing);

        camHolder.localPosition = new Vector3(camHolder.localPosition.x, cameraHeight, camHolder.localPosition.z);


        characterController.height = Mathf.SmoothDamp(characterController.height, currentStance.StanceCollider.height,ref stanceCapsuleHeightVelocity, playerStanceSmoothing);
        characterController.center = Vector3.SmoothDamp(characterController.center, currentStance.StanceCollider.center,ref stanceCapsuleCenterVelocity, playerStanceSmoothing);




    }




    private void Jump()
    {
        if(!characterController.isGrounded || playerStance == PlayerStance.Prone)
        {
            return;
        }

        if(playerStance == PlayerStance.Crouching)
        {
            if (StandCheack(PlayerStandStance.StanceCollider.height))
            {
                return;
            }


            playerStance = PlayerStance.Standing;
            return;
        }



        jumpingForce = Vector3.up * playerSet.JumpingHeight;
        playerGravity = 0;

    }


    private void Crouch()
    {
        if(playerStance ==  PlayerStance.Crouching)
        {
            if (StandCheack(PlayerStandStance.StanceCollider.height))
            {
                return;
            }



            playerStance = PlayerStance.Standing;
            return;
        }

        if (StandCheack(PlayerCrouchStance.StanceCollider.height))
        {
            return;
        }


        playerStance = PlayerStance.Crouching;
    }


    private void Prone()
    {
        playerStance = PlayerStance.Prone;
    }

    private bool StandCheack(float stanceCheckheight)
    {
        var start = new Vector3(feetTransfrom.position.x,feetTransfrom.position.y + characterController.radius + stanceCheckForError, feetTransfrom.position.z);
        var end = new Vector3(feetTransfrom.position.x, feetTransfrom.position.y - characterController.radius - stanceCheckForError + stanceCheckheight, feetTransfrom.position.z);





        return Physics.CheckCapsule(start,end,characterController.radius, playerMask);
    }

    private void ToggleSprint()
    {
        if (input_Movement.y <= 0.2f)
        {
            isSprinting = false;
            return;
        }





        isSprinting = !isSprinting;
    }

    private void StopSprint()
    {
        if(playerSet.SprintingHold)
        {
            isSprinting = false;
        }



    }



}
