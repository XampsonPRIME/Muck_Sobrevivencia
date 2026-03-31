using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 6f;
    public float runSpeed = 10f;
    public float jumpForce = 2f;
    public float gravity = -9.8f;

    [Header("Mouse")]
    public float mouseSensitivity = 0.44f;
    public Transform cameraHolder;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;

    CharacterController controller;
    PlayerControls controls;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation = 0f;

    bool isRunning;
    bool jumpPressed;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();

        currentStamina = maxStamina;

        // MOVIMENTO
        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        // OLHAR
        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        // CORRER
        controls.Player.Run.performed += ctx => isRunning = true;
        controls.Player.Run.canceled += ctx => isRunning = false;

        // PULAR
        controls.Player.Jump.performed += ctx => jumpPressed = true;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (cameraHolder == null)
        {
            Debug.LogError("CameraHolder não foi atribuído!");
        }
    }

    void Update()
    {
        HandleStamina();
        Move();
        Look();
    }

    void HandleStamina()
    {
        bool isMoving = moveInput.magnitude > 0.1f;

        // Só corre se tiver stamina
        bool canRun = currentStamina > 0;

        if (isRunning && isMoving && canRun)
        {
            currentStamina -= staminaDrain * Time.deltaTime;

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                isRunning = false;
            }
        }
        else
        {
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRecovery * Time.deltaTime;
            }
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
    }

    void Move()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && yVelocity < 0)
        {
            yVelocity = -2f;
        }

        // PULO
        if (jumpPressed && isGrounded)
        {
            yVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            jumpPressed = false;
        }

        // GRAVIDADE
        yVelocity += gravity * Time.deltaTime;
        yVelocity = Mathf.Clamp(yVelocity, -50f, 50f);

        // DIREÇÃO
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        bool isMoving = moveInput.magnitude > 0.1f;
        bool canRun = currentStamina > 0;

        // VELOCIDADE FINAL
        float currentSpeed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        Vector3 velocity = move * currentSpeed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * 100f * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }
}