using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public GameObject settingsPopupCanvas;
    public Button graphicsBtn;
    public Button soundBtn;
    public Button exitBtn;
    public Button closeBtn;
    
    void Start()
    {
        if (settingsPopupCanvas != null)
            settingsPopupCanvas.SetActive(false);

        if (graphicsBtn != null)
            graphicsBtn.onClick.AddListener(OpenGraphics);
            
        if (soundBtn != null)
            soundBtn.onClick.AddListener(OpenSound);
            
        if (exitBtn != null)
            exitBtn.onClick.AddListener(ExitGame);
            
        if (closeBtn != null)
            closeBtn.onClick.AddListener(ClosePopup);
    }
    
    public void OpenPopup()
    {
        if (settingsPopupCanvas != null)
            settingsPopupCanvas.SetActive(true);
    }
    
    public void ClosePopup()
    {
        if (settingsPopupCanvas != null)
            settingsPopupCanvas.SetActive(false);
    }
    
    void OpenGraphics()
    {
        Debug.Log("Open Graphics Settings");
        // graphics settings logic 
    }
    
    void OpenSound()
    {
        Debug.Log("Open Sound Settings");
        // sound settings logic
    }
    
    void ExitGame()
    {
        Debug.Log("Exit Game");
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}