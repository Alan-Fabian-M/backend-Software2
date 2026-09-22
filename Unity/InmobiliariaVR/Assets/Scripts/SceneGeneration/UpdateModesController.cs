using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

/// <summary>
/// Las 4 modalidades de "Actualizar" del modo de edición universal (Punto 2 del plan de edición
/// avanzada, 2026-09-18), abiertas desde CrudPanelController:
///
///   • Edición flotante: el objeto flota frente al jugador para reposicionarlo/rotarlo cómodo,
///     con "Confirmar" (deja la nueva posición) o "Cancelar" (vuelve exactamente a donde estaba).
///   • Edición real: el sistema normal de agarrar/mover/rotar/pintar de siempre -- no hace falta
///     ningún modo especial, ya funciona así. Nota importante: la selección por RAYO (no solo
///     mano) que pediste ya la cubre el rig del proyecto (Left/Right_NearFarInteractor de la XR
///     Interaction Toolkit soporta las dos distancias en el mismo interactor), así que no hizo
///     falta agregar nada de código extra para eso -- se verifica en el Quest real igual.
///   • Edición con objetivo fijo: teletransporta al jugador Y al objeto a un espacio aislado
///     (piso + fondo negro contrastante, sin el resto de la sala de por medio) para poder verlo
///     sin distracciones; "Volver" trae a los dos de regreso a donde estaban.
///   • Solo dimensión: ver ResizeHandlesController -- se movió ahí (ajuste pedido por Alan,
///     2026-09-19, punto 2): en vez del panel +/- que había antes, ahora aparecen esferitas
///     agarrables alrededor del objeto para redimensionarlo arrastrando con la mano.
///
/// Simplificación consciente en los otros 3 modos: en vez de gizmos/flechas 3D arrastrables (que
/// necesitarían un sistema de drag propio, imposible de verificar sin acceso al Editor), se usan
/// paneles con botones y confirmar/cancelar -- el mismo resultado funcional, con una interacción
/// más simple y confiable de primera.
/// </summary>
public class UpdateModesController : MonoBehaviour
{
    private static UpdateModesController instance;
    public static UpdateModesController Instance => instance;

    private SceneGenerator sceneGeneratorCache;

