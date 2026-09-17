using UnityEngine;

public class DecodeInteract : MonoBehaviour 
{
    public Transform player;           
    public GameObject interactCanvas; 
    public GameObject quizCanvas;
    
    [Header("Distance Settings")]
    public float showInteractDistance = 4f;  
    public float hideInteractDistance = 2f;   
    public float autoCloseDistance = 6f;      

    private DecodeQuizPuzzleManager quizManager;

    void Start()
    {
        if (quizCanvas != null)
        {
            quizManager = quizCanvas.GetComponent<DecodeQuizPuzzleManager>();
            quizCanvas.SetActive(false);
        }

        if (interactCanvas != null)
        {
            interactCanvas.SetActive(false);
        }
    }

    void Update() 
    {
        if (player == null) return;

        float distance = Vector3.Distance(player.position, transform.position);

        // Auto-close quiz if player walks too far away
        if (quizCanvas != null && quizCanvas.activeSelf) 
        {
            if (distance > autoCloseDistance) 
            {
                CloseQuiz();
            }
            
            if (interactCanvas != null && interactCanvas.activeSelf)
                interactCanvas.SetActive(false);
            
            return;
        }

        // Handle interact prompt visibility based on distance ranges
        if (distance >= hideInteractDistance && distance <= showInteractDistance) 
        {
            if (interactCanvas != null && !interactCanvas.activeSelf) 
                interactCanvas.SetActive(true);
        } 
        else 
        {
            if (interactCanvas != null && interactCanvas.activeSelf)
                interactCanvas.SetActive(false);
        }
    }

    public void OnTapInteract() 
    {
        if (interactCanvas != null)
            interactCanvas.SetActive(false);
        
        if (quizManager != null)
        {
            // Check if it's already completed before forcing open
            if (!quizManager.isCompleted)
            {
                quizManager.OnQuizOpened();
            }
        }
        else if (quizCanvas != null)
        {
            quizCanvas.SetActive(true);
        }
    }
    
    private void CloseQuiz()
    {
        if (quizManager != null)
        {
            quizManager.CloseQuiz();
        }
        else if (quizCanvas != null)
        {
            quizCanvas.SetActive(false);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, showInteractDistance);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, hideInteractDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, autoCloseDistance);
    }
}