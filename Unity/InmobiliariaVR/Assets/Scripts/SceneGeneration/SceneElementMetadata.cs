using UnityEngine;

/// <summary>
/// Metadata component attached to every generated scene element.
/// Stores information about the element's type, ID, and original JSON data for debugging/recreation.
/// </summary>
public class SceneElementMetadata : MonoBehaviour
{
    [SerializeField] public string elementId;
    [SerializeField] public string elementType;
    [SerializeField] public string jsonData;
    [SerializeField] public bool isSelectable = true;
    [SerializeField] public bool isDeletable = true;
    [SerializeField] public bool isColorable = true;
    [SerializeField] public bool isMovable = true;

    private void OnDestroy()
    {
        // Log when element is destroyed
        if (Application.isPlaying)
        {
            // Clean up any associated scripts
        }
    }

    public void SetInteractionFlags(
        bool selectable = true,
        bool deletable = true,
        bool colorable = true,
        bool movable = true)
    {
        isSelectable = selectable;
        isDeletable = deletable;
        isColorable = colorable;
        isMovable = movable;
    }

    public override string ToString()
    {
        return $"[{elementType}] {elementId} (Sel:{isSelectable} Del:{isDeletable} Col:{isColorable} Mov:{isMovable})";
    }
}
