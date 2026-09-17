using UnityEngine;
using UnityEngine.EventSystems;

public class UIDropTarget : MonoBehaviour, IDropHandler
{
    [Header("Option Settings")]
    [Tooltip("Index 0, 1, 2, or 3 corresponding to options A, B, C, D")]
    public int optionIndex; 

    public bool IsTriangleInside { get; private set; }

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        // Check if the dragged object is UI_Triangle_A
        UIDragHandler dragHandler = eventData.pointerDrag.GetComponent<UIDragHandler>();
        if (dragHandler != null)
        {
            // Snap triangle center directly over this drop panel
            RectTransform triangleRect = dragHandler.GetComponent<RectTransform>();
            triangleRect.position = transform.position;

            IsTriangleInside = true;
            dragHandler.SetCurrentDropTarget(this);
        }
    }

    public void SetTriangleInside(bool inside)
    {
        IsTriangleInside = inside;
    }
}