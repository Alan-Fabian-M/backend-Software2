using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Sexta ronda (2026-09-19), a pedido de Alan: "quiero que en el cuarto principal que es
/// Sala_Mvp haya un modal futurista donde estan los mandos del usuario mostrando cada boton que
/// es lo que hace". Panel de ayuda con la lista de botones de los dos controles y para qué sirve
/// cada uno en este visor -- pensado para que un usuario nuevo (por ejemplo, quien evalúe el
/// examen) entienda los controles sin que alguien se los tenga que explicar antes de probarlo.
///
/// Arquitectura idéntica a la del resto de los paneles del proyecto (Canvas World Space +
/// CanvasScaler + TrackedDeviceGraphicRaycaster + Text clásico de UGUI con la fuente
/// "LegacyRuntime.ttf", mismo patrón que ConfirmDialogUI/PresetLoaderController), armado 100% por
/// código en runtime -- no depende de ningún prefab hecho a mano en el Editor, porque esta sesión
/// no tiene forma de abrir el Editor de Unity. "Futurista" acá se resolvió con una paleta oscura
/// (fondo azul casi negro) y acentos en cian/magenta imitando una interfaz de ciencia ficción,
/// sin depender de ninguna imagen o shader custom (nada de eso se pudo probar/ver en el visor
/// real desde esta sesión, así que si el contraste o el tamaño de letra no queda cómodo en el
/// Quest, avisame los ajustes y los repito).
///
/// Se abre desde un nuevo botón "🎮 Controles" agregado al panel de selección de monoambiente
/// (PresetLoaderController) -- se decidió no atarlo a un botón físico nuevo del control (ya no
/// queda ninguno libre: X, A, B e Y están todos asignados, y los gatillos y el grip también
/// tienen su función) sino reusar el menú que ya se abre con A al entrar a Sala_MVP, agregándole
/// esta segunda opción.
///
/// La lista de abajo se armó revisando el binding real de cada script de esta carpeta
/// (FurnitureColorButtonController, WallDividerToolButtonController, PlayerFlightController,
/// UniversalEditModeController, FurnitureCatalogController, PresetLoaderController) para no
/// inventar ni documentar mal ningún botón.
/// </summary>
public class ControlsHelpPanelController : MonoBehaviour
{
    private static ControlsHelpPanelController instance;

    /// <summary>
    /// Séptima ronda (2026-09-19): consultado por PlayerFlightController para no ascender/
    /// descender mientras este panel está abierto (así el gatillo queda libre para clickear su
    /// botón "✕" en vez de competir con el vuelo). Mismo patrón que en los demás paneles.
    /// </summary>
    public static bool HayPanelAbierto => instance != null && instance.canvasGO != null && instance.canvasGO.activeSelf;

    private GameObject canvasGO;

    private static readonly Color ColorFondo = new Color(0.03f, 0.05f, 0.12f, 0.97f);
    private static readonly Color ColorFilaA = new Color(0.06f, 0.09f, 0.19f, 1f);
    private static readonly Color ColorFilaB = new Color(0.045f, 0.07f, 0.155f, 1f);
    private static readonly Color ColorCian = new Color(0.15f, 0.95f, 0.95f);
    private static readonly Color ColorMagenta = new Color(0.95f, 0.25f, 0.85f);

    private struct Fila
    {
        public string boton;
        public string funcion;
        public Fila(string b, string f) { boton = b; funcion = f; }
    }

    private static readonly Fila[] Filas = new Fila[]
    {
        new Fila("X (mando izquierdo)",
            "Con un mueble en la mano: cambia su color. Con un Muro divisorio en la mano: abre su panel de pintado/altura/longitud. Con las manos vacías: activa o desactiva el Vuelo libre."),
        new Fila("Gatillo izquierdo",
            "En Vuelo libre y con las manos vacías: subís."),
        new Fila("Gatillo derecho",
            "En Vuelo libre y con las manos vacías: bajás (se frena solo al llegar al piso o a un mueble)."),
        new Fila("Y (mando izquierdo)",
            "Apuntá con el rayo y apretá sobre un mueble o una pared para abrir su panel de edición (actualizar, rotar, eliminar)."),
        new Fila("A (mando derecho)",
            "Abre o cierra el menú para elegir el monoambiente (Estándar, Compacto, Grande, Loft)."),
        new Fila("B (mando derecho)",
            "Abre o cierra el catálogo de muebles para agregar."),
        new Fila("Agarre / Grip (cualquier mano)",
            "Agarra y suelta muebles, muros divisorios y las flechitas de redimensionar."),
        new Fila("Stick / Joystick",
            "Te movés por la sala."),
    };

    public static void EnsureExists()
    {
        if (instance != null) return;

        instance = Object.FindAnyObjectByType<ControlsHelpPanelController>();
        if (instance != null) return;

        GameObject managerGO = new GameObject("ControlsHelpPanelManager");
        instance = managerGO.AddComponent<ControlsHelpPanelController>();
        Object.DontDestroyOnLoad(managerGO);
    }

    /// <summary>Abre el panel, posicionándolo frente a la cámara actual.</summary>
    public static void Show()
    {
        EnsureExists();
        instance.ShowInternal();
    }

