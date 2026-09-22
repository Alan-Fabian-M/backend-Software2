using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Redimensionar arrastrando esferitas alrededor del objeto (ajuste pedido por Alan el
/// 2026-09-19, punto 2: "poneme un recuadro sobre el objeto... que salga puntos o flechas
/// alrededor del objeto para cambiar el tamaño"), en reemplazo del panel de botones "－/＋" que
/// había antes en "Solo dimensión" (UpdateModesController.EntrarSoloDimension, eliminado).
///
/// Al entrar a este modo aparecen 8 esferitas amarillas, una en cada esquina de la caja del
/// objeto (usando SceneGenerator.ComputeLocalBounds para ubicarlas bien pegadas a su tamaño real,
/// sin importar en qué ángulo esté rotado). Agarrar cualquiera de ellas y alejarla o acercarla del
/// objeto lo agranda o achica de forma pareja en los 3 ejes -- mismo resultado que antes lograban
/// los botones +/-, pero arrastrando con la mano en vez de apretar de a poco. Soltarla deja el
/// nuevo tamaño fijo y reacomoda las 8 esquinas a la nueva caja.
///
/// Mientras dura el modo, el objeto real se cubre con el material verde temporal de
/// TemporaryGhostVisual (punto 3 del mismo pedido) para dar mejor referencia visual de qué se
/// está agrandando/achicando -- "Listo" lo confirma y le devuelve su aspecto y colores reales.
///
/// AJUSTE 2026-09-19 (pedido de Alan, punto 2: "redimensionar loque se muestra dónde está el
/// objeto" -- mensaje ambiguo/con errores de tipeo, así que esta es una interpretación de mejor
/// esfuerzo, a confirmar con Alan): se entendió como que el TAMAÑO VISUAL de las esferitas de
/// agarre debía corresponder mejor al tamaño real del objeto que están rodeando, en vez de ser
/// todas del mismo tamaño fijo (0.07) sin importar si el mueble es diminuto o enorme -- ver
/// PosicionarHandles() más abajo, que ahora calcula el tamaño de cada esferita en proporción a la
/// diagonal de la caja del objeto. Si esto no era lo que se pedía, avisar para ajustarlo.
///
/// AJUSTE 2026-09-19 (tercera ronda, dos pedidos más):
///
///   • "las bolitas... muchas veces me lo puedo llevar, llevándose una mala experiencia de
///     usuario": mientras este modo está activo, el propio objeto seguía teniendo su
///     XRGrabInteractable normal habilitado -- si al intentar agarrar una esferita la mano/rayo
///     tocaba el objeto de al lado en cambio, terminabas moviéndolo (o arrastrándolo lejos) en vez
///     de redimensionarlo. Ahora, mientras se está redimensionando, el XRGrabInteractable del
///     objeto se DESHABILITA (se reactiva solo al salir con "Listo") -- así el único agarre posible
///     en este modo es una de las esferitas.
///
///   • "los muros... solo se pueda editar el ancho o el largo... no como los muebles que sea de
///     las esquinas": para un mueble normal se mantienen las 8 esferitas de esquina (escalado
///     parejo en los 3 ejes). Para un muro/"Muro divisorio" (MuroEditable), en cambio, aparecen
///     solo 2 esferitas -- una en cada punta del LARGO del muro (eje local X) -- que solo estiran o
///     acortan esa longitud, dejando alto y espesor intactos (esos se ajustan aparte, con los
///     botones +/- de WallPaintPanelController). Un muro EXTERIOR/fijo de la sala directamente no
///     ofrece esta opción (ver CrudPanelController.ShowInternal) -- redimensionarlo arbitrariamente
///     rompería la geometría de la sala.
///
/// AJUSTE 2026-09-19 (cuarta ronda, pedido de Alan: "no hay esa opcion para que se aumento de
/// altura solo altura en los muros agregados"): se agrega una TERCERA esferita para el "Muro
/// divisorio", arriba del muro (eje local Y), que ajusta ÚNICAMENTE su altura (localScale.y) --
/// misma fórmula que MuroEditable.AdjustHeight, reacomodando la posición en Y para que la base del
/// muro se quede pegada al piso en vez de crecer/achicarse parejo para ambos lados. Las 2 esferitas
/// de las puntas siguen controlando solo el largo, sin tocar la altura.
/// </summary>
public class ResizeHandlesController : MonoBehaviour
{
    private static ResizeHandlesController instance;

    private GameObject objetivo;
    public GameObject ObjetivoActual => objetivo;

    /// <summary>True mientras hay un objeto activamente en modo "Cambiar tamaño" -- consultado por
    /// UniversalEditModeController para no dejar que un click de Y en otro objeto interrumpa esto
    /// a mitad de camino (ajuste 2026-09-19, tercera ronda, "un modal a la vez").</summary>
    public static bool HayModoActivo => instance != null && instance.objetivo != null;

    private Vector3 escalaOriginal;
    private MuroEditable muroActivo;
    private XRGrabInteractable grabDelObjetivo;
    private readonly GameObject[] handles = new GameObject[8];
    private static readonly int[] SIGNOS = { -1, 1 };
    private static readonly Vector3[] EXTREMOS_LARGO_MURO = { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };

    // AJUSTE 2026-09-19 (cuarta ronda): esferita extra para ajustar SOLO la altura de un "Muro
    // divisorio" -- ocupa el índice fijo HANDLE_ALTURA_MURO_IDX dentro del mismo pool de 8
    // esferitas que ya existía (el resto del pool queda oculto, igual que antes).
    private static readonly Vector3[] EXTREMOS_ALTO_MURO = { new Vector3(0f, 1f, 0f) };
    private const int HANDLE_ALTURA_MURO_IDX = 2;

    private GameObject panelGO;
    private Text panelLabel;
    private Transform mainCameraTransform;

    private static readonly Color ColorHandle = new Color(1f, 0.82f, 0.15f);
    private static readonly Color ColorConfirmar = new Color(0.2f, 0.55f, 0.35f);
    private static readonly Color ColorNeutro = new Color(0.3f, 0.3f, 0.34f);
    private const float ESCALA_MIN_FACTOR = 0.35f;
    private const float ESCALA_MAX_FACTOR = 2.2f;

    // AJUSTE 2026-09-19 (pedido de Alan, punto 2: "redimensionar loque se muestra dónde está el
    // objeto" -- interpretado como que las esferitas de agarre deben verse proporcionales al
    // tamaño real del objeto, no todas del mismo tamaño fijo sin importar si el mueble es una
    // silla chica o un placard enorme). TAMANO_HANDLE queda como valor default/de arranque (para
    // el primer frame, antes de conocer el tamaño real) y como referencia de los límites; el
    // tamaño real de cada esferita se recalcula en cada PosicionarHandles() a partir de la
    // diagonal de la caja del objeto.
    private const float TAMANO_HANDLE = 0.07f;
    private const float TAMANO_HANDLE_MIN = 0.035f;
    private const float TAMANO_HANDLE_MAX = 0.16f;
    private const float FACTOR_TAMANO_HANDLE = 0.05f;

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("ResizeHandlesManager");
        instance = go.AddComponent<ResizeHandlesController>();
    }

    private void Awake()
    {
        instance = this;
    }

    /// <summary>Punto de entrada: activa el modo de redimensionar por arrastre para este objeto.</summary>
    public static void Activar(GameObject objetivoAActivar)
    {
        EnsureExists();
        instance.EntrarInternal(objetivoAActivar);
    }

    private void EntrarInternal(GameObject objetivoNuevo)
    {
        if (objetivoNuevo == null) return;

        // Por si ya había un objetivo previo sin cerrar (no debería pasar en el flujo normal,
        // pero evita dejar esferitas huérfanas o el material verde pegado en el objeto anterior).
        if (objetivo != null) SalirInternal();

        objetivo = objetivoNuevo;
        escalaOriginal = objetivo.transform.localScale;
        muroActivo = objetivo.GetComponent<MuroEditable>();

        // Ajuste 2026-09-19 (tercera ronda, "las bolitas... muchas veces me lo puedo llevar"): se
        // deshabilita el agarre normal del objeto mientras se redimensiona, para que la única
        // forma de agarrar algo en este modo sea una esferita -- se reactiva en SalirInternal.
        grabDelObjetivo = objetivo.GetComponent<XRGrabInteractable>();
        if (grabDelObjetivo != null) grabDelObjetivo.enabled = false;

        TemporaryGhostVisual.Activar(objetivo);
        CrearHandlesSiHaceFalta();
        PosicionarHandles();

        EnsurePanel();
        PosicionarFrenteACamara(panelGO.transform, 1.1f, -0.25f);
        panelGO.SetActive(true);
        ActualizarLabelPanel(1f);

        ToastNotificationUI.Show(
            muroActivo != null
                ? "📐 Esferitas de las puntas = largo. Esferita de arriba = altura del muro."
                : "📐 Arrastrá cualquiera de las esferitas amarillas para agrandar o achicar el objeto.",
            4f);
    }

    private void SalirInternal()
    {
        if (objetivo != null)
        {
            if (muroActivo == null)
                SceneGenerator.SnapToFloor(objetivo);
            TemporaryGhostVisual.Desactivar(objetivo);
        }

        if (grabDelObjetivo != null)
        {
            grabDelObjetivo.enabled = true;
            grabDelObjetivo = null;
        }

        for (int i = 0; i < handles.Length; i++)
            if (handles[i] != null) handles[i].SetActive(false);

        if (panelGO != null) panelGO.SetActive(false);

        objetivo = null;
        muroActivo = null;
    }

    // -----------------------------------------------------------------
    // Esferitas de arrastre
    // -----------------------------------------------------------------

    private void CrearHandlesSiHaceFalta()
    {
        if (handles[0] != null) return;

        int idx = 0;
        foreach (int sx in SIGNOS)
        foreach (int sy in SIGNOS)
        foreach (int sz in SIGNOS)
        {
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            handle.name = $"ResizeHandle_{idx}";
            handle.transform.localScale = Vector3.one * TAMANO_HANDLE;
            SetColor(handle, ColorHandle);

            Rigidbody rb = handle.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            XRGrabInteractable grab = handle.AddComponent<XRGrabInteractable>();
            grab.movementType = XRGrabInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;

            ResizeHandleDrag drag = handle.AddComponent<ResizeHandleDrag>();
            drag.controlador = this;

            handle.SetActive(false);
            handles[idx] = handle;
            idx++;
        }
    }

    /// <summary>
    /// Recoloca las 8 esferitas en las esquinas actuales de la caja del objeto. Si se le pasa
    /// exceptoHandle, esa esferita puntual no se toca (es la que el usuario está arrastrando en
    /// este momento -- su posición la maneja el propio XRGrabInteractable siguiendo la mano, no
    /// nosotros).
    /// </summary>
    private void PosicionarHandles(GameObject exceptoHandle = null)
    {
        if (objetivo == null) return;

        SceneGenerator.ComputeLocalBounds(objetivo, out Vector3 halfExt, out Vector3 centerOffset);
        Vector3 halfExtEscalado = Vector3.Scale(halfExt, objetivo.transform.lossyScale);

        // Punto 2: tamaño de esferita proporcional a la diagonal de la caja del objeto (escalada
        // por el localScale actual, para que un mueble ya agrandado/achicado también recalcule
        // bien), clamped entre un mínimo (para que no desaparezcan en objetos muy chicos) y un
        // máximo (para que no tapen todo un objeto enorme).
        float diagonal = halfExtEscalado.magnitude * 2f;
        float tamanoHandle = Mathf.Clamp(diagonal * FACTOR_TAMANO_HANDLE, TAMANO_HANDLE_MIN, TAMANO_HANDLE_MAX);

        if (muroActivo != null)
        {
            // Ajuste 2026-09-19 (tercera ronda + cuarta ronda): un muro/divisor ofrece 3 esferitas
            // -- 2 en las puntas de su LARGO (eje local X) y 1 arriba (eje local Y) para la ALTURA
            // -- ver comentario de clase. El resto del pool de 8 queda oculto.
            for (int i = 0; i < handles.Length; i++)
            {
                bool esExtremoLargo = i < EXTREMOS_LARGO_MURO.Length;
                bool esExtremoAlto = i == HANDLE_ALTURA_MURO_IDX;
                bool activo = esExtremoLargo || esExtremoAlto;
                handles[i].SetActive(activo);
                if (!activo || handles[i] == exceptoHandle) continue;

                Vector3 localPunto = esExtremoLargo
                    ? centerOffset + Vector3.Scale(halfExt, EXTREMOS_LARGO_MURO[i])
                    : centerOffset + Vector3.Scale(halfExt, EXTREMOS_ALTO_MURO[0]);
                handles[i].transform.position = objetivo.transform.TransformPoint(localPunto);
                handles[i].transform.localScale = Vector3.one * tamanoHandle;
            }
            return;
        }

        int idx = 0;
        foreach (int sx in SIGNOS)
        foreach (int sy in SIGNOS)
        foreach (int sz in SIGNOS)
        {
            handles[idx].SetActive(true);
            if (handles[idx] != exceptoHandle)
            {
                Vector3 localCorner = centerOffset + Vector3.Scale(halfExt, new Vector3(sx, sy, sz));
                handles[idx].transform.position = objetivo.transform.TransformPoint(localCorner);
                handles[idx].transform.localScale = Vector3.one * tamanoHandle;
            }
            idx++;
        }
    }

    /// <summary>Llamado por ResizeHandleDrag en cada frame mientras se arrastra una esferita.</summary>
    public void AplicarFactorEscala(float factor, GameObject handleActivo)
    {
        if (objetivo == null) return;

        float factorClamped = Mathf.Clamp(factor, ESCALA_MIN_FACTOR, ESCALA_MAX_FACTOR);

        if (muroActivo != null)
        {
            bool esHandleAltura = System.Array.IndexOf(handles, handleActivo) == HANDLE_ALTURA_MURO_IDX;
            Vector3 escalaMuro = objetivo.transform.localScale;

            if (esHandleAltura)
            {
                // Ajuste 2026-09-19 (cuarta ronda, "no hay esa opcion para que se aumento de
                // altura solo altura en los muros agregados"): la esferita de arriba ajusta SOLO
                // localScale.y -- misma fórmula que MuroEditable.AdjustHeight, reacomodando
                // position.y para que la base del muro se quede pegada al piso en vez de crecer
                // parejo para ambos lados.
                escalaMuro.y = Mathf.Max(0.5f, escalaOriginal.y * factorClamped);
                objetivo.transform.localScale = escalaMuro;
                Vector3 pos = objetivo.transform.position;
                pos.y = escalaMuro.y * 0.5f;
                objetivo.transform.position = pos;
            }
            else
            {
                // Ajuste 2026-09-19 (tercera ronda, "los muros... solo se pueda editar el ancho o
                // el largo... no como los muebles que sea de las esquinas"): en vez de escalar
                // parejo en los 3 ejes como un mueble, para un muro esto solo estira/achica su
                // LARGO (eje local X) -- el espesor queda fijo.
                escalaMuro.x = Mathf.Max(0.3f, escalaOriginal.x * factorClamped);
                objetivo.transform.localScale = escalaMuro;
            }
        }
        else
        {
            objetivo.transform.localScale = escalaOriginal * factorClamped;
        }

        PosicionarHandles(handleActivo);
        ActualizarLabelPanel(factorClamped);
    }

    /// <summary>Llamado por ResizeHandleDrag al soltar una esferita.</summary>
    public void NotificarFinArrastre()
    {
        if (objetivo == null) return;
        if (muroActivo == null)
            SceneGenerator.SnapToFloor(objetivo);
        PosicionarHandles();
    }

    // -----------------------------------------------------------------
    // Panel chico (Restaurar / Listo)
    // -----------------------------------------------------------------

    private void EnsurePanel()
    {
        if (panelGO != null) return;

        panelGO = new GameObject("ResizeHandlesPanel");
        panelGO.transform.SetParent(transform, false);

        Canvas canvas = panelGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        panelGO.AddComponent<CanvasScaler>();
        panelGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform rect = panelGO.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(420f, 180f);
        panelGO.transform.localScale = Vector3.one * 0.0015f;

        // Cuarta ronda (2026-09-19): revertido -- Alan pidió volver a que los paneles queden FIJOS
        // donde se posicionaron al abrirse, en vez de seguir a la cámara cada frame (ver
        // FollowCameraPanel, que ya no se usa en ningún panel; se deja el archivo en el proyecto
        // por si se quiere retomar más adelante, pero no se referencia desde ningún lado).
        Image fondo = panelGO.AddComponent<Image>();
        fondo.color = new Color(0.08f, 0.08f, 0.09f, 0.97f);

        GameObject tituloGO = new GameObject("Titulo");
        tituloGO.transform.SetParent(panelGO.transform, false);
        RectTransform tituloRect = tituloGO.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.5f, 0.5f);
        tituloRect.anchorMax = new Vector2(0.5f, 0.5f);
        tituloRect.anchoredPosition = new Vector2(0f, 55f);
        tituloRect.sizeDelta = new Vector2(380f, 50f);
        Text titulo = tituloGO.AddComponent<Text>();
        titulo.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titulo.fontSize = 24;
        titulo.alignment = TextAnchor.MiddleCenter;
        titulo.color = Color.white;
        titulo.text = "📐 Cambiar tamaño";

        GameObject labelGO = new GameObject("Label");
        labelGO.transform.SetParent(panelGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(0f, 10f);
        labelRect.sizeDelta = new Vector2(300f, 40f);
        panelLabel = labelGO.AddComponent<Text>();
        panelLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        panelLabel.fontSize = 22;
        panelLabel.alignment = TextAnchor.MiddleCenter;
        panelLabel.color = Color.white;

        CreateBotonChico(panelGO, "Restaurar", new Vector2(0.3f, 0.5f), -60f, ColorNeutro, RestaurarTamano);
        CreateBotonChico(panelGO, "Listo", new Vector2(0.7f, 0.5f), -60f, ColorConfirmar, SalirInternal);

        panelGO.SetActive(false);
    }

    private void RestaurarTamano()
    {
        if (objetivo == null) return;
        objetivo.transform.localScale = escalaOriginal;
        if (muroActivo == null)
        {
            SceneGenerator.SnapToFloor(objetivo);
        }
        else
        {
            // Cuarta ronda: si se había ajustado la altura (esferita de arriba), position.y quedó
            // corrido -- al restaurar el tamaño original hay que recalcularla también, para que la
            // base del muro no quede flotando o hundida.
            Vector3 pos = objetivo.transform.position;
            pos.y = escalaOriginal.y * 0.5f;
            objetivo.transform.position = pos;
        }
        PosicionarHandles();
        ActualizarLabelPanel(1f);
    }

    private void ActualizarLabelPanel(float factor)
    {
        if (panelLabel == null) return;
        panelLabel.text = $"Tamaño: {Mathf.RoundToInt(factor * 100f)}%";
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private void CreateBotonChico(GameObject padre, string texto, Vector2 anchorCentro, float posY, Color color, System.Action onClick)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(padre.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = anchorCentro;
        rect.anchorMax = anchorCentro;
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(170f, 56f);

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

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material mat = new Material(shader);
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    /// <summary>Mismo helper que ya usan varios paneles del proyecto para flotar frente a la cámara.</summary>
    private void PosicionarFrenteACamara(Transform target, float distancia, float offsetVertical)
    {
        if (mainCameraTransform == null)
        {
            Camera cam = Camera.main;
            if (cam != null) mainCameraTransform = cam.transform;
        }
        if (mainCameraTransform == null) return;

        Vector3 desired = mainCameraTransform.position
            + mainCameraTransform.forward * distancia
            + mainCameraTransform.up * offsetVertical;
        desired = SceneGenerator.ClampInFrontOfObstruction(mainCameraTransform.position, desired);
        target.position = desired;
        target.rotation = Quaternion.LookRotation(target.position - mainCameraTransform.position);
    }
}

/// <summary>
/// Componente puesto en cada esferita de ResizeHandlesController: mientras se la sostiene con la
/// mano, cada frame calcula qué tan lejos quedó del objeto en comparación a cuando la agarró, y le
/// pide al controlador que escale el objeto en esa misma proporción.
/// </summary>
public class ResizeHandleDrag : MonoBehaviour
{
    public ResizeHandlesController controlador;

    private XRGrabInteractable grab;
    private bool arrastrando;
    private float distanciaInicial;

    private void Awake()
    {
        grab = GetComponent<XRGrabInteractable>();
    }

    private void OnEnable()
    {
        if (grab == null) grab = GetComponent<XRGrabInteractable>();
        if (grab == null) return;
        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);
    }

    private void OnDisable()
    {
        if (grab == null) return;
        grab.selectEntered.RemoveListener(OnGrabbed);
        grab.selectExited.RemoveListener(OnReleased);
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        if (controlador == null || controlador.ObjetivoActual == null) return;
        arrastrando = true;
        distanciaInicial = Vector3.Distance(controlador.ObjetivoActual.transform.position, transform.position);
        if (distanciaInicial < 0.02f) distanciaInicial = 0.02f;
    }

    private void OnReleased(SelectExitEventArgs args)
    {
        arrastrando = false;
        controlador?.NotificarFinArrastre();
    }

    private void Update()
    {
        if (!arrastrando || controlador == null || controlador.ObjetivoActual == null) return;

        float distanciaActual = Vector3.Distance(controlador.ObjetivoActual.transform.position, transform.position);
        float factor = distanciaActual / distanciaInicial;
        controlador.AplicarFactorEscala(factor, gameObject);
    }
}
