using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movimento")]
    public float walkSpeed = 4f;
    public float runSpeed = 8f;
    public float gravity = -9.8f;

    [Header("Mouse")]
    public float mouseSensitivity = 2f;

    [Header("Camera")]
    public Transform cameraHolder;
    public bool thirdPerson = false;

    public Vector3 firstPersonOffset = new Vector3(0, 1.6f, 0);
    public Vector3 thirdPersonOffset = new Vector3(0, 2f, -3f);

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;

    [Header("❤️ Vida")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("🍗 Fome")]
    public float maxHunger = 100f;
    public float currentHunger;

    public float hungerDrain = 5f; // por segundo
    public float hungerDamageRate = 10f; // dano quando zerado

    CharacterController controller;
    PlayerControls controls;
    Animator anim;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation;
    bool isRunning;

    GameObject playerModel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();
        anim = GetComponentInChildren<Animator>();

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentHunger = maxHunger;

        playerModel = anim.gameObject;

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Player.Look.canceled += ctx => lookInput = Vector2.zero;

        controls.Player.Run.performed += ctx => isRunning = true;
        controls.Player.Run.canceled += ctx => isRunning = false;
    }

    void OnEnable() => controls.Enable();
    void OnDisable() => controls.Disable();

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // alternar câmera
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            thirdPerson = !thirdPerson;
        }

        HandleStamina();
        Move();
        Look();
        HandleModelVisibility();
        HandleHunger();

        // 🔥 TESTE DE DANO
        if (Keyboard.current.hKey.wasPressedThisFrame)
        {
            TakeDamage(10f);
        }
    }
    
    void HandleHunger()
    {
        // diminui fome
        currentHunger -= hungerDrain * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        // se zerou → perde vida
        if (currentHunger <= 0)
        {
            TakeDamage(hungerDamageRate * Time.deltaTime);
        }
    }

    public void Heal(float amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        Debug.Log("Curou: " + currentHealth);
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            Die();
        }
    }

    void Die()
    {
        Debug.Log("Player morreu");
    }

    void HandleModelVisibility()
    {
        if (!thirdPerson)
            playerModel.SetActive(false); // FPS
        else
            playerModel.SetActive(true);  // TPS
    }

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
                currentStamina += staminaRecovery * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0, maxStamina);
    }

    // 🏃 MOVIMENTO
    void Move()
    {
        bool grounded = controller.isGrounded;

        if (grounded && yVelocity < 0)
            yVelocity = -2f;

        yVelocity += gravity * Time.deltaTime;

        Vector3 move;

        if (!thirdPerson)
        {
            // FPS
            Vector3 forward = cameraHolder.forward;
            Vector3 right = cameraHolder.right;

            forward.y = 0;
            right.y = 0;

            move = forward * moveInput.y + right * moveInput.x;
        }
        else
        {
            // TPS
            Vector3 forward = cameraHolder.forward;
            forward.y = 0;

            move = forward * moveInput.y;
        }

        bool isMoving = moveInput.magnitude > 0.1f;
        bool canRun = currentStamina > 0;

        float speed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        Vector3 velocity = move.normalized * speed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);

        float animSpeed;

        if (isRunning && moveInput.magnitude > 0.1f)
            animSpeed = 1f; // RUN
        else
            animSpeed = moveInput.magnitude * 0.5f; // WALK

        anim.SetFloat("Speed", animSpeed);
    }

    // 🎥 LOOK
    void Look()
    {
        float mouseX = lookInput.x * mouseSensitivity * 100f * Time.deltaTime;
        float mouseY = lookInput.y * mouseSensitivity * 100f * Time.deltaTime;

        if (!thirdPerson)
        {
            // FPS
            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f);

            cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            cameraHolder.localPosition = firstPersonOffset;

            transform.Rotate(Vector3.up * mouseX);
        }
        else
        {
            // TPS
            transform.Rotate(Vector3.up * mouseX);

            cameraHolder.localRotation = Quaternion.Euler(15f, 0f, 0f);
            cameraHolder.localPosition = thirdPersonOffset;
        }
    }
}