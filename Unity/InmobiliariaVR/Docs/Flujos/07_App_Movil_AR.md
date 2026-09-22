# Flujo: App móvil — colocar un mueble en AR y cambiarle el color

Cubre la escena `Assets/Scenes/AppMovil_AR.unity`, la app móvil complementaria que usa los sensores del celular (cámara y micrófono) — requisito obligatorio del examen, ver [Rubrica_Examen_Preguntas.md](../Rubrica_Examen_Preguntas.md). A diferencia de los demás flujos, corre en un celular Android (no en el visor VR), pero reusa `OllamaAIController` y `MaterialChangerVR` tal cual.

## Precondición

Al cargar la escena, [`ArFurniturePlacer.Awake()`](../../Assets/Scripts/ArFurniturePlacer.cs) fuerza `StateManager.Instance.ChangeState(AppState.Personalizacion)`, porque `MaterialChangerVR.SetMaterialByIndex` (igual que en la versión VR) solo actúa en ese estado y esta app no tiene menú de modos.

## Paso a paso

1. **La app arranca** y el `AR Session`/`XR Origin` de la escena activa la cámara del celular (permiso de cámara lo gestiona AR Foundation automáticamente) y empieza a buscar superficies horizontales (el piso) vía `ARPlaneManager`.
2. **Al detectar una superficie**, se instancia el prefab `Assets/Prefabs/AR_DefaultPlane.prefab` (un plano celeste semitransparente) sobre ella, para que el usuario vea dónde puede colocar el mueble.
3. **El jugador elige qué mueble colocar** con los botones "Sofa"/"Mesa"/"TV" del panel → [`ArFurniturePlacer.SeleccionarMueble(indice)`](../../Assets/Scripts/ArFurniturePlacer.cs) guarda el índice elegido (0=Sofa por defecto).
4. **El jugador toca la pantalla** sobre el plano detectado. En `Update()`, `ArFurniturePlacer` lee el toque (o el click del mouse, como respaldo para probar con XR Simulation/mouse) y lanza un `ARRaycastManager.Raycast` contra los planos detectados.
5. Si el toque cae sobre un plano, [`ColocarEn(pose)`](../../Assets/Scripts/ArFurniturePlacer.cs):
   - Destruye el mueble colocado anteriormente (si había uno).
   - Instancia el prefab elegido (mismos prefabs de `Assets/HomeStuff/Prefabs/` que usa `Sala_MVP`: `Sofa`, `SmallTable`, `TV`) en la posición/rotación del punto tocado.
   - Si el mueble es el Sofá (índice 0), le agrega un `MaterialChangerVR` con la misma paleta de colores que la versión VR (`Sofa_Cuero`/`Sofa_Tela_Azul`/`Sofa_Tela_Verde`) y lo asigna como el campo `sofa` del `OllamaAIController` de la escena.
6. **El jugador pide un cambio de color** escribiendo en el campo de texto del panel (ej. *"poné el sillón azul"*) y tocando "Enviar" → [`OllamaAIController.EnviarComandoDesdeInput()`](../../Assets/Scripts/OllamaAIController.cs), el mismo método que usa la versión de escritorio. De ahí en adelante es exactamente el [flujo del asistente de IA](04_Asistente_IA.md) ya documentado: Ollama corriendo en la red local interpreta el pedido y `AplicarComando` llama a `SetMaterialByIndex` sobre el Sofá recién colocado.
7. **Botón de micrófono** ("Hablar"): [`MobileVoiceCommandController.ToggleEscucha()`](../../Assets/Scripts/MobileVoiceCommandController.cs) pide el permiso de micrófono en runtime y arranca una grabación real con `Microphone.Start`. **Todavía no transcribe ese audio a texto** (Unity no trae un motor de speech-to-text para Android) — queda documentado como pendiente, y mientras tanto el pedido se escribe a mano en el paso 6.

## Diferencias clave con la versión VR

- No hay menú de pestañas ni cambio entre Recorrido/Personalización/Pintura — la app arranca directo en modo "colocar y personalizar".
- Solo el Sofá tiene cambio de color (igual que en `Sala_MVP`, donde Mesa/TV tampoco lo tienen), así que el vocabulario de `OllamaAIController` (`pared`/`sofa`) no necesitó extenderse.
- `OllamaAIController.paredes` queda como array vacío en esta escena (no hay paredes que pintar en AR).

## Qué falta

- Probar la detección de planos y la colocación en un celular Android real (con el proveedor ARCore activado en `Project Settings → XR Plug-in Management → Android`) — no se puede validar dentro del Editor sin un dispositivo o el paquete de XR Simulation.
- Conectar la transcripción de voz a texto (plugin nativo de Android o un STT on-device como Vosk) para que el botón "Hablar" alimente a `OllamaAIController.EnviarComando()` igual que ya lo hace `VoiceCommandController` en Windows.

Ver sección 14 de [`Progreso_MVP_Unity.md`](../Progreso_MVP_Unity.md) para el detalle completo de la implementación y las decisiones técnicas.
