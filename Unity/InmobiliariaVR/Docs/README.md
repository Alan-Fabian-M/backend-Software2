# InmobiliariaVR — Documentación técnica

Esta carpeta documenta **cómo está armada la aplicación por dentro**: la arquitectura de escenas y scripts, y el paso a paso de cada flujo importante dentro de Unity. Para la descripción de producto, el stack y cómo abrir/compilar el proyecto, ver el [README de la raíz](../README.md). Para la propuesta original, el estado de avance y la investigación de impresión 3D, ver los demás documentos de esta misma carpeta (`Propuesta_Sistema_Inmobiliario_VR.md`, `Estado_Avance_Examen_ISW2.md`, `investigacion_VR_impresion3D.md`, etc.).

## Qué es la aplicación

InmobiliariaVR es el **prototipo VR** de un sistema inmobiliario de preventa: el cliente entra a una sala modelada (`Sala_MVP`), la recorre con un visor VR, y desde ahí puede:

- Mover y rotar los muebles de la sala.
- Cambiar el material/color de muebles y superficies (paredes, piso, techo), a mano o pintando con un rayo.
- Pedirle cambios a un asistente de IA local por texto o por voz ("poné la pared gris").
- Exportar la sala (con la disposición y colores elegidos) a un archivo `.stl` para imprimir una maqueta 3D.

No hay backend ni multiplayer: todo corre local en el Editor o en el visor, y la IA habla con un servidor Ollama en `localhost`.

## Arquitectura general

### El estado de la app: `StateManager` + `AppState`

Casi todos los scripts de interacción dependen de un único estado global, manejado por [`StateManager`](../Assets/Scripts/StateManager.cs) (singleton `DontDestroyOnLoad`) sobre el enum [`AppState`](../Assets/Scripts/AppState.cs):

| Estado | Significado | Quién lo activa |
|---|---|---|
| `Recorrido` | Modo por defecto: caminar/teletransportarse por la sala | Al cerrar cualquier menú |
| `Personalizacion` | Mover/rotar muebles y elegir materiales desde el menú | `MenuControllerVR`, `SideMenuVR` |
| `Pintura` | Pintar superficies con el rayo del control | `SideMenuVR` / `MenuControllerVR.SetMode(2)` |
| `Menu` | Reservado para pantallas de menú que no son de personalización | — (declarado, no usado activamente en `Sala_MVP`) |

Cada script de interacción (`FurnitureManipulatorVR`, `MaterialChangerVR`, `RayPaintVR`) empieza su acción con un chequeo de `StateManager.Instance.CurrentState`, así una misma acción del jugador (apretar el grip sobre un mueble, por ejemplo) no hace nada indebido si el estado actual no corresponde. Esto evita, por ejemplo, mover un mueble por accidente mientras se está pintando una pared.

### Los scripts y su rol

