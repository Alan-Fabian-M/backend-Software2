using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using System.Collections.Generic;

/// <summary>
/// UI de "Guardar escena" / "Mis escenas": un botón flotante (mismo patrón que
/// FurnitureCatalogController) que abre un panel para guardar el estado actual de la escena
/// generada (SceneGenerator.ExportCurrentSceneToJson) en el almacenamiento local del Quest
/// (SceneGraphSaveSystem, sin servidor ni base de datos externa), y para volver a cargar o
/// borrar cualquier escena guardada anteriormente.
///
/// Complementa a PresetLoaderController (que carga los 4 presets fijos) y a
/// CroquisSceneCompilerController (que pide una escena al backend): esta es la tercera fuente
/// posible de una escena, la que el propio usuario guardó.
/// </summary>
public class SceneSaveLoadController : MonoBehaviour
{
    [Tooltip("Offset del botón lanzador relativo a la cámara: X=derecha, Y=arriba, Z=adelante")]
    [SerializeField] private Vector3 launcherOffset = new Vector3(-0.55f, -0.35f, 1.2f);

    [Tooltip("Offset del panel completo relativo a la cámara")]
    [SerializeField] private Vector3 panelOffset = new Vector3(0, 0, 1.6f);

    private SceneGenerator sceneGenerator;
    private Transform mainCameraTransform;

    private GameObject launcherButton;
    private GameObject panel;
    private Transform listContent;
    private Text statusText;

    private void Start()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCameraTransform = mainCamera.transform;

        sceneGenerator = Object.FindAnyObjectByType<SceneGenerator>();
        if (sceneGenerator == null)
        {
            GameObject managerGO = new GameObject("SceneGeneratorManager");
            sceneGenerator = managerGO.AddComponent<SceneGenerator>();
        }

