using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// Controls preset loading and scene generation.
/// Provides UI buttons for selecting and loading monoambiente presets.
/// El menú se posiciona relativo a la cámara del jugador (no en coordenadas
/// absolutas del mundo), para que siempre aparezca frente a donde está mirando.
/// Antes quedaba SIEMPRE visible flotando frente al jugador; ahora arranca oculto y se
/// abre/cierra con el botón A del control derecho (un botón físico del control, en vez de
/// tener que apuntar y clickear un botón 3D flotante).
/// </summary>
public class PresetLoaderController : MonoBehaviour
{
    private static PresetLoaderController instance;

    /// <summary>
    /// Séptima ronda (2026-09-19): consultado por PlayerFlightController para no ascender/
    /// descender mientras este panel está abierto (así el gatillo queda libre para clickear sus
    /// botones en vez de competir con el vuelo). Ver el mismo patrón en FurnitureCatalogController.
    /// </summary>
    public static bool HayPanelAbierto => instance != null && instance.presetMenu != null && instance.presetMenu.activeSelf;

    private SceneGenerator sceneGenerator;
    private GameObject presetMenu;
    private TextMeshProUGUI statusText;
    private Transform mainCameraTransform;

    [Tooltip("Offset del menú relativo a la cámara: X=derecha, Y=arriba, Z=adelante")]
    [SerializeField] private Vector3 menuOffset = new Vector3(0, -0.2f, 1.5f);

    // Botón A del control derecho (Touch/Touch Plus vía OpenXR): abre/cierra este menú.
    // Ruta genérica de Input System para XR ("<XRController>{RightHand}/primaryButton"),
    // el mismo esquema que ya usa "XRI Default Input Actions" incluido en el proyecto.
    private InputAction toggleMenuAction;

    // El proyecto tiene DOS interaction profiles de OpenXR habilitados a la vez para Android
    // (Meta Quest Touch Plus Y Oculus Touch, ver OpenXRPackageSettings.asset) -- es posible que
    // el runtime termine exponiendo el control físico de A por dos caminos distintos a la vez,
    // y que un solo apretón dispare "performed" dos veces seguidas (abre y vuelve a cerrar de
    // inmediato -- lo que se ve como que el modal "salta"). Este debounce ignora una segunda
    // señal que llegue demasiado pronto después de la anterior, sea cual sea la causa real.
    private float lastToggleTime = -999f;
    private const float ToggleDebounceSeconds = 0.3f;

    private void OnEnable()
    {
        toggleMenuAction = new InputAction(
            name: "TogglePresetMenu",
            type: InputActionType.Button,
            binding: "<XRController>{RightHand}/primaryButton");
        toggleMenuAction.performed += OnToggleMenuButtonPressed;
        toggleMenuAction.Enable();
    }

    private void OnDisable()
    {
        if (toggleMenuAction == null) return;
        toggleMenuAction.performed -= OnToggleMenuButtonPressed;
        toggleMenuAction.Disable();
        toggleMenuAction.Dispose();
        toggleMenuAction = null;
    }

    private void OnToggleMenuButtonPressed(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime - lastToggleTime < ToggleDebounceSeconds) return;
        lastToggleTime = Time.unscaledTime;
        TogglePresetMenu();
    }

    /// <summary>
    /// Muestra u oculta el menú de presets, reposicionándolo frente a la cámara cada vez que se
    /// abre (si el jugador se movió mientras estaba cerrado, no reaparece en un lugar viejo).
    /// </summary>
    public void TogglePresetMenu()
    {
        if (presetMenu == null) return;

        bool seVaAMostrar = !presetMenu.activeSelf;
        if (seVaAMostrar)
            PositionInFrontOfCamera(presetMenu.transform);

        presetMenu.SetActive(seVaAMostrar);
    }

    private void Start()
    {
        instance = this;

        // Encontrar la cámara principal (la del XR Rig)
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            mainCameraTransform = mainCamera.transform;
        else
            Debug.LogWarning("PresetLoaderController: No se encontró Camera.main. El menú se creará en el origen del mundo.");

        // Find or create SceneGenerator
        sceneGenerator = Object.FindAnyObjectByType<SceneGenerator>();
        if (sceneGenerator == null)
        {
            GameObject managerGO = new GameObject("SceneGeneratorManager");
            sceneGenerator = managerGO.AddComponent<SceneGenerator>();
        }

        // Subscribe to events
        sceneGenerator.OnProgress += OnGenerationProgress;
        sceneGenerator.OnComplete += OnGenerationComplete;
        sceneGenerator.OnError += OnGenerationError;

        // Create UI
        CreatePresetMenu();
    }

    private void CreatePresetMenu()
    {
        // Create Canvas
        GameObject canvasGO = new GameObject("PresetMenuCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Sin esto, los botones de este menu NUNCA reciben el click del rayo del control VR:
        // el GraphicRaycaster comun se basa en una posicion de puntero 2D de pantalla, no en
        // un rayo 3D. Es el mismo componente que ya usan los canvases fijos de Sala_MVP.unity.
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800, 600);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        // Posicionar frente a la cámara del jugador (o en el origen si no hay cámara)
        PositionInFrontOfCamera(canvasGO.transform);

        // Background
        Image bgImage = canvasGO.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchoredPosition = new Vector2(0, 240);
        titleRect.sizeDelta = new Vector2(700, 80);

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "Selecciona Monoambiente";
        titleText.fontSize = 60;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        // Status Text
        GameObject statusGO = new GameObject("StatusText");
        statusGO.transform.SetParent(canvasGO.transform, false);
        RectTransform statusRect = statusGO.AddComponent<RectTransform>();
        statusRect.anchoredPosition = new Vector2(0, 150);
        statusRect.sizeDelta = new Vector2(700, 60);

        statusText = statusGO.AddComponent<TextMeshProUGUI>();
        statusText.text = "Listo";
        statusText.fontSize = 30;
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.color = new Color(0, 1, 0, 1);

        // Preset Buttons
        // Séptima ronda (2026-09-19, pedido de Alan: "los monoambientes los amplies que sea algo
        // mas grande con 7 o 10 habitaciones") -- se agregó un 5to preset ("Casa Grande", 9
        // habitaciones). Para que entren los 5 botones en la misma fila sin agrandar el canvas,
        // se angostó cada botón de 150 a 130 de ancho y se acortó el espaciado entre ellos.
        string[] presets = { "standard", "compact", "large", "loft", "casa_grande" };
        string[] labels = { "Estándar\n4.2m × 5.5m", "Compacto\n3.5m × 4.0m", "Grande\n5.0m × 6.5m", "Loft\n6.0m × 7.0m", "Casa Grande\n16×14m · 9 amb." };
        Vector2[] positions = { new Vector2(-320, -50), new Vector2(-160, -50), new Vector2(0, -50), new Vector2(160, -50), new Vector2(320, -50) };

        for (int i = 0; i < presets.Length; i++)
        {
            CreatePresetButton(canvasGO, presets[i], labels[i], positions[i]);
        }

        // Sexta ronda (2026-09-19, pedido de Alan): botón para abrir el modal de ayuda con la
        // lista de controles del visor.
        CreateControlsHelpButton(canvasGO);

        // Sexta ronda (2026-09-19, pedido de Alan: "agregues al modal de monoambientes le
        // agregues lo que es un boton de X para cerra el modal") -- hasta ahora este panel solo
        // se cerraba volviendo a apretar el botón A del control derecho (o eligiendo un preset);
        // ahora además tiene su propio botón visible de cerrar, como el resto de los paneles.
        CreateCloseButton(canvasGO);

        presetMenu = canvasGO;
        presetMenu.SetActive(false);
    }

    /// <summary>
    /// Botón "✕" en la esquina superior derecha del panel -- cierra el menú directamente (no
    /// pasa por el debounce/toggle de OnToggleMenuButtonPressed, simplemente lo oculta).
    /// </summary>
    private void CreateCloseButton(GameObject parent)
    {
        GameObject btnGO = new GameObject("Btn_Cerrar");
        btnGO.transform.SetParent(parent.transform, false);

        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(360, 270);
        rect.sizeDelta = new Vector2(60, 60);

        Image btnImage = btnGO.AddComponent<Image>();
        btnImage.color = new Color(0.75f, 0.15f, 0.2f, 0.9f);

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(() => { if (presetMenu != null) presetMenu.SetActive(false); });

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "✕";
        btnText.fontSize = 34;
        btnText.fontStyle = FontStyles.Bold;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
    }

    /// <summary>
    /// Botón "🎮 Controles" que abre el modal futurista con la lista de botones del control y
    /// para qué sirve cada uno (ControlsHelpPanelController, sexta ronda).
    /// </summary>
    private void CreateControlsHelpButton(GameObject parent)
    {
        GameObject btnGO = new GameObject("Btn_Controles");
        btnGO.transform.SetParent(parent.transform, false);

        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0, -220);
        rect.sizeDelta = new Vector2(320, 70);

        Image btnImage = btnGO.AddComponent<Image>();
        btnImage.color = new Color(0.15f, 0.55f, 0.5f, 0.85f);

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(() => ControlsHelpPanelController.Show());

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(btnGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI btnText = textGO.AddComponent<TextMeshProUGUI>();
        btnText.text = "🎮 Controles";
        btnText.fontSize = 28;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
    }

    /// <summary>
    /// Coloca el transform frente a la cámara del jugador, mirando hacia ella.
    /// Si no hay cámara disponible, lo deja en el origen del mundo como fallback.
    /// </summary>
    private void PositionInFrontOfCamera(Transform target)
    {
        if (mainCameraTransform == null)
        {
            target.position = menuOffset;
            return;
        }

        Vector3 posicionDeseada = mainCameraTransform.position
            + mainCameraTransform.forward * menuOffset.z
            + mainCameraTransform.up * menuOffset.y
            + mainCameraTransform.right * menuOffset.x;

        // Si el jugador está parado cerca de una pared o un mueble, la posición "deseada" del
        // menú (a una distancia fija de la cámara) puede caer adentro o detrás de esa geometría
        // -- por eso el menú queda tapado. Se hace un raycast desde la cámara hacia esa posición
        // y, si algo lo bloquea antes, se trae el menú justo delante de eso.
        target.position = SceneGenerator.ClampInFrontOfObstruction(
            mainCameraTransform.position, posicionDeseada);

        // Que el menú mire hacia el jugador (no al revés)
        target.rotation = Quaternion.LookRotation(target.position - mainCameraTransform.position);
    }

    private void CreatePresetButton(GameObject parent, string presetName, string label, Vector2 position)
    {
        GameObject buttonGO = new GameObject($"Btn_{presetName}");
        buttonGO.transform.SetParent(parent.transform, false);

        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(130, 120);

        Image btnImage = buttonGO.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.6f, 1f, 0.8f);

        Button btn = buttonGO.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(() => LoadPreset(presetName));

        // Button Text
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);

        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(130, 120);

        TextMeshProUGUI btnText = textGO.AddComponent<TextMeshProUGUI>();
        btnText.text = label;
        btnText.fontSize = 20;
        btnText.alignment = TextAlignmentOptions.Center;
        btnText.color = Color.white;
    }

    public void LoadPreset(string presetName)
    {
        statusText.text = "Cargando...";
        statusText.color = new Color(1, 1, 0, 1); // Yellow

        TextAsset jsonAsset = Resources.Load<TextAsset>($"ScenePresets/preset_{presetName}");
        if (jsonAsset == null)
        {
            statusText.text = $"Error: preset_{presetName} no encontrado";
            statusText.color = new Color(1, 0, 0, 1); // Red
            return;
        }

        sceneGenerator.GenerateSceneAsync(jsonAsset.text);

        // Cerrar el modal al elegir una opción (comportamiento esperado de un modal). Los
        // errores igual se ven: OnGenerationError además dispara un ToastNotificationUI, que no
        // depende de que este panel esté abierto.
        if (presetMenu != null) presetMenu.SetActive(false);
    }

    private void OnGenerationProgress(int current, int total, string message)
    {
        statusText.text = $"{message} ({current}/{total})";
        statusText.color = new Color(1, 1, 0, 1); // Yellow
    }

    private void OnGenerationComplete(bool success, string message)
    {
        statusText.text = "✓ Listo - Generación completada";
        statusText.color = new Color(0, 1, 0, 1); // Green
    }

    private void OnGenerationError(string errorMessage)
    {
        statusText.text = $"✗ Error: {errorMessage}";
        statusText.color = new Color(1, 0, 0, 1); // Red
        ToastNotificationUI.ShowError(errorMessage, 5f);
    }

    private void OnDestroy()
    {
        if (sceneGenerator != null)
        {
            sceneGenerator.OnProgress -= OnGenerationProgress;
            sceneGenerator.OnComplete -= OnGenerationComplete;
            sceneGenerator.OnError -= OnGenerationError;
        }
    }
}
