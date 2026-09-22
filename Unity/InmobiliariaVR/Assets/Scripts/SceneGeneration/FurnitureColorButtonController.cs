using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Botón X del control izquierdo (Touch/Touch Plus vía OpenXR, layout "primaryButton" del
/// XRController de mano izquierda): cambia el color del mueble que se está agarrando en ese
/// momento con la mano derecha, si es colorable.
///
/// Antes, el color cambiaba automáticamente al agarrar el mueble (mismo evento "Select" que ya
/// usa XRGrabInteractable para levantarlo) -- resultaba molesto, porque cada vez que agarrabas
/// el sofá para moverlo, de paso también le cambiabas el color sin querer. Ahora el agarre solo
/// mueve el mueble, y el color se cambia a pedido con este botón dedicado en la otra mano
/// (un gesto de dos manos: una sostiene, la otra decide el color -- normal en apps de VR).
///
/// Un único componente en la escena (creado bajo demanda la primera vez que hace falta, mismo
/// patrón "anti-duplicados" que SceneGenerator/PresetLoaderController) guarda una referencia
/// estática al MaterialChangerVR del mueble sostenido actualmente; SceneGenerator.HacerInteractivo
/// la actualiza al agarrar/soltar cada mueble movible y colorable.
/// </summary>
public class FurnitureColorButtonController : MonoBehaviour
{
    private static MaterialChangerVR heldColorChanger;
    private static FurnitureColorButtonController instance;

    private InputAction changeColorAction;

    // Mismo debounce que PresetLoaderController/FurnitureCatalogController: con dos interaction
    // profiles de OpenXR habilitados a la vez (ver OpenXRPackageSettings.asset), un solo apretón
    // podría disparar "performed" más de una vez. Acá pasaría casi desapercibido (saltearía un
    // color de la paleta en vez de mostrarse como un parpadeo), pero se deja por las dudas.
    private float lastPressTime = -999f;
    private const float PressDebounceSeconds = 0.3f;

    /// <summary>
    /// Crea el manager la primera vez que hace falta (idempotente). Se llama desde
    /// SceneGenerator.HacerInteractivo cuando conecta el primer mueble movible+colorable.
    /// </summary>
    public static void EnsureExists()
    {
        if (instance != null) return;

        instance = Object.FindAnyObjectByType<FurnitureColorButtonController>();
        if (instance != null) return;

        GameObject managerGO = new GameObject("FurnitureColorButtonController");
        instance = managerGO.AddComponent<FurnitureColorButtonController>();
        Object.DontDestroyOnLoad(managerGO);
    }

    /// <summary>
    /// Quinta ronda (2026-09-19, "si presiono X sosteniendo el mueble solo se cambiará de color el
    /// mueble, no volaré"): consultado por PlayerFlightController para saber si el botón X de la
    /// mano izquierda tiene que dedicarse exclusivamente a cambiar color en este momento (mueble
    /// agarrado con la otra mano) en vez de a saltar/volar.
    /// </summary>
    public static bool HayMuebleSostenido => heldColorChanger != null;

    /// <summary>Llamar desde el selectEntered del XRGrabInteractable de un mueble colorable.</summary>
    public static void SetHeldColorChanger(MaterialChangerVR changer)
    {
        heldColorChanger = changer;
    }

    /// <summary>
    /// Llamar desde el selectExited. Solo limpia la referencia si sigue siendo la misma (por si
    /// ya se soltó y se agarró otro mueble distinto antes de que llegue este evento).
    /// </summary>
    public static void ClearHeldColorChanger(MaterialChangerVR changer)
    {
        if (heldColorChanger == changer)
            heldColorChanger = null;
    }

    private void OnEnable()
    {
        changeColorAction = new InputAction(
            name: "ChangeHeldFurnitureColor",
            type: InputActionType.Button,
            binding: "<XRController>{LeftHand}/primaryButton");
        changeColorAction.performed += OnChangeColorButtonPressed;
        changeColorAction.Enable();
    }

    private void OnDisable()
    {
        if (changeColorAction == null) return;
        changeColorAction.performed -= OnChangeColorButtonPressed;
        changeColorAction.Disable();
        changeColorAction.Dispose();
        changeColorAction = null;
    }

    private void OnChangeColorButtonPressed(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime - lastPressTime < PressDebounceSeconds) return;
        lastPressTime = Time.unscaledTime;

        if (heldColorChanger == null) return;

        // BUG encontrado (2026-09-19, cuarta ronda -- reportado por Alan: "el cambio de color de
        // los muebles no se aplican"): MaterialChangerVR.ChangeToNextMaterial() es un componente
        // heredado del proyecto AR/mobile viejo (MaterialChangerVR.cs vive en Assets/Scripts, no
        // en SceneGeneration) y solo cambia de color si StateManager.Instance.CurrentState ==
        // AppState.Personalizacion -- un estado global que SceneGenerator ya fuerza a
        // Personalizacion apenas termina de generar la sala (ver el comentario en
        // GenerateSceneCoroutine), pero que no tiene NINGÚN otro punto en todo el proyecto que lo
        // reafirme después -- si por lo que sea ese estado global no queda seteado (u otra parte
        // del proyecto legado lo cambia), el botón de color queda respondiendo (sin error, sin
        // aviso) pero sin aplicar nada, exactamente el síntoma reportado. En vez de perseguir cada
        // lugar posible que podría dejar mal ese estado, se fuerza acá mismo, justo antes de
        // cambiar el color -- es precisamente el momento en el que SÍ queremos estar en modo
        // Personalización, así que no hay downside en asegurarlo de nuevo cada vez.
        StateManager.Instance.ChangeState(AppState.Personalizacion);
        heldColorChanger.ChangeToNextMaterial();
    }
}
