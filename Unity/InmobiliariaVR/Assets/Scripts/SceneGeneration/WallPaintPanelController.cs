using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Linq;

/// <summary>
/// Panel de pintado de muros (Sección B del plan de edición avanzada, 2026-09-17). Se abre al
/// seleccionar un muro (ver MuroEditable / SceneGenerator.HacerMuroInteractivo) y resuelve el
/// pedido de "nadie quiere pintar muro por muro": primero elegís un color (o "Restaurar
/// original"), y ese color pendiente recién se aplica cuando elegís el alcance:
///
///   1) 🎨 Pintar todos los muros -- copia el color/restauración a TODOS los muros de la sala
///      actual (incluidos los muros interiores nuevos, cuando existan).
///   2) 🖌️ Solo este muro -- aplica nada más al muro que señalaste.
///
/// Armado por código en runtime (mismo patrón que ConfirmDialogUI/PresetLoaderController).
/// </summary>
public class WallPaintPanelController : MonoBehaviour
{
    private static WallPaintPanelController instance;

    /// <summary>True mientras este panel está visible -- ajuste 2026-09-19 (punto 6) para la
    /// exclusión mutua con CrudPanelController (ver el comentario largo en esa clase): los dos
    /// paneles se cierran entre sí antes de mostrarse, para que nunca queden dos canvases
    /// world-space superpuestos flotando frente a la cámara al mismo tiempo.</summary>
    public static bool HayPanelAbierto { get; private set; }

    private static readonly (string nombre, Color color)[] PALETA = new (string, Color)[]
    {
        ("Blanco", new Color(0.92f, 0.92f, 0.90f)),
        ("Gris",   new Color(0.55f, 0.55f, 0.56f)),
        ("Beige",  new Color(0.80f, 0.72f, 0.58f)),
        ("Azul",   new Color(0.30f, 0.45f, 0.70f)),
        ("Verde",  new Color(0.35f, 0.55f, 0.38f)),
        ("Negro",  new Color(0.12f, 0.12f, 0.13f)),
    };

    private GameObject canvasGO;
    private MuroEditable muroObjetivo;
    private System.Action<MuroEditable> accionPendiente;
    private Text estadoText;
    private Button botonTodos;
    private Button botonSoloEste;
    private Image botonTodosImg;
    private Image botonSoloEsteImg;

    // Ajuste de altura (Supuesto 1 del plan): activado por default, para que el techo quede
    // parejo salvo que el usuario apague el interruptor a propósito para este muro puntual.
    private bool aplicarAlturaATodos = true;
    private Text alturaToggleLabel;
    private Image alturaToggleImg;

    // Fila de longitud (Sección A: solo aplica a un "Muro divisorio", se oculta para los muros
    // exteriores que vienen del JSON de la sala -- esos no se pueden acortar/alargar).
    private GameObject filaLongitudGO;