    private void Awake()
    {
        instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("ControlsHelpCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(950f, 800f);
        canvasGO.transform.localScale = Vector3.one * 0.0013f;

        GameObject panelRoot = new GameObject("Panel");
        panelRoot.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = panelRoot.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image bg = panelRoot.AddComponent<Image>();
        bg.color = ColorFondo;

        // Borde/acento "futurista": un marco fino en cian detrás del fondo, apenas más grande.
        GameObject borde = new GameObject("BordeAcento");
        borde.transform.SetParent(panelRoot.transform, false);
        borde.transform.SetAsFirstSibling();
        RectTransform bordeRect = borde.AddComponent<RectTransform>();
        bordeRect.anchorMin = Vector2.zero;
        bordeRect.anchorMax = Vector2.one;
        bordeRect.offsetMin = new Vector2(-6f, -6f);
        bordeRect.offsetMax = new Vector2(6f, 6f);
        Image bordeImg = borde.AddComponent<Image>();
        bordeImg.color = ColorCian;

        // Encabezado
        GameObject headerGO = new GameObject("Header");
        headerGO.transform.SetParent(panelRoot.transform, false);
        RectTransform headerRect = headerGO.AddComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.sizeDelta = new Vector2(0f, 90f);
        headerRect.anchoredPosition = Vector2.zero;
        Image headerBg = headerGO.AddComponent<Image>();
        headerBg.color = new Color(ColorCian.r, ColorCian.g, ColorCian.b, 0.18f);

        GameObject titleGO = new GameObject("Titulo");
        titleGO.transform.SetParent(headerGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.04f, 0f);
        titleRect.anchorMax = new Vector2(0.86f, 1f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;
        Text titleText = titleGO.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyle.Bold;
        titleText.color = ColorCian;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.text = "🎮 Controles";

        // Botón de cerrar (✕), esquina superior derecha del encabezado.
        CreateCloseButton(headerGO.transform);

        // Filas de contenido, apiladas verticalmente debajo del encabezado.
        GameObject listaGO = new GameObject("Filas");
        listaGO.transform.SetParent(panelRoot.transform, false);
        RectTransform listaRect = listaGO.AddComponent<RectTransform>();
        listaRect.anchorMin = new Vector2(0f, 0f);
        listaRect.anchorMax = new Vector2(1f, 1f);
        listaRect.offsetMin = new Vector2(20f, 20f);
        listaRect.offsetMax = new Vector2(-20f, -100f);

        float altoDisponible = 800f - 100f - 20f;
        float altoFila = altoDisponible / Filas.Length;

        for (int i = 0; i < Filas.Length; i++)
        {
            CreateFila(listaGO.transform, Filas[i], i, altoFila);
        }

        canvasGO.SetActive(false);
    }

    private void CreateFila(Transform parent, Fila fila, int indice, float altoFila)
    {
        GameObject filaGO = new GameObject($"Fila_{indice}");
        filaGO.transform.SetParent(parent, false);
        RectTransform rect = filaGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, altoFila - 4f);
        rect.anchoredPosition = new Vector2(0f, -indice * altoFila);

        Image filaBg = filaGO.AddComponent<Image>();
        filaBg.color = indice % 2 == 0 ? ColorFilaA : ColorFilaB;

        GameObject botonGO = new GameObject("Boton");
        botonGO.transform.SetParent(filaGO.transform, false);
        RectTransform botonRect = botonGO.AddComponent<RectTransform>();
        botonRect.anchorMin = new Vector2(0f, 0f);
        botonRect.anchorMax = new Vector2(0.34f, 1f);
        botonRect.offsetMin = new Vector2(16f, 4f);
        botonRect.offsetMax = new Vector2(-8f, -4f);
        Text botonText = botonGO.AddComponent<Text>();
        botonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        botonText.fontSize = 22;
        botonText.fontStyle = FontStyle.Bold;
        botonText.color = ColorMagenta;
        botonText.alignment = TextAnchor.MiddleLeft;
        botonText.horizontalOverflow = HorizontalWrapMode.Wrap;
        botonText.verticalOverflow = VerticalWrapMode.Overflow;
        botonText.text = fila.boton;

        GameObject funcionGO = new GameObject("Funcion");
        funcionGO.transform.SetParent(filaGO.transform, false);
        RectTransform funcionRect = funcionGO.AddComponent<RectTransform>();
        funcionRect.anchorMin = new Vector2(0.34f, 0f);
        funcionRect.anchorMax = new Vector2(1f, 1f);
        funcionRect.offsetMin = new Vector2(8f, 4f);
        funcionRect.offsetMax = new Vector2(-16f, -4f);
        Text funcionText = funcionGO.AddComponent<Text>();
        funcionText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        funcionText.fontSize = 20;
        funcionText.color = Color.white;
        funcionText.alignment = TextAnchor.MiddleLeft;
        funcionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        funcionText.verticalOverflow = VerticalWrapMode.Overflow;
        funcionText.text = fila.funcion;
    }

    private void CreateCloseButton(Transform parent)
    {
        GameObject btnGO = new GameObject("BotonCerrar");
        btnGO.transform.SetParent(parent, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-16f, 0f);
        rect.sizeDelta = new Vector2(64f, 64f);

        Image img = btnGO.AddComponent<Image>();
        img.color = new Color(0.75f, 0.15f, 0.2f, 0.9f);
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Hide);

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 32;
        labelText.fontStyle = FontStyle.Bold;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = "✕";
    }

    private void ShowInternal()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 desired = cam.transform.position + cam.transform.forward * 1.4f;
            desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);
            canvasGO.transform.position = desired;
            canvasGO.transform.rotation = Quaternion.LookRotation(canvasGO.transform.position - cam.transform.position);
        }

        canvasGO.SetActive(true);
    }

    private void Hide()
    {
        canvasGO.SetActive(false);
    }
}
