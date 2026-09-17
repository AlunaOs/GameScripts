using UnityEngine;
using StarterAssets; // Important for finding the joystick

public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private UIVirtualJoystick joystick; 

    [Header("Settings")]
    [SerializeField] private float cameraSensitivity = 15f;
    [SerializeField] private float moveSpeed = 5f;

    private float cameraPitch;
    private int rightFingerId = -1;
    private Vector2 lookInput;
    private float halfScreenWidth;

    void Start()
    {
        halfScreenWidth = Screen.width / 2;
    }

    void Update()
    {
        HandleTouchInput();
        
        // 1. Look Logic (Swipe right side)
        if (rightFingerId != -1)
        {
            cameraPitch = Mathf.Clamp(cameraPitch - lookInput.y, -80f, 80f);
            cameraTransform.localRotation = Quaternion.Euler(cameraPitch, 0, 0);
            transform.Rotate(Vector3.up * lookInput.x);
        }

        // 2. Move Logic (Fixed for Starter Assets Joystick)
        // We use the virtual joystick's internal values directly
        Vector2 joyInput = new Vector2(joystick.transform.GetChild(0).localPosition.x, joystick.transform.GetChild(0).localPosition.y).normalized;
        Vector3 move = transform.right * joyInput.x + transform.forward * joyInput.y;
        characterController.Move(move * moveSpeed * Time.deltaTime);
    }

    void HandleTouchInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            if (t.position.x > halfScreenWidth)
            {
                if (t.phase == TouchPhase.Began && rightFingerId == -1)
                    rightFingerId = t.fingerId;

                if (t.fingerId == rightFingerId)
                {
                    if (t.phase == TouchPhase.Moved)
                        lookInput = t.deltaPosition * cameraSensitivity * Time.deltaTime;
                    else if (t.phase == TouchPhase.Stationary || t.phase == TouchPhase.Ended)
                        lookInput = Vector2.zero;
                    
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                        rightFingerId = -1;
                }
            }
        }
    }
}