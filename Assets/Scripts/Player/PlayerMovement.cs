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
    public float thirdPersonPitch = 15f;

    public Vector3 firstPersonOffset = new Vector3(0, 1.6f, 0);
    public Vector3 thirdPersonOffset = new Vector3(0, 2f, -3f);

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float currentStamina;
    public float staminaDrain = 20f;
    public float staminaRecovery = 15f;

    [Header("Vida")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Fome")]
    public float maxHunger = 100f;
    public float currentHunger;

    public float hungerDrain = 5f;
    public float hungerDamageRate = 10f;

    CharacterController controller;
    PlayerControls controls;
    Animator anim;
    InputAction toggleCameraAction;
    InputAction damageTestAction;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation;
    float yRotation;
    bool isRunning;

    GameObject playerModel;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();
        anim = GetComponentInChildren<Animator>();
        toggleCameraAction = new InputAction("ToggleCamera", binding: "<Keyboard>/c");
        damageTestAction = new InputAction("DamageTest", binding: "<Keyboard>/h");

        currentStamina = maxStamina;
        currentHealth = maxHealth;
        currentHunger = maxHunger;

        playerModel = anim.gameObject;
    }

    void OnEnable()
    {
        controls.Enable();
        toggleCameraAction.Enable();
        damageTestAction.Enable();
    }

    void OnDisable()
    {
        damageTestAction.Disable();
        toggleCameraAction.Disable();
        controls.Disable();
    }

    void Start()
    {
        yRotation = transform.eulerAngles.y;
        xRotation = thirdPerson ? thirdPersonPitch : 0f;
        ApplyCameraPose();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        moveInput = controls.Player.Move.ReadValue<Vector2>();
        lookInput = controls.Player.Look.ReadValue<Vector2>();
        isRunning = controls.Player.Run.IsPressed();

        if (toggleCameraAction.WasPressedThisFrame())
        {
            thirdPerson = !thirdPerson;
            xRotation = thirdPerson ? thirdPersonPitch : Mathf.Clamp(xRotation, -80f, 80f);
        }

        HandleStamina();
        Look();
        Move();
        HandleModelVisibility();
        HandleHunger();

        if (damageTestAction.WasPressedThisFrame())
        {
            TakeDamage(10f);
        }
    }

    void LateUpdate()
    {
        ApplyCameraPose();
    }

    void HandleHunger()
    {
        currentHunger -= hungerDrain * Time.deltaTime;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);

        if (currentHunger <= 0)
        {
            TakeDamage(hungerDamageRate * Time.deltaTime);
        }
    }

    public void RestoreHunger(float amount)
    {
        currentHunger += amount;
        currentHunger = Mathf.Clamp(currentHunger, 0, maxHunger);
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
        if (playerModel == null)
            return;

        playerModel.SetActive(thirdPerson);
    }

    void HandleStamina()
    {
        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool canRun = currentStamina > 0f;

        if (isRunning && isMoving && canRun)
        {
            currentStamina -= staminaDrain * Time.deltaTime;

            if (currentStamina <= 0f)
            {
                currentStamina = 0f;
                isRunning = false;
            }
        }
        else if (currentStamina < maxStamina)
        {
            currentStamina += staminaRecovery * Time.deltaTime;
        }

        currentStamina = Mathf.Clamp(currentStamina, 0f, maxStamina);
    }

    void Move()
    {
        bool grounded = controller.isGrounded;

        if (grounded && yVelocity < 0f)
            yVelocity = -2f;

        yVelocity += gravity * Time.deltaTime;

        Quaternion yawRotationOnly = Quaternion.Euler(0f, yRotation, 0f);
        Vector3 forward = yawRotationOnly * Vector3.forward;
        Vector3 right = yawRotationOnly * Vector3.right;
        Vector3 move = (forward * moveInput.y) + (right * moveInput.x);

        bool isMoving = moveInput.sqrMagnitude > 0.01f;
        bool canRun = currentStamina > 0f;
        float speed = (isRunning && isMoving && canRun) ? runSpeed : walkSpeed;

        Vector3 velocity = move.normalized * speed;
        velocity.y = yVelocity;

        controller.Move(velocity * Time.deltaTime);

        if (anim != null)
        {
            float animSpeed = (isRunning && isMoving) ? 1f : moveInput.magnitude * 0.5f;
            anim.SetFloat("Speed", animSpeed);
        }
    }

    void Look()
    {
        Vector2 mouseDelta = lookInput * mouseSensitivity;

        yRotation += mouseDelta.x;

        if (!thirdPerson)
        {
            xRotation -= mouseDelta.y;
            xRotation = Mathf.Clamp(xRotation, -80f, 80f);
        }
        else
        {
            xRotation = thirdPersonPitch;
        }
    }

    void ApplyCameraPose()
    {
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        if (cameraHolder == null)
            return;

        cameraHolder.localPosition = thirdPerson ? thirdPersonOffset : firstPersonOffset;
        cameraHolder.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}
