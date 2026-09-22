using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Panel CRUD del modo de edición universal (Punto 2 del plan de edición avanzada, 2026-09-18):
/// se abre sobre CUALQUIER mueble o pared con un CLICK de Y (ver UniversalEditModeController --
/// antes había que mantenerlo 3 segundos, se cambió a un solo click a pedido de Alan el
/// 2026-09-19) con 5 opciones -- Crear / Eliminar / Rotar / Actualizar / Salir.
///
/// - Crear: duplica el objeto (mueble o "Muro divisorio") al lado del original.
/// - Eliminar: borra el objeto, con la misma confirmación Sí/No que ya usa ConfirmDialogUI (en
///   vez de la papelera física de antes, eliminada a pedido de Alan el 2026-09-19).
/// - Rotar: alterna el modo Simple/Avanzada de ESTE objeto puntual (ajuste 2026-09-19, punto 3:
///   "rotación simple o avanzada por cada objeto y no individual" -- antes era un estado global
///   único compartido por el botón flotante de RotationModeController, que ya no existe).
/// - Actualizar: abre el submenú con las modalidades del plan (Edición flotante / Edición real /
///   Edición con objetivo fijo / Cambiar tamaño) -- ver UpdateModesController y
///   ResizeHandlesController.
/// - Pintar (solo en muros/divisores, ajuste 2026-09-19 punto 4 "poder editar los muros y división
///   al igual que los muebles"): abre WallPaintPanelController para este muro, sin necesidad de
///   sostenerlo con la mano y apretar el botón X aparte -- ahora es una opción más del mismo menú
///   unificado que usan los muebles.
/// - Salir: cierra el panel sin hacer nada.
///
/// Un muro EXTERIOR/fijo de la sala (JSON/preset, no un "Muro divisorio") no se puede duplicar ni
/// borrar (dejaría un agujero permanente en la sala) -- para esos, "Crear" y "Eliminar" quedan
/// ocultos y solo se ofrece Pintar/Rotar/Actualizar/Salir.
///
/// AJUSTE 2026-09-19 (punto 6, "solución el problema de división real no aparece nada y solo
/// aparece un texto cubriendo todo ilegibles"): este panel y WallPaintPanelController son dos
/// canvases world-space independientes que antes no sabían nada uno del otro -- si los dos
/// terminaban abiertos a la vez (por ejemplo: se abre este menú sobre un muro, se elige
/// "Actualizar", y mientras tanto seguía abierto el panel de pintado que se había disparado antes
/// con el botón X sosteniendo el muro) quedaban DOS canvases superpuestos flotando en el mismo
/// lugar frente a la cámara, lo que se veía como texto ilegible amontonado tapándolo todo. La
/// solución es exclusión mutua: cada uno se asegura de cerrar al otro antes de mostrarse (ver
/// ShowInternal acá abajo y OcultarSiAbierto/HayPanelAbierto en WallPaintPanelController).
/// </summary>
public class CrudPanelController : MonoBehaviour
{
    private static CrudPanelController instance;

    /// <summary>True mientras este panel (cualquiera de sus páginas) está visible -- consultado
    /// por UniversalEditModeController para no seguir acumulando el timer de hold mientras el
    /// panel ya está abierto.</summary>
    public static bool HayPanelAbierto { get; private set; }

    private GameObject canvasGO;
    private GameObject paginaPrincipal;
    private GameObject paginaActualizar;
    private Text tituloPrincipal;
    private Button botonCrear;
    private Button botonEliminar;
    private Button botonPintar;
    private Button botonAbrirCerrarAbertura;
    private Text abrirCerrarAberturaLabel;
    private Button botonRotar;
    private Button botonActualizar;
    private Button botonCambiarTamano;
    private Text rotarLabel;

    // Botones para el piso (maqueta completa)
    private Button botonExportarMaquetaPrincipal;
    private Button botonImprimirMaquetaPrincipal;
    private Button botonConfigurarBambuPrincipal;

    private GameObject objetivoActual;
    private SceneGenerator sceneGeneratorCache;

    // NUEVO: Bambu Lab
    // CORREGIDO (2026-09-19): antes este controller creaba su PROPIA copia de
    // BambuLabConfigPanel/BambuLabMQTT vía AddComponent en BuildUI() sobre este mismo GameObject
    // ("CrudPanelManager", que no tiene ninguna UI real), totalmente separada del panel real con
    // los inputs/botones que crea BambuLabConfigFormBuilder bajo el Canvas de la escena. Combinado
    // con el singleton destructivo que tenía BambuLabMQTT.Awake() (Destroy(gameObject) al detectar
    // una segunda instancia -- ver BambuLabMQTT.cs), esto podía borrar el GameObject completo del
    // panel de configuración real (toda su UI) apenas Unity ejecutara los Awake() en el orden
    // "equivocado" entre objetos distintos. Y aunque no se llegara a borrar nada, el botón
    // "⚙️ Bambu Config" hubiera lanzado NullReferenceException porque la copia fantasma nunca
    // tiene panelBackground asignado. La solución es no crear nada acá: se busca perezosamente
    // (una sola vez, cacheada) el panel REAL que ya existe en la escena -- ver
    // ObtenerBambuConfigPanel() más abajo -- y se usa BambuLabConfigPanel.BambuMqtt para llegar a
    // la única instancia real de BambuLabMQTT (vive junto al panel real, no acá).
    private BambuLabConfigPanel bambuConfigPanelCache;
    private Button botonConfigurarBambu;
    private Button botonEnviarBambu;

