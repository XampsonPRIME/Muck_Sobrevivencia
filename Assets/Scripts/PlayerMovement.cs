using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float speed = 6f;
    public float mouseSensitivity = 3f;
    public float gravity = -9.8f;

    public Transform cameraHolder;

    CharacterController controller;
    PlayerControls controls;

    Vector2 moveInput;
    Vector2 lookInput;

    float yVelocity;
    float xRotation = 0f;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        controls = new PlayerControls();

        controls.Player.Move.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Player.Move.canceled += ctx => moveInput = Vector2.zero;

        controls.Player.Look.performed += ctx => lookInput = ctx.ReadValue<Vector2>();
        controls.Player.Look.canceled += ctx => lookInput = Vector2.zero;
    }

    void OnEnable()
    {
        controls.Enable();
    }

    void OnDisable()
    {
        controls.Disable();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // 🔥 Segurança: evita erro se esquecer de setar no Inspector
        if (cameraHolder == null)
        {
            Debug.LogError("CameraHolder não foi atribuído!");
        }
    }

    void Update()
    {
        Move();
        Look();
    }

    void Move()
    {
        Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;

        if (controller.isGrounded && yVelocity < 0)
        {
            yVelocity = -2f; // mantém grudado no chão
        }

        yVelocity += gravity * Time.deltaTime;
        yVelocity = Mathf.Clamp(yVelocity, -50f, 50f);

        Vector3 velocity = move * speed;
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