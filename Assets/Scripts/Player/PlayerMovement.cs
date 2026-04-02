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
    public float mouseSensitivity = 0.3f;
    public Transform cameraHolder;

    [Header("Camera Advanced")]
    public float smoothTime = 0.05f;
    public float headbobSpeed = 10f;
    public float headbobAmount = 0.05f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;

    [Header("❤️ Vida")]
    public float maxHealth = 100f;
    public float currentHealth;

    CharacterController controller;
    PlayerControls controls;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation = 0f;

    bool isRunning;
    bool jumpPressed;

    float headbobTimer;
    Vector3 cameraStartPos;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();

        currentStamina = maxStamina;
        currentHealth = maxHealth;

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        controls.Player.Run.performed += ctx => isRunning = true;
        controls.Player.Run.canceled += ctx => isRunning = false;

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
            return;
        }

        cameraStartPos = cameraHolder.localPosition;
    }

    void Update()
    {
        HandleStamina();
        Move();
        Look();
        HandleHeadbob();

        // 🔥 TESTE DE DANO
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            TakeDamage(10f);
        }
    }

    // ❤️ VIDA
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
    }

    void Die()
    {
        Debug.Log("Player morreu");
    }

    // 🔋 STAMINA
    void HandleStamina()
    {
        bool isMoving = moveInput.magnitude > 0.1f;
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

    // 🏃 MOVIMENTO
    void Move()
    {
        bool isGrounded = controller.isGrounded;

        if (isGrounded && yVelocity < 0)
            yVelocity = -2f;

        if (jumpPressed && isGrounded)
        {
            yVelocity = Mathf.Sqrt(jumpForce * -2f * gravity);
            jumpPressed = false;
        }

        yVelocity += gravity * Time.deltaTime;
        yVelocity = Mathf.Clamp(yVelocity, -50f, 50f);

        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        bool isMoving = moveInput.magnitude > 0.1f;
        bool canRun = currentStamina > 0;

        float currentSpeed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        Vector3 velocity = move * currentSpeed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    // 🎥 LOOK
    void Look()
    {
        if (cameraHolder == null) return;

        float mouseX = Mathf.Clamp(lookInput.x, -10f, 10f) * mouseSensitivity;
        float mouseY = Mathf.Clamp(lookInput.y, -10f, 10f) * mouseSensitivity;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);
    }

    // 🎥 HEADBOB
    void HandleHeadbob()
    {
        if (controller.isGrounded && moveInput.magnitude > 0.1f)
        {
            headbobTimer += Time.deltaTime * headbobSpeed;

            float bobX = Mathf.Cos(headbobTimer) * headbobAmount;
            float bobY = Mathf.Sin(headbobTimer * 2f) * headbobAmount;

            cameraHolder.localPosition = cameraStartPos + new Vector3(bobX, bobY, 0);
        }
        else
        {
            headbobTimer = 0f;

            cameraHolder.localPosition = Vector3.Lerp(
                cameraHolder.localPosition,
                cameraStartPos,
                Time.deltaTime * 5f
            );
        }
    }
}