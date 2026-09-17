using UnityEngine;
using UnityEngine.EventSystems;

public class LeverUIProxy : MonoBehaviour, IPointerClickHandler
{
    public LeverInteraction leverInteraction;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (leverInteraction != null)
        {
            leverInteraction.OnPointerClick(eventData);
        }
    }
}