using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFirstPersonController : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 6f;
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float gravity = -20f;
    [SerializeField, HideInInspector] private float externalSpeedMultiplier = 1f;

    [Header("Crosshair")]
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private Color crosshairColor = Color.white;
    [SerializeField] private float crosshairLineLength = 10f;
    [SerializeField] private float crosshairThickness = 2f;
    [SerializeField] private float crosshairGap = 4f;

    public float MovementSpeed => movementSpeed * externalSpeedMultiplier;

    private CharacterController characterController;
    [SerializeField] private Transform cameraTransform;
    private float cameraPitch;
    private float verticalVelocity;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Start()
    {
        // A previous modifier screen can leave the editor paused when entering
        // Play Mode again, so always start a new run at normal speed.
        Time.timeScale = 1f;
        LockCursor();
    }

    public void Configure(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    public void MultiplyExternalSpeed(float multiplier)
    {
        externalSpeedMultiplier *= Mathf.Max(0.05f, multiplier);
    }

    private void Update()
    {
        // RoundManager pauses the game while its modifier buttons are open.
        // Do not capture the click or relock the cursor during that screen.
        if (Time.timeScale <= 0f)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                LockCursor();
            }
        }

        bool cursorIsLocked = Cursor.lockState == CursorLockMode.Locked;
        if (!cursorIsLocked)
        {
            if (Input.GetMouseButtonDown(0))
            {
                LockCursor();
                cursorIsLocked = true;
            }
        }

        if (cursorIsLocked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up * mouseX);

            cameraPitch = Mathf.Clamp(cameraPitch - mouseY, -85f, 85f);
            if (cameraTransform != null)
            {
                cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }

        Vector3 movement =
            transform.right * Input.GetAxisRaw("Horizontal") +
            transform.forward * Input.GetAxisRaw("Vertical");

        movement = Vector3.ClampMagnitude(movement, 1f) * MovementSpeed;

        if (characterController.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;
        movement.y = verticalVelocity;

        characterController.Move(movement * Time.deltaTime);
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnGUI()
    {
        if (!showCrosshair ||
            Cursor.lockState != CursorLockMode.Locked ||
            Time.timeScale <= 0f)
        {
            return;
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float length = Mathf.Max(1f, crosshairLineLength);
        float thickness = Mathf.Max(1f, crosshairThickness);
        float gap = Mathf.Max(0f, crosshairGap);

        Color previousColor = GUI.color;
        GUI.color = crosshairColor;

        GUI.DrawTexture(
            new Rect(centerX - gap - length, centerY - thickness * 0.5f, length, thickness),
            Texture2D.whiteTexture
        );
        GUI.DrawTexture(
            new Rect(centerX + gap, centerY - thickness * 0.5f, length, thickness),
            Texture2D.whiteTexture
        );
        GUI.DrawTexture(
            new Rect(centerX - thickness * 0.5f, centerY - gap - length, thickness, length),
            Texture2D.whiteTexture
        );
        GUI.DrawTexture(
            new Rect(centerX - thickness * 0.5f, centerY + gap, thickness, length),
            Texture2D.whiteTexture
        );

        GUI.color = previousColor;
    }
}
