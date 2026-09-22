using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

/// <summary>
/// "Vuelo libre" con el botón X del control izquierdo -- TERCERA versión de este sistema
/// (2026-09-19, sexta ronda de feedback), a pedido de Alan. Reemplaza por completo el diseño de
/// la ronda anterior (mantener X para subir + apretón corto para desactivar + gatillo izquierdo
/// para bajar + diálogo de confirmación "¿Activar vuelo libre?").
///
/// DISEÑO ACTUAL:
///   1) Con las manos vacías (sin sostener ningún mueble ni "Muro divisorio"): un apretón de X
///      ACTIVA el vuelo libre si estaba apagado, o lo DESACTIVA si ya estaba encendido -- un
///      simple interruptor de encendido/apagado, sin mantener apretado ni distinguir por cuánto
///      tiempo se lo aprieta. Ya NO hay diálogo de confirmación: este pedido describió la
///      activación como algo directo con la X ("que se active o desactive con la X"), sin
///      mencionar ningún diálogo, así que se sacó para que sea más inmediato. Si en realidad lo
///      querías conservar, avisame y lo agrego de nuevo.
///   2) Ya en vuelo, con las manos vacías: mantener apretado el GATILLO IZQUIERDO sube; mantener
///      apretado el GATILLO DERECHO baja (antes los dos, activar Y subir, estaban del lado
///      izquierdo -- ahora quedan repartidos, uno por mano, como pediste esta vez).
///   3) Al bajar con el gatillo derecho, no se atraviesa el piso ni los muebles que haya justo
///      debajo: se hace un raycast hacia abajo y el descenso se frena apenas toca algo, en vez de
///      seguir hundiéndose ("que descienda... cuando no esté posado sobre un objeto, para que la
///      experiencia sea buena"). El resto del vuelo (subir, o moverse de costado con el stick)
///      sigue sin colisión, así que igual se puede volar por ENCIMA de paredes y muebles sin
///      problema y después bajar y aterrizar limpiamente arriba de lo que sea.
///   4) Igual que en la ronda anterior: si se está sosteniendo un mueble o un "Muro divisorio"
///      con la otra mano, el botón X y los dos gatillos quedan 100% para lo de siempre de esos
///      objetos (cambiar color / abrir panel del muro / agarrar) -- este componente no hace nada
///      mientras haya algo en la mano, ni siquiera si ya se estaba volando antes de agarrarlo (el
///      vuelo simplemente queda "en pausa" -- ascenso/descenso no responden -- hasta soltar).
///   5) SÉPTIMA RONDA (2026-09-19): mientras cualquier panel clickeable del proyecto esté abierto
///      (menú de monoambientes, catálogo, CRUD, pintar muros, un diálogo Sí/No, el modal de
///      controles, o los modos de Edición flotante/Objetivo fijo/Cambiar tamaño), los gatillos
///      TAMPOCO suben/bajan -- quedan libres para clickear el panel en vez de competir con el
///      vuelo. Antes, si volabas y apuntabas a un botón, el click SÍ llegaba pero además subías o
///      bajabas al mismo tiempo. Ver el comentario largo en HayPanelAbierto() más abajo.
///
/// BUG ENCONTRADO Y ARREGLADO ("no me puedo elevar"): la versión anterior dejaba SIEMPRE
/// encendido el CharacterController del jugador y solo apagaba la colisión por capas
/// (Physics.IgnoreLayerCollision) para poder atravesar cosas. Eso no alcanza para el ascenso: el
/// XR Origin de este proyecto (el mismo rig de donde sale el JumpProvider que ya se desactivó en
/// DisablePlayerJump.cs) trae también, de fábrica, un sistema de gravedad que en cada frame llama
/// characterController.Move(Vector3.down * ...) para hacer caer al jugador si no está
/// "grounded" -- y ese chequeo y esos llamados a Move() siguen funcionando SIN IMPORTAR que la
/// colisión por capas esté apagada (Physics.IgnoreLayerCollision solo cambia contra qué otras
/// capas choca el jugador, no apaga el CharacterController en sí). Resultado: apenas se soltaba
/// el botón de subir, esa gravedad de fábrica volvía a tirar al jugador para abajo en el mismo
/// frame o el siguiente, y como una caída real acelera con el tiempo, terminaba ganándole por
/// completo al ascenso -- se sentía exactamente como "no puedo elevarme".
///
/// El arreglo (best-effort: no se pudo compilar ni probar en el visor real desde esta sesión,
/// avisame si de última seguís sin poder subir y se revisa de nuevo con más detalle) tiene dos
/// partes:
///   a) Mientras se está volando, se DESACTIVA el propio componente CharacterController del
///      jugador (characterController.enabled = false). Sin el componente encendido, cualquier
///      llamado a su Move() -- el de la gravedad de fábrica incluido -- no hace nada, así que deja
///      de pelearle al ascenso. Se reactiva al salir del modo vuelo.
///   b) Como red de seguridad extra (por si esa gravedad de fábrica no se aplica a través de
///      CharacterController.Move() sino de otra forma), se desactivan también, mientras se vuela,
///      todos los MonoBehaviour cuyo nombre de clase contenga "Gravity" (sin importar
///      mayúsculas/minúsculas) -- mismo truco por nombre de clase que ya usa DisablePlayerJump.cs
///      para el salto, para no depender del namespace exacto del XR Interaction Toolkit. Se
///      reactivan también al salir.
///
/// Cómo se mueve: se sigue moviendo directamente el Transform del jugador (no
/// CharacterController.Move()) tanto al subir como al bajar -- ya no compite con nada porque,
/// mientras se vuela, el CharacterController que podría "pelearle" está apagado.
///
/// Cómo se saca la colisión durante el vuelo: además de apagar el CharacterController, se
/// mantiene el Physics.IgnoreLayerCollision de la capa del jugador contra las 32 capas (por si el
/// rig tiene otros colliders aparte del CharacterController) -- no afecta al agarre de XRI (usa
/// raycasts/triggers, no colisión física), así que se puede seguir agarrando muebles al volar.
/// </summary>
public class PlayerFlightController : MonoBehaviour
{
    private static PlayerFlightController instance;

