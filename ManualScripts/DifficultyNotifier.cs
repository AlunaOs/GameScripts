using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DifficultyNotifier : MonoBehaviour
{
    public Image notificationImage;
    public Sprite increaseSprite;
    public Sprite decreaseSprite;
    public float displayDuration = 2f;
    public float fadeDuration = 0.3f;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        gameObject.SetActive(false);
    }

    public void ShowIncrease()
    {
        notificationImage.sprite = increaseSprite;
        ShowNotification();
    }

    public void ShowDecrease()
    {
        notificationImage.sprite = decreaseSprite;
        ShowNotification();
    }

    private void ShowNotification()
    {
        StopAllCoroutines();
        gameObject.SetActive(true);
        StartCoroutine(AnimateNotification());
    }

    private IEnumerator AnimateNotification()
    {
        // Fade in
        float elapsed = 0f;
        canvasGroup.alpha = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Wait
        yield return new WaitForSeconds(displayDuration);

        // Fade out
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 0f;

        gameObject.SetActive(false);
    }
}