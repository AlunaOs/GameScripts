using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class UIDragHandler : MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private Canvas parentCanvas;
    private Vector3 initialPosition;
    private CanvasGroup canvasGroup;

    public UIDropTarget CurrentTarget { get; private set; }

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        initialPosition = rectTransform.anchoredPosition;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Allow raycasts to pass through the dragged triangle so OnDrop hits the Drop Target below it
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling();

        if (CurrentTarget != null)
        {
            CurrentTarget.SetTriangleInside(false);
            CurrentTarget = null;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (parentCanvas == null) return;
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Re-enable raycasts for touch/mouse detection
        canvasGroup.blocksRaycasts = true;

        // If dropped outside any target zone, return to original spawn point
        if (CurrentTarget == null)
        {
            ResetPosition();
        }
    }

    public void SetCurrentDropTarget(UIDropTarget target)
    {
        CurrentTarget = target;
    }

    public void ResetPosition()
    {
        if (CurrentTarget != null)
        {
            CurrentTarget.SetTriangleInside(false);
            CurrentTarget = null;
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = initialPosition;
        }
    }
}