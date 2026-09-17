using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class TutorialPopupManager : MonoBehaviour
{
    // A single tutorial slide structure
    [System.Serializable]
    public struct TutorialPage
    {
        public Sprite tutorialImage;
        [TextArea(3, 10)]
        public string tutorialDescription;
    }

    [Header("UI Element References")]
    [Tooltip("The main container Panel of the tutorial window to turn on/off.")]
    public GameObject tutorialWindowPanel;
    public Image displayImageHolder;
    public TextMeshProUGUI txtDescriptionDisplay;
    public TextMeshProUGUI txtTitleDisplay;

    [Header("Navigation Controls")]
    public Button btnPrevious;
    public Button btnNext;
    public Button btnClose;

    [Header("Page Progress Indicators")]
    [Tooltip("Container holding your page dots/indicators.")]
    public Transform dotsContainer;
    [Tooltip("Prefab of a single dot image.")]
    public GameObject dotPrefab;
    public Sprite activeDotSprite;
    public Sprite inactiveDotSprite;

    [Header("Tutorial Content Config")]
    public string tutorialTitle = "Gameplay Tutorial";
    public List<TutorialPage> tutorialPages = new List<TutorialPage>();

    private int currentPageIndex = 0;
    private List<Image> spawnedDots = new List<Image>();

    void Awake()
    {
        // Bind button events cleanly
        if (btnNext != null) btnNext.onClick.AddListener(ShowNextPage);
        if (btnPrevious != null) btnPrevious.onClick.AddListener(ShowPreviousPage);
        if (btnClose != null) btnClose.onClick.AddListener(HideTutorial);
        
        tutorialWindowPanel.SetActive(false);
    }

    /// <summary>
    /// Call this public method from your puzzle's "?" Help Button onClick event!
    /// </summary>
    public void OpenTutorial()
    {
        if (tutorialPages.Count == 0) return;

        tutorialWindowPanel.SetActive(true);
        currentPageIndex = 0;
        
        if (txtTitleDisplay != null) txtTitleDisplay.text = tutorialTitle;

        GeneratePageDots();
        UpdatePageVisuals();
    }

    private void UpdatePageVisuals()
    {
        // 1. Update Content
        TutorialPage currentPage = tutorialPages[currentPageIndex];
        if (displayImageHolder != null) displayImageHolder.sprite = currentPage.tutorialImage;
        if (txtDescriptionDisplay != null) txtDescriptionDisplay.text = currentPage.tutorialDescription;

        // 2. Manage Navigation Button Visibilities
        if (btnPrevious != null) btnPrevious.gameObject.SetActive(currentPageIndex > 0);
        
        // Change Next button behavior or hide it on the last slide
        if (btnNext != null) btnNext.gameObject.SetActive(currentPageIndex < tutorialPages.Count - 1);

        // 3. Update Progress Page Dots
        for (int i = 0; i < spawnedDots.Count; i++)
        {
            if (spawnedDots[i] != null)
            {
                spawnedDots[i].sprite = (i == currentPageIndex) ? activeDotSprite : inactiveDotSprite;
            }
        }
    }

    public void ShowNextPage()
    {
        if (currentPageIndex < tutorialPages.Count - 1)
        {
            currentPageIndex++;
            UpdatePageVisuals();
        }
    }

    public void ShowPreviousPage()
    {
        if (currentPageIndex > 0)
        {
            currentPageIndex--;
            UpdatePageVisuals();
        }
    }

    public void HideTutorial()
    {
        tutorialWindowPanel.SetActive(false);
    }

    private void GeneratePageDots()
    {
        // Clear old visual tracking dots
        foreach (var dot in spawnedDots) if (dot != null) Destroy(dot.gameObject);
        spawnedDots.Clear();

        if (dotPrefab == null || dotsContainer == null) return;

        // Instantiate tracker dots matching our array bounds
        for (int i = 0; i < tutorialPages.Count; i++)
        {
            GameObject newDot = Instantiate(dotPrefab, dotsContainer);
            Image dotImage = newDot.GetComponent<Image>();
            if (dotImage != null) spawnedDots.Add(dotImage);
        }
    }
}