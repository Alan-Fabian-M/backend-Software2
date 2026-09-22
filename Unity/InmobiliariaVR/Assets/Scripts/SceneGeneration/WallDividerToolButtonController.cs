using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Botón X del control izquierdo (mismo binding que FurnitureColorButtonController:
/// "&lt;XRController&gt;{LeftHand}/primaryButton"): mientras se sostiene un "Muro divisorio"
/// (Sección A del plan de edición avanzada) con la mano derecha, abre su panel de
/// pintado/altura/longitud (WallPaintPanelController) en vez de ciclar un color directo -- un
/// muro divisorio necesita elegir alcance (todos/solo este) y longitud, no solo color, así que
/// no puede reusar el ciclo simple de MaterialChangerVR que usan los muebles.
///
/// Es un componente totalmente aparte de FurnitureColorButtonController (no lo modifica): ambos
/// escuchan el mismo botón físico sin pisarse, porque cada uno solo actúa si su propia
/// referencia estática (heldWall / heldColorChanger) está seteada, y nunca se setean las dos a
/// la vez para el mismo objeto sostenido.
/// </summary>
public class WallDividerToolButtonController : MonoBehaviour
{
    private static MuroEditable heldWall;
    private static WallDividerToolButtonController instance;

    private InputAction openPanelAction;

    private float lastPressTime = -999f;
    private const float PressDebounceSeconds = 0.3f;

    public static void EnsureExists()
    {
        if (instance != null) return;

        instance = Object.FindAnyObjectByType<WallDividerToolButtonController>();
        if (instance != null) return;

        GameObject managerGO = new GameObject("WallDividerToolButtonController");
        instance = managerGO.AddComponent<WallDividerToolButtonController>();
        Object.DontDestroyOnLoad(managerGO);
    }

    /// <summary>
    /// Quinta ronda (2026-09-19): consultado por PlayerFlightController -- mismo motivo que
    /// FurnitureColorButtonController.HayMuebleSostenido, para un "Muro divisorio" agarrado.
    /// </summary>
    public static bool HayMuroSostenido => heldWall != null;

    /// <summary>Llamar desde el selectEntered del XRGrabInteractable de un muro divisorio.</summary>
    public static void SetHeldWall(MuroEditable muro)
    {
        heldWall = muro;
    }

    /// <summary>
    /// Llamar desde el selectExited. Solo limpia la referencia si sigue siendo la misma (por si
    /// ya se soltó y se agarró otro muro/mueble distinto antes de que llegue este evento).
    /// </summary>
    public static void ClearHeldWall(MuroEditable muro)
    {
        if (heldWall == muro)
            heldWall = null;
    }

    private void OnEnable()
    {
        openPanelAction = new InputAction(
            name: "OpenHeldWallDividerPanel",
            type: InputActionType.Button,
            binding: "<XRController>{LeftHand}/primaryButton");
        openPanelAction.performed += OnButtonPressed;
        openPanelAction.Enable();
    }

    private void OnDisable()
    {
        if (openPanelAction == null) return;
        openPanelAction.performed -= OnButtonPressed;
        openPanelAction.Disable();
        openPanelAction.Dispose();
        openPanelAction = null;
    }

    private void OnButtonPressed(InputAction.CallbackContext context)
    {
        if (Time.unscaledTime - lastPressTime < PressDebounceSeconds) return;
        lastPressTime = Time.unscaledTime;

        if (heldWall != null)
            WallPaintPanelController.ShowFor(heldWall);
    }
}
