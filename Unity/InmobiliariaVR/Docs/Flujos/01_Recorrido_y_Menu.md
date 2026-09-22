# Flujo: Recorrido y cambio de modo (menú principal)

Este es el flujo "raíz": todos los demás flujos dependen de que la app esté en el `AppState` correcto, y ese estado se cambia siempre a través de este menú.

## Paso a paso

1. **Estado inicial.** Al arrancar la escena `Sala_MVP`, `StateManager.CurrentState` empieza en `AppState.Recorrido`. El jugador puede caminar/teletransportarse libremente (locomoción provista por XR Interaction Toolkit, sin script propio).
2. **El jugador presiona el botón de menú** (`Boton_Menu` en la escena) → dispara el evento XRI `Select Entered`, que la escena tiene conectado al método [`ToggleMenuVR.Toggle()`](../../Assets/Scripts/ToggleMenuVR.cs).
3. `Toggle()` activa el `GameObject` del menú (`menuUI`) y lo reposiciona siempre **frente a la cámara del jugador** (`PosicionarFrenteAlJugador`), a 0.8 m de distancia y con una leve corrección de altura, para que el menú nunca quede fuera de vista sin importar dónde esté parado el jugador.
4. Con el menú visible, el jugador elige una pestaña en [`SideMenuVR`](../../Assets/Scripts/SideMenuVR.cs) (por ejemplo "Personalizar" o "Pintar"), llamando a `SeleccionarPestana(indice)` desde el `OnClick` del botón correspondiente. Esto:
   - Activa el panel de contenido asociado a esa pestaña (`contents[indice]`) y oculta el resto.
   - Cambia `StateManager.CurrentState` al `AppState` configurado para esa pestaña (`appStates[indice]`) — por ejemplo `Personalizacion` o `Pintura`.
5. Alternativamente, algunos botones llaman directamente a [`MenuControllerVR.SetMode(modeValue)`](../../Assets/Scripts/MenuControllerVR.cs), que hace el mismo cambio de estado sin pasar por pestañas (0=Recorrido, 1=Personalización, 2=Pintura).
6. **El jugador vuelve a presionar el botón de menú** → `ToggleMenuVR.Toggle()` esta vez lo *oculta* y llama a `StateManager.Instance.ChangeState(AppState.Recorrido)`, devolviendo la app al modo de recorrido libre sin importar en qué pestaña había quedado el menú.

## Quién escucha el cambio de estado

`StateManager` no hace nada por sí mismo más que guardar el estado actual y notificar por el evento `StateChanged`. Los scripts que sí reaccionan:

- [`RayPaintVR`](../../Assets/Scripts/RayPaintVR.cs) se suscribe a `StateChanged` en `OnEnable()` y activa/desactiva su propio rayo de pintura según si el nuevo estado es `Pintura`.
- [`MaterialChangerVR`](../../Assets/Scripts/MaterialChangerVR.cs) y [`FurnitureManipulatorVR`](../../Assets/Scripts/FurnitureManipulatorVR.cs) **no** se suscriben al evento: en cambio, cada vez que se les pide hacer algo (cambiar material, mover un mueble) preguntan en ese momento si `CurrentState == AppState.Personalizacion`. El efecto es el mismo (bloquear la acción fuera de ese modo) pero sin necesidad de escuchar el evento.

## Notas de diseño

- También existe `StateManager.RevertPreviousState()`, que vuelve al estado anterior en vez de forzar `Recorrido`. La usa `MenuControllerVR.ToggleMenu()` (una alternativa a `ToggleMenuVR` para el mismo propósito, presente en el código pero no necesariamente cableada en la escena actual — ver comentario en el propio script).
- `AppState.Menu` está declarado en el enum pero ningún flujo de `Sala_MVP` lo activa hoy; queda reservado para una futura pantalla de menú que no sea la de personalización.
