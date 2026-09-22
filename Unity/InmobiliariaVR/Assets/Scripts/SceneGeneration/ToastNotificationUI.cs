using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Displays floating toast notifications in 3D VR space.
/// Shows error, success, and info messages that auto-dismiss after specified duration.
///
/// BUG encontrado y corregido (2026-09-19, tercera ronda -- reportado por Alan: "al elegir la
/// edición elevada aparecen letras gigantes en todo el juego que son ilegibles"): esta era la
/// ÚNICA pantalla de todo el proyecto armada con TextMeshProUGUI en vez del Text clásico de UGUI +
/// Resources.GetBuiltinResource("LegacyRuntime.ttf") que usan CrudPanelController,
/// WallPaintPanelController, UpdateModesController, ResizeHandlesController, ConfirmDialogUI y
/// FurnitureCatalogController. TextMeshPro necesita sus "TMP Essential Resources" importados y
/// configurados (un Font Asset por defecto en TMP Settings) para renderizar bien -- sin eso, el
/// texto puede salir con un tamaño completamente descontrolado, tapando toda la pantalla. Como
/// EntrarEdicionFlotante (y varios otros lugares) muestran un toast apenas entrás a un modo, esto
/// coincide exactamente con el momento reportado. Se reemplaza por el mismo Text clásico +
/// fuente embebida que ya usa el resto del proyecto -- técnica probada, sin depender de ningún
/// asset de TMP que pueda faltar.
/// </summary>
public class ToastNotificationUI : MonoBehaviour
{
    private static ToastNotificationUI instance;
    private Transform mainCameraTransform;

    [SerializeField] private float defaultDuration = 3f;
    [SerializeField] private Vector3 notificationOffset = new Vector3(0, 0.5f, 2f);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (mainCameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                mainCameraTransform = mainCamera.transform;
        }
    }

    /// <summary>
    /// Show a toast notification with custom duration.
    /// </summary>
    public static void Show(string message, float duration = 3f, NotificationType type = NotificationType.Info)
    {
        if (instance == null)
        {
            // Create instance if needed
            GameObject go = new GameObject("ToastNotificationUI");
            instance = go.AddComponent<ToastNotificationUI>();
        }

        instance.DisplayNotification(message, duration, type);
    }

    /// <summary>
    /// Show error notification (red).
    /// </summary>
    public static void ShowError(string message, float duration = 3f)
    {
        Show(message, duration, NotificationType.Error);
    }

    /// <summary>
    /// Show success notification (green).
    /// </summary>
    public static void ShowSuccess(string message, float duration = 3f)
    {
        Show(message, duration, NotificationType.Success);
    }

    /// <summary>
    /// Show warning notification (yellow).
    /// </summary>
    public static void ShowWarning(string message, float duration = 3f)
    {
        Show(message, duration, NotificationType.Warning);
    }

    private void DisplayNotification(string message, float duration, NotificationType type)
    {
        StartCoroutine(ShowNotificationCoroutine(message, duration, type));
    }

    private IEnumerator ShowNotificationCoroutine(string message, float duration, NotificationType type)
    {
        // Use default duration if none was specified as override
        if (duration <= 0f) duration = defaultDuration;

        // Create notification panel
        GameObject panelGO = new GameObject("Toast_" + System.Guid.NewGuid().ToString("N").Substring(0, 8));
        panelGO.transform.SetParent(transform);

        // Position relative to camera
        if (mainCameraTransform != null)
        {
            panelGO.transform.position = mainCameraTransform.position + mainCameraTransform.TransformDirection(notificationOffset);
            panelGO.transform.rotation = mainCameraTransform.rotation;
        }

        // Create canvas
        Canvas canvas = panelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelGO.AddComponent<CanvasScaler>();

        RectTransform canvasRect = panelGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400, 100);
        // Mismo factor de escala que usa el resto de los paneles world-space del proyecto
        // (CrudPanelController, WallPaintPanelController, etc.) -- 0.0015, no 0.001, por
        // consistencia (ninguno de los dos causaba el bug real, que era el font de TMP).
        canvasRect.localScale = Vector3.one * 0.0015f;

        // Create background image
        Image bgImage = panelGO.AddComponent<Image>();
        bgImage.color = GetColorForType(type);

        // Create text -- Text clásico de UGUI + fuente embebida (ver comentario de clase: TMP sin
        // sus recursos configurados es lo que causaba las "letras gigantes ilegibles").
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(panelGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 8f);
        textRect.offsetMax = new Vector2(-16f, -8f);

        Text textMesh = textGO.AddComponent<Text>();
        textMesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textMesh.text = message;
        textMesh.fontSize = 24;
        textMesh.alignment = TextAnchor.MiddleCenter;
        textMesh.color = Color.white;
        textMesh.horizontalOverflow = HorizontalWrapMode.Wrap;
        textMesh.verticalOverflow = VerticalWrapMode.Overflow;

        // Animate notification
        float elapsedTime = 0f;

        // Fade in (0.3s)
        while (elapsedTime < 0.3f)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsedTime / 0.3f);
            Color c = bgImage.color;
            c.a = alpha * GetAlphaForType(type);
            bgImage.color = c;
            yield return null;
        }

        // Wait
        yield return new WaitForSeconds(Mathf.Max(0, duration - 0.6f));

        // Fade out (0.3s)
        elapsedTime = 0f;
        while (elapsedTime < 0.3f)
        {
            elapsedTime += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsedTime / 0.3f));
            Color c = bgImage.color;
            c.a = alpha * GetAlphaForType(type);
            bgImage.color = c;
            yield return null;
        }

        // Destroy
        Destroy(panelGO);
    }

    private Color GetColorForType(NotificationType type)
    {
        return type switch
        {
            NotificationType.Error => new Color(1, 0, 0, 0.9f),
            NotificationType.Success => new Color(0, 1, 0, 0.9f),
            NotificationType.Warning => new Color(1, 1, 0, 0.9f),
            NotificationType.Info => new Color(0, 0.5f, 1, 0.9f),
            _ => new Color(0.5f, 0.5f, 0.5f, 0.9f)
        };
    }

    private float GetAlphaForType(NotificationType type)
    {
        return 0.9f;
    }

    public enum NotificationType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