    private InputAction xButtonAction;
    private InputAction leftTriggerAction;
    private InputAction rightTriggerAction;

    private Transform jugadorTransform;
    private CharacterController characterController;
    private int capaJugador = -1;

    private bool volando = false;

    private readonly List<MonoBehaviour> componentesGravedadDesactivados = new List<MonoBehaviour>();

    private const float VELOCIDAD_VUELO_MS = 2.2f;
    private const float MARGEN_ATERRIZAJE_M = 0.05f;

    // Mismo motivo que en FurnitureColorButtonController/PresetLoaderController/etc.: con dos
    // interaction profiles de OpenXR habilitados a la vez, un solo apretón físico puede disparar
    // "performed" más de una vez -- acá sería especialmente molesto (encendería y apagaría el
    // vuelo en el mismo instante, pareciendo que "no hace nada"), así que se debounce.
    private float lastXPressTime = -999f;
    private const float XPressDebounceSeconds = 0.3f;

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("PlayerFlightController");
        instance = go.AddComponent<PlayerFlightController>();
        Object.DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        instance = this;
    }

    private void OnEnable()
    {
        xButtonAction = new InputAction(name: "PlayerFlyToggle", type: InputActionType.Button);
        xButtonAction.AddBinding("<XRController>{LeftHand}/primaryButton");
        xButtonAction.AddBinding("<MetaQuestTouchPlusController>{LeftHand}/primaryButton");
        xButtonAction.AddBinding("<OculusTouchController>{LeftHand}/primaryButton");
        xButtonAction.performed += OnXButtonPressed;
        xButtonAction.Enable();

        leftTriggerAction = new InputAction(name: "PlayerFlyAscend", type: InputActionType.Value);
        leftTriggerAction.AddBinding("<XRController>{LeftHand}/trigger");
        leftTriggerAction.AddBinding("<MetaQuestTouchPlusController>{LeftHand}/trigger");
        leftTriggerAction.AddBinding("<OculusTouchController>{LeftHand}/trigger");
        leftTriggerAction.Enable();

        rightTriggerAction = new InputAction(name: "PlayerFlyDescend", type: InputActionType.Value);
        rightTriggerAction.AddBinding("<XRController>{RightHand}/trigger");
        rightTriggerAction.AddBinding("<MetaQuestTouchPlusController>{RightHand}/trigger");
        rightTriggerAction.AddBinding("<OculusTouchController>{RightHand}/trigger");
        rightTriggerAction.Enable();
    }

    private void OnDisable()
    {
        if (xButtonAction != null)
        {
            xButtonAction.performed -= OnXButtonPressed;
            xButtonAction.Disable();
            xButtonAction.Dispose();
            xButtonAction = null;
        }
        if (leftTriggerAction != null)
        {
            leftTriggerAction.Disable();
            leftTriggerAction.Dispose();
            leftTriggerAction = null;
        }
        if (rightTriggerAction != null)
        {
            rightTriggerAction.Disable();
            rightTriggerAction.Dispose();
            rightTriggerAction = null;
        }

        // Por si se desactiva este componente mientras se estaba volando (no debería pasar en el
        // flujo normal), evita dejar el CharacterController/la colisión/la gravedad apagados para
        // siempre.
        if (volando) RestaurarColisionYGravedad();
    }

    private void EnsureRig()
    {
        if (jugadorTransform != null) return;

        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        if (xrOrigin == null) return;

        characterController = xrOrigin.GetComponentInChildren<CharacterController>();
        jugadorTransform = characterController != null ? characterController.transform : xrOrigin.transform;
        capaJugador = jugadorTransform.gameObject.layer;
    }

    /// <summary>
    /// Punto 4 del comentario de clase: mientras se sostiene un mueble o un muro divisorio, este
    /// componente no hace absolutamente nada con X ni con los gatillos -- esos botones son 100%
    /// de FurnitureColorButtonController/WallDividerToolButtonController en ese momento.
    /// </summary>
    private static bool HayAlgoSostenido()
    {
        return FurnitureColorButtonController.HayMuebleSostenido || WallDividerToolButtonController.HayMuroSostenido;
    }

    /// <summary>
    /// Séptima ronda (2026-09-19), a pedido de Alan: "quiero que sepa cuando estoy apuntando a un
    /// botón para dejar de elevarme o bajar, si no que haga click en el botón si presiono ese
    /// gatillo" -- hasta ahora, si tenías algún panel abierto (el menú de monoambientes, el
    /// catálogo, el panel CRUD, pintar muros, un diálogo Sí/No, el modal de controles) y apretabas
    /// el gatillo para volar, el click SÍ le llegaba al botón que estabas apuntando (Input System
    /// no le "roba" el evento a nadie, cada sistema lee el mismo botón físico por separado), pero
    /// AL MISMO TIEMPO también subías o bajabas -- molesto si estás tratando de clickear algo con
    /// precisión mientras volás.
    ///
    /// En vez de tratar de detectar "¿el rayo de esta mano está apuntando exactamente a un botón
    /// en este instante?" (dependería de la API interna del NearFarInteractor de XRI, que no se
    /// pudo verificar sin acceso al Editor/compilador), se usa una señal más simple y confiable:
    /// mientras CUALQUIERA de los paneles clickeables del proyecto esté abierto, los gatillos de
    /// vuelo quedan en pausa (igual que ya pasa cuando tenés un mueble en la mano) -- en la
    /// práctica, un botón para clickear solo existe cuando alguno de estos paneles está en
    /// pantalla, así que el resultado es el mismo sin depender de esa API interna. La única
    /// diferencia con "apuntando exactamente al botón" es que mientras cualquiera de estos paneles
    /// esté abierto, tampoco vas a subir/bajar aunque NO estés mirando ese panel en ese instante --
    /// un compromiso razonable, ya que en la práctica abrís un panel para usarlo, no para seguir
    /// volando con él abierto de fondo. Si en la práctica esto se siente demasiado restrictivo,
    /// avisame.
    /// </summary>
    private static bool HayPanelAbierto()
    {
        return PresetLoaderController.HayPanelAbierto
            || FurnitureCatalogController.HayPanelAbierto
            || CrudPanelController.HayPanelAbierto
            || WallPaintPanelController.HayPanelAbierto
            || ConfirmDialogUI.HayPanelAbierto
            || ControlsHelpPanelController.HayPanelAbierto
            || ResizeHandlesController.HayModoActivo
            || UpdateModesController.HayModoEspecialActivo;
    }

    private void OnXButtonPressed(InputAction.CallbackContext context)
    {
        if (HayAlgoSostenido()) return;
        if (Time.unscaledTime - lastXPressTime < XPressDebounceSeconds) return;
        lastXPressTime = Time.unscaledTime;

        EnsureRig();
        if (jugadorTransform == null) return;

        if (volando) SalirModoVuelo();
        else EntrarModoVuelo();
    }

    // -----------------------------------------------------------------
    // Modo vuelo
    // -----------------------------------------------------------------

    private void EntrarModoVuelo()
    {
        volando = true;
        DesactivarColisionYGravedad();
        ToastNotificationUI.Show(
            "🕊️ Vuelo libre activado -- gatillo izquierdo para subir, gatillo derecho para bajar. Volvé a apretar X para desactivarlo.",
            4f);
    }

    private void SalirModoVuelo()
    {
        volando = false;
        RestaurarColisionYGravedad();
        ToastNotificationUI.Show("🕊️ Vuelo libre desactivado.", 2.5f);
    }

    private void DesactivarColisionYGravedad()
    {
        if (capaJugador >= 0)
        {
            for (int otraCapa = 0; otraCapa < 32; otraCapa++)
                Physics.IgnoreLayerCollision(capaJugador, otraCapa, true);
        }

        if (characterController != null)
            characterController.enabled = false;

        // Ver el "BUG ENCONTRADO Y ARREGLADO" en el comentario de clase -- esto es lo que
        // realmente permite subir sin que la gravedad de fábrica del XR Origin lo cancele.
        componentesGravedadDesactivados.Clear();
        MonoBehaviour[] todosLosComponentes = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (MonoBehaviour componente in todosLosComponentes)
        {
            if (componente == null || componente == this || !componente.enabled) continue;
            if (componente.GetType().Name.IndexOf("Gravity", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                componente.enabled = false;
                componentesGravedadDesactivados.Add(componente);
            }
        }
    }

    private void RestaurarColisionYGravedad()
    {
        if (capaJugador >= 0)
        {
            for (int otraCapa = 0; otraCapa < 32; otraCapa++)
                Physics.IgnoreLayerCollision(capaJugador, otraCapa, false);
        }

        if (characterController != null)
            characterController.enabled = true;

        foreach (MonoBehaviour componente in componentesGravedadDesactivados)
            if (componente != null) componente.enabled = true;
        componentesGravedadDesactivados.Clear();
    }

    private void ActualizarVuelo()
    {
        if (!volando || jugadorTransform == null) return;
        if (HayAlgoSostenido()) return; // manos ocupadas -- ver comentario de clase, punto 4
        if (HayPanelAbierto()) return; // panel abierto -- séptima ronda, ver el comentario en HayPanelAbierto()

        float valorGatilloIzq = leftTriggerAction != null ? leftTriggerAction.ReadValue<float>() : 0f;
        float valorGatilloDer = rightTriggerAction != null ? rightTriggerAction.ReadValue<float>() : 0f;

        if (valorGatilloIzq > 0.1f)
        {
            float subir = VELOCIDAD_VUELO_MS * Time.deltaTime * valorGatilloIzq;
            jugadorTransform.position += new Vector3(0f, subir, 0f);
        }
        else if (valorGatilloDer > 0.1f)
        {
            float bajarDeseado = VELOCIDAD_VUELO_MS * Time.deltaTime * valorGatilloDer;

            // Punto 3 del comentario de clase: no atravesar el piso/muebles de abajo al bajar --
            // se frena el descenso apenas el raycast hacia abajo encuentra algo dentro de esa
            // distancia, en vez de seguir hundiéndose.
            int mascara = capaJugador >= 0 ? ~(1 << capaJugador) : ~0;
            if (Physics.Raycast(jugadorTransform.position, Vector3.down, out RaycastHit hit,
                    bajarDeseado + MARGEN_ATERRIZAJE_M, mascara, QueryTriggerInteraction.Ignore))
            {
                bajarDeseado = Mathf.Clamp(hit.distance - MARGEN_ATERRIZAJE_M, 0f, bajarDeseado);
            }

            if (bajarDeseado > 0f)
                jugadorTransform.position += new Vector3(0f, -bajarDeseado, 0f);
        }
    }

    private void Update()
    {
        EnsureRig();
        if (jugadorTransform == null) return;

        ActualizarVuelo();
    }
}
