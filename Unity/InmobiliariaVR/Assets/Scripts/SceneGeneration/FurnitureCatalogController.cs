using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Catálogo de muebles: un botón flotante siempre visible (igual de seleccionable que
/// cualquier mueble o menú del proyecto, vía puntero/mano XR) que abre un panel con pestañas
/// -- una por categoría del PrefabDatabase ("Sofas", "Kitchen", "Bathroom", etc.) -- y dentro
/// de cada pestaña, botones con el nombre de cada variante disponible. Al elegir una variante,
/// se instancia el prefab justo frente al usuario, se le da la misma interactividad que a
/// cualquier mueble generado (mover/rotar, cambiar color, borrar con doble click, según
/// PrefabMapper.GetMetadata) y se registra en el SceneGenerator para que cuente al exportar/
/// guardar la escena actual.
///
/// Sigue el mismo patrón que PresetLoaderController/ManipulatorMenuFactory: Canvas World Space
/// creado en runtime, posicionado relativo a Camera.main, con botones de UI clásicos (Button +
/// Image + Text), sin dependencias nuevas.
///
/// Uso: agregar este componente a un GameObject vacío en la escena (por ejemplo, agregando un
/// paso más a SETUP_SceneGeneration.cs, o manualmente vía Inspector). No requiere configuración:
/// arma toda su UI en Start().
/// </summary>
public class FurnitureCatalogController : MonoBehaviour
{
    private static FurnitureCatalogController instance;

    /// <summary>
    /// Séptima ronda (2026-09-19): consultado por PlayerFlightController para no ascender/
    /// descender mientras este panel está abierto (así el gatillo queda libre para clickear sus
    /// botones en vez de competir con el vuelo). Se calcula en vivo desde `catalogPanel.activeSelf`
    /// en vez de trackearlo a mano en cada uno de los varios lugares que lo abren/cierran (el botón
    /// B, el botón "✕", después de colocar un mueble, etc.) -- así no hay riesgo de que se
    /// desincronice si en el futuro se agrega otro lugar más que lo cierre.
    /// </summary>
    public static bool HayPanelAbierto => instance != null && instance.catalogPanel != null && instance.catalogPanel.activeSelf;

    [Tooltip("Offset del botón lanzador relativo a la cámara: X=derecha, Y=arriba, Z=adelante")]
    [SerializeField] private Vector3 launcherOffset = new Vector3(0.55f, -0.35f, 1.2f);

    [Tooltip("Offset del panel completo del catálogo relativo a la cámara")]
    [SerializeField] private Vector3 panelOffset = new Vector3(0, 0, 1.6f);

    [Tooltip("Dónde aparece un mueble recién elegido del catálogo, relativo a la cámara")]
    [SerializeField] private Vector3 spawnOffset = new Vector3(0, -0.3f, 1.0f);

    private SceneGenerator sceneGenerator;
    private Transform mainCameraTransform;

    private GameObject launcherButton;
    private GameObject catalogPanel;
    private Transform tabsRow;
    private Transform itemsContent;
    private Text categoryTitleText;
    private InputField searchField;
    private Text selectedItemText;
    private Button placeButton;

    private List<string> categories = new List<string>();
    private string currentCategory;
    private int spawnedCount = 0;

    // --- Selección actual dentro del panel (Punto 1 del plan de edición avanzada, 2026-09-17:
    // rediseño del panel a grilla centrada tipo "7 Days to Die"). Antes, tocar un item lo
    // colocaba al instante; ahora tocar un item solo lo SELECCIONA (se resalta en la grilla y se
    // muestra su nombre abajo), y recién se coloca al apretar el botón "Colocar mueble" del pie
    // del panel -- igual que en la referencia que mandaste.
    private readonly Dictionary<string, Image> tabImages = new Dictionary<string, Image>();
    private GameObject selectedTileGO;
    private Image selectedTileImg;
    private Color selectedTileOriginalColor;
    private GameObject selectedPrefab;
    private string selectedCategoryKey;
    private int selectedVariantNumber;
    private bool selectedEsMuroDivisorio;
    private bool selectedEsAbertura;
    private string selectedTipoAbertura;
    private string selectedDisplayName;

    // Tiles de la categoría actualmente mostrada (para el filtro de la barra de búsqueda) --
    // listas paralelas en vez de una tupla para no depender de ninguna versión particular de C#.
    private readonly List<GameObject> currentItemTiles = new List<GameObject>();
    private readonly List<string> currentItemNames = new List<string>();

    private static readonly Color ColorTabInactiva = new Color(0.22f, 0.22f, 0.24f, 0.9f);
    private static readonly Color ColorTabActiva = new Color(0.15f, 0.45f, 0.85f, 0.95f);
    private static readonly Color ColorTileSeleccionado = new Color(0.85f, 0.65f, 0.15f, 1f);

    // Categorías especiales agregadas a mano
    private const string CATEGORIA_ABERTURAS = "Puertas y Ventanas";
    private const string CATEGORIA_MUROS = "Muros y divisiones";

    // Botón B del control derecho (Touch/Touch Plus vía OpenXR): abre/cierra el catálogo. Es la
    // ÚNICA forma de abrirlo desde 2026-09-19 -- antes también existía un botón flotante "🛋
    // Catálogo" siempre visible, pero se sacó a pedido de Alan (ajuste punto 1).
    //
    // REASIGNACIÓN (Punto 2 del plan de edición avanzada, 2026-09-18): el botón Y del control
    // IZQUIERDO abría/cerraba el catálogo con un toque -- ahora se reservó por completo para el
    // modo de edición universal (mantenerlo 3 segundos sobre cualquier mueble o pared, ver
    // UniversalEditModeController), porque "un toque" y "mantener 3 segundos" son gestos que
    // compiten por el mismo botón físico y no se pueden distinguir de forma confiable a la vez.
    // El catálogo se queda solo con el botón B de la mano derecha.
    //
    // "secondaryButton" no respondió probando en el visor real (a diferencia de "primaryButton",
    // que sí anda para A/X). Revisando OpenXRPackageSettings.asset del proyecto se ve que hay DOS
    // interaction profiles habilitados para Android: "Meta Quest Touch Plus Controller Profile" Y
    // "Oculus Touch Controller Profile" -- el runtime del Quest 3 probablemente negocia el primero
    // (es el que matchea el hardware real), que en Input System se expone como el layout
    // "MetaQuestTouchPlusController", no el genérico "XRController". Se agregan como bindings
    // alternativos (además del genérico, por las dudas) para cubrir cuál sea el que realmente
    // esté resolviendo los controles en este runtime -- un binding a un layout no registrado
    // simplemente no matchea nada, no rompe nada.
    private InputAction toggleCatalogAction;

    private static readonly string[] SecondaryButtonBindingPaths = new string[]
    {
        "<XRController>{RightHand}/secondaryButton",
        "<MetaQuestTouchPlusController>{RightHand}/secondaryButton",
        "<OculusTouchController>{RightHand}/secondaryButton",
    };

    // Con varios bindings candidatos en la misma acción (por lo de arriba), si más de uno
    // termina resolviendo a un control real al mismo tiempo, un solo apretón podría disparar
    // "performed" más de una vez (abre y cierra de nuevo enseguida). Este debounce lo evita.
    private float lastToggleTime = -999f;
    private const float ToggleDebounceSeconds = 0.3f;

    private void OnEnable()
    {
        toggleCatalogAction = new InputAction(name: "ToggleFurnitureCatalog", type: InputActionType.Button);
        foreach (string path in SecondaryButtonBindingPaths)
            toggleCatalogAction.AddBinding(path);
        toggleCatalogAction.performed += OnToggleCatalogButtonPressed;
        toggleCatalogAction.Enable();
    }

    private void OnDisable()
    {
        if (toggleCatalogAction == null) return;
        toggleCatalogAction.performed -= OnToggleCatalogButtonPressed;
        toggleCatalogAction.Disable();
        toggleCatalogAction.Dispose();
        toggleCatalogAction = null;
    }

    private void OnToggleCatalogButtonPressed(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime - lastToggleTime < ToggleDebounceSeconds) return;
        lastToggleTime = Time.unscaledTime;
        ToggleCatalogo();
    }

    private void Start()
    {
        instance = this;

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCameraTransform = mainCamera.transform;
        else
            Debug.LogWarning("FurnitureCatalogController: No se encontró Camera.main. El catálogo se creará en el origen del mundo.");

        sceneGenerator = Object.FindAnyObjectByType<SceneGenerator>();
        if (sceneGenerator == null)
        {
            GameObject managerGO = new GameObject("SceneGeneratorManager");
            sceneGenerator = managerGO.AddComponent<SceneGenerator>();
        }

        categories = PrefabMapper.GetTopLevelCategories();
        bool sinCategoriasDelPack = categories.Count == 0;
        categories.Add(CATEGORIA_ABERTURAS);
        categories.Add(CATEGORIA_MUROS);

        // El botón lanzador flotante "🛋 Catálogo" se sacó a pedido de Alan (2026-09-19, ajuste
        // punto 1: "eliminar los botones que aparecen frente al usuario") -- el catálogo se abre
        // igual con el botón B de la mano derecha (ver SecondaryButtonBindingPaths más arriba),
        // así que no hacía falta un botón siempre flotando en la vista.
        CreateCatalogPanel();
        catalogPanel.SetActive(false);

        if (sinCategoriasDelPack)
        {
            Debug.LogWarning("FurnitureCatalogController: PrefabDatabase no tiene categorías del " +
                              "Furniture Mega Pack. Ejecuta Menu > InmobiliariaVR > Build Prefab Database " +
                              "primero (la categoría 'Muros y divisiones' sí funciona igual, no depende de eso).");
        }

        SeleccionarCategoria(categories[0]);
    }

    // ---------------------------------------------------------------------
    // Botón lanzador (siempre visible, flotando cerca del usuario)
    // ---------------------------------------------------------------------

    private void CreateLauncherButton()
    {
        GameObject canvasGO = new GameObject("CatalogLauncherCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // GraphicRaycaster (pantalla/mouse) no detecta el rayo 3D del control VR sobre un
        // canvas World Space; hace falta el raycaster de XRI, igual que en los canvases fijos
        // de Sala_MVP.unity.
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220, 90);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        PositionRelativeToCamera(canvasGO.transform, launcherOffset, true);
        canvasGO.transform.SetParent(null); // se reposiciona a mano cada frame (LateUpdate)

        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.45f, 0.85f, 0.9f);

        Button btn = canvasGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(ToggleCatalogo);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(220, 90);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "🛋 Catálogo";

        launcherButton = canvasGO;
    }

    private void LateUpdate()
    {
        // El botón lanzador sigue a la cámara todo el tiempo (como un menú de muñeca);
        // el panel completo, en cambio, solo se reposiciona cada vez que se abre (más abajo).
        if (launcherButton != null)
            PositionRelativeToCamera(launcherButton.transform, launcherOffset, true);
    }

    public void ToggleCatalogo()
    {
        if (catalogPanel == null) return;

        bool nuevoEstado = !catalogPanel.activeSelf;
        if (nuevoEstado)
            PositionRelativeToCamera(catalogPanel.transform, panelOffset, true);

        catalogPanel.SetActive(nuevoEstado);
    }

    // ---------------------------------------------------------------------
    // Panel completo del catálogo (pestañas + grilla de variantes)
    // ---------------------------------------------------------------------

    private void CreateCatalogPanel()
    {
        // Punto 1 del plan de edición avanzada (2026-09-17): rediseño a panel centrado tipo
        // "7 Days to Die" (referencia mandada por el usuario) -- header con título + cerrar,
        // pestañas, barra de búsqueda, grilla de tiles más grandes tipo miniatura y un pie con
        // el nombre del item elegido + botón "Colocar mueble". Antes tocar un item lo colocaba
        // al instante; ahora solo lo selecciona/resalta, y recién se coloca desde el pie.
        GameObject canvasGO = new GameObject("FurnitureCatalogCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(1000, 800);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Cuarta ronda (2026-09-19): revertido el "seguir a la cámara" (FollowCameraPanel) que se
        // había agregado en la tercera ronda -- Alan pidió volver a que el catálogo quede FIJO
        // donde se posicionó al abrirse (ver ToggleCatalogo).
        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        // --- Header: título + botón cerrar (✕) ---
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(-70, 360);
        titleRect.sizeDelta = new Vector2(780, 60);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 36;
        titleText.alignment = TextAnchor.MiddleLeft;
        titleText.color = Color.white;
        titleText.text = "CATÁLOGO DE MUEBLES";

        GameObject closeGO = new GameObject("Btn_CerrarCatalogo");
        closeGO.transform.SetParent(canvasGO.transform, false);
        RectTransform closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(460, 360);
        closeRect.sizeDelta = new Vector2(60, 60);
        Image closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(() => catalogPanel.SetActive(false));
        GameObject closeTextGO = new GameObject("Text");
        closeTextGO.transform.SetParent(closeGO.transform, false);
        RectTransform closeTextRect = closeTextGO.AddComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.sizeDelta = Vector2.zero;
        Text closeText = closeTextGO.AddComponent<Text>();
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 30;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.text = "✕";

        // --- Fila de pestañas (una por categoría de nivel superior del PrefabDatabase) ---
        GameObject tabsGO = new GameObject("Tabs");
        tabsGO.transform.SetParent(canvasGO.transform, false);
        RectTransform tabsRect = tabsGO.AddComponent<RectTransform>();
        tabsRect.anchoredPosition = new Vector2(0, 285);
        tabsRect.sizeDelta = new Vector2(960, 55);
        HorizontalLayoutGroup tabsLayout = tabsGO.AddComponent<HorizontalLayoutGroup>();
        tabsLayout.spacing = 8;
        tabsLayout.childAlignment = TextAnchor.MiddleCenter;
        tabsLayout.childForceExpandWidth = false;
        tabsLayout.childForceExpandHeight = true;
        tabsRow = tabsGO.transform;

        foreach (string categoria in categories)
        {
            CreateTabButton(categoria);
        }

        // --- Barra de búsqueda ---
        GameObject searchGO = new GameObject("SearchBar");
        searchGO.transform.SetParent(canvasGO.transform, false);
        RectTransform searchRect = searchGO.AddComponent<RectTransform>();
        searchRect.anchoredPosition = new Vector2(0, 225);
        searchRect.sizeDelta = new Vector2(960, 50);
        Image searchBg = searchGO.AddComponent<Image>();
        searchBg.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);
        InputField searchInput = searchGO.AddComponent<InputField>();
        searchInput.targetGraphic = searchBg;

        GameObject searchTextGO = new GameObject("Text");
        searchTextGO.transform.SetParent(searchGO.transform, false);
        RectTransform searchTextRect = searchTextGO.AddComponent<RectTransform>();
        searchTextRect.anchorMin = Vector2.zero;
        searchTextRect.anchorMax = Vector2.one;
        searchTextRect.offsetMin = new Vector2(18, 6);
        searchTextRect.offsetMax = new Vector2(-18, -6);
        Text searchText = searchTextGO.AddComponent<Text>();
        searchText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        searchText.fontSize = 24;
        searchText.alignment = TextAnchor.MiddleLeft;
        searchText.color = Color.white;
        searchText.supportRichText = false;

        GameObject placeholderGO = new GameObject("Placeholder");
        placeholderGO.transform.SetParent(searchGO.transform, false);
        RectTransform placeholderRect = placeholderGO.AddComponent<RectTransform>();
        placeholderRect.anchorMin = Vector2.zero;
        placeholderRect.anchorMax = Vector2.one;
        placeholderRect.offsetMin = new Vector2(18, 6);
        placeholderRect.offsetMax = new Vector2(-18, -6);
        Text placeholderText = placeholderGO.AddComponent<Text>();
        placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderText.fontSize = 24;
        placeholderText.fontStyle = FontStyle.Italic;
        placeholderText.alignment = TextAnchor.MiddleLeft;
        placeholderText.color = new Color(1f, 1f, 1f, 0.5f);
        placeholderText.text = "Buscar mueble...";

        searchInput.textComponent = searchText;
        searchInput.placeholder = placeholderText;
        searchInput.onValueChanged.AddListener(FiltrarItems);
        searchField = searchInput;

        // Nombre de la categoría actualmente mostrada (chico, arriba de la grilla)
        GameObject categoryTitleGO = new GameObject("CategoryTitle");
        categoryTitleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform categoryTitleRect = categoryTitleGO.AddComponent<RectTransform>();
        categoryTitleRect.anchoredPosition = new Vector2(0, 188);
        categoryTitleRect.sizeDelta = new Vector2(960, 30);
        categoryTitleText = categoryTitleGO.AddComponent<Text>();
        categoryTitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        categoryTitleText.fontSize = 22;
        categoryTitleText.alignment = TextAnchor.MiddleCenter;
        categoryTitleText.color = new Color(0.75f, 0.75f, 0.75f);

        // --- Área con scroll: grilla de tiles (antes: lista de una sola columna, porque el
        // Content no tenía ancho fijo y GridLayoutGroup no tenía dónde acomodar más de una
        // columna -- esto es justo lo que el usuario reportó como "lo veo en una lista de la
        // izquierda") ---
        GameObject scrollGO = new GameObject("ItemsScroll");
        scrollGO.transform.SetParent(canvasGO.transform, false);
        RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector2(0, -40);
        scrollRect.sizeDelta = new Vector2(960, 400);

        Image scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0, 0, 0, 0.25f);

        ScrollRect scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportGO = new GameObject("Viewport");
        viewportGO.transform.SetParent(scrollGO.transform, false);
        RectTransform viewportRect = viewportGO.AddComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.sizeDelta = Vector2.zero;
        viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // necesario para el Mask
        Mask mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(0, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        // Ancho FIJO igual al del viewport: sin esto, GridLayoutGroup solo tiene el ancho por
        // defecto de un RectTransform nuevo (100) para acomodar celdas, y con celdas de más de
        // 100px de ancho eso da como resultado una sola columna -- una lista, no una grilla.
        contentRect.sizeDelta = new Vector2(960, 0);

        GridLayoutGroup grid = contentGO.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(215, 200);
        grid.spacing = new Vector2(14, 14);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.childAlignment = TextAnchor.UpperLeft;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;

        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        itemsContent = contentGO.transform;

        // --- Pie: nombre del item seleccionado + botón "Colocar mueble" ---
        GameObject selectedTextGO = new GameObject("SelectedItemText");
        selectedTextGO.transform.SetParent(canvasGO.transform, false);
        RectTransform selectedTextRect = selectedTextGO.AddComponent<RectTransform>();
        selectedTextRect.anchoredPosition = new Vector2(-230, -360);
        selectedTextRect.sizeDelta = new Vector2(500, 60);
        selectedItemText = selectedTextGO.AddComponent<Text>();
        selectedItemText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        selectedItemText.fontSize = 24;
        selectedItemText.alignment = TextAnchor.MiddleLeft;
        selectedItemText.color = new Color(0.9f, 0.9f, 0.9f);
        selectedItemText.text = "Ningún item seleccionado";

        GameObject placeGO = new GameObject("Btn_ColocarMueble");
        placeGO.transform.SetParent(canvasGO.transform, false);
        RectTransform placeRect = placeGO.AddComponent<RectTransform>();
        placeRect.anchoredPosition = new Vector2(330, -360);
        placeRect.sizeDelta = new Vector2(280, 64);
        Image placeImg = placeGO.AddComponent<Image>();
        placeImg.color = new Color(0.15f, 0.55f, 0.25f, 0.95f);
        Button placeBtn = placeGO.AddComponent<Button>();
        placeBtn.targetGraphic = placeImg;
        placeBtn.interactable = false;
        placeBtn.onClick.AddListener(ColocarSeleccionActual);
        GameObject placeTextGO = new GameObject("Text");
        placeTextGO.transform.SetParent(placeGO.transform, false);
        RectTransform placeTextRect = placeTextGO.AddComponent<RectTransform>();
        placeTextRect.anchorMin = Vector2.zero;
        placeTextRect.anchorMax = Vector2.one;
        placeTextRect.sizeDelta = Vector2.zero;
        Text placeText = placeTextGO.AddComponent<Text>();
        placeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeText.fontSize = 26;
        placeText.alignment = TextAnchor.MiddleCenter;
        placeText.color = Color.white;
        placeText.text = "Colocar mueble";
        placeButton = placeBtn;

        catalogPanel = canvasGO;
    }

    private void CreateTabButton(string categoria)
    {
        GameObject tabGO = new GameObject($"Tab_{categoria}");
        tabGO.transform.SetParent(tabsRow, false);

        LayoutElement layoutElement = tabGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 140;
        layoutElement.preferredHeight = 50;

        Image img = tabGO.AddComponent<Image>();
        img.color = ColorTabInactiva;

        Button btn = tabGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SeleccionarCategoria(categoria));

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(tabGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = categoria;

        tabImages[categoria] = img;
    }

    /// <summary>Resalta la pestaña de la categoría activa y apaga el resto (estilo referencia).</summary>
    private void HighlightActiveTab(string categoriaActiva)
    {
        foreach (var kv in tabImages)
            kv.Value.color = (kv.Key == categoriaActiva) ? ColorTabActiva : ColorTabInactiva;
    }

    private void SeleccionarCategoria(string categoria)
    {
        currentCategory = categoria;
        if (categoryTitleText != null)
            categoryTitleText.text = categoria;

        HighlightActiveTab(categoria);
        LimpiarSeleccion();

        // Limpiar items previos
        for (int i = itemsContent.childCount - 1; i >= 0; i--)
            Destroy(itemsContent.GetChild(i).gameObject);
        currentItemTiles.Clear();
        currentItemNames.Clear();

        if (searchField != null)
            searchField.text = "";

        if (categoria == CATEGORIA_ABERTURAS)
        {
            CreateAberturasItemButtons();
            return;
        }

        if (categoria == CATEGORIA_MUROS)
        {
            CreateWallDividerItemButton();
            return;
        }

        List<GameObject> variantes = PrefabMapper.GetVariants(categoria);
        for (int i = 0; i < variantes.Count; i++)
        {
            CreateItemButton(variantes[i], i + 1);
        }
    }

    /// <summary>Filtra los tiles de la categoría actual por nombre (barra de búsqueda).</summary>
    private void FiltrarItems(string query)
    {
        query = (query ?? string.Empty).Trim().ToLowerInvariant();
        for (int i = 0; i < currentItemTiles.Count; i++)
        {
            if (currentItemTiles[i] == null) continue;
            bool visible = string.IsNullOrEmpty(query) || currentItemNames[i].ToLowerInvariant().Contains(query);
            currentItemTiles[i].SetActive(visible);
        }
    }

    /// <summary>
    /// Único botón de la categoría especial "Muros y divisiones" (no viene de PrefabDatabase):
    /// crea un segmento de muro recto para dividir ambientes dentro de un monoambiente, en vez
    /// de instanciar un prefab del Furniture Mega Pack. Ver ColocarMuroDivisorio.
    /// </summary>
    private void CreateWallDividerItemButton()
    {
        GameObject itemGO = new GameObject("Item_MuroDivisorio");
        itemGO.transform.SetParent(itemsContent, false);

        Color colorOriginal = new Color(0.45f, 0.42f, 0.38f, 0.9f); // tono "ladrillo/concreto", distinto de los muebles
        Image img = itemGO.AddComponent<Image>();
        img.color = colorOriginal;

        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SeleccionarItem(itemGO, img, colorOriginal, null, null, 0, true, "Muro divisorio"));

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(itemGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "🧱 Muro\ndivisorio";

        currentItemTiles.Add(itemGO);
        currentItemNames.Add("Muro divisorio");
    }

    /// <summary>
    /// Crea un "Muro divisorio" nuevo frente al usuario (mismo cálculo de posición que
    /// ColocarMueble) y lo deja listo para mover/rotar/pintar/redimensionar -- ver
    /// SceneGenerator.CreateInteriorWallDivider.
    /// </summary>
    private void ColocarMuroDivisorio()
    {
        if (sceneGenerator == null) return;

        Vector3 posicion = mainCameraTransform != null
            ? mainCameraTransform.position + mainCameraTransform.forward * spawnOffset.z
                + mainCameraTransform.up * spawnOffset.y
                + mainCameraTransform.right * spawnOffset.x
            : spawnOffset;

        sceneGenerator.CreateInteriorWallDivider(posicion);
        spawnedCount++;

        ToastNotificationUI.Show("Muro divisorio agregado -- agarralo para ubicarlo y con X abrís su panel", 3f);

        if (catalogPanel != null) catalogPanel.SetActive(false);
    }

    /// <summary>
    /// Botones de la categoría "Puertas y Ventanas":
    /// Ofrece Puerta Simple, Puerta Doble, Ventana de Pared y Ventana Doble.
    /// Al colocarse frente al usuario se acoplan magnéticamente a la pared cercana más próxima.
    /// </summary>
    private void CreateAberturasItemButtons()
    {
        CreateAberturaTile("Item_PuertaSimple", "🚪 Puerta\nSimple", "puerta_simple", "Puerta Simple");
        CreateAberturaTile("Item_PuertaDoble", "🚪🚪 Puerta\nDoble", "puerta_doble", "Puerta Doble");
        CreateAberturaTile("Item_VentanaPared", "🪟 Ventana\nde Pared", "ventana", "Ventana de Pared");
        CreateAberturaTile("Item_VentanaDoble", "🪟🪟 Ventana\nDoble", "ventana_doble", "Ventana Doble");
    }

    private void CreateAberturaTile(string id, string textLabel, string tipoAbertura, string displayName)
    {
        GameObject itemGO = new GameObject(id);
        itemGO.transform.SetParent(itemsContent, false);

        Color colorOriginal = new Color(0.2f, 0.42f, 0.52f, 0.9f);
        Image img = itemGO.AddComponent<Image>();
        img.color = colorOriginal;

        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SeleccionarItemAbertura(itemGO, img, colorOriginal, tipoAbertura, displayName));

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(itemGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = textLabel;

        currentItemTiles.Add(itemGO);
        currentItemNames.Add(displayName);
    }

    private void SeleccionarItemAbertura(GameObject tileGO, Image tileImg, Color colorOriginalTile, string tipoAbertura, string displayName)
    {
        if (selectedTileImg != null)
            selectedTileImg.color = selectedTileOriginalColor;

        selectedTileGO = tileGO;
        selectedTileImg = tileImg;
        selectedTileOriginalColor = colorOriginalTile;
        tileImg.color = ColorTileSeleccionado;

        selectedPrefab = null;
        selectedCategoryKey = null;
        selectedVariantNumber = 0;
        selectedEsMuroDivisorio = false;
        selectedEsAbertura = true;
        selectedTipoAbertura = tipoAbertura;
        selectedDisplayName = displayName;

        if (selectedItemText != null)
            selectedItemText.text = displayName;
        if (placeButton != null)
            placeButton.interactable = true;
    }

    private void ColocarAbertura(string tipoAbertura)
    {
        if (sceneGenerator == null) return;

        Vector3 posicion = mainCameraTransform != null
            ? mainCameraTransform.position + mainCameraTransform.forward * spawnOffset.z
                + mainCameraTransform.up * spawnOffset.y
                + mainCameraTransform.right * spawnOffset.x
            : spawnOffset;

        Quaternion rotacion = mainCameraTransform != null
            ? Quaternion.Euler(0f, mainCameraTransform.eulerAngles.y, 0f)
            : Quaternion.identity;

        GameObject obj = null;
        switch (tipoAbertura)
        {
            case "puerta_simple":
                obj = sceneGenerator.CreateSingleDoor(posicion, rotacion);
                break;
            case "puerta_doble":
                obj = sceneGenerator.CreateDoubleDoor(posicion, rotacion);
                break;
            case "ventana":
                obj = sceneGenerator.CreateWindow(posicion, rotacion);
                break;
            case "ventana_doble":
                obj = sceneGenerator.CreateDoubleWindow(posicion, rotacion);
                break;
        }

        spawnedCount++;
        string nombre = tipoAbertura.Contains("puerta") ? "Puerta" : "Ventana";
        ToastNotificationUI.Show($"{nombre} agregada -- se acopla a la pared y con gatillo se abre/cierra", 3f);

        if (catalogPanel != null) catalogPanel.SetActive(false);
    }

    private void CreateItemButton(GameObject prefab, int variantNumber)
    {
        if (prefab == null) return;

        GameObject itemGO = new GameObject($"Item_{prefab.name}");
        itemGO.transform.SetParent(itemsContent, false);

        Image img = itemGO.AddComponent<Image>();

        // Si existe un thumbnail generado por FurnitureThumbnailGenerator, se usa; si no,
        // se muestra un tile liso con el nombre del prefab (no bloquea el flujo).
        Sprite thumbnail = Resources.Load<Sprite>($"FurnitureThumbnails/{prefab.name}");
        Color colorOriginal = thumbnail != null ? Color.white : new Color(0.3f, 0.3f, 0.35f, 0.9f);
        if (thumbnail != null)
            img.sprite = thumbnail;
        img.color = colorOriginal;

        string categoriaCapturada = currentCategory;
        Button btn = itemGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => SeleccionarItem(itemGO, img, colorOriginal, prefab, categoriaCapturada, variantNumber, false, prefab.name));

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(itemGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0, 0);
        textRect.anchorMax = new Vector2(1, thumbnail != null ? 0.25f : 1f);
        textRect.sizeDelta = Vector2.zero;
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = prefab.name;
        if (thumbnail != null)
        {
            Outline outline = textGO.AddComponent<Outline>();
            outline.effectColor = Color.black;
        }

        currentItemTiles.Add(itemGO);
        currentItemNames.Add(prefab.name);
    }

    /// <summary>
    /// Selecciona (resalta) un tile del catálogo sin colocarlo todavía -- la colocación real
    /// pasa a hacerse desde el botón "Colocar mueble" del pie del panel (ColocarSeleccionActual),
    /// igual que en la referencia "7 Days to Die" que mandó el usuario.
    /// </summary>
    private void SeleccionarItem(GameObject tileGO, Image tileImg, Color colorOriginalTile,
        GameObject prefab, string categoryKey, int variantNumber, bool esMuroDivisorio, string displayName)
    {
        // Si había otro tile resaltado, se le devuelve su color de fábrica.
        if (selectedTileImg != null)
            selectedTileImg.color = selectedTileOriginalColor;

        selectedTileGO = tileGO;
        selectedTileImg = tileImg;
        selectedTileOriginalColor = colorOriginalTile;
        tileImg.color = ColorTileSeleccionado;

        selectedPrefab = prefab;
        selectedCategoryKey = categoryKey;
        selectedVariantNumber = variantNumber;
        selectedEsMuroDivisorio = esMuroDivisorio;
        selectedDisplayName = displayName;

        if (selectedItemText != null)
            selectedItemText.text = displayName;
        if (placeButton != null)
            placeButton.interactable = true;
    }

    /// <summary>Limpia la selección actual (al cambiar de categoría, buscar, o tras colocar un item).</summary>
    private void LimpiarSeleccion()
    {
        if (selectedTileImg != null)
            selectedTileImg.color = selectedTileOriginalColor;

        selectedTileGO = null;
        selectedTileImg = null;
        selectedPrefab = null;
        selectedCategoryKey = null;
        selectedVariantNumber = 0;
        selectedEsMuroDivisorio = false;
        selectedEsAbertura = false;
        selectedTipoAbertura = null;
        selectedDisplayName = null;

        if (selectedItemText != null)
            selectedItemText.text = "Ningún item seleccionado";
        if (placeButton != null)
            placeButton.interactable = false;
    }

    /// <summary>Coloca en la escena lo que esté actualmente seleccionado en la grilla (botón "Colocar mueble").</summary>
    private void ColocarSeleccionActual()
    {
        if (selectedEsAbertura && !string.IsNullOrEmpty(selectedTipoAbertura))
        {
            ColocarAbertura(selectedTipoAbertura);
        }
        else if (selectedEsMuroDivisorio)
        {
            ColocarMuroDivisorio();
        }
        else if (selectedPrefab != null)
        {
            ColocarMueble(selectedPrefab, selectedCategoryKey, selectedVariantNumber);
        }
        else
        {
            return;
        }

        LimpiarSeleccion();
    }

    /// <summary>
    /// Instancia la variante elegida frente al usuario, la registra en el SceneGenerator y le
    /// da la misma interactividad (mover/rotar, cambiar color, borrar) que cualquier mueble
    /// generado desde un JSON, usando la categoría del PrefabDatabase como "type".
    /// </summary>
    private void ColocarMueble(GameObject prefab, string categoryKey, int variantNumber)
    {
        if (prefab == null || sceneGenerator == null) return;

        Vector3 posicion = mainCameraTransform != null
            ? mainCameraTransform.position + mainCameraTransform.forward * spawnOffset.z
                + mainCameraTransform.up * spawnOffset.y
                + mainCameraTransform.right * spawnOffset.x
            : spawnOffset;

        GameObject obj = Instantiate(prefab, posicion, Quaternion.identity);
        // Mismo ajuste que SceneGenerator.ApplyTransforms para los muebles de presets/croquis:
        // mide el tamaño real del mueble ya instanciado y lo reescala a un tamaño realista,
        // en vez de asumir un multiplicador fijo (los prefabs de Furniture Mega Pack no todos
        // traen la misma escala horneada adentro).
        SceneGenerator.AutoScaleFurnitureToFootprint(obj, GetTargetFootprintForCategory(categoryKey));
        // Igual que los muebles generados desde un preset/croquis: apoyarlo sobre el piso (u
        // otro mueble debajo) en vez de dejarlo flotando a la altura fija en la que apareció
        // frente a la cámara.
        SceneGenerator.SnapToFloor(obj);
        spawnedCount++;

        var meta = obj.AddComponent<SceneElementMetadata>();
        meta.elementId = $"catalogo_{categoryKey.Replace('/', '_')}_{spawnedCount}";
        meta.elementType = categoryKey;
        meta.jsonData = "";

        sceneGenerator.RegisterExternalObject(obj);
        sceneGenerator.HacerInteractivo(obj, categoryKey);

        ToastNotificationUI.Show($"Agregado: {prefab.name}", 2f);

        // Cerrar el modal al elegir una opción (comportamiento esperado de un modal): ya se
        // agregó el mueble y quedó agarrable/movible, no hace falta seguir tapando la vista.
        if (catalogPanel != null) catalogPanel.SetActive(false);
    }

    /// <summary>
    /// Tamaño realista aproximado (footprint horizontal en metros) para reescalar un mueble
    /// agregado desde el catálogo, según la categoría de nivel superior del PrefabDatabase
    /// (las keys son las carpetas de Assets/Furniture Mega Pack/Prefabs, no los tipos en
    /// español de PrefabMapper). Mismo criterio que SceneGenerator.FURNITURE_TARGET_FOOTPRINT_M.
    /// </summary>
    private static float GetTargetFootprintForCategory(string categoryKey)
    {
        string topLevel = categoryKey.Split('/')[0];
        switch (topLevel)
        {
            case "Sofas": return 2.0f;
            case "Tables": return 1.0f;
            case "Beds": return 2.0f;
            case "Chairs": return 0.55f;
            case "Closets": return 1.0f;
            case "Drawers": return 0.6f;
            case "Cushioins": // typo real de la carpeta del asset pack (ver PrefabMapper)
            case "Cushions": return 0.45f;
            case "Kitchen": return 0.7f;
            case "Bathroom": return 1.0f;
            default: return 1.0f;
        }
    }

    /// <summary>
    /// Posiciona y orienta un transform relativo a la cámara del jugador (mismo patrón que
    /// PresetLoaderController.PositionInFrontOfCamera). Si mirarACamara es true, el objeto
    /// queda mirando hacia el jugador.
    /// </summary>
    private void PositionRelativeToCamera(Transform target, Vector3 offset, bool mirarACamara)
    {
        if (mainCameraTransform == null)
        {
            target.position = offset;
            return;
        }

        Vector3 posicionDeseada = mainCameraTransform.position
            + mainCameraTransform.forward * offset.z
            + mainCameraTransform.up * offset.y
            + mainCameraTransform.right * offset.x;

        // Si el jugador está parado cerca de una pared o un mueble, esta posición "deseada" (a
        // distancia fija de la cámara) puede caer adentro o detrás de esa geometría -- por eso
        // el panel/botón queda tapado. Se corrige con un raycast desde la cámara.
        target.position = SceneGenerator.ClampInFrontOfObstruction(
            mainCameraTransform.position, posicionDeseada);

        if (mirarACamara)
            target.rotation = Quaternion.LookRotation(target.position - mainCameraTransform.position);
    }
}