    private static readonly Color ColorNeutro = new Color(0.3f, 0.3f, 0.34f);
    private static readonly Color ColorCrear = new Color(0.2f, 0.55f, 0.35f);
    private static readonly Color ColorEliminar = new Color(0.6f, 0.2f, 0.2f);
    private static readonly Color ColorActualizar = new Color(0.2f, 0.45f, 0.65f);
    private static readonly Color ColorSalir = new Color(0.35f, 0.35f, 0.38f);

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("CrudPanelManager");
        instance = go.AddComponent<CrudPanelController>();
    }

    public static void ShowFor(GameObject objetivo)
    {
        EnsureExists();
        instance.ShowInternal(objetivo);
    }

    private void Awake()
    {
        instance = this;
        BuildUI();
    }

    // -----------------------------------------------------------------
    // Construcción de la UI
    // -----------------------------------------------------------------

    private void BuildUI()
    {
        canvasGO = new GameObject("CrudPanelCanvas");
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        // Se agrandó de 460 a 560, luego a 650, y ahora a 900 de alto (ajuste 2026-09-19) para que
        // en la página "Actualizar" entre el nuevo botón "📤 Enviar a PC" (mandar el mueble por WiFi
        // a la compu/servidor para slicear) además de los de edición, Bambu Config e Imprimir, sin
        // que se corten los botones "Volver/Salir" de abajo -- ese apretujamiento era justo lo que
        // hacía parecer que "faltaba" la opción de enviar a la PC.
        canvasRect.sizeDelta = new Vector2(520f, 900f);
        canvasGO.transform.localScale = Vector3.one * 0.0015f;

        // Cuarta ronda (2026-09-19): revertido el "seguir a la cámara" (FollowCameraPanel) que se
        // había agregado en la tercera ronda -- Alan pidió volver a que el panel quede FIJO donde
        // se posicionó al abrirse (ver MostrarPagina más abajo).
        Image fondo = canvasGO.AddComponent<Image>();
        fondo.color = new Color(0.08f, 0.08f, 0.09f, 0.97f);

        paginaPrincipal = BuildPaginaPrincipal();
        paginaActualizar = BuildPaginaActualizar();

        paginaPrincipal.SetActive(false);
        paginaActualizar.SetActive(false);
        canvasGO.SetActive(false);

        // Bambu Lab: ya NO se crea nada acá (ver el comentario largo junto a los campos de
        // Bambu Lab más arriba) -- el panel y su BambuLabMQTT se buscan perezosamente cuando
        // hacen falta, vía ObtenerBambuConfigPanel().
    }

    private GameObject BuildPaginaPrincipal()
    {
        GameObject pagina = CreatePagina("PaginaPrincipal");

        GameObject tituloGO = new GameObject("Titulo");
        tituloGO.transform.SetParent(pagina.transform, false);
        RectTransform tituloRect = tituloGO.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.5f, 0.5f);
        tituloRect.anchorMax = new Vector2(0.5f, 0.5f);
        tituloRect.anchoredPosition = new Vector2(0f, 290f);
        tituloRect.sizeDelta = new Vector2(480f, 60f);
        tituloPrincipal = tituloGO.AddComponent<Text>();
        tituloPrincipal.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tituloPrincipal.fontSize = 26;
        tituloPrincipal.alignment = TextAnchor.MiddleCenter;
        tituloPrincipal.color = Color.white;

        botonCrear = CreateBoton(pagina, "➕  Crear (duplicar)", 195f, ColorCrear, Duplicar);
        botonEliminar = CreateBoton(pagina, "🗑️  Eliminar", 105f, ColorEliminar, Eliminar);

        // Ajuste pedido por Alan (2026-09-19, punto 4: "poder editar los muros y división al igual
        // que los muebles"): solo visible cuando el objetivo es un muro/divisor (ver ShowInternal)
        // -- para muebles no tiene sentido, ya tienen su propio ciclo de color con el botón físico.
        // Botón pintar (solo muros/divisores)
        botonPintar = CreateBoton(pagina, "🎨  Pintar", 15f, ColorNeutro, AbrirPintar);

        // Botón abrir/cerrar puerta o ventana interactiva
        botonAbrirCerrarAbertura = CreateBoton(pagina, "🚪  Abrir / Cerrar", 15f, new Color(0.18f, 0.55f, 0.65f), AlternarAbertura);
        abrirCerrarAberturaLabel = botonAbrirCerrarAbertura.GetComponentInChildren<Text>();
        botonAbrirCerrarAbertura.gameObject.SetActive(false);

        // Ajuste pedido por Alan (2026-09-19, punto 3): ahora es por objeto, no un estado global
        botonRotar = CreateBoton(pagina, "🔄  Rotar", -75f, ColorNeutro, () =>
        {
            RotationModeController.ToggleModoPara(objetivoActual);
            ActualizarLabelRotar();
        });
        rotarLabel = botonRotar.GetComponentInChildren<Text>();

        botonActualizar = CreateBoton(pagina, "✏️  Actualizar", -165f, ColorActualizar, () => MostrarPagina(paginaActualizar));

        // Botones dedicados cuando se selecciona el Piso (maqueta completa)
        botonExportarMaquetaPrincipal = CreateBoton(pagina, "📦  Enviar Maqueta a PC (STL)", 195f, new Color(0.18f, 0.52f, 0.85f), () => EnviarMaquetaCompleta(objetivoActual));
        botonImprimirMaquetaPrincipal = CreateBoton(pagina, "🖨️  Imprimir Maqueta (Bambu)", 105f, new Color(0.18f, 0.58f, 0.38f), () => EnviarABambuLab(objetivoActual));
        botonConfigurarBambuPrincipal = CreateBoton(pagina, "⚙️  Bambu Config", 15f, ColorNeutro, () =>
        {
            var panel = ObtenerBambuConfigPanel();
            if (panel != null) panel.AbrirPanel();
            else ShowToast("❌ No se encontró el panel de configuración de Bambu Lab en la escena.");
        });

        botonExportarMaquetaPrincipal.gameObject.SetActive(false);
        botonImprimirMaquetaPrincipal.gameObject.SetActive(false);
        botonConfigurarBambuPrincipal.gameObject.SetActive(false);

        CreateBoton(pagina, "🚪  Salir", -255f, ColorSalir, Ocultar);

        return pagina;
    }

    private void AlternarAbertura()
    {
        if (objetivoActual == null) return;
        AberturaInteractivaVR abertura = objetivoActual.GetComponent<AberturaInteractivaVR>();
        if (abertura == null) abertura = objetivoActual.GetComponentInChildren<AberturaInteractivaVR>();
        if (abertura != null)
        {
            abertura.Toggle();
            ActualizarLabelAbertura(abertura);
        }
    }

    private void ActualizarLabelAbertura(AberturaInteractivaVR abertura)
    {
        if (abrirCerrarAberturaLabel == null || abertura == null) return;
        bool esVentana = abertura.tipo == AberturaInteractivaVR.TipoAbertura.Ventana;
        string icono = esVentana ? "🪟" : "🚪";
        string accion = abertura.EstaAbierta ? "Cerrar" : "Abrir";
        string nombre = esVentana ? "Ventana" : (abertura.tipo == AberturaInteractivaVR.TipoAbertura.PuertaDoble ? "Puerta Doble" : "Puerta");
        abrirCerrarAberturaLabel.text = $"{icono}  {accion} {nombre}";
    }

    /// <summary>Botón "🎨 Pintar" de la página principal -- solo aparece para muros/divisores
    /// (ver ShowInternal). Abre WallPaintPanelController para el muro actual y cierra este menú,
    /// respetando la exclusión mutua entre los dos paneles (ver comentario de clase, punto 6).</summary>
    private void AbrirPintar()
    {
        MuroEditable muro = objetivoActual != null ? objetivoActual.GetComponent<MuroEditable>() : null;
        Ocultar();
        if (muro != null)
            WallPaintPanelController.ShowFor(muro);
    }

    private GameObject BuildPaginaActualizar()
    {
        GameObject pagina = CreatePagina("PaginaActualizar");

        GameObject tituloGO = new GameObject("Titulo");
        tituloGO.transform.SetParent(pagina.transform, false);
        RectTransform tituloRect = tituloGO.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.5f, 0.5f);
        tituloRect.anchorMax = new Vector2(0.5f, 0.5f);
        tituloRect.anchoredPosition = new Vector2(0f, 380f);
        tituloRect.sizeDelta = new Vector2(480f, 60f);
        Text titulo = tituloGO.AddComponent<Text>();
        titulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titulo.fontSize = 26;
        titulo.alignment = TextAnchor.MiddleCenter;
        titulo.color = Color.white;
        titulo.text = "✏️ Actualizar";

        // Posiciones re-espaciadas (2026-09-19) tras agrandar el canvas a 900 de alto: ahora entran
        // los 3 modos de edición + Cambiar tamaño + "Enviar a PC" + Bambu Config + Imprimir + los
        // botones Volver/Salir, todos con aire entre sí y sin cortarse.
        CreateBoton(pagina, "🪄  Edición flotante", 290f, ColorNeutro,
            () => { UpdateModesController.EnsureExists(); UpdateModesController.Instance?.EntrarEdicionFlotante(objetivoActual); Ocultar(); });

        CreateBoton(pagina, "✋  Edición real", 205f, ColorNeutro,
            () => { UpdateModesController.EnsureExists(); UpdateModesController.Instance?.EntrarEdicionReal(objetivoActual); Ocultar(); });

        CreateBoton(pagina, "🎯  Edición con objetivo fijo", 120f, ColorNeutro,
            () => { UpdateModesController.EnsureExists(); UpdateModesController.Instance?.EntrarEdicionObjetivoFijo(objetivoActual); Ocultar(); });

        // Ajuste pedido por Alan (2026-09-19, punto 2): ya no es un panel de +/-, ahora abre el
        // gizmo de esferitas arrastrables alrededor del objeto (ResizeHandlesController).
        // Oculto para un muro EXTERIOR/fijo (ver ShowInternal): resize arbitrario rompería la
        // geometría de la sala, igual que Crear/Eliminar.
        botonCambiarTamano = CreateBoton(pagina, "📐  Cambiar tamaño", 35f, ColorNeutro,
            () => { ResizeHandlesController.Activar(objetivoActual); Ocultar(); });

        // NUEVO (2026-09-19): "Enviar a PC" -- exporta el STL de este mueble y lo manda por WiFi a
        // la compu/servidor (STLTransferClient -> stl_receiver_service.py), que lo guarda y lo abre
        // en Bambu Studio para slicear. Este es el PASO 1 del flujo de impresión. No depende de la
        // config de Bambu Lab (esa es para el paso 2), por eso siempre está habilitado.
        CreateBoton(pagina, "📤  Enviar a PC", -55f, new Color(0.2f, 0.55f, 0.8f),
            () => EnviarSTLaComputadora(objetivoActual));

        // Botón para configurar Bambu Lab (IP, Access Code, Serial de la impresora).
        botonConfigurarBambu = CreateBoton(pagina, "⚙️  Bambu Config", -145f, ColorNeutro,
            () => {
                var panel = ObtenerBambuConfigPanel();
                if (panel != null)
                    panel.AbrirPanel();
                else
                    ShowToast("❌ No se encontró el panel de configuración de Bambu Lab en la escena.");
            }
        );

        // PASO 2: enviar el archivo YA sliceado (.gcode.3mf) directo a la impresora por MQTT/FTPS.
        botonEnviarBambu = CreateBoton(pagina, "🖨️  Imprimir", -235f, ColorNeutro,
            () => {
                if (objetivoActual != null)
                {
                    EnviarABambuLab(objetivoActual);
                }
                else
                {
                    ShowToast("❌ Selecciona un objeto primero");
                }
            }
        );

        ActualizarEstadoBotonesBambu();

        CreateBotonChico(pagina, "← Volver", new Vector2(0.28f, 0.5f), -330f, ColorSalir,
            () => MostrarPagina(paginaPrincipal));
        CreateBotonChico(pagina, "Salir", new Vector2(0.72f, 0.5f), -330f, ColorSalir, Ocultar);

        return pagina;
    }

    private GameObject CreatePagina(string nombre)
    {
        GameObject pagina = new GameObject(nombre);
        pagina.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = pagina.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return pagina;
    }

    private Button CreateBoton(GameObject padre, string texto, float posY, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(padre.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(440f, 76f);

        Image img = btnGO.AddComponent<Image>();
        img.color = color;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 23;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = texto;

        return btn;
    }

    private void CreateBotonChico(GameObject padre, string texto, Vector2 anchorCentro, float posY, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(padre.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorCentro;
        rect.anchorMax = anchorCentro;
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(190f, 56f);

        Image img = btnGO.AddComponent<Image>();
        img.color = color;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        Text label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 21;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = texto;
    }

    // -----------------------------------------------------------------
    // Mostrar/ocultar
    // -----------------------------------------------------------------

    private void ShowInternal(GameObject objetivo)
    {
        // Ajuste 2026-09-19, punto 6: si el panel de pintado de muros estaba abierto, cerrarlo
        // primero -- evita que los dos canvases queden superpuestos (ver comentario de clase).
        WallPaintPanelController.OcultarSiAbierto();

        objetivoActual = objetivo;

        bool esPiso = EsPiso(objetivo);
        MuroEditable muro = objetivo != null ? objetivo.GetComponent<MuroEditable>() : null;
        bool esMuroExterior = muro != null && !muro.EsDivisorInterior;
        bool esMuro = muro != null;

        AberturaInteractivaVR abertura = objetivo != null ? objetivo.GetComponent<AberturaInteractivaVR>() : null;
        if (abertura == null && objetivo != null)
            abertura = objetivo.GetComponentInChildren<AberturaInteractivaVR>();
        bool esAbertura = abertura != null;

        if (esPiso)
        {
            tituloPrincipal.text = "🏛️ Configuración de Sala / Piso";
            botonCrear.gameObject.SetActive(false);
            botonEliminar.gameObject.SetActive(false);
            botonPintar.gameObject.SetActive(false);
            if (botonAbrirCerrarAbertura != null) botonAbrirCerrarAbertura.gameObject.SetActive(false);
            if (botonRotar != null) botonRotar.gameObject.SetActive(false);
            if (botonActualizar != null) botonActualizar.gameObject.SetActive(false);
            if (botonCambiarTamano != null) botonCambiarTamano.gameObject.SetActive(false);

            if (botonExportarMaquetaPrincipal != null) botonExportarMaquetaPrincipal.gameObject.SetActive(true);
            if (botonImprimirMaquetaPrincipal != null) botonImprimirMaquetaPrincipal.gameObject.SetActive(true);
            if (botonConfigurarBambuPrincipal != null) botonConfigurarBambuPrincipal.gameObject.SetActive(true);
        }
        else
        {
            tituloPrincipal.text = "🔧 " + NombreLegible(objetivo);
            botonCrear.gameObject.SetActive(!esMuroExterior);
            botonEliminar.gameObject.SetActive(!esMuroExterior);
            botonPintar.gameObject.SetActive(esMuro);
            if (botonAbrirCerrarAbertura != null)
            {
                botonAbrirCerrarAbertura.gameObject.SetActive(esAbertura);
                if (esAbertura) ActualizarLabelAbertura(abertura);
            }
            if (botonRotar != null) botonRotar.gameObject.SetActive(true);
            if (botonActualizar != null) botonActualizar.gameObject.SetActive(true);
            // Ajuste 2026-09-19: "Cambiar tamaño" oculto para muro exterior
            if (botonCambiarTamano != null) botonCambiarTamano.gameObject.SetActive(!esMuroExterior);

            if (botonExportarMaquetaPrincipal != null) botonExportarMaquetaPrincipal.gameObject.SetActive(false);
            if (botonImprimirMaquetaPrincipal != null) botonImprimirMaquetaPrincipal.gameObject.SetActive(false);
            if (botonConfigurarBambuPrincipal != null) botonConfigurarBambuPrincipal.gameObject.SetActive(false);

            ActualizarLabelRotar();
        }

        ActualizarEstadoBotonesBambu();

        MostrarPagina(paginaPrincipal);
    }

    /// <summary>Refleja en el botón "🔄 Rotar" el modo Simple/Avanzada de ESTE objeto puntual
    /// (ajuste 2026-09-19, punto 3 -- ya no es un estado global) cada vez que se abre el panel o
    /// se lo toca.</summary>
    private void ActualizarLabelRotar()
    {
        if (rotarLabel == null) return;
        rotarLabel.text = (RotationModeController.ObtenerModo(objetivoActual) == ModoRotacion.Simple)
            ? "🔄  Rotar (actual: Simple)"
            : "🔄  Rotar (actual: Avanzada)";
    }

    /// <summary>Cierra este panel si está abierto -- llamado por WallPaintPanelController antes de
    /// mostrarse, para la exclusión mutua del punto 6 (ver comentario de clase).</summary>
    public static void OcultarSiAbierto()
    {
        if (instance != null && HayPanelAbierto)
            instance.Ocultar();
    }

    /// <summary>
    /// Cambia qué página del panel está visible (Principal o Actualizar) y reposiciona el canvas
    /// frente a la cámara. Antes se llamaba también "ShowInternal" (igual que el método de arriba),
    /// pero al tener los dos exactamente la misma firma (un solo parámetro GameObject) el compilador
    /// los trataba como una sobrecarga duplicada e ilegal -- error CS0111 que rompía el build entero
    /// (2026-09-18, reportado por Alan tras el primer intento real de build en el Editor).
    /// </summary>
    private void MostrarPagina(GameObject pagina)
    {
        paginaPrincipal.SetActive(pagina == paginaPrincipal);
        paginaActualizar.SetActive(pagina == paginaActualizar);

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 desired = cam.transform.position + cam.transform.forward * 1.2f;
            desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);
            canvasGO.transform.position = desired;
            canvasGO.transform.rotation = Quaternion.LookRotation(canvasGO.transform.position - cam.transform.position);
        }

        canvasGO.SetActive(true);
        HayPanelAbierto = true;
    }

    private void Ocultar()
    {
        canvasGO.SetActive(false);
        HayPanelAbierto = false;
    }

    // -----------------------------------------------------------------
    // Crear (duplicar)
    // -----------------------------------------------------------------

    private void Duplicar()
    {
        GameObject objetivo = objetivoActual;
        Ocultar();
        if (objetivo == null) return;

        MuroEditable muro = objetivo.GetComponent<MuroEditable>();
        if (muro != null && muro.EsDivisorInterior)
            DuplicarMuroDivisorio(objetivo, muro);
        else if (muro == null)
            DuplicarMueble(objetivo);
        // Un muro exterior nunca llega acá: el botón queda oculto para ese caso (ver ShowInternal).
    }

    private void DuplicarMuroDivisorio(GameObject original, MuroEditable muroOriginal)
    {
        SceneGenerator sceneGenerator = ObtenerSceneGenerator();
        if (sceneGenerator == null) return;

        Vector3 nuevaPos = original.transform.position + original.transform.right * 0.5f;

        GameObject nuevo = sceneGenerator.CreateInteriorWallDivider(
            nuevaPos,
            longitudInicialM: original.transform.localScale.x,
            alturaM: original.transform.localScale.y,
            espesorM: original.transform.localScale.z);

        // CreateInteriorWallDivider siempre arranca con el material default de fábrica -- si el
        // original estaba repintado, se copia ese color para que el duplicado salga igual.
        Renderer rendOriginal = original.GetComponent<Renderer>();
        MuroEditable muroNuevo = nuevo != null ? nuevo.GetComponent<MuroEditable>() : null;
        if (rendOriginal != null && rendOriginal.sharedMaterial != null && muroNuevo != null)
            muroNuevo.SetColor(rendOriginal.sharedMaterial.color);

        ToastNotificationUI.Show("🧱 Muro divisorio duplicado", 2.5f);
    }

    /// <summary>
    /// Duplica un mueble clonando el GameObject en vivo (para que el duplicado sea visualmente
    /// idéntico -- misma variante/escala/color -- y no una variante random del mismo tipo).
    ///
    /// Ojo con esto: Instantiate() clona los componentes y su estado SERIALIZADO, pero NO los
    /// listeners que SceneGenerator.HacerInteractivo agregó por código (selectEntered/hoverEntered/
    /// etc. vía AddListener no quedan en ningún dato serializado, existen solo en memoria del
    /// componente original) -- si se dejaran esos componentes clonados tal cual, el duplicado
    /// quedaría con un XRGrabInteractable/XRSimpleInteractable "mudo" (sin ninguna de esas
    /// conexiones) y FurnitureDeleterVR con una referencia rota a un interactable que se destruye
    /// después. Por eso se destruyen esos componentes puntuales del clon y se llama de nuevo a
    /// HacerInteractivo para que los reconstruya de cero, ya conectados -- mismo patrón que ya usa
    /// ColocarMueble en FurnitureCatalogController para un mueble instanciado desde cero, adaptado
    /// acá a un mueble clonado en vez de instanciado desde un prefab limpio.
    /// </summary>
    private void DuplicarMueble(GameObject original)
    {
        SceneGenerator sceneGenerator = ObtenerSceneGenerator();
        if (sceneGenerator == null) return;

        SceneElementMetadata metaOriginal = original.GetComponent<SceneElementMetadata>();
        string tipo = metaOriginal != null ? metaOriginal.elementType : null;
        if (string.IsNullOrEmpty(tipo))
        {
            ToastNotificationUI.ShowWarning("No se pudo duplicar: este objeto no tiene un tipo reconocido.", 3f);
            return;
        }

        Vector3 nuevaPos = original.transform.position + original.transform.right * 0.4f;
        GameObject clone = Instantiate(original, nuevaPos, original.transform.rotation);
        clone.name = original.name + "_copia";

        DestroyComponenteSiExiste<XRGrabInteractable>(clone);
        DestroyComponenteSiExiste<XRSimpleInteractable>(clone);
        DestroyComponenteSiExiste<MaterialChangerVR>(clone);
        DestroyComponenteSiExiste<FurnitureDeleterVR>(clone);
        DestroyComponenteSiExiste<SceneElementMetadata>(clone);

        var metaClone = clone.AddComponent<SceneElementMetadata>();
        metaClone.elementId = $"{metaOriginal.elementId}_copia_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";
        metaClone.elementType = tipo;
        metaClone.jsonData = "";

        sceneGenerator.RegisterExternalObject(clone);
        sceneGenerator.HacerInteractivo(clone, tipo);

        var aberturaClone = clone.GetComponent<AberturaInteractivaVR>();
        if (aberturaClone == null) aberturaClone = clone.GetComponentInChildren<AberturaInteractivaVR>();
        if (aberturaClone != null)
        {
            SceneGenerator.SnapToWall(clone, aberturaClone.tipo == AberturaInteractivaVR.TipoAbertura.Ventana);
        }
        else
        {
            SceneGenerator.SnapToFloor(clone);
        }

        ToastNotificationUI.Show($"Duplicado: {NombreLegible(original)}", 2.5f);
    }

    private void DestroyComponenteSiExiste<T>(GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        // DestroyImmediate (no Destroy): hace falta que el componente desaparezca YA, dentro de
        // esta misma llamada, porque un par de líneas más abajo se vuelve a llamar a
        // HacerInteractivo esperando que el componente YA NO esté -- con Destroy (que recién
        // limpia al final del frame) todavía lo encontraría y no lo recrearía.
        if (comp != null) DestroyImmediate(comp);
    }

    // -----------------------------------------------------------------
    // Eliminar
    // -----------------------------------------------------------------

    private void Eliminar()
    {
        GameObject objetivo = objetivoActual;
        Ocultar();
        if (objetivo == null) return;

        string nombre = NombreLegible(objetivo);
        ConfirmDialogUI.Show($"¿Eliminar {nombre}?",
            onYes: () =>
            {
                if (objetivo != null)
                {
                    Destroy(objetivo);
                    ToastNotificationUI.Show($"Eliminado: {nombre}", 2f);
                }
            },
            onNo: null);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private SceneGenerator ObtenerSceneGenerator()
    {
        if (sceneGeneratorCache == null)
            sceneGeneratorCache = Object.FindAnyObjectByType<SceneGenerator>();
        return sceneGeneratorCache;
    }

    /// <summary>
    /// Busca (una sola vez, cacheado) el BambuLabConfigPanel REAL de la escena -- el que crea
    /// BambuLabConfigFormBuilder bajo el Canvas, con su UI de verdad. Ver el comentario largo
    /// junto a los campos de Bambu Lab de esta clase para el porqué de este cambio.
    /// </summary>
    private BambuLabConfigPanel ObtenerBambuConfigPanel()
    {
        // OJO (bug real encontrado 2026-09-19): BambuLabConfigPanel.Start() empieza ocultando
        // panelBackground -- que en la versión generada por BambuLabConfigFormBuilder es el
        // MISMO GameObject que tiene este componente, no solo su parte visual. O sea: apenas
        // arranca el juego, el panel entero (incluido el propio componente) queda inactivo.
        // FindAnyObjectByType SIN el parámetro FindObjectsInactive.Include ignora objetos
        // inactivos por default -- por eso nunca lo encontraba (el botón "⚙️ Bambu Config"
        // tiraba "No se encontró el panel..." aunque el panel existiera perfectamente en la
        // Hierarchy). Con Include lo sigue encontrando esté abierto o cerrado.
        if (bambuConfigPanelCache == null)
            bambuConfigPanelCache = Object.FindAnyObjectByType<BambuLabConfigPanel>(FindObjectsInactive.Include);
        return bambuConfigPanelCache;
    }

    private static string NombreLegible(GameObject obj)
    {
        if (obj == null) return "objeto";
        return obj.name.Replace("Item_", "").Replace("(Clone)", "").Trim();
    }

    // -----------------------------------------------------------------
    // Bambu Lab Integration
    // -----------------------------------------------------------------

    private void ActualizarEstadoBotonesBambu()
    {
        var panel = ObtenerBambuConfigPanel();
        if (panel == null)
            return;

        bool tieneConfig = panel.TieneConfiguracion();

        if (botonEnviarBambu != null)
        {
            botonEnviarBambu.interactable = tieneConfig;

            // Cambiar color si está deshabilitado
            ColorBlock colors = botonEnviarBambu.colors;
            if (!tieneConfig)
            {
                colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
            botonEnviarBambu.colors = colors;
        }

        if (botonImprimirMaquetaPrincipal != null)
        {
            botonImprimirMaquetaPrincipal.interactable = tieneConfig;
            ColorBlock colors = botonImprimirMaquetaPrincipal.colors;
            if (!tieneConfig)
            {
                colors.disabledColor = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            }
            botonImprimirMaquetaPrincipal.colors = colors;
        }
    }

    /// <summary>Comprueba si el objeto seleccionado es el piso de la sala o departamento, excluyendo el entorno VR.</summary>
    private static bool EsPiso(GameObject obj)
    {
        if (obj == null) return false;
        if (SceneGenerator.EsElementoEntornoVirtual(obj)) return false;

        string n = obj.name.ToLower();
        if (n.Contains("grid") || n.Contains("teleport") || n.Contains("environment") || n.Contains("template"))
            return false;

        if (obj.CompareTag("Floor")) return true;
        if (n.Contains("piso") || n.Contains("floor")) return true;
        var meta = obj.GetComponent<SceneElementMetadata>();
        if (meta != null && meta.elementType != null && meta.elementType.ToLower() == "piso") return true;
        return false;
    }

    /// <summary>
    /// Exporta la maqueta completa de la sala (piso + muros + muebles dentro del perímetro)
    /// a un archivo STL escalado para impresión y lo envía a la PC por WiFi.
    /// Delimita estrictamente la exportación a la superficie real del departamento.
    /// </summary>
    private void EnviarMaquetaCompleta(GameObject piso)
    {
        if (piso == null)
        {
            ShowToast("❌ Selecciona el piso primero");
            return;
        }

        try
        {
            var sceneGen = ObtenerSceneGenerator();
            System.Collections.Generic.List<GameObject> objetosSala;
            Bounds limitePiso = SceneGenerator.CalcularBoundsPisoCompleto(piso);

            if (sceneGen != null)
            {
                objetosSala = sceneGen.GetObjectsInsideFloor(piso);
            }
            else
            {
                objetosSala = new System.Collections.Generic.List<GameObject> { piso };
            }

            if (objetosSala == null || objetosSala.Count == 0)
            {
                ShowToast("❌ No se encontraron objetos dentro del piso.");
                return;
            }

            STLTransferClient client = GetComponent<STLTransferClient>();
            if (client == null)
                client = gameObject.AddComponent<STLTransferClient>();

            string nombreMaqueta = $"Maqueta_Sala_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            ShowToast($"🏛️ Exportando maqueta ({objetosSala.Count} objetos)...", 2.5f);
            client.ExportAndSendRoomSTL(objetosSala, nombreMaqueta, limitePiso);
        }
        catch (System.Exception ex)
        {
            ShowToast($"❌ Error exportando maqueta: {ex.Message}");
            Debug.LogError($"[CrudPanelController] Error en EnviarMaquetaCompleta: {ex}");
        }
    }

    /// <summary>
    /// Flujo real de impresión (corregido 2026-09-19 -- ver BambuLabMQTT.cs para el detalle
    /// completo del porqué). Una impresora Bambu Lab NO puede imprimir un STL crudo: necesita
    /// G-code, que se genera "sliceando" el modelo (normalmente con Bambu Studio en una PC).
    /// Este método por lo tanto:
    ///   1. Busca si ya existe un archivo YA SLICEADO ({nombre}.gcode.3mf) en la carpeta
    ///      "Listos_para_imprimir" -- si existe, lo sube por FTPS y dispara la impresión.
    ///   2. Si no existe, exporta el STL a "STL_para_slicear" y le explica al usuario el paso
    ///      manual que falta (abrirlo en Bambu Studio, slicearlo, guardar el resultado con el
    ///      nombre esperado en "Listos_para_imprimir"). La próxima vez que presione "Imprimir"
    ///      para ese mismo objeto, el archivo ya sliceado se enviará directo.
    /// </summary>
    /// <summary>
    /// PASO 1 del flujo de impresión: exporta el STL de este mueble o maqueta y lo manda por WiFi a la
    /// compu/servidor (vía STLTransferClient -> stl_receiver_service.py). Allá se guarda en
    /// ~/Documents/InmobiliariaVR_Exports/STL_para_slicear/ y se abre en Bambu Studio para
    /// slicear. NO depende de la config de Bambu Lab (eso es el paso 2, Imprimir).
    /// </summary>
    private void EnviarSTLaComputadora(GameObject objeto)
    {
        if (objeto == null)
        {
            ShowToast("❌ Selecciona un objeto primero");
            return;
        }

        if (EsPiso(objeto))
        {
            EnviarMaquetaCompleta(objeto);
            return;
        }

        try
        {
            STLTransferClient client = GetComponent<STLTransferClient>();
            if (client == null)
                client = gameObject.AddComponent<STLTransferClient>();

            // ExportAndSendSTL ya muestra sus propios toasts de progreso/resultado.
            client.ExportAndSendSTL(objeto);
        }
        catch (System.Exception ex)
        {
            ShowToast($"❌ Error enviando a la PC: {ex.Message}");
            Debug.LogError($"Error en EnviarSTLaComputadora: {ex}");
        }
    }

    private void EnviarABambuLab(GameObject objeto)
    {
        if (objeto == null)
        {
            ShowToast("❌ Selecciona un objeto primero");
            return;
        }

        if (EsPiso(objeto))
        {
            // Para la maqueta completa de la sala: enviar el STL a la PC para slicear en Bambu Studio
            EnviarMaquetaCompleta(objeto);
            return;
        }
        var panel = ObtenerBambuConfigPanel();
        if (panel == null)
        {
            ShowToast("❌ No se encontró el panel de configuración de Bambu Lab en la escena.");
            return;
        }

        if (!panel.TieneConfiguracion())
        {
            ShowToast("⚠️ Configura primero: ⚙️ Bambu Config\n(IP, Access Code y Serial Number)");
            return;
        }

        BambuLabMQTT bambuMQTT = panel.BambuMqtt;
        if (bambuMQTT == null)
        {
            ShowToast("❌ BambuLabMQTT no está inicializado en el panel de configuración.");
            return;
        }

        // Nota: el enum Environment.SpecialFolder de .NET no tiene un miembro "Documents" -- el
        // nombre real es "MyDocuments" (esto rompía la compilación con CS0117).
        string carpetaBase = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
            "InmobiliariaVR_Exports");
        string carpetaSTL = System.IO.Path.Combine(carpetaBase, "STL_para_slicear");
        string carpetaListos = System.IO.Path.Combine(carpetaBase, "Listos_para_imprimir");

        try
        {
            System.IO.Directory.CreateDirectory(carpetaSTL);
            System.IO.Directory.CreateDirectory(carpetaListos);
        }
        catch (System.Exception ex)
        {
            ShowToast($"❌ Error creando carpetas de exportación: {ex.Message}");
            return;
        }

        string nombreBase = NombreLegible(objeto).Replace(" ", "_");
        string rutaGcode3mf = System.IO.Path.Combine(carpetaListos, $"{nombreBase}.gcode.3mf");

        if (!System.IO.File.Exists(rutaGcode3mf))
        {
            // Todavía no hay una versión sliceada de este objeto -- usamos STLTransferClient
            // para enviar el STL por WiFi a la PC, donde se abre automáticamente en Bambu Studio.
            // El usuario hace el slicing ahí y guarda el resultado como {nombreBase}.gcode.3mf
            // en la carpeta Listos_para_imprimir.

            ShowToast("📤 Exportando STL y enviando a tu PC por WiFi...");
            Debug.Log($"📤 Enviando STL de {nombreBase} a PC por WiFi");

            try
            {
                // Obtener cliente de transferencia (o crear uno)
                STLTransferClient client = GetComponent<STLTransferClient>();
                if (client == null)
                    client = gameObject.AddComponent<STLTransferClient>();

                // Enviar STL a PC por WiFi - el servidor lo abrirá en Bambu Studio
                client.ExportAndSendSTL(objeto);

                // Mensaje informativo
                ShowToast($"📤 Enviando {nombreBase}.stl a tu PC...\n\nEn Bambu Studio:\n1. Slice el modelo\n2. Guarda como:\n\"{nombreBase}.gcode.3mf\"\nen Listos_para_imprimir/\n\n3. Presiona Imprimir de nuevo");
            }
            catch (System.Exception ex)
            {
                ShowToast($"❌ Error enviando STL: {ex.Message}");
                Debug.LogError($"Error: {ex}");
            }
            return;
        }

        // Ya existe un archivo sliceado con este nombre: se sube por FTPS y se manda a imprimir.
        string ip = panel.ObtenerIPGuardada();
        string accessCode = panel.ObtenerAccessCodeGuardado();
        string serial = panel.ObtenerSerialNumberGuardado();

        ShowToast($"📡 Conectando a {ip}...");
        Debug.Log($"📡 Intentando conectar a {ip} (serial {serial})");

        try
        {
            bambuMQTT.ConnectToLocalPrinter(ip, accessCode, serial);
            StartCoroutine(EsperarConexionYEnviar(bambuMQTT, rutaGcode3mf, nombreBase, ip));
        }
        catch (System.Exception ex)
        {
            ShowToast($"❌ Error: {ex.Message}");
            Debug.LogError($"Error enviando a Bambu: {ex}");
            if (bambuMQTT != null)
                bambuMQTT.Disconnect();
        }
    }

    private System.Collections.IEnumerator EsperarConexionYEnviar(
        BambuLabMQTT bambuMQTT, string rutaArchivo, string nombreObjeto, string ip)
    {
        float tiempoEspera = 0f;
        float tiempoMaximo = 5f;

        // Esperar a que se complete el handshake TLS + CONNACK
        while (tiempoEspera < tiempoMaximo)
        {
            if (bambuMQTT.IsConnected())
            {
                Debug.Log($"✅ Conectado a Bambu Lab: {ip}");
                break;
            }

            tiempoEspera += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }

        if (!bambuMQTT.IsConnected())
        {
            ShowToast($"❌ No se conectó a {ip}\nVerifica IP, Access Code y Serial Number");
            Debug.LogError($"Timeout: No se conectó a {ip}");
            bambuMQTT.Disconnect();
            yield break;
        }

        // Subir el archivo por FTPS y publicar el comando MQTT de impresión (ver
        // BambuLabMQTT.PrintFileCoroutine -- hace ambos pasos y avisa cuando termina).
        ShowToast("📤 Subiendo archivo (FTPS) y enviando comando de impresión...");

        bool exitoFinal = false;
        string mensajeFinal = "";
        yield return bambuMQTT.PrintFileCoroutine(rutaArchivo, (exito, msg) =>
        {
            exitoFinal = exito;
            mensajeFinal = msg;
        });

        if (exitoFinal)
        {
            ShowToast($"✅ ¡Impresión iniciada!\n{nombreObjeto}");
            Debug.Log($"✅ {nombreObjeto} enviado a imprimir");
        }
        else
        {
            ShowToast($"❌ {mensajeFinal}");
            Debug.LogError($"Error en PrintFileCoroutine: {mensajeFinal}");
        }

        // Desconectar después de 2 segundos
        yield return new WaitForSeconds(2f);
        if (bambuMQTT != null)
        {
            bambuMQTT.Disconnect();
            Debug.Log("📡 Desconectado de Bambu Lab");
        }
    }

    private void ShowToast(string mensaje, float duracion = 3f)
    {
        Debug.Log($"[TOAST] {mensaje}");
        ToastNotificationUI.Show(mensaje, duracion);
    }
}
