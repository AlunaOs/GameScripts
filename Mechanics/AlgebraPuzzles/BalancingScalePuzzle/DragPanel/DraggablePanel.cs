using UnityEngine;
using UnityEngine.EventSystems;

public class DraggablePanel : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Target To Drag")]
    [Tooltip("The RectTransform that actually moves. Leave empty to drag this object.")]
    public RectTransform panelRectTransform;

    [Header("Canvas Reference")]
    public Canvas parentCanvas;

    private Vector2 originalAnchoredPosition;
    private Vector2 pointerOffset;

    void Awake()
    {
        if (panelRectTransform == null)
            panelRectTransform = GetComponent<RectTransform>();

        if (parentCanvas == null)
            parentCanvas = GetComponentInParent<Canvas>();

        // Store the default centered position on startup
        if (panelRectTransform != null)
            originalAnchoredPosition = panelRectTransform.anchoredPosition;
    }

    void OnEnable()
    {
        // Reset to center whenever the panel is opened/enabled
        ResetPosition();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (panelRectTransform == null || parentCanvas == null) return;

        // Calculate touch position offset relative to panel pivot
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out pointerOffset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (panelRectTransform == null || parentCanvas == null) return;

        RectTransform canvasRect = parentCanvas.transform as RectTransform;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPointerPos))
        {
            // Move panel based on touch location minus initial touch offset
            panelRectTransform.anchoredPosition = localPointerPos - pointerOffset;
        }
    }

    public void ResetPosition()
    {
        if (panelRectTransform != null)
        {
            panelRectTransform.anchoredPosition = originalAnchoredPosition;
        }
    }
}