    private static readonly Color colorBotonActivo = new Color(0.85f, 0.65f, 0.15f);
    private static readonly Color colorBotonInactivo = new Color(0.3f, 0.3f, 0.32f);

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("WallPaintPanelManager");
        instance = go.AddComponent<WallPaintPanelController>();
    }

    public static void ShowFor(MuroEditable muro)
    {
        EnsureExists();
        instance.ShowInternal(muro);
    }

    private void Awake()
    {
        instance = this;
        BuildUI();
    }

    private void BuildUI()
    {
        canvasGO = new GameObject("WallPaintCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        // Mismo bug encontrado y corregido en ConfirmDialogUI (Punto 5, 2026-09-18): un
        // GraphicRaycaster normal no detecta el rayo 3D de los controles VR sobre un Canvas World
        // Space -- hace falta TrackedDeviceGraphicRaycaster (como FurnitureCatalogController).
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(520f, 490f);
        canvasGO.transform.localScale = Vector3.one * 0.0015f;

        // Cuarta ronda (2026-09-19): revertido el "seguir a la cámara" (FollowCameraPanel) que se
        // había agregado en la tercera ronda -- Alan pidió volver a que el panel quede FIJO donde
        // se posicionó al abrirse (ver ShowInternal).
        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGO.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.09f, 0.96f);

        // Título
        GameObject tituloGO = new GameObject("Titulo");
        tituloGO.transform.SetParent(panel.transform, false);
        RectTransform tituloRect = tituloGO.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.04f, 0.888f);
        tituloRect.anchorMax = new Vector2(0.96f, 0.969f);
        tituloRect.offsetMin = Vector2.zero;
        tituloRect.offsetMax = Vector2.zero;
        Text titulo = tituloGO.AddComponent<Text>();
        titulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titulo.fontSize = 24;
        titulo.color = Color.white;
        titulo.alignment = TextAnchor.MiddleLeft;
        titulo.text = "Pintar muro";

        // Fila de colores + botón de restaurar
        int totalSwatches = PALETA.Length + 1; // +1 por "Restaurar original"
        float swatchMargin = 0.02f;
        float swatchWidth = (1f - swatchMargin * (totalSwatches + 1)) / totalSwatches;
        float x = swatchMargin;

        for (int i = 0; i < PALETA.Length; i++)
        {
            Color c = PALETA[i].color;
            CreateSwatchButton(panel.transform, x, x + swatchWidth, 0.759f, 0.871f, c, () => ChooseColor(c));
            x += swatchWidth + swatchMargin;
        }
        CreateRestoreSwatchButton(panel.transform, x, x + swatchWidth, 0.759f, 0.871f, () => ChooseRestore());

        // Texto de estado ("Elegiste: Azul -- ahora elegí a quién aplicarlo")
        GameObject estadoGO = new GameObject("Estado");
        estadoGO.transform.SetParent(panel.transform, false);
        RectTransform estadoRect = estadoGO.AddComponent<RectTransform>();
        estadoRect.anchorMin = new Vector2(0.04f, 0.669f);
        estadoRect.anchorMax = new Vector2(0.96f, 0.735f);
        estadoRect.offsetMin = Vector2.zero;
        estadoRect.offsetMax = Vector2.zero;
        estadoText = estadoGO.AddComponent<Text>();
        estadoText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        estadoText.fontSize = 19;
        estadoText.color = new Color(0.85f, 0.85f, 0.85f);
        estadoText.alignment = TextAnchor.MiddleLeft;
        estadoText.text = "Elegí un color (o restaurar el original) arriba.";

        // Botones de alcance: Todos los muros / Solo este muro
        botonTodos = CreateActionButton(panel.transform, "🎨 Pintar todos los muros",
            new Vector2(0.04f, 0.547f), new Vector2(0.48f, 0.649f), out botonTodosImg, ApplyToAll);
        botonSoloEste = CreateActionButton(panel.transform, "🖌️ Solo este muro",
            new Vector2(0.52f, 0.547f), new Vector2(0.96f, 0.649f), out botonSoloEsteImg, ApplyToThis);

        // Fila de altura (Supuesto 1 del plan): -/+ para subir/bajar el muro, y un interruptor
        // para elegir si el cambio de altura se aplica a todos los muros o solo a este.
        CreateActionButton(panel.transform, "− Altura",
            new Vector2(0.04f, 0.441f), new Vector2(0.24f, 0.527f), out _, () => AjustarAltura(-0.1f),
            new Color(0.28f, 0.3f, 0.34f));
        CreateActionButton(panel.transform, "+ Altura",
            new Vector2(0.76f, 0.441f), new Vector2(0.96f, 0.527f), out _, () => AjustarAltura(0.1f),
            new Color(0.28f, 0.3f, 0.34f));

        GameObject toggleGO = new GameObject("ToggleAlcanceAltura");
        toggleGO.transform.SetParent(panel.transform, false);
        RectTransform toggleRect = toggleGO.AddComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(0.26f, 0.441f);
        toggleRect.anchorMax = new Vector2(0.74f, 0.527f);
        toggleRect.offsetMin = Vector2.zero;
        toggleRect.offsetMax = Vector2.zero;
        alturaToggleImg = toggleGO.AddComponent<Image>();
        Button toggleBtn = toggleGO.AddComponent<Button>();
        toggleBtn.onClick.AddListener(ToggleAlcanceAltura);

        GameObject toggleLabelGO = new GameObject("Label");
        toggleLabelGO.transform.SetParent(toggleGO.transform, false);
        RectTransform toggleLabelRect = toggleLabelGO.AddComponent<RectTransform>();
        toggleLabelRect.anchorMin = Vector2.zero;
        toggleLabelRect.anchorMax = Vector2.one;
        toggleLabelRect.offsetMin = Vector2.zero;
        toggleLabelRect.offsetMax = Vector2.zero;
        alturaToggleLabel = toggleLabelGO.AddComponent<Text>();
        alturaToggleLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        alturaToggleLabel.fontSize = 15;
        alturaToggleLabel.color = Color.white;
        alturaToggleLabel.alignment = TextAnchor.MiddleCenter;

        // Fila de longitud (Sección A: solo para "Muro divisorio" -- se oculta en ShowInternal
        // para un muro exterior, que no se puede acortar/alargar).
        filaLongitudGO = new GameObject("FilaLongitud");
        filaLongitudGO.transform.SetParent(panel.transform, false);
        RectTransform filaLongitudRect = filaLongitudGO.AddComponent<RectTransform>();
        filaLongitudRect.anchorMin = new Vector2(0f, 0.339f);
        filaLongitudRect.anchorMax = new Vector2(1f, 0.424f);
        filaLongitudRect.offsetMin = Vector2.zero;
        filaLongitudRect.offsetMax = Vector2.zero;

        CreateActionButton(filaLongitudGO.transform, "− Longitud",
            new Vector2(0.04f, 0f), new Vector2(0.24f, 1f), out _, () => AjustarLongitud(-0.2f),
            new Color(0.28f, 0.3f, 0.34f));
        GameObject longitudLabelGO = new GameObject("Label");
        longitudLabelGO.transform.SetParent(filaLongitudGO.transform, false);
        RectTransform longitudLabelRect = longitudLabelGO.AddComponent<RectTransform>();
        longitudLabelRect.anchorMin = new Vector2(0.26f, 0f);
        longitudLabelRect.anchorMax = new Vector2(0.74f, 1f);
        longitudLabelRect.offsetMin = Vector2.zero;
        longitudLabelRect.offsetMax = Vector2.zero;
        Text longitudLabel = longitudLabelGO.AddComponent<Text>();
        longitudLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        longitudLabel.fontSize = 15;
        longitudLabel.color = new Color(0.85f, 0.85f, 0.85f);
        longitudLabel.alignment = TextAnchor.MiddleCenter;
        longitudLabel.text = "Longitud del muro";
        CreateActionButton(filaLongitudGO.transform, "+ Longitud",
            new Vector2(0.76f, 0f), new Vector2(0.96f, 1f), out _, () => AjustarLongitud(0.2f),
            new Color(0.28f, 0.3f, 0.34f));

        // Cancelar
        CreateActionButton(panel.transform, "Cancelar",
            new Vector2(0.30f, 0.241f), new Vector2(0.70f, 0.318f), out _, Hide,
            new Color(0.35f, 0.16f, 0.16f));

        SetScopeButtonsEnabled(false);
        ActualizarLabelToggleAltura();
        canvasGO.SetActive(false);
    }

    private void AjustarLongitud(float deltaMetros)
    {
        muroObjetivo?.AdjustLength(deltaMetros);
        // El panel se queda abierto, igual que con la altura, para poder seguir ajustando.
    }

    private void AjustarAltura(float deltaMetros)
    {
        if (muroObjetivo == null) return;

        if (aplicarAlturaATodos)
            MuroEditable.AdjustHeightAll(deltaMetros);
        else
            muroObjetivo.AdjustHeight(deltaMetros);
        // El panel se queda abierto a propósito, para poder seguir ajustando de a 10cm sin
        // tener que volver a seleccionar el muro cada vez.
    }

    private void ToggleAlcanceAltura()
    {
        aplicarAlturaATodos = !aplicarAlturaATodos;
        ActualizarLabelToggleAltura();
    }

    private void ActualizarLabelToggleAltura()
    {
        if (alturaToggleLabel == null) return;
        alturaToggleLabel.text = aplicarAlturaATodos
            ? "✓ Altura: todos los muros"
            : "Altura: solo este muro";
        if (alturaToggleImg != null)
            alturaToggleImg.color = aplicarAlturaATodos ? colorBotonActivo : colorBotonInactivo;
    }

    private void CreateSwatchButton(Transform parent, float xMin, float xMax, float yMin, float yMax, Color color, System.Action onClick)
    {
        GameObject go = new GameObject("Swatch");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = color;
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());
    }

    private void CreateRestoreSwatchButton(Transform parent, float xMin, float xMax, float yMin, float yMax, System.Action onClick)
    {
        GameObject go = new GameObject("Swatch_Restaurar");
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(xMin, yMin);
        rect.anchorMax = new Vector2(xMax, yMax);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.27f);
        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 13;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = "↩ Original";
    }

    private Button CreateActionButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
        out Image imgOut, System.Action onClick, Color? colorOverride = null)
    {
        GameObject go = new GameObject("Boton_" + label);
        go.transform.SetParent(parent, false);
        RectTransform rect = go.AddComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image img = go.AddComponent<Image>();
        img.color = colorOverride ?? colorBotonInactivo;
        imgOut = img;

        Button btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick());

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(go.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text labelText = labelGO.AddComponent<Text>();
        labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelText.fontSize = 18;
        labelText.color = Color.white;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.text = label;

        return btn;
    }

    /// <summary>Cierra este panel si está abierto -- llamado por CrudPanelController antes de
    /// mostrarse, para la exclusión mutua del punto 6 (ver comentario de clase).</summary>
    public static void OcultarSiAbierto()
    {
        if (instance != null && HayPanelAbierto)
            instance.Hide();
    }

    private void ShowInternal(MuroEditable muro)
    {
        // Cuarta ronda (2026-09-19, "un modal a la vez"): faltaba este chequeo -- si hay una
        // sesión especial activa en OTRO objeto (redimensionar con esferitas, edición flotante o
        // de objetivo fijo), no se abre este panel encima; se avisa con un toast y se corta acá,
        // en vez de amontonar modales. (CrudPanelController.ShowInternal ya hace este mismo
        // chequeo desde la tercera ronda -- esta era la entrada que quedaba sin cubrir, tanto
        // desde el botón "🎨 Pintar" del CRUD como desde WallDividerToolButtonController.)
        if (ResizeHandlesController.HayModoActivo || UpdateModesController.HayModoEspecialActivo)
        {
            ToastNotificationUI.Show("⚠️ Terminá la edición en curso antes de pintar otro muro.", 3f);
            return;
        }

        // Ajuste 2026-09-19, punto 6: si el menú CRUD estaba abierto, cerrarlo primero -- evita
        // que los dos canvases queden superpuestos (ver comentario de clase de CrudPanelController).
        CrudPanelController.OcultarSiAbierto();

        muroObjetivo = muro;
        accionPendiente = null;
        estadoText.text = "Elegí un color (o restaurar el original) arriba.";
        SetScopeButtonsEnabled(false);

        // Por default, el ajuste de altura se aplica a todos los muros del cuarto (para que el
        // techo quede parejo) -- el usuario puede apagarlo para este muro puntual si quiere.
        aplicarAlturaATodos = true;
        ActualizarLabelToggleAltura();

        if (filaLongitudGO != null)
            filaLongitudGO.SetActive(muro != null && muro.EsDivisorInterior);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 desired = cam.transform.position + cam.transform.forward * 1.3f;
            desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);
            canvasGO.transform.position = desired;
            canvasGO.transform.rotation = Quaternion.LookRotation(canvasGO.transform.position - cam.transform.position);
        }

        canvasGO.SetActive(true);
        HayPanelAbierto = true;
    }

    private void ChooseColor(Color color)
    {
        accionPendiente = (m) => m.SetColor(color);
        estadoText.text = "Color elegido -- ahora elegí a quién aplicarlo:";
        SetScopeButtonsEnabled(true);
    }

    private void ChooseRestore()
    {
        accionPendiente = (m) => m.RestoreOriginal();
        estadoText.text = "Restaurar color original -- ahora elegí a quién aplicarlo:";
        SetScopeButtonsEnabled(true);
    }

    private void SetScopeButtonsEnabled(bool enabled)
    {
        if (botonTodos != null) botonTodos.interactable = enabled;
        if (botonSoloEste != null) botonSoloEste.interactable = enabled;
        if (botonTodosImg != null) botonTodosImg.color = enabled ? colorBotonActivo : colorBotonInactivo;
        if (botonSoloEsteImg != null) botonSoloEsteImg.color = enabled ? colorBotonActivo : colorBotonInactivo;
    }

    private void ApplyToThis()
    {
        if (accionPendiente != null && muroObjetivo != null)
            accionPendiente(muroObjetivo);
        Hide();
    }

    private void ApplyToAll()
    {
        if (accionPendiente != null)
        {
            // ToArray para no romper la iteración si algo dispara un OnDestroy/registro en el medio.
            foreach (var muro in MuroEditable.TodosLosMuros.ToArray())
                accionPendiente(muro);
        }
        Hide();
    }

    private void Hide()
    {
        canvasGO.SetActive(false);
        muroObjetivo = null;
        accionPendiente = null;
        HayPanelAbierto = false;
    }
}
