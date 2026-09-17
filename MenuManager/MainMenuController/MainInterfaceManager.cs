using UnityEngine;

public class MainInterfaceManager : MonoBehaviour
{
    [Header("UI Frame References")]
    [SerializeField] private GameObject mainMenuContainer;
    [SerializeField] private CanvasGroup menuButtonsGroup;
    [SerializeField] private GameObject headerContainer;
    [SerializeField] private GameObject dungeonContentFrame;
    [SerializeField] private GameObject shopContentFrame;
    [SerializeField] private GameObject manualContentFrame;
    [SerializeField] private GameObject accountContentFrame;
    [SerializeField] private GameObject creditsPanel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetSessionOnBoot()
    {
        PlayerPrefs.SetInt("HasPlayedThisSession", 0);
        PlayerPrefs.Save();
    }

    private void Awake()
    {
        if (mainMenuContainer != null && menuButtonsGroup == null)
        {
            menuButtonsGroup = mainMenuContainer.GetComponent<CanvasGroup>();
            if (menuButtonsGroup == null)
            {
                menuButtonsGroup = mainMenuContainer.AddComponent<CanvasGroup>();
            }
        }
    }

    private void Start()
    {
        int hasPlayed = PlayerPrefs.GetInt("HasPlayedThisSession", 0);

        if (hasPlayed == 1)
        {
            SkipMainMenu();
        }
        else
        {
            ShowMainMenu();
        }
    }
    public void ShowMainMenu()
    {
        if (menuButtonsGroup != null) menuButtonsGroup.alpha = 1f;

        if (mainMenuContainer) mainMenuContainer.SetActive(true);
        if (headerContainer) headerContainer.SetActive(true);
        if (creditsPanel) creditsPanel.SetActive(false);
        if (dungeonContentFrame) dungeonContentFrame.SetActive(false);
        if (shopContentFrame) shopContentFrame.SetActive(false);
        if (manualContentFrame) manualContentFrame.SetActive(false);
        if (accountContentFrame) accountContentFrame.SetActive(false);
    }

    private void SkipMainMenu()
    {
        if (mainMenuContainer) mainMenuContainer.SetActive(false);
        if (headerContainer) headerContainer.SetActive(true);
        if (dungeonContentFrame) dungeonContentFrame.SetActive(true);
        if (creditsPanel) creditsPanel.SetActive(false);
    }
}