using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Handles furniture deletion via double-click interaction in VR.
/// Shows a confirmation menu when attempting to delete an object.
/// </summary>
public class FurnitureDeleterVR : MonoBehaviour
{
    private float lastClickTime = 0f;
    private const float DOUBLE_CLICK_THRESHOLD = 0.3f;
    private GameObject activeConfirmationMenu;

    // Antes esto exigía específicamente un XRSimpleInteractable. Desde que los muebles movibles
    // usan XRGrabInteractable (para poder agarrarlos con la mano en vez del menú de flechas
    // viejo), buscar solo XRSimpleInteractable hacía que este componente agregara UN SEGUNDO
    // interactable de más en el mismo mueble (compitiendo con el XRGrabInteractable por el rayo
    // del control). Usando la clase base XRBaseInteractable funciona igual con cualquiera de
    // los dos tipos, sin duplicar nada.
    private XRBaseInteractable interactable;

    private void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable == null)
            interactable = gameObject.AddComponent<XRSimpleInteractable>();
    }

    private void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }
    }

    private void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        DetectDoubleClick();
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        // No-op for now; reserved for future press/hold interactions
    }

    private void DetectDoubleClick()
    {
        float timeSinceLastClick = Time.time - lastClickTime;

        if (timeSinceLastClick < DOUBLE_CLICK_THRESHOLD)
        {
            // Double click detected
            ShowDeleteConfirmation();
        }

        lastClickTime = Time.time;
    }

    private void ShowDeleteConfirmation()
    {
        if (activeConfirmationMenu != null)
            return; // Menu already open

        Debug.Log($"Showing delete confirmation for {gameObject.name}");
        CreateConfirmationMenu();
    }

    private void CreateConfirmationMenu()
    {
        // Create a simple canvas at the object's position
        GameObject canvasGO = new GameObject("DeleteConfirmation");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = Vector3.up * 0.3f;

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Sin esto los botones de confirmar/cancelar no responden al rayo del control VR.
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rectTransform = canvasGO.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(200, 100);
        rectTransform.localScale = new Vector3(0.001f, 0.001f, 0.001f); // Small size for VR

        // Create confirm button
        GameObject confirmButtonGO = new GameObject("ConfirmButton");
        confirmButtonGO.transform.SetParent(canvasGO.transform, false);

        RectTransform buttonRect = confirmButtonGO.AddComponent<RectTransform>();
        buttonRect.anchoredPosition = new Vector2(0, 25);
        buttonRect.sizeDelta = new Vector2(180, 40);

        Image confirmButtonImage = confirmButtonGO.AddComponent<Image>();
        confirmButtonImage.color = new Color(1, 0, 0, 0.8f);

        Button confirmButton = confirmButtonGO.AddComponent<Button>();
        confirmButton.targetGraphic = confirmButtonImage;
        confirmButton.onClick.AddListener(() => ConfirmDelete());

        // Create cancel button
        GameObject cancelButtonGO = new GameObject("CancelButton");
        cancelButtonGO.transform.SetParent(canvasGO.transform, false);

        RectTransform cancelRect = cancelButtonGO.AddComponent<RectTransform>();
        cancelRect.anchoredPosition = new Vector2(0, -25);
        cancelRect.sizeDelta = new Vector2(180, 40);

        Image cancelButtonImage = cancelButtonGO.AddComponent<Image>();
        cancelButtonImage.color = new Color(0, 1, 0, 0.8f);

        Button cancelButton = cancelButtonGO.AddComponent<Button>();
        cancelButton.targetGraphic = cancelButtonImage;
        cancelButton.onClick.AddListener(() => CancelDelete(canvasGO));

        activeConfirmationMenu = canvasGO;
    }

    private void ConfirmDelete()
    {
        Debug.Log($"Deleting {gameObject.name}");

        // Show toast notification
        ToastNotificationUI.Show($"Eliminado: {gameObject.name}", 2f);

        // Destroy the object (menu is a child, gets destroyed with it)
        Destroy(gameObject);
    }

    private void CancelDelete(GameObject menu)
    {
        Debug.Log($"Delete cancelled for {gameObject.name}");

        // Close the confirmation menu
        activeConfirmationMenu = null;
        Destroy(menu);
    }

    /// <summary>
    /// Allows external code to delete this furniture without confirmation.
    /// </summary>
    public void DeleteImmediately()
    {
        Debug.Log($"Immediately deleting {gameObject.name}");
        Destroy(gameObject);
    }
}
