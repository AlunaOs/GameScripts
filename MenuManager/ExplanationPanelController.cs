using UnityEngine;
using UnityEngine.UI;

public class ExplanationPanelController : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    void Start()
    {
        if (openButton != null)  openButton.onClick.AddListener(OpenPanel);
        if (closeButton != null) closeButton.onClick.AddListener(ClosePanel);

        ClosePanel();
    }

    public void OpenPanel()
    {
        gameObject.SetActive(true);
    }

    public void ClosePanel()
    {
        gameObject.SetActive(false);
    }
}