using UnityEngine;

public class TouchLook : MonoBehaviour
{
    public float Sensitivity = 0.5f;
    public float Smoothing = 2.0f;
    public float BottomClamp = -80f;
    public float TopClamp = 80f;

    private float _xRotation = 0f;
    private float _currentXRotation = 0f;

    void Update()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            // Only rotate if touching the right side of the screen
            if (touch.position.x > Screen.width / 2)
            {
                if (touch.phase == TouchPhase.Moved)
                {
                    // Calculate rotation based on vertical swipe
                    float mouseY = touch.deltaPosition.y * Sensitivity;
                    _xRotation -= mouseY;
                    _xRotation = Mathf.Clamp(_xRotation, BottomClamp, TopClamp);
                }
            }
        }

        // Apply smoothing so it doesn't feel "jittery"
        _currentXRotation = Mathf.Lerp(_currentXRotation, _xRotation, Time.deltaTime * Smoothing * 10f);
        transform.localRotation = Quaternion.Euler(_currentXRotation, 0f, 0f);
    }
}