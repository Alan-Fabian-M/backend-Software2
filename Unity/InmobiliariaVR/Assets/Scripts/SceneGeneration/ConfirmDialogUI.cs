using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Diálogo de confirmación genérico "Sí/No" en un Canvas World Space flotante frente a la
/// cámara del jugador -- pensado en primer lugar para la papelera (punto 4 del plan de edición
/// avanzada: "¿Estás seguro de eliminar [mueble]?"), pero escrito como utilidad reusable para
/// cualquier otra confirmación futura del proyecto (por ejemplo, "Eliminar" dentro del modo de
/// edición universal CRUD).
///
/// Armado por código en runtime (mismo patrón que ya usan PresetLoaderController y
/// FurnitureCatalogController: Canvas + Button clásicos de UGUI, sin depender de un prefab
/// hecho a mano en el Editor).
/// </summary>
public class ConfirmDialogUI : MonoBehaviour
{
    private static ConfirmDialogUI instance;

    /// <summary>
    /// Séptima ronda (2026-09-19): consultado por PlayerFlightController para no ascender/
    /// descender mientras este diálogo está abierto (así el gatillo queda libre para clickear
    /// "Sí"/"No" en vez de competir con el vuelo). Mismo patrón que en los demás paneles.
    /// </summary>
    public static bool HayPanelAbierto => instance != null && instance.canvasGO != null && instance.canvasGO.activeSelf;

    private GameObject canvasGO;
    private GameObject panelRoot;
    private Text messageText;
    private System.Action pendingYes;
    private System.Action pendingNo;

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("ConfirmDialogManager");
        instance = go.AddComponent<ConfirmDialogUI>();
    }

    /// <summary>
    /// Muestra el diálogo con el mensaje dado, centrado frente a la cámara actual. Si ya había
    /// un diálogo pendiente sin resolver, se descarta (se prioriza el más nuevo) -- en la
    /// práctica no debería pasar porque solo hay un mueble sostenido a la vez por mano.
    /// </summary>
    public static void Show(string message, System.Action onYes, System.Action onNo)
    {
        EnsureExists();
        instance.ShowInternal(message, onYes, onNo);
    }

    private void Awake()
    {
        instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("ConfirmDialogCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        // BUG encontrado al implementar el gatito ayudante (Punto 5, 2026-09-18) y corregido acá
        // de paso: un GraphicRaycaster normal (pantalla/mouse) NO detecta el rayo 3D de los
        // controles VR sobre un Canvas World Space -- hace falta el TrackedDeviceGraphicRaycaster
        // de XRI (como ya usan FurnitureCatalogController/PresetLoaderController/etc.). Este
        // diálogo llevaba el raycaster equivocado desde que se creó (Fase 2), así que sus botones
        // "Sí/No" probablemente no respondían al rayo del control -- todavía no se había probado
        // en el Quest real para notarlo.
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(420f, 220f);
        canvasGO.transform.localScale = Vector3.one * 0.0015f;

        // Cuarta ronda (2026-09-19): revertido el "seguir a la cámara" (FollowCameraPanel) que se
        // había agregado en la tercera ronda -- Alan pidió volver a que el diálogo quede FIJO
        // donde se posicionó al abrirse.
        panelRoot = new GameObject("Panel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image bg = panelRoot.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);

        GameObject textGO = new GameObject("Mensaje");
        textGO.transform.SetParent(panelRoot.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.06f, 0.42f);
        textRect.anchorMax = new Vector2(0.94f, 0.94f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        messageText = textGO.AddComponent<Text>();
        messageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        messageText.fontSize = 26;
        messageText.color = Color.white;
        messageText.alignment = TextAnchor.MiddleCenter;
        messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
        messageText.verticalOverflow = VerticalWrapMode.Overflow;

        CreateButton("Sí, eliminar", new Vector2(0.07f, 0.10f), new Vector2(0.49f, 0.35f),
            new Color(0.72f, 0.20f, 0.19f), () => Resolve(true));
        CreateButton("No, conservar", new Vector2(0.51f, 0.10f), new Vector2(0.93f, 0.35f),
            new Color(0.24f, 0.5f, 0.28f), () => Resolve(false));

        canvasGO.SetActive(false);
    }

    private void CreateButton(string label, Vector2 anchorMin, Vector2 anchorMax, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject("Boton_" + label);
        btnGO.transform.SetParent(panelRoot.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = btnGO.AddComponent<Image>();
        img.color = color;
        Button btn = btnGO.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 22;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = label;
    }

    private void ShowInternal(string message, System.Action onYes, System.Action onNo)
    {
        pendingYes = onYes;
        pendingNo = onNo;
        messageText.text = message;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 desired = cam.transform.position + cam.transform.forward * 1.1f;
            desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);
            canvasGO.transform.position = desired;
            canvasGO.transform.rotation = Quaternion.LookRotation(canvasGO.transform.position - cam.transform.position);
        }

        canvasGO.SetActive(true);
    }

    private void Resolve(bool yes)
    {
        canvasGO.SetActive(false);

        System.Action toRun = yes ? pendingYes : pendingNo;
        pendingYes = null;
        pendingNo = null;
        toRun?.Invoke();
    }
}
