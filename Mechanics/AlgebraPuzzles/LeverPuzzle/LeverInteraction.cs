using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class LeverInteraction : MonoBehaviour, IPointerClickHandler
{
    [Header("3D Setup")]
    public Camera leverCamera;
    public GameObject leverPuzzlePanel;
    public Transform handleTransform;

    [Header("Rotation Settings (Z-Axis)")]
    public float downAngleZ = 320f;
    public float upAngleZ = 197f;
    public float rotateDuration = 0.3f;

    [Header("Puzzle & Delay")]
    public float tapCooldown = 0.4f;

    [Header("Panel Transition Settings (Right to Left Slide)")]
    public float slideDuration = 0.3f;
    private RectTransform panelRectTransform;

    [Header("UI Feedback & Displays")]
    public GameObject correctPanel; 
    public GameObject errorPanel;   
    public TMP_Text questionTextUI;
    public TMP_Text counterTextUI;
    public TMP_Text successTextUI; 

    [Header("Player Kit UI Reference")]
    [Tooltip("Drag your player's HUD / Controls / Kit GameObject here to hide it during the puzzle and bring it back after.")]
    public GameObject playerKitUI;

    [Header("Target Wall")]
    public AutomaticMovingWall targetMovingWall;

    [Header("World Trigger Reference")]
    public LeverWorldTrigger worldTrigger;

    [Header("Permanent Close UI Reference")]
    [Tooltip("Drag the world 'Interact Button' GameObject here so it can be permanently hidden when solved.")]
    public GameObject interactButtonUI;

    // Internal State
    private int correctAnswer = 0;
    private int currentPulls = 0;
    private bool isDown = true;
    private bool isCooldown = false;
    private bool isPuzzleSolved = false;
    private Coroutine activeRotationCoroutine;
    private Coroutine slideCoroutine;

    private void Start()
    {
        isDown = true;

        if (leverCamera == null)
        {
            GameObject camObj = GameObject.Find("LeverCamera");
            if (camObj != null) leverCamera = camObj.GetComponent<Camera>();
        }

        if (leverPuzzlePanel != null)
        {
            panelRectTransform = leverPuzzlePanel.GetComponent<RectTransform>();
        }

        if (handleTransform != null)
        {
            Vector3 currentRot = handleTransform.localEulerAngles;
            handleTransform.localEulerAngles = new Vector3(currentRot.x, currentRot.y, downAngleZ);
        }

        // Ensure feedback panels/texts are hidden initially
        if (correctPanel != null) correctPanel.SetActive(false);
        if (errorPanel != null) errorPanel.SetActive(false);
        if (successTextUI != null) successTextUI.gameObject.SetActive(false);

        GenerateAlgebraQuestion();
    }

    public void OpenLeverPuzzleUI()
    {
        if (isPuzzleSolved) return;

        if (playerKitUI != null)
        {
            playerKitUI.SetActive(false);
        }

        if (leverPuzzlePanel != null)
        {
            leverPuzzlePanel.SetActive(true);
            
            if (panelRectTransform != null)
            {
                if (slideCoroutine != null) StopCoroutine(slideCoroutine);
                slideCoroutine = StartCoroutine(SlidePanelRoutine(Screen.width, 0f, false)); // Slide IN
            }
        }
    }

    public void CloseLeverPuzzleUI()
    {
        if (playerKitUI != null)
        {
            playerKitUI.SetActive(true);
        }

        if (panelRectTransform != null)
        {
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlidePanelRoutine(0f, Screen.width, true)); // Slide OUT then Deactivate
        }
        else if (leverPuzzlePanel != null)
        {
            leverPuzzlePanel.SetActive(false);
        }
    }

    private IEnumerator SlidePanelRoutine(float startX, float targetX, bool deactivateOnEnd)
    {
        Vector2 startPos = new Vector2(startX, panelRectTransform.anchoredPosition.y);
        Vector2 targetPos = new Vector2(targetX, panelRectTransform.anchoredPosition.y);

        panelRectTransform.anchoredPosition = startPos;

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            t = t * t * (3f - 2f * t);

            panelRectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        panelRectTransform.anchoredPosition = targetPos;

        if (deactivateOnEnd && leverPuzzlePanel != null)
        {
            leverPuzzlePanel.SetActive(false);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        ProcessTouchInput(eventData);
    }

    private void ProcessTouchInput(PointerEventData eventData)
    {
        if (isPuzzleSolved) return;
        if (leverPuzzlePanel != null && !leverPuzzlePanel.activeInHierarchy) return;
        if (isCooldown || leverCamera == null) return;

        RectTransform rectTransform = eventData.pointerPressRaycast.gameObject != null 
            ? eventData.pointerPressRaycast.gameObject.GetComponent<RectTransform>() 
            : null;

        if (rectTransform != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint
            );

            Rect rect = rectTransform.rect;
            Vector2 viewportPoint = new Vector2(
                (localPoint.x - rect.x) / rect.width,
                (localPoint.y - rect.y) / rect.height
            );

            Ray ray = leverCamera.ViewportPointToRay(viewportPoint);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    StartCoroutine(PullLeverRoutine());
                }
            }
        }
    }

    private void GenerateAlgebraQuestion()
    {
        bool valid = false;
        string textStr = "";

        while (!valid)
        {
            int pattern = Random.Range(0, 3);
            if (pattern == 0)
            {
                int a = Random.Range(1, 4), b = Random.Range(1, 3), c = Random.Range(1, 4);
                correctAnswer = (a * b) + c;
                textStr = $"Solve for X:\n({a} × {b}) + {c} = X";
            }
            else if (pattern == 1)
            {
                int a = Random.Range(2, 4), b = Random.Range(2, 4), c = Random.Range(1, 4);
                correctAnswer = (a * b) - c;
                textStr = $"Solve for X:\n({a} × {b}) - {c} = X";
            }
            else
            {
                int a = Random.Range(1, 3), b = Random.Range(1, 3), c = Random.Range(1, 4);
                correctAnswer = a + b + c;
                textStr = $"Solve for X:\n{a} + {b} + {c} = X";
            }

            if (correctAnswer >= 1 && correctAnswer <= 10) valid = true;
        }

        if (questionTextUI != null) questionTextUI.text = textStr;
        ResetPulls();
    }

    private IEnumerator PullLeverRoutine()
    {
        isCooldown = true;

        if (isDown)
        {
            if (handleTransform != null)
            {
                if (activeRotationCoroutine != null) StopCoroutine(activeRotationCoroutine);
                activeRotationCoroutine = StartCoroutine(RotateHandleToAngle(upAngleZ));
            }

            isDown = false;
            currentPulls++;
            
            if (currentPulls > 10) 
            {
                currentPulls = 0;
            }

            UpdateCounterDisplay();
        }
        else
        {
            if (handleTransform != null)
            {
                if (activeRotationCoroutine != null) StopCoroutine(activeRotationCoroutine);
                activeRotationCoroutine = StartCoroutine(RotateHandleToAngle(downAngleZ));
            }

            isDown = true;
        }

        yield return new WaitForSeconds(tapCooldown);
        isCooldown = false;
    }

    public void ResetPulls()
    {
        currentPulls = 0;
        UpdateCounterDisplay();

        isDown = true;
        if (handleTransform != null)
        {
            if (activeRotationCoroutine != null) StopCoroutine(activeRotationCoroutine);
            handleTransform.localEulerAngles = new Vector3(handleTransform.localEulerAngles.x, handleTransform.localEulerAngles.y, downAngleZ);
        }
    }

    public void SubmitAnswer()
    {
        if (isPuzzleSolved) return;

        if (currentPulls == correctAnswer)
        {
            isPuzzleSolved = true;

            if (correctPanel != null) correctPanel.SetActive(true);
            if (errorPanel != null) errorPanel.SetActive(false);

            if (successTextUI != null)
            {
                successTextUI.gameObject.SetActive(true);
                successTextUI.text = "Success! Moving wall activated";
            }

            if (targetMovingWall != null)
            {
                targetMovingWall.ActivateWallMovement();
            }

            if (interactButtonUI != null)
            {
                interactButtonUI.SetActive(false);
            }

            StartCoroutine(ClosePuzzleAfterSuccessRoutine());
        }
        else
        {
            if (errorPanel != null)
            {
                errorPanel.SetActive(true);
                StartCoroutine(HideErrorPanelRoutine(1.5f));
            }
        }
    }

    private IEnumerator ClosePuzzleAfterSuccessRoutine()
    {
        yield return new WaitForSeconds(1.5f); 
        
        if (correctPanel != null) correctPanel.SetActive(false);
        if (successTextUI != null) successTextUI.gameObject.SetActive(false);

        CloseLeverPuzzleUI();
    }

    private IEnumerator HideErrorPanelRoutine(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (errorPanel != null) errorPanel.SetActive(false);
    }

    private IEnumerator RotateHandleToAngle(float targetZ)
    {
        Quaternion startRotation = handleTransform.localRotation;
        Vector3 currentEuler = handleTransform.localEulerAngles;
        Quaternion targetRotation = Quaternion.Euler(currentEuler.x, currentEuler.y, targetZ);

        float elapsed = 0f;
        while (elapsed < rotateDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / rotateDuration);
            t = t * t * (3f - 2f * t); 
            handleTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }

        handleTransform.localRotation = targetRotation;
    }

    private void UpdateCounterDisplay()
    {
        if (counterTextUI != null)
        {
            counterTextUI.text = $"Pulls: {currentPulls}";
        }
    }
}