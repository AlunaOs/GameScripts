using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class GridSwipeLetter : MonoBehaviour, IPointerDownHandler, IPointerEnterHandler, IPointerUpHandler
{
    public int gridX;
    public int gridY;
    public string letter;

    private PuzzleManager manager;
    private Image buttonImage;
    private Color defaultColor = Color.white;
    private Vector2 pointerDownPosition;
    private bool swipeThresholdMet = false;

    public bool isSwiped { get; private set; } = false;
    private const float swipeDragThreshold = 15f;

    public void Setup(int x, int y, string letterChar, PuzzleManager puzzleMgr)
    {
        gridX = x;
        gridY = y;
        letter = letterChar;
        manager = puzzleMgr;

        buttonImage = GetComponent<Image>();
        if (buttonImage != null)
        {
            defaultColor = buttonImage.color;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerDownPosition = eventData.position;
        swipeThresholdMet = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (manager == null) return;

        if (manager.IsSwipingActive())
        {
            manager.OnSwipeDragEnter(this);
            return;
        }

        if (eventData.dragging || Input.GetMouseButton(0) || Input.touchCount > 0)
        {
            float dist = Vector2.Distance(pointerDownPosition, eventData.position);
            if (dist >= swipeDragThreshold && !swipeThresholdMet)
            {
                swipeThresholdMet = true;
                manager.OnSwipeStart(this);
            }
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (manager != null)
        {
            manager.OnSwipeEnd();
        }
    }

    public void SetHighlight(bool highlighted, Color activeColor)
    {
        isSwiped = highlighted;
        if (buttonImage != null)
        {
            buttonImage.color = highlighted ? activeColor : defaultColor;
        }
    }

    public void ResetColor()
    {
        isSwiped = false;
        if (buttonImage != null)
        {
            buttonImage.color = defaultColor;
        }
    }
}