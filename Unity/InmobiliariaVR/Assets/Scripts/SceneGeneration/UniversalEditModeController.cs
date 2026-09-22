using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Activación del modo de edición universal (Punto 2 del plan de edición avanzada, 2026-09-18, el
/// más grande de todos): hacer CLICK con el botón Y del control izquierdo apuntando/tocando
/// CUALQUIER objeto (mueble o pared, exterior o "Muro divisorio") abre el panel CRUD
/// (Crear/Rotar/Eliminar/Actualizar/Salir) para ese objeto -- ver CrudPanelController.
///
/// Ajuste pedido por Alan (2026-09-19, punto 1): antes había que MANTENER presionado 3 segundos;
/// ahora alcanza con un click (un solo apretón), tal cual pidió ("vamos a hacer click sobre un
/// objeto y nos salga [el menú]"). Se detecta el flanco de subida del botón a mano (comparando el
/// estado de este frame contra el del frame anterior) en vez de depender de WasPressedThisFrame()
/// de una versión puntual del Input System, para no arriesgar nada que no se pueda verificar sin
/// el Editor.
///
/// "A qué objeto se está apuntando" se resuelve reusando el hover que la propia XR Interaction
/// Toolkit ya calcula para resaltar interactables bajo el rayo/mano (SceneGenerator.HacerInteractivo
/// y HacerMuroInteractivo llaman a SetHoverTarget/ClearHoverTarget en hoverEntered/hoverExited de
/// cada objeto) -- así no hace falta un raycast propio aparte.
///
/// El botón Y del control izquierdo (secondaryButton) sigue reservado EXCLUSIVAMENTE para esto
/// (ver el comentario en FurnitureCatalogController sobre por qué se le sacó el catálogo) -- el
/// catálogo se quedó solo con el botón B del control derecho.
///
/// AJUSTE 2026-09-19 (tercera ronda, pedido de Alan: "el modal... se pueda abrir con cualquier
/// mueble, o sea que se cierre el modal que estaba abierto y se abra el nuevo... un modal a la vez
/// para que no se amontonen"): antes, si el panel CRUD ya estaba abierto (para CUALQUIER objeto),
/// un nuevo click de Y sobre otro objeto distinto no hacía nada -- había que cerrar el panel a
/// mano primero para poder abrirlo en otro. Ahora un click siempre (re)abre el panel CRUD para lo
/// que se esté apuntando, sea cual sea el objeto -- como CrudPanelController es un panel único
/// reusado (no se crea uno nuevo por objeto), esto automáticamente "cierra" el anterior y "abre" el
/// nuevo con solo cambiarle el objetivo, sin que nunca lleguen a convivir dos.
///
/// Lo único que sigue bloqueando el click es que haya una SESIÓN de edición especial en curso
/// (Edición flotante / Edición con objetivo fijo / Cambiar tamaño) -- a diferencia del panel CRUD,
/// esas tienen estado propio (jugador teletransportado, posición original guardada) y no tendría
/// sentido interrumpirlas a mitad de camino con un click en otro objeto cualquiera.
/// </summary>
public class UniversalEditModeController : MonoBehaviour
{
    private static UniversalEditModeController instance;

    private static GameObject hoverTarget;

    private InputAction clickAction;
    private bool presionadoElFrameAnterior;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("UniversalEditModeManager");
        instance = go.AddComponent<UniversalEditModeController>();
    }

    private void Awake()
    {
        instance = this;
    }

    /// <summary>Llamado desde SceneGenerator en el hoverEntered de cualquier mueble o pared.</summary>
    public static void SetHoverTarget(GameObject obj)
    {
        hoverTarget = obj;
    }

    /// <summary>
    /// Llamado en hoverExited -- solo limpia si sigue siendo el mismo objeto (por si el rayo ya
    /// pasó a apuntar a otra cosa distinta antes de que llegue este evento del objeto anterior).
    /// </summary>
    public static void ClearHoverTarget(GameObject obj)
    {
        if (hoverTarget == obj) hoverTarget = null;
    }

    private void OnEnable()
    {
        clickAction = new InputAction(name: "UniversalEditClick", type: InputActionType.Button);
        clickAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
        clickAction.AddBinding("<MetaQuestTouchPlusController>{LeftHand}/secondaryButton");
        clickAction.AddBinding("<OculusTouchController>{LeftHand}/secondaryButton");
        clickAction.Enable();
    }

    private void OnDisable()
    {
        if (clickAction == null) return;
        clickAction.Disable();
        clickAction.Dispose();
        clickAction = null;
    }

    private void Update()
    {
        if (clickAction == null) return;

        bool presionadoAhora = clickAction.IsPressed();
        bool esClickNuevo = presionadoAhora && !presionadoElFrameAnterior;
        presionadoElFrameAnterior = presionadoAhora;

        if (!esClickNuevo) return;

        bool hayObjetivoValido = hoverTarget != null;

        // Ajuste 2026-09-19 (tercera ronda): ya NO se bloquea porque el panel CRUD o el de pintar
        // ya estén abiertos (ver comentario de clase) -- solo se bloquea si hay una sesión de
        // edición especial en curso, que sí tiene estado propio que no conviene interrumpir.
        bool sesionEspecialActiva = UpdateModesController.HayModoEspecialActivo || ResizeHandlesController.HayModoActivo;
        if (!hayObjetivoValido || sesionEspecialActiva) return;

        GameObject objetivo = hoverTarget;
        CrudPanelController.EnsureExists();
        CrudPanelController.ShowFor(objetivo);
    }
}
