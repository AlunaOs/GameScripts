using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ResponsiveIntroMenu : MonoBehaviour
{
    [Header("Canvas")]
    public Canvas introCanvas;
    public CanvasScaler canvasScaler;
    
    [Header("Background Slideshow")]
    public Image backgroundImage;
    public Sprite[] backgroundSprites;
    public float slideDuration = 5f;
    public float transitionDuration = 1f;
    public bool randomOrder = true;
    
    [Header("Main Menu Panel")]
    public RectTransform menuPanel;
    public TextMeshProUGUI titleText;
    public Button newGameButton;
    public Button creditsButton;
    public Button quitButton;
    
    [Header("Confirmation Popup")]
    public GameObject confirmationPopup;
    public TextMeshProUGUI confirmationText;
    public Button confirmYesButton;
    public Button confirmNoButton;
    
    [Header("Credits Popup")]
    public GameObject creditsPopup;
    public TextMeshProUGUI creditsText;
    public Button closeCreditsButton;
    
    [Header("Main Game Objects")]
    public GameObject mainInterface;
    public GameObject player;
    public GameObject terrain;
    public GameObject joysticks;
    public GameObject menuCamera;
    public GameObject loadingScreen;
    
    [Header("Audio")]
    public AudioSource buttonClickSound;
    
    private int currentSlideIndex = 0;
    private Coroutine slideshowCoroutine;
    private Material blurMaterial;
    
    void Start()
    {
        // Setup canvas scaler for responsiveness
        SetupCanvasScaler();
        
        // Setup responsive UI elements
        SetupResponsiveUI();
        
        // Start background slideshow
        if (backgroundSprites != null && backgroundSprites.Length > 0)
        {
            StartSlideshow();
        }
        
        // Show intro canvas
        introCanvas.gameObject.SetActive(true);
        
        // Hide main game objects
        HideMainGameObjects();
        
        // Hide popups
        confirmationPopup.SetActive(false);
        creditsPopup.SetActive(false);
        
        // Setup button listeners
        newGameButton.onClick.AddListener(OnNewGameClicked);
        creditsButton.onClick.AddListener(OnCreditsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);
        
        confirmYesButton.onClick.AddListener(OnConfirmYes);
        confirmNoButton.onClick.AddListener(OnConfirmNo);
        
        if (closeCreditsButton != null)
            closeCreditsButton.onClick.AddListener(OnCloseCredits);
        
        // Add blur material if not present
        if (backgroundImage != null)
        {
            blurMaterial = backgroundImage.material;
            if (blurMaterial == null)
            {
                blurMaterial = new Material(Shader.Find("UI/Default"));
                backgroundImage.material = blurMaterial;
            }
        }
    }
    
    void SetupCanvasScaler()
    {
        if (canvasScaler == null)
            canvasScaler = introCanvas.GetComponent<CanvasScaler>();
        
        if (canvasScaler != null)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1080, 1920);
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.matchWidthOrHeight = 0.5f;
        }
    }
    
    void SetupResponsiveUI()
    {
        // Menu Panel - centered, responsive
        if (menuPanel != null)
        {
            menuPanel.anchorMin = new Vector2(0.5f, 0.5f);
            menuPanel.anchorMax = new Vector2(0.5f, 0.5f);
            menuPanel.sizeDelta = new Vector2(600, 500);
        }
        
        // Title text - auto size
        if (titleText != null)
        {
            titleText.fontSize = 80;
            titleText.enableAutoSizing = true;
            titleText.fontSizeMin = 40;
            titleText.fontSizeMax = 100;
        }
        
        // Setup buttons with Layout Element for consistent sizing
        SetupButtonResponsive(newGameButton);
        SetupButtonResponsive(creditsButton);
        SetupButtonResponsive(quitButton);
    }
    
    void SetupButtonResponsive(Button button)
    {
        if (button == null) return;
        
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.sizeDelta = new Vector2(400, 80);
        }
        
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.fontSize = 32;
            buttonText.enableAutoSizing = true;
            buttonText.fontSizeMin = 24;
            buttonText.fontSizeMax = 40;
        }
        
        // Add Layout Element for consistent sizing
        LayoutElement layout = button.GetComponent<LayoutElement>();
        if (layout == null)
            layout = button.gameObject.AddComponent<LayoutElement>();
        
        layout.preferredWidth = 400;
        layout.preferredHeight = 80;
    }
    
    void StartSlideshow()
    {
        if (slideshowCoroutine != null)
            StopCoroutine(slideshowCoroutine);
        
        if (randomOrder)
        {
            // Randomize sprite order
            for (int i = 0; i < backgroundSprites.Length; i++)
            {
                Sprite temp = backgroundSprites[i];
                int randomIndex = Random.Range(i, backgroundSprites.Length);
                backgroundSprites[i] = backgroundSprites[randomIndex];
                backgroundSprites[randomIndex] = temp;
            }
        }
        
        currentSlideIndex = 0;
        if (backgroundSprites.Length > 0)
            backgroundImage.sprite = backgroundSprites[0];
        
        slideshowCoroutine = StartCoroutine(SlideshowCoroutine());
    }
    
    IEnumerator SlideshowCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(slideDuration);
            
            // Move to next slide
            currentSlideIndex = (currentSlideIndex + 1) % backgroundSprites.Length;
            
            // Transition with blur effect
            yield return StartCoroutine(BlurTransition(backgroundSprites[currentSlideIndex]));
        }
    }
    
    IEnumerator BlurTransition(Sprite newSprite)
    {
        float elapsed = 0;
        
        // Fade out current image with blur
        while (elapsed < transitionDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration / 2);
            float blurAmount = Mathf.Lerp(0, 1, t);
            
            if (blurMaterial != null)
                blurMaterial.SetFloat("_BlurAmount", blurAmount);
            
            Color color = backgroundImage.color;
            color.a = Mathf.Lerp(1, 0, t);
            backgroundImage.color = color;
            
            yield return null;
        }
        
        // Change sprite
        backgroundImage.sprite = newSprite;
        
        elapsed = 0;
        
        // Fade in new image
        while (elapsed < transitionDuration / 2)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (transitionDuration / 2);
            float blurAmount = Mathf.Lerp(1, 0, t);
            
            if (blurMaterial != null)
                blurMaterial.SetFloat("_BlurAmount", blurAmount);
            
            Color color = backgroundImage.color;
            color.a = Mathf.Lerp(0, 1, t);
            backgroundImage.color = color;
            
            yield return null;
        }
        
        // Finalize
        if (blurMaterial != null)
            blurMaterial.SetFloat("_BlurAmount", 0);
        
        Color finalColor = backgroundImage.color;
        finalColor.a = 1;
        backgroundImage.color = finalColor;
    }
    
    void HideMainGameObjects()
    {
        if (mainInterface != null) mainInterface.SetActive(false);
        if (player != null) player.SetActive(false);
        if (terrain != null) terrain.SetActive(false);
        if (joysticks != null) joysticks.SetActive(false);
        if (menuCamera != null) menuCamera.SetActive(false);
        if (loadingScreen != null) loadingScreen.SetActive(false);
    }
    
    void ShowMainGameObjects()
    {
        if (mainInterface != null) mainInterface.SetActive(true);
        if (player != null) player.SetActive(true);
        if (terrain != null) terrain.SetActive(true);
        if (joysticks != null) joysticks.SetActive(true);
        if (menuCamera != null) menuCamera.SetActive(false);
    }
    
    void OnNewGameClicked()
    {
        PlayClickSound();
        ShowConfirmationPopup("Start a new adventure?");
    }
    
    void OnCreditsClicked()
    {
        PlayClickSound();
        ShowCreditsPopup();
    }
    
    void OnQuitClicked()
    {
        PlayClickSound();
        ShowConfirmationPopup("Exit MathSoleum?");
    }
    
    void ShowConfirmationPopup(string message)
    {
        confirmationText.text = message;
        confirmationPopup.SetActive(true);
    }
    
    void OnConfirmYes()
    {
        PlayClickSound();
        confirmationPopup.SetActive(false);
        
        if (confirmationText.text.Contains("Start"))
        {
            StartNewGame();
        }
        else if (confirmationText.text.Contains("Exit"))
        {
            QuitGame();
        }
    }
    
    void OnConfirmNo()
    {
        PlayClickSound();
        confirmationPopup.SetActive(false);
    }
    
    void StartNewGame()
    {
        StartCoroutine(LoadGame());
    }
    
    IEnumerator LoadGame()
    {
        if (loadingScreen != null)
            loadingScreen.SetActive(true);
        
        // Optional: Show loading progress
        float progress = 0;
        float duration = 2f;
        float startTime = Time.time;
        
        while (progress < 1f)
        {
            float elapsed = Time.time - startTime;
            progress = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        
        introCanvas.gameObject.SetActive(false);
        ShowMainGameObjects();
        
        if (loadingScreen != null)
            loadingScreen.SetActive(false);
    }
    
    void ShowCreditsPopup()
    {
        creditsPopup.SetActive(true);
    }
    
    void OnCloseCredits()
    {
        PlayClickSound();
        creditsPopup.SetActive(false);
    }
    
    void QuitGame()
    {
        Debug.Log("Quitting game...");
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    void PlayClickSound()
    {
        if (buttonClickSound != null)
            buttonClickSound.Play();
    }
}