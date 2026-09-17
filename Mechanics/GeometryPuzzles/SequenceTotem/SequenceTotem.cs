using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems; // Required for EventSystem check
using TMPro;

public class SequenceTotem : MonoBehaviour
{
    public int totemID; // 0, 1, or 2
    [SerializeField] private TextMeshPro labelText; // World-space TextMeshPro attached to the totem
    [SerializeField] private GameObject activeHighlight;

    private SequenceTotemPuzzleManager manager;
    private float[] availableOptions = new float[] { 30f, 60f, 90f, 180f }; // Default fallback
    private int currentOptionIndex = 0;
    private bool isRotating = false;
    private bool isActive = true;

    public float SelectedValue => availableOptions.Length > 0 ? availableOptions[currentOptionIndex] : 0f;

    void Awake()
    {
        UpdateWorldLabel();
    }

    public void SetupTotemOptions(float[] options)
    {
        if (options != null && options.Length > 0)
        {
            availableOptions = options;
            currentOptionIndex = 0;
            UpdateWorldLabel();
        }
    }

    public void SetActiveState(bool active)
    {
        if (activeHighlight != null) activeHighlight.SetActive(active);
    }

    private void Update()
    {
        // Detect 3D taps directly on the totem
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            // Block touch detection if touch landed on UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
            {
                return;
            }

            DetectTouch(Input.GetTouch(0).position);
        }
        else if (Input.GetMouseButtonDown(0))
        {
            // Block mouse click detection if pointer is over UI element
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            DetectTouch(Input.mousePosition);
        }
    }

    private void DetectTouch(Vector3 screenPosition)
    {
        if (!isActive || isRotating) return;

        // Ensure Camera.main is assigned and valid in scene
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("SequenceTotem: Main Camera is missing or not tagged as 'MainCamera'.");
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(screenPosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.transform == transform)
            {
                CycleToNextAnswer();
            }
        }
    }

    private void CycleToNextAnswer()
    {
        currentOptionIndex = (currentOptionIndex + 1) % availableOptions.Length;
        StartCoroutine(RotateStep(90f));
    }

    private IEnumerator RotateStep(float angleStep)
    {
        isRotating = true;
        Quaternion startRot = transform.rotation;
        Quaternion endRot = startRot * Quaternion.Euler(0, angleStep, 0);

        float elapsed = 0f;
        float duration = 0.25f;

        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(startRot, endRot, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.rotation = endRot;
        UpdateWorldLabel();
        isRotating = false;
    }

    private void UpdateWorldLabel()
    {
        if (labelText != null)
        {
            labelText.text = $"{SelectedValue}°";
        }
    }
}