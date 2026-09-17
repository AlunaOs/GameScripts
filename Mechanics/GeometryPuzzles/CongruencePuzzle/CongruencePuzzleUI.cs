using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CongruencePuzzleUI : MonoBehaviour
{
    [Header("Main Panels")]
    [SerializeField] private GameObject mainBoardPanel;
    [SerializeField] private CanvasGroup puzzleCanvasGroup;

    [Header("Header Elements")]
    [SerializeField] private Button closeButton;
    [SerializeField] private HintScrollUI hintScrollUI;

    [Header("Question Elements")]
    [SerializeField] private TMP_Text questionNumberText;
    [SerializeField] private TMP_Text questionText;
    [SerializeField] private Image questionImage;

    [Header("Answer Buttons")]
    [SerializeField] private Button[] answerButtons;
    [SerializeField] private TMP_Text[] answerTexts;

    [Header("Progress Elements")]
    [SerializeField] private TMP_Text questionProgressText;
    [SerializeField] private TMP_Text attemptText;

    [Header("Feedback Elements")]
    [SerializeField] private CanvasGroup feedbackCanvasGroup;
    [SerializeField] private GameObject correctFeedback;
    [SerializeField] private GameObject incorrectFeedback;

    public event Action<int> OnAnswerSelected;
    public event Action OnCloseRequested;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(() => OnCloseRequested?.Invoke());
        }

        for (int i = 0; i < answerButtons.Length; i++)
        {
            int index = i;
            answerButtons[i].onClick.RemoveAllListeners();
            answerButtons[i].onClick.AddListener(() => OnAnswerSelected?.Invoke(index));
        }
    }

    public void RenderQuestion(CongruenceQuestionData question, int currentQuestionIndex, int totalQuestions)
    {
        questionNumberText.text = $"Question {currentQuestionIndex + 1} / {totalQuestions}";
        questionText.text = question.questionText;

        for (int i = 0; i < answerButtons.Length; i++)
        {
            if (i < question.options.Length)
            {
                answerButtons[i].gameObject.SetActive(true);
                answerTexts[i].text = question.options[i];
            }
            else
            {
                answerButtons[i].gameObject.SetActive(false);
            }
        }

        questionProgressText.text = $"Progress: {currentQuestionIndex} / {totalQuestions}";
    }

    public void UpdateAttempts(int currentAttempts, int maxAttempts)
    {
        // Strict guard: Clamp UI display to prevent -1 or higher than max
        int safeAttempts = Mathf.Clamp(currentAttempts, 0, maxAttempts);
        attemptText.text = $"Attempts: {safeAttempts} / {maxAttempts}";
    }

    public void SetAnswerButtonsInteractable(bool interactable)
    {
        foreach (var btn in answerButtons)
        {
            btn.interactable = interactable;
        }
    }

    public void HighlightCorrectAnswer(int correctIndex)
    {
        if (correctIndex >= 0 && correctIndex < answerButtons.Length)
        {
            ColorBlock colors = answerButtons[correctIndex].colors;
            colors.normalColor = Color.green;
            answerButtons[correctIndex].colors = colors;
        }
    }

    public IEnumerator DisplayFeedbackRoutine(bool isCorrect)
    {
        feedbackCanvasGroup.alpha = 1f;
        correctFeedback.SetActive(isCorrect);
        incorrectFeedback.SetActive(!isCorrect);

        yield return new WaitForSeconds(1.2f);

        feedbackCanvasGroup.alpha = 0f;
        correctFeedback.SetActive(false);
        incorrectFeedback.SetActive(false);
    }

    public void SetupHintButton(Action onUseHint)
    {
        if (hintScrollUI != null)
        {
            hintScrollUI.EnableHint(() =>
            {
                if (ShopManagers.Instance != null)
                {
                    ShopManagers.Instance.UseHintScroll();
                }
                onUseHint?.Invoke();
            });
        }
    }
}