    private static readonly Color ColorNeutro = new Color(0.3f, 0.3f, 0.34f);
    private static readonly Color ColorConfirmar = new Color(0.2f, 0.55f, 0.35f);
    private static readonly Color ColorCancelar = new Color(0.6f, 0.2f, 0.2f);

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("UpdateModesManager");
        instance = go.AddComponent<UpdateModesController>();
    }

    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// AJUSTE 2026-09-19 (pedido de Alan, puntos 5 y 8): true si "obj" está siendo manejado ahora
    /// mismo por "Edición flotante" o "Edición con objetivo fijo". Lo consulta SceneGenerator en
    /// el selectExited de sus XRGrabInteractable -- si el objeto está en uno de estos modos
    /// especiales, agarrarlo con la mano para acomodarlo y soltarlo NO debe disparar el clamp a
    /// los límites de la sala real ni el apoyo forzado en el piso (eso era lo que hacía que el
    /// objeto "pareciera desaparecer" al reclampearse a las coordenadas de la sala real estando
    /// parado lejos, en el escenario aislado, o que no se pudiera dejar flotando en el aire
    /// durante la edición flotante). El propio modo especial decide cuándo y cómo terminar, con
    /// sus botones Confirmar/Cancelar/Volver.
    /// </summary>
    public static bool EstaGestionadoPorModoEspecial(GameObject obj)
    {
        if (obj == null || instance == null) return false;
        return obj == instance.objetivoFlotante || obj == instance.objetivoEscenario;
    }

    /// <summary>
    /// Quinta ronda (2026-09-19): variante más específica de la de arriba, solo para "Edición con
    /// objetivo fijo" (el escenario aislado) -- ver SceneGenerator, selectExited de los muebles:
    /// ahí se usa para decidir si hay que volver a poner la piel verde temporal al soltar el
    /// objeto agarrado a mano (sí, para el escenario aislado -- así se ve "en edición" hasta
    /// apretar Volver; NO para Edición flotante, donde Alan pidió que el objeto se vea con su
    /// color real salvo el instante en que efectivamente lo estás sosteniendo).
    /// </summary>
    public static bool EstaGestionadoPorEscenario(GameObject obj)
    {
        if (obj == null || instance == null) return false;
        return obj == instance.objetivoEscenario;
    }

    /// <summary>
    /// AJUSTE 2026-09-19 (tercera ronda, "un modal a la vez para que no se amontonen"): true si
    /// hay una "Edición flotante" o "Edición con objetivo fijo" en curso ahora mismo (con
    /// cualquier objeto). A diferencia de CrudPanelController/WallPaintPanelController -- que son
    /// menús simples que se pueden cerrar y reabrir sin perder nada -- estos dos son SESIONES de
    /// edición con estado propio (posición original guardada, jugador teletransportado, etc.), así
    /// que UniversalEditModeController lo consulta para NO permitir que un click de Y sobre otro
    /// objeto distinto las interrumpa a mitad de camino (dejaría al jugador teletransportado con
    /// el objeto original a medio editar). Se cierran únicamente con sus propios botones
    /// Confirmar/Cancelar/Volver.
    /// </summary>
    public static bool HayModoEspecialActivo => instance != null &&
        (instance.objetivoFlotante != null || instance.objetivoEscenario != null);

    // ===================================================================
    // Edición real (passthrough -- ver comentario de la clase)
    // ===================================================================

    public void EntrarEdicionReal(GameObject objetivo)
    {
        if (objetivo == null) return;
        ToastNotificationUI.Show(
            "✋ Edición real: agarrá el objeto con la mano o apuntándole con el rayo, como siempre, para moverlo/rotarlo/pintarlo.",
            4f);
    }

    // ===================================================================
    // Solo dimensión -- ver ResizeHandlesController.Activar (movido ahí, 2026-09-19)
    // ===================================================================

    // ===================================================================
    // Edición flotante
    // ===================================================================

    private GameObject flotanteCanvasGO;
    private GameObject objetivoFlotante;
    private Vector3 posOriginalFlotante;
    private Quaternion rotOriginalFlotante;

    public void EntrarEdicionFlotante(GameObject objetivo)
    {
        if (objetivo == null) return;

        EnsureFlotanteUI();
        objetivoFlotante = objetivo;
        posOriginalFlotante = objetivo.transform.position;
        rotOriginalFlotante = objetivo.transform.rotation;

        // Quinta ronda (2026-09-19, pedido de Alan: "la edicion flotante sigue apareciendo verde
        // ... deberia aparecer normal sin el efecto que se esta agarrando"): REVERTIDO -- antes
        // (tercera ronda) se cubría el objeto con la piel verde temporal apenas entrabas a
        // Edición flotante, así que se veía verde todo el tiempo que estuviera flotando, no solo
        // mientras lo sostenías con la mano. Ahora se ve con su color/material real mientras
        // flota; si lo agarrás físicamente con la mano para acomodarlo, SÍ se pone verde un
        // momento (esa parte la maneja aparte SceneGenerator, en el selectEntered/selectExited del
        // XRGrabInteractable -- ver StartFootprintTracking/StopFootprintTracking), igual que
        // cualquier otro mueble mientras se lo sostiene.
        Camera cam = Camera.main;
        if (cam != null)
            objetivo.transform.position = cam.transform.position + cam.transform.forward * 1.0f;

        PosicionarFrenteACamara(flotanteCanvasGO.transform, 1.7f, -0.35f);
        flotanteCanvasGO.SetActive(true);

        ToastNotificationUI.Show(
            "🪄 Edición flotante: el objeto está flotando frente tuyo -- girálo con los botones o agarralo con la mano, y confirmá cuando quede como querés.",
            4.5f);
    }

    private void EnsureFlotanteUI()
    {
        if (flotanteCanvasGO != null) return;

        flotanteCanvasGO = CreateCanvasBase("EdicionFlotanteCanvas", new Vector2(500f, 240f));

        CreateTitulo(flotanteCanvasGO, "🪄 Edición flotante", 85f);

        CreateBotonRedondo(flotanteCanvasGO, "⟲", new Vector2(-140f, -10f), ColorNeutro, () => RotarFlotante(-45f));
        CreateBotonRedondo(flotanteCanvasGO, "⟳", new Vector2(140f, -10f), ColorNeutro, () => RotarFlotante(45f));

        CreateBotonChico(flotanteCanvasGO, "Cancelar", new Vector2(0.32f, 0.5f), -95f, ColorCancelar, CancelarFlotante);
        CreateBotonChico(flotanteCanvasGO, "Confirmar", new Vector2(0.68f, 0.5f), -95f, ColorConfirmar, ConfirmarFlotante);

        flotanteCanvasGO.SetActive(false);
    }

    private void RotarFlotante(float gradosY)
    {
        if (objetivoFlotante != null)
            objetivoFlotante.transform.Rotate(Vector3.up, gradosY, Space.World);
    }

    private void ConfirmarFlotante()
    {
        if (objetivoFlotante != null)
        {
            SceneGenerator sceneGenerator = ObtenerSceneGenerator();
            sceneGenerator?.FinalizarColocacionExterna(objetivoFlotante);
            ToastNotificationUI.Show("Nueva posición confirmada", 2f);
        }
        CerrarFlotante();
    }

    private void CancelarFlotante()
    {
        if (objetivoFlotante != null)
        {
            objetivoFlotante.transform.position = posOriginalFlotante;
            objetivoFlotante.transform.rotation = rotOriginalFlotante;
            TemporaryGhostVisual.Desactivar(objetivoFlotante);
        }
        CerrarFlotante();
    }

    private void CerrarFlotante()
    {
        flotanteCanvasGO.SetActive(false);
        objetivoFlotante = null;
    }

    // ===================================================================
    // Edición con objetivo fijo (escenario aislado)
    // ===================================================================

    // Bien lejos de cualquier sala generada (que suelen medir unos pocos metros) -- así no hay
    // riesgo de que el "escenario" quede superpuesto con la sala real por coincidencia.
    private static readonly Vector3 PosicionEscenario = new Vector3(500f, 50f, 500f);

    private GameObject escenarioGO;
    private GameObject escenarioCanvasGO;
    private GameObject objetivoEscenario;
    private Vector3 posOriginalEscenario;
    private Quaternion rotOriginalEscenario;
    private Transform xrOriginEscenario;
    private Vector3 posOriginalJugadorEscenario;
    private Quaternion rotOriginalJugadorEscenario;

    public void EntrarEdicionObjetivoFijo(GameObject objetivo)
    {
        if (objetivo == null) return;

        EnsureEscenario();
        EnsureEscenarioUI();

        objetivoEscenario = objetivo;
        posOriginalEscenario = objetivo.transform.position;
        rotOriginalEscenario = objetivo.transform.rotation;

        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        if (xrOrigin != null)
        {
            xrOriginEscenario = xrOrigin.transform;
            posOriginalJugadorEscenario = xrOrigin.transform.position;
            rotOriginalJugadorEscenario = xrOrigin.transform.rotation;
            xrOrigin.transform.position = PosicionEscenario + new Vector3(0f, 0f, -1.6f);
            xrOrigin.transform.rotation = Quaternion.identity;
        }

        objetivo.transform.SetPositionAndRotation(PosicionEscenario, Quaternion.identity);

        escenarioGO.SetActive(true);
        PosicionarFrenteACamara(escenarioCanvasGO.transform, 1.0f, -0.55f);
        escenarioCanvasGO.SetActive(true);

        ToastNotificationUI.Show(
            "🎯 Edición con objetivo fijo: te llevé a un espacio aislado para ver el objeto sin distracciones. Apretá \"Volver\" cuando termines.",
            4.5f);
    }

    // Medio-lado de la plataforma del escenario aislado (queda un cuadrado de 6x6). El jugador
    // entra en PosicionEscenario + (0,0,-1.6), bien adentro de este cuadrado, así que con las 4
    // paredes de acá abajo (ajuste pedido por Alan, 2026-09-19, punto 8: "colocar en el personaje
    // en un cuarto cerrado para que el personaje no caiga") queda físicamente imposible caminar
    // hasta el borde y caerse -- antes solo había piso + 1 pared de fondo, así que por los otros 3
    // lados no había nada que lo frenara.
    private const float MedioLadoEscenario = 3f;
    private const float AlturaParedEscenario = 3f;
    private const float EspesorParedEscenario = 0.15f;

    private void EnsureEscenario()
    {
        if (escenarioGO != null) return;

        escenarioGO = new GameObject("EscenarioAislado");
        escenarioGO.transform.position = PosicionEscenario;

        float lado = MedioLadoEscenario * 2f;

        GameObject piso = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piso.name = "Piso";
        piso.transform.SetParent(escenarioGO.transform, false);
        piso.transform.localPosition = new Vector3(0f, -0.05f, 0f);
        piso.transform.localScale = new Vector3(lado, 0.1f, lado);
        SetColor(piso, new Color(0.16f, 0.16f, 0.17f));

        // Las 4 paredes que encierran la plataforma -- mismo color oscuro y contrastante que
        // tenía la única pared de fondo original, ahora en los 4 lados.
        Color colorPared = Color.black;
        float mitadAltura = AlturaParedEscenario * 0.5f;

        CrearParedEscenario("ParedFondo", new Vector3(0f, mitadAltura, MedioLadoEscenario),
            new Vector3(lado, AlturaParedEscenario, EspesorParedEscenario), colorPared);
        CrearParedEscenario("ParedFrente", new Vector3(0f, mitadAltura, -MedioLadoEscenario),
            new Vector3(lado, AlturaParedEscenario, EspesorParedEscenario), colorPared);
        CrearParedEscenario("ParedIzquierda", new Vector3(-MedioLadoEscenario, mitadAltura, 0f),
            new Vector3(EspesorParedEscenario, AlturaParedEscenario, lado), colorPared);
        CrearParedEscenario("ParedDerecha", new Vector3(MedioLadoEscenario, mitadAltura, 0f),
            new Vector3(EspesorParedEscenario, AlturaParedEscenario, lado), colorPared);

        GameObject luzGO = new GameObject("LuzEscenario");
        luzGO.transform.SetParent(escenarioGO.transform, false);
        luzGO.transform.localPosition = new Vector3(0f, 3f, -1f);
        luzGO.transform.localRotation = Quaternion.Euler(50f, -30f, 0f);
        Light luz = luzGO.AddComponent<Light>();
        luz.type = LightType.Directional;
        luz.intensity = 1.2f;

        escenarioGO.SetActive(false);
    }

    /// <summary>Helper para no repetir 4 veces la creación de un cubo-pared del escenario aislado.</summary>
    private void CrearParedEscenario(string nombre, Vector3 localPos, Vector3 escala, Color color)
    {
        GameObject pared = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pared.name = nombre;
        pared.transform.SetParent(escenarioGO.transform, false);
        pared.transform.localPosition = localPos;
        pared.transform.localScale = escala;
        SetColor(pared, color);
    }

    private void EnsureEscenarioUI()
    {
        if (escenarioCanvasGO != null) return;

        escenarioCanvasGO = CreateCanvasBase("EscenarioAisladoCanvas", new Vector2(420f, 180f));
        CreateTitulo(escenarioCanvasGO, "🎯 Objetivo fijo", 55f);
        CreateBotonChico(escenarioCanvasGO, "← Volver a la sala", new Vector2(0.5f, 0.5f), -30f, ColorConfirmar, VolverDeEscenario, 320f);

        escenarioCanvasGO.SetActive(false);
    }

    private void VolverDeEscenario()
    {
        if (objetivoEscenario != null)
        {
            objetivoEscenario.transform.SetPositionAndRotation(posOriginalEscenario, rotOriginalEscenario);
            SceneGenerator.SnapToFloor(objetivoEscenario);

            // Ajuste 2026-09-19 (tercera ronda, bug reportado: "se pone en verde y no deja de
            // estar en verde"): por si el objeto todavía tenía puesta la piel verde temporal (por
            // ejemplo si el jugador lo soltó sosteniéndolo justo antes de apretar "Volver"), se le
            // devuelve acá su material real -- red de seguridad, mismo patrón que ya usa
            // SceneGenerator.FinalizePlacement. No hace nada si no la tenía puesta.
            TemporaryGhostVisual.Desactivar(objetivoEscenario);
        }

        if (xrOriginEscenario != null)
            xrOriginEscenario.SetPositionAndRotation(posOriginalJugadorEscenario, rotOriginalJugadorEscenario);

        escenarioGO.SetActive(false);
        escenarioCanvasGO.SetActive(false);
        objetivoEscenario = null;
    }

    // ===================================================================
    // Helpers de UI compartidos (mismo estilo que ConfirmDialogUI/CatMenuPanelController)
    // ===================================================================

    private GameObject CreateCanvasBase(string nombre, Vector2 tamano)
    {
        GameObject canvasGO = new GameObject(nombre);
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rect = canvasGO.GetComponent<RectTransform>();
        rect.sizeDelta = tamano;
        canvasGO.transform.localScale = Vector3.one * 0.0015f;

        // Cuarta ronda (2026-09-19): revertido el "seguir a la cámara" (FollowCameraPanel) que se
        // había agregado en la tercera ronda -- Alan pidió volver a que el panel quede FIJO donde
        // se posicionó al entrar al modo (ver PosicionarFrenteACamara más abajo).
        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.09f, 0.97f);

        return canvasGO;
    }

    private void CreateTitulo(GameObject canvasGO, string texto, float posY)
    {
        GameObject tituloGO = new GameObject("Titulo");
        tituloGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = tituloGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(420f, 50f);
        Text t = tituloGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = texto;
    }

    private Text CreateTextoCentral(GameObject canvasGO, float posY)
    {
        GameObject textoGO = new GameObject("TextoCentral");
        textoGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = textoGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(300f, 50f);
        Text t = textoGO.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 24;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        return t;
    }

    private void CreateBotonRedondo(GameObject canvasGO, string texto, Vector2 posicion, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = posicion;
        rect.sizeDelta = new Vector2(80f, 80f);

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
        label.fontSize = 34;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = texto;
    }

    private void CreateBotonChico(GameObject canvasGO, string texto, Vector2 anchorCentro, float posY, Color color,
        System.Action onClick, float ancho = 190f)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(canvasGO.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorCentro;
        rect.anchorMax = anchorCentro;
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(ancho, 56f);

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

    private void SetColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(shader != null ? shader : renderer.sharedMaterial.shader);
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    /// <summary>Mismo helper que ya usan varios paneles del proyecto para flotar frente a la cámara.</summary>
    private void PosicionarFrenteACamara(Transform target, float distancia, float offsetVertical)
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 desired = cam.transform.position + cam.transform.forward * distancia + cam.transform.up * offsetVertical;
        desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);
        target.position = desired;
        target.rotation = Quaternion.LookRotation(target.position - cam.transform.position);
    }

    private SceneGenerator ObtenerSceneGenerator()
    {
        if (sceneGeneratorCache == null)
            sceneGeneratorCache = Object.FindAnyObjectByType<SceneGenerator>();
        return sceneGeneratorCache;
    }
}