        CreateLauncherButton();
        CreatePanel();
        panel.SetActive(false);
        RefrescarListaEscenas();
    }

    private void LateUpdate()
    {
        if (launcherButton != null)
            PositionRelativeToCamera(launcherButton.transform, launcherOffset, true);
    }

    // ---------------------------------------------------------------------
    // Botón lanzador
    // ---------------------------------------------------------------------

    private void CreateLauncherButton()
    {
        GameObject canvasGO = new GameObject("SaveLoadLauncherCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // GraphicRaycaster (pantalla/mouse) no detecta el rayo 3D del control VR; hace falta
        // el raycaster de XRI, igual que en los canvases fijos de Sala_MVP.unity.
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(220, 90);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);
        canvasGO.transform.SetParent(null);

        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.6f, 0.35f, 0.9f);

        Button btn = canvasGO.AddComponent<Button>();
        btn.targetGraphic = bg;
        btn.onClick.AddListener(TogglePanel);

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(canvasGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(220, 90);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 26;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = "💾 Mis Escenas";

        launcherButton = canvasGO;
    }

    private void TogglePanel()
    {
        if (panel == null) return;

        bool nuevoEstado = !panel.activeSelf;
        if (nuevoEstado)
        {
            PositionRelativeToCamera(panel.transform, panelOffset, true);
            RefrescarListaEscenas();
        }
        panel.SetActive(nuevoEstado);
    }

    // ---------------------------------------------------------------------
    // Panel: botón guardar + lista de escenas guardadas
    // ---------------------------------------------------------------------

    private void CreatePanel()
    {
        GameObject canvasGO = new GameObject("SceneSaveLoadCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800, 650);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);

        // Título
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 295);
        titleRect.sizeDelta = new Vector2(760, 60);
        Text titleText = titleGO.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 38;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.white;
        titleText.text = "Mis Escenas Guardadas";

        // Botón: guardar la escena actual con un nombre automático (fecha/hora) --
        // se evita pedir texto por teclado, poco práctico en VR.
        GameObject saveGO = new GameObject("Btn_GuardarActual");
        saveGO.transform.SetParent(canvasGO.transform, false);
        RectTransform saveRect = saveGO.AddComponent<RectTransform>();
        saveRect.anchoredPosition = new Vector2(0, 220);
        saveRect.sizeDelta = new Vector2(500, 60);
        Image saveImg = saveGO.AddComponent<Image>();
        saveImg.color = new Color(0.2f, 0.55f, 0.3f, 0.9f);
        Button saveBtn = saveGO.AddComponent<Button>();
        saveBtn.targetGraphic = saveImg;
        saveBtn.onClick.AddListener(GuardarEscenaActual);
        GameObject saveTextGO = new GameObject("Text");
        saveTextGO.transform.SetParent(saveGO.transform, false);
        RectTransform saveTextRect = saveTextGO.AddComponent<RectTransform>();
        saveTextRect.sizeDelta = new Vector2(500, 60);
        Text saveText = saveTextGO.AddComponent<Text>();
        saveText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        saveText.fontSize = 26;
        saveText.alignment = TextAnchor.MiddleCenter;
        saveText.color = Color.white;
        saveText.text = "💾 Guardar escena actual";

        // Status
        GameObject statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(canvasGO.transform, false);
        RectTransform statusRect = statusGO.AddComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 165);
        statusRect.sizeDelta = new Vector2(760, 40);
        statusText = statusGO.AddComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = 20;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.color = new Color(0, 1, 0);
        statusText.text = "";

        // Lista con scroll de escenas guardadas
        GameObject scrollGO = new GameObject("ListScroll");
        scrollGO.transform.SetParent(canvasGO.transform, false);
        RectTransform scrollRect = scrollGO.AddComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector2(0, -60);
        scrollRect.sizeDelta = new Vector2(760, 300);
        scrollGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.25f);
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
        viewportGO.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
        Mask mask = viewportGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        GameObject contentGO = new GameObject("Content");
        contentGO.transform.SetParent(viewportGO.transform, false);
        RectTransform contentRect = contentGO.AddComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(0, 1);
        contentRect.pivot = new Vector2(0, 1);
        contentRect.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup vlayout = contentGO.AddComponent<VerticalLayoutGroup>();
        vlayout.spacing = 6;
        vlayout.padding = new RectOffset(8, 8, 8, 8);
        vlayout.childForceExpandHeight = false;
        vlayout.childForceExpandWidth = true;
        ContentSizeFitter fitter = contentGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        listContent = contentGO.transform;

        // Botón cerrar
        GameObject closeGO = new GameObject("Btn_Cerrar");
        closeGO.transform.SetParent(canvasGO.transform, false);
        RectTransform closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.anchoredPosition = new Vector2(0, -295);
        closeRect.sizeDelta = new Vector2(220, 60);
        Image closeImg = closeGO.AddComponent<Image>();
        closeImg.color = new Color(0.6f, 0.2f, 0.2f, 0.9f);
        Button closeBtn = closeGO.AddComponent<Button>();
        closeBtn.targetGraphic = closeImg;
        closeBtn.onClick.AddListener(() => panel.SetActive(false));
        GameObject closeTextGO = new GameObject("Text");
        closeTextGO.transform.SetParent(closeGO.transform, false);
        RectTransform closeTextRect = closeTextGO.AddComponent<RectTransform>();
        closeTextRect.sizeDelta = new Vector2(220, 60);
        Text closeText = closeTextGO.AddComponent<Text>();
        closeText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        closeText.fontSize = 26;
        closeText.alignment = TextAnchor.MiddleCenter;
        closeText.color = Color.white;
        closeText.text = "Cerrar";

        panel = canvasGO;
    }

    private void GuardarEscenaActual()
    {
        if (sceneGenerator == null) return;

        string nombre = SceneGraphSaveSystem.GenerarNombrePorDefecto();
        string json = sceneGenerator.ExportCurrentSceneToJson(nombre);
        SceneGraphSaveSystem.GuardarEscena(nombre, json);

        if (statusText != null)
            statusText.text = $"✓ Guardada como '{nombre}'";

        ToastNotificationUI.Show($"Escena guardada: {nombre}", 2.5f);
        RefrescarListaEscenas();
    }

    private void RefrescarListaEscenas()
    {
        if (listContent == null) return;

        for (int i = listContent.childCount - 1; i >= 0; i--)
            Destroy(listContent.GetChild(i).gameObject);

        string[] escenas = SceneGraphSaveSystem.ListarEscenas();
        foreach (string nombre in escenas)
        {
            CreateFilaEscena(nombre);
        }

        if (escenas.Length == 0)
        {
            GameObject emptyGO = new GameObject("Empty");
            emptyGO.transform.SetParent(listContent, false);
            LayoutElement le = emptyGO.AddComponent<LayoutElement>();
            le.preferredHeight = 50;
            Text emptyText = emptyGO.AddComponent<Text>();
            emptyText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            emptyText.fontSize = 20;
            emptyText.alignment = TextAnchor.MiddleCenter;
            emptyText.color = new Color(0.7f, 0.7f, 0.7f);
            emptyText.text = "Todavía no guardaste ninguna escena";
        }
    }

    private void CreateFilaEscena(string nombre)
    {
        GameObject rowGO = new GameObject($"Fila_{nombre}");
        rowGO.transform.SetParent(listContent, false);
        LayoutElement rowLayout = rowGO.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 60;
        HorizontalLayoutGroup hlayout = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlayout.spacing = 8;
        hlayout.childForceExpandHeight = true;
        hlayout.childForceExpandWidth = false;

        // Nombre
        GameObject nameGO = new GameObject("Nombre");
        nameGO.transform.SetParent(rowGO.transform, false);
        LayoutElement nameLayout = nameGO.AddComponent<LayoutElement>();
        nameLayout.preferredWidth = 420;
        Text nameText = nameGO.AddComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 20;
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        nameText.text = nombre;

        // Botón cargar
        CreateRowButton(rowGO.transform, "Cargar", 130, new Color(0.2f, 0.5f, 0.85f, 0.9f),
            () => CargarEscena(nombre));

        // Botón borrar
        CreateRowButton(rowGO.transform, "Borrar", 130, new Color(0.6f, 0.2f, 0.2f, 0.9f),
            () => BorrarEscena(nombre));
    }

    private void CreateRowButton(Transform parent, string label, float width, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject($"Btn_{label}");
        btnGO.transform.SetParent(parent, false);
        LayoutElement layout = btnGO.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        Image img = btnGO.AddComponent<Image>();
        img.color = color;
        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
    }

    private void CargarEscena(string nombre)
    {
        string json = SceneGraphSaveSystem.CargarEscena(nombre);
        if (json == null)
        {
            if (statusText != null) statusText.text = $"✗ No se pudo cargar '{nombre}'";
            ToastNotificationUI.ShowError($"No se pudo cargar '{nombre}'", 3f);
            return;
        }

        sceneGenerator.GenerateSceneAsync(json);
        if (statusText != null) statusText.text = $"Cargando '{nombre}'...";
        panel.SetActive(false);
    }

    private void BorrarEscena(string nombre)
    {
        SceneGraphSaveSystem.BorrarEscena(nombre);
        if (statusText != null) statusText.text = $"Escena '{nombre}' borrada";
        RefrescarListaEscenas();
    }

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