| Script | Responsabilidad |
|---|---|
| [`StateManager`](../Assets/Scripts/StateManager.cs) / [`AppState`](../Assets/Scripts/AppState.cs) | Estado global de la app y notificación de cambios (evento `StateChanged`) |
| [`ToggleMenuVR`](../Assets/Scripts/ToggleMenuVR.cs) | Abre/cierra el menú principal frente al jugador |
| [`MenuControllerVR`](../Assets/Scripts/MenuControllerVR.cs) | Muestra/oculta el canvas de personalización y cambia de `AppState` según el botón elegido |
| [`SideMenuVR`](../Assets/Scripts/SideMenuVR.cs) | Pestañas del menú (Recorrido/Personalizar/Pintar), cada una activa un panel y un `AppState` |
| [`FurnitureSelectable`](../Assets/Scripts/FurnitureSelectable.cs) | Detecta "mantener presionado" sobre un mueble para abrir su menú de mover/rotar |
| [`FurnitureManipulatorVR`](../Assets/Scripts/FurnitureManipulatorVR.cs) | Menú compartido de mover/rotar el mueble seleccionado |
| [`MaterialChangerVR`](../Assets/Scripts/MaterialChangerVR.cs) | Cicla o fija el material de un objeto (mueble o superficie) desde una paleta |
| [`RayPaintVR`](../Assets/Scripts/RayPaintVR.cs) | "Pinta" superficies (paredes/piso/techo) apuntando con un rayo |
| [`OllamaAIController`](../Assets/Scripts/OllamaAIController.cs) | Envía el pedido en lenguaje natural a Ollama y aplica el color que responda |
| [`VoiceCommandController`](../Assets/Scripts/VoiceCommandController.cs) | Dictado de voz (solo Windows) que alimenta a `OllamaAIController` |
| [`SaveDataSerializer`](../Assets/Scripts/SaveSystem/SaveDataSerializer.cs) / [`DesignLayoutData`](../Assets/Scripts/SaveSystem/DesignLayoutData.cs) | Guardar/cargar el diseño personalizado como JSON local |
| [`StlExportController`](../Assets/Scripts/StlExportController.cs) / [`StlExporter`](../Assets/Scripts/StlExporter.cs) | Exportar la sala completa a un archivo `.stl` para impresión 3D |
| [`ArFurniturePlacer`](../Assets/Scripts/ArFurniturePlacer.cs) | App móvil: coloca muebles en AR tocando el piso detectado por la cámara |
| [`MobileVoiceCommandController`](../Assets/Scripts/MobileVoiceCommandController.cs) | App móvil: permiso y captura de micrófono para el asistente de IA |

### Escenas

- `Assets/Scenes/Sala_MVP.unity` — escena principal del MVP: la sala modelada, los muebles, el rig de XR Interaction Toolkit y los menús descriptos arriba.
- `Assets/Scenes/AppMovil_AR.unity` — app móvil complementaria (requisito del examen): AR Foundation + ARCore, coloca muebles con la cámara del celular y reusa `OllamaAIController`/`MaterialChangerVR` para cambiarles el color. Ver [flujo 07](Flujos/07_App_Movil_AR.md).
- `Assets/Scenes/BasicScene.unity` / `SampleScene.unity` — escenas de prueba/plantilla del template de XR, no forman parte del flujo del producto.

### Patrón de interacción común

La mayoría de las acciones del jugador llegan a estos scripts desde **eventos de Unity/XRI conectados en el Inspector** (no desde código): botones UI (`OnClick`) o eventos del `XRSimpleInteractable`/`XR Ray Interactor` (`Select Entered`, `Select Exited`, `Activate`). Esto significa que para entender el "cableado" real de un flujo (qué botón dispara qué método) hay que mirar tanto el script como los eventos configurados en la escena `Sala_MVP`, no solo el código.

## Flujos documentados

Ver [`Flujos/`](Flujos/) para el paso a paso de cada flujo principal:

1. [Recorrido y cambio de modo](Flujos/01_Recorrido_y_Menu.md) — cómo se abre el menú y se cambia entre Recorrido/Personalización/Pintura.
2. [Mover y rotar un mueble](Flujos/02_Mover_Rotar_Mueble.md) — desde que el jugador agarra un mueble hasta que lo suelta en su nueva posición.
3. [Cambiar material de mueble o superficie](Flujos/03_Cambio_Material.md) — ciclo de materiales a mano y pintura de paredes con el rayo.
4. [Asistente de IA (texto y voz)](Flujos/04_Asistente_IA.md) — desde el pedido del usuario hasta que Ollama devuelve el cambio y se aplica en la escena.
5. [Guardar y cargar un diseño](Flujos/05_Guardado_Diseno.md) — serialización del layout a JSON local.
6. [Exportar la sala a STL](Flujos/06_Exportacion_STL.md) — de la escena de Unity al archivo `.stl` listo para imprimir.
7. [App móvil en AR](Flujos/07_App_Movil_AR.md) — detectar el piso con la cámara del celular, colocar un mueble y cambiarle el color por IA.
