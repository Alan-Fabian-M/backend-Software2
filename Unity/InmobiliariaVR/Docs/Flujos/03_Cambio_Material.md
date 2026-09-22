# Flujo: Cambiar material de un mueble o de una superficie

Hay dos variantes de este flujo, con la misma lógica de "índice dentro de una paleta de materiales" pero disparadas distinto: cambiar el material de un objeto puntual (mueble) tocándolo, o "pintar" una superficie apuntando con un rayo.

## Variante A — Ciclar el material de un mueble (ej. el Sofá)

Precondición: `AppState.Personalizacion`.

1. El jugador apunta al Sofá y hace un **toque corto** con el grip (Select) — igual que el primer paso del [flujo de mover/rotar](02_Mover_Rotar_Mueble.md), pero soltando antes de los 0.35 s.
2. El evento `Select Entered` del `XRSimpleInteractable` del Sofá tiene, además del listener de `FurnitureSelectable`, otro listener apuntando a [`MaterialChangerVR.ChangeToNextMaterial()`](../../Assets/Scripts/MaterialChangerVR.cs) — este listener está en la escena desde antes y no se modificó al agregar el manipulador de mover/rotar.
3. `ChangeToNextMaterial()` verifica el estado, y si es válido, avanza `currentMaterialIndex` (volviendo a 0 al llegar al final del array `materials`) y asigna `targetRenderer.material` al nuevo material.
4. `targetRenderer` se resuelve una sola vez en `Start()` con `GetComponentInChildren<Renderer>()`, porque el mesh real suele estar en un hijo del prefab importado, no en el mismo `GameObject` que tiene el script.

También existe [`SetMaterialByIndex(index)`](../../Assets/Scripts/MaterialChangerVR.cs) para fijar un material puntual desde un botón de menú con un color específico (en vez de ciclar) — es el método que usa el asistente de IA (ver [flujo de IA](04_Asistente_IA.md)).

## Variante B — Pintar una superficie con el rayo (paredes/piso/techo)

Precondición: `AppState.Pintura` (se entra a este modo desde la pestaña "Pintar" del menú — ver [flujo de recorrido y menú](01_Recorrido_y_Menu.md)).

1. Al entrar a `AppState.Pintura`, [`RayPaintVR`](../../Assets/Scripts/RayPaintVR.cs) (suscripto a `StateManager.StateChanged`) marca `isEnabled = true`.
2. En cada `Update()` mientras `isEnabled`, `RayPaintVR` dibuja un rayo visual (`LineRenderer`) desde `rayOrigin` (el Ray Interactor de la mano) hasta donde impacte contra algo, o hasta `maxDistance` si no impacta nada.
3. **El jugador elige un color** en el menú de pintura, disparando [`SetMaterialByIndex(index)`](../../Assets/Scripts/RayPaintVR.cs), que guarda ese material en `materialToApply` (mismo esquema de índices que `MaterialChangerVR`, para reusar los mismos botones de menú).
4. **El jugador presiona Select** apuntando a una pared/piso/techo → el evento `Select Entered` del Ray Interactor llama a [`TryPaint()`](../../Assets/Scripts/RayPaintVR.cs).
5. `TryPaint()` lanza un `Physics.Raycast` desde `rayOrigin`. Si golpea un collider cuyo tag esté en `paintableTags` (`Wall`, `Floor`, `Ceiling` por defecto), le busca el `Renderer` y le asigna `materialToApply` directamente (no cicla, aplica el color elegido de una vez).
6. Al salir de `AppState.Pintura` (cambiar de pestaña o cerrar el menú), `OnStateChanged` pone `isEnabled = false` y apaga el `LineRenderer`, así el rayo no queda visible en modo Recorrido o Personalización.

## Por qué estas dos variantes conviven

`MaterialChangerVR` y `RayPaintVR` comparten la misma idea de "paleta de materiales por índice" a propósito: así el asistente de IA (que solo conoce `SetMaterialByIndex`) y los botones de menú de color pueden aplicar el mismo cambio sin importar si el objetivo es un mueble tocable o una superficie que se pinta a distancia.
