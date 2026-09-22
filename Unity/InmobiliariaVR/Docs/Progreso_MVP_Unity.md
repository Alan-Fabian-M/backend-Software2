# Avance del MVP — Sistema Inmobiliario VR (Unity)

> [!INFO] Qué es este documento
> Registro del trabajo hecho en el proyecto Unity `InmobiliariaVR` sobre la base de la [[Propuesta_Sistema_Inmobiliario_VR]]. Cubre la sala de prueba (MVP), el menú de personalización y la integración de IA local con Ollama. Pensado para que cualquiera del equipo pueda entender qué hay hecho, cómo probarlo y qué falta.

---

## 1. Estado antes de este trabajo

El proyecto ya tenía instalados los paquetes base de VR (XR Interaction Toolkit, XR Hands, OpenXR, Composition Layers) y una escena demo (`SampleScene`) heredada del **VR Template** de Unity, con objetos de ejemplo (cubos, torus, blaster) sin relación con el inmueble. También existían dos scripts ya escritos pero no conectados a nada: `MaterialChangerVR.cs` y `MenuControllerVR.cs`.

---

## 2. Escena `Sala_MVP`

> [!TIP] Ubicación
> `Assets/Scenes/Sala_MVP.unity` — agregada a Build Settings junto a `SampleScene`.

Se duplicó el template, se le sacaron los objetos de ejemplo (incluyendo el `Template Environment` que tapaba todo, superpuesto en el origen) y se construyó una sala de bloqueo (primitivas, sin arte final) siguiendo la estrategia de MVP de la propuesta:

```
Sala
 ├── Piso
 ├── Pared_Norte / Pared_Sur / Pared_Este / Pared_Oeste
 ├── Puerta
 ├── Ventana
 ├── Sofa
 ├── Mesa
 ├── TV
 ├── Boton_Menu        (abre/cierra el menú de personalización)
 ├── MenuPersonalizacion (Canvas, oculto por defecto)
 ├── IA_Panel            (Canvas del asistente IA, siempre visible)
 └── IALocalManager / MenuManager (scripts que conectan todo)
```

Se mantiene el XR Rig original del template (manos, teletransporte, `EventSystem` con `XRUIInputModule`), así que la interacción por rayo/mano con la UI funciona sin configuración extra.

> [!WARNING] Son primitivas, no assets finales
> Sofá, mesa y TV son cubos de color. Cuando YOSS/JAZMIN tengan el modelo de Blender (paredes, muebles reales), se importa y se reemplaza cada cubo por su prefab correspondiente — los scripts no necesitan cambiar, solo el `MeshFilter`/`MeshRenderer` de cada objeto.

---

## 3. Interacción directa (tocar para cambiar)

Las 4 paredes y el sofá tienen `XRSimpleInteractable` + `MaterialChangerVR`. Al seleccionarlos con el controlador/mano, ciclan entre 3 materiales:

| Objeto | Materiales (en orden) |
|---|---|
| Paredes (las 4) | `Pared_Blanco` → `Pared_Beige` → `Pared_Gris` |
| Sofa | `Sofa_Cuero` → `Sofa_Tela_Azul` → `Sofa_Tela_Verde` |

Script: [`MaterialChangerVR.cs`](../Assets/Scripts/MaterialChangerVR.cs) — expone `ChangeToNextMaterial()` (ciclar) y `SetMaterialByIndex(int)` (fijar un color puntual, usado por el menú y la IA).

---

## 4. Menú de personalización

> [!TIP] Cómo abrirlo
> En Play Mode, apuntar y seleccionar el cilindro azul (`Boton_Menu`) cerca de la puerta.

Panel `MenuPersonalizacion` (Canvas World Space + `TrackedDeviceGraphicRaycaster`), oculto por defecto, con botones que fijan un color exacto (no ciclan):

- **Pared: Blanco / Beige / Gris** — aplica a las 4 paredes a la vez.
- **Sofa: Cuero / Azul / Verde** — aplica al sofá.
- **Cerrar** — oculta el panel (`MenuControllerVR.ToggleMenu()`).

Script: [`MenuControllerVR.cs`](../Assets/Scripts/MenuControllerVR.cs).

---

## 5. Mover y rotar muebles (Sofa, Mesa, TV)

> [!INFO] Rescatado de un proyecto anterior
> Adaptado de `FurnitureManipulator.cs` de **Room Designer** (TeamFWS, `D:\unity project\room-designer`), un proyecto de Mixed Reality con Meta Quest. Se le saco toda la dependencia de `OVRSpatialAnchor`/MRUK (alli el mueble se ancla a un cuarto real escaneado; en `Sala_MVP` los muebles son fijos, no hace falta anclarlos) y el slider de rotacion se reemplazo por botones (+-15°), mucho mas confiable en VR que arrastrar un slider con un rayo.

Cierra un hueco de la Etapa 5 de la propuesta ("cambiar muebles"): antes solo se podia ciclar el color/material, no reposicionarlos.

### Como funciona (actualizado — un solo boton)

> [!INFO] Cambio de diseño
> La primera versión usaba `Activate` (Trigger) separado de `Select` (Grip) para no pisar el ciclo de color del Sofa. Probando a mano (sección 10) se descubrió que `Activate` en XRI solo se dispara si el objeto ya está `Select`-ado en simultáneo — un gesto de dos botones a la vez, incómodo y casi imposible de probar con teclado/mouse. Se cambió a un diseño de **un solo botón** (`Select` / Grip), diferenciado por tiempo de sostenido.

- **Toque corto** de `Select` (Grip) sobre el Sofa → cicla el color como siempre (`MaterialChangerVR.ChangeToNextMaterial()`, sin cambios). En Mesa/TV un toque corto no hace nada (no tienen `MaterialChangerVR`).
- **Sostener `Select`** ~0.35s sobre Sofa/Mesa/TV → abre el menú de mover/rotar junto al mueble. Se puede soltar el botón después de que el menú aparece, no hace falta seguir sosteniendo.
- Menu con 6 botones — Adelante / Atras / Izquierda / Derecha (mover) y Rotar Izq / Rotar Der (rotar 15° por click) — mas "Cerrar". Solo actua si `StateManager` esta en `AppState.Personalizacion` (mismo criterio que `MaterialChangerVR`).

Scripts: [`FurnitureManipulatorVR.cs`](../Assets/Scripts/FurnitureManipulatorVR.cs) (logica de mover/rotar y estado del menu) y [`FurnitureSelectable.cs`](../Assets/Scripts/FurnitureSelectable.cs) (mide cuanto tiempo se sostiene `Select Entered`/`Select Exited` del `XRSimpleInteractable` de cada mueble y abre el menu si supera el umbral).

### Pruebas realizadas (Play Mode, real — con espera real entre eventos, no solo Invoke() consecutivos)

| Prueba | Resultado |
|---|---|
| `selectEntered` + `selectExited` en el mismo frame (toque instantáneo) en Sofa | Material cicló (Azul→Verde) ✅, menú **no** se abrió ✅ |
| `selectEntered` en Mesa + esperar 1s real (`sleep`) | Menú se abrió con título "Mesa" ✅ |

### Pruebas realizadas (Play Mode, automatizadas)

Se agrego el prefab `XR Device Simulator` (de `Assets/Samples/XR Interaction Toolkit/3.6.0/XR Device Simulator/`) a la escena para poder probar sin visor. Con la escena en Play Mode se disparo el evento `activated` de cada `XRSimpleInteractable` (el mismo camino que dispara el input real, no un llamado directo al metodo) sobre Sofa, Mesa y TV, y los clicks de los botones del menu:

| Prueba | Resultado |
|---|---|
| `Activate` en Sofa (en `AppState.Personalizacion`) | Menu se abre, titulo = "Sofa", posicionado sobre el mueble ✅ |
| Click "Adelante" | Sofa se desplazo +0.1m en Z ✅ |
| Click "Rotar Der" | Sofa roto +15° en Y ✅ |
| Click "Cerrar" | Menu se oculta ✅ |
| `Activate` en Mesa / TV | Menu se re-apunta a cada uno (titulo correcto, movimiento aplica al mueble correcto) ✅ |
| `Activate` en Sofa estando en `AppState.Recorrido` | Menu NO se abre (guard de estado funciona) ✅ |

> [!TIP] Como probarlo a mano con el XR Device Simulator — mapeo real (verificado, ver sección 10)
> Con el simulador ya en la escena, en Play Mode y con la mano derecha seleccionada (`Tab` → Controller, `Y`): **tecla `G`** = Grip = `Select` (cicla material en paredes/sofá, pinta con `RayPaintVR`), **click izquierdo del mouse** = Trigger = `Activate` (abre el menú de mover/rotar, pero SOLO si el objeto ya está seleccionado con Grip en simultáneo — ver nota de la sección 5). `Tab`/`Y` cambian de mano/dispositivo, `U` vuelve al HMD.

---

## 6. IA local (Ollama) — cumple el requisito obligatorio del examen

> [!WARNING] Requisito del parcial
> El examen exige que toda app desarrollada use **IA local, sin conexión a servidor externo**. Esta integración lo cumple: todo corre en `localhost`, sin internet.

### Arquitectura implementada

```
[Usuario escribe pedido en el panel "Asistente IA Local"]
         ↓
[Unity: OllamaAIController -> UnityWebRequest POST]
         ↓ http://localhost:11434/api/generate
[Ollama local (modelo gemma2:2b, 2.6B params)]
         ↓ responde JSON: {"target":"pared","color":"gris"}
[Unity parsea la respuesta y llama SetMaterialByIndex(...)]
         ↓
[La pared o el sofá cambian en tiempo real]
```

Script: [`OllamaAIController.cs`](../Assets/Scripts/OllamaAIController.cs). Panel `IA_Panel` en la pared sur (campo de texto + botón "Enviar" + texto de estado).

### Pruebas reales (corridas en Play Mode contra Ollama local)

| Comando escrito | Respuesta del modelo | Resultado en la escena |
|---|---|---|
| "pone la pared beige" | `{"target":"pared","color":"beige"}` | Las 4 paredes → beige ✅ |
| "quiero el sillon de color azul" | `{"target":"sofa","color":"azul"}` | Sofá → tela azul ✅ (entendió "sillón" como sinónimo) |
| "que hora es?" | `{"target":"ninguno","color":"ninguno"}` | "No pude interpretar ese pedido" ✅ |

### Pendiente sobre esto
- El campo de texto usa teclado físico/mouse. En el visor real hace falta un **teclado virtual** o, mejor, **reconocimiento de voz local** (Windows Speech Recognition u otro STT offline) para que sea manos libres, como plantea la propuesta.
- Hay que dejar `ollama serve` corriendo (o configurado como servicio) en la máquina que hace de servidor — ver sección de despliegue de la propuesta (Opción 1 PC-VR vs Opción 2 APK + servidor local).

---

## 6bis. Menú principal por pestañas (Recorrido / Personalizar / Pintar) + modo Pintura habilitado

> [!INFO] Rescatado de un proyecto anterior
> Adaptado de `ToggleMenu.cs` y `SideMenu.cs` de **Room Designer** (TeamFWS, `D:\unity project\room-designer`). El `ToggleMenu` original escuchaba el botón "menu" del control izquierdo por *polling* de la API legacy `UnityEngine.XR` cada frame; se cambió por un método `Toggle()` llamado desde el evento `Select Entered` de `Boton_Menu` (ya existente), para no depender de que el runtime OpenXR mapee ese botón igual en todos los visores. El `SideMenu` original resaltaba a mano el toggle activo; acá se simplificó a Botones normales (mismo patrón que el resto del proyecto), sin necesidad de `ToggleGroup`.

Esto reemplaza al viejo `MenuControllerVR` (ya no se usa en la escena, el GameObject `MenuManager` se eliminó; el script sigue en el repo por si hace falta como referencia) y **cierra un hueco real**: `RayPaintVR.cs` ya estaba escrito y conectado al control derecho (`selectEntered` → `TryPaint()`, paleta con `Pared_Blanco/Beige/Gris`), pero no había ninguna forma de entrar a `AppState.Pintura` desde la UI. Ahora sí.

### Estructura nueva en `Sala`

```
MenuPrincipal (vacío, se reposiciona frente al jugador al abrir)
 ├── MenuTabs        (Canvas: botones Recorrido / Personalizar / Pintar)
 ├── MenuPersonalizacion  (reparentado; sin cambios internos, igual que antes)
 └── MenuPintura     (nuevo, clon de MenuPersonalizacion: botones Blanco/Beige/Gris → RayPaintVR.SetMaterialByIndex)
MenuPrincipalManager (siempre activo; tiene el ToggleMenuVR que sabe cual es "MenuPrincipal")
```

- **`Boton_Menu`** (Select Entered) → `ToggleMenuVR.Toggle()`: abre/cierra `MenuPrincipal` y lo reposiciona siempre frente a la cámara (antes quedaba fijo en un punto de la pared).
- **`SideMenuVR`** (en `MenuPrincipal`) decide qué panel mostrar y qué `AppState` activar según la pestaña: Recorrido (sin panel), Personalizar (`MenuPersonalizacion`), Pintar (`MenuPintura`).
- Al abrir el menú siempre arranca en la pestaña **Recorrido**. Al cerrarlo (botón "Cerrar" en cualquier panel, o `Boton_Menu` de nuevo) vuelve a `AppState.Recorrido`.

Scripts: [`ToggleMenuVR.cs`](../Assets/Scripts/ToggleMenuVR.cs), [`SideMenuVR.cs`](../Assets/Scripts/SideMenuVR.cs).

### Pruebas realizadas (Play Mode, automatizadas — mismo método que la sección 5: disparando los `UnityEvent` reales, no llamando a los métodos directo)

| Prueba | Resultado |
|---|---|
| `Select` en `Boton_Menu` | `MenuPrincipal` se activa, se posiciona frente a la cámara, arranca en pestaña Recorrido ✅ |
| Click pestaña "Personalizar" | `AppState.Personalizacion`, se muestra `MenuPersonalizacion` ✅ |
| Click color de pared en `MenuPersonalizacion` | Pared cambió de material (sigue funcionando tras el reparenteo) ✅ |
| Click pestaña "Pintar" | `AppState.Pintura`, se muestra `MenuPintura`, se oculta `MenuPersonalizacion` ✅ |
| Click "Beige" en `MenuPintura` | `RayPaintVR.materialToApply` cambió a `Pared_Beige` ✅ |
| Click "Cerrar" | Menú se oculta, `AppState.Recorrido` ✅ |
| Reabrir con `Boton_Menu` | Vuelve a pestaña Recorrido por defecto ✅ |

> [!TIP] Actualización: esto ya se probó a mano — ver sección 10
> El "pintar" de verdad (rayo real + tecla real) se validó manualmente después de escribir esto. Resultado: funciona, pero **no con el botón que se documentó al principio** — ver sección 10 para el mapeo correcto de botones descubierto en la prueba.

---

## 7. Bug encontrado en el plugin MCP for Unity

Durante el desarrollo automatizado detectamos que `manage_components` (agregar componentes) y la resolución de objetos por nombre/ID en `manage_material` (asignar material) tiran `NotImplementedException` en la versión actual del paquete `com.coplaydev.unity-mcp`. Se trabajó alrededor del bug usando `execute_code` (C# directo) para todo. Si van a seguir usando MCP for Unity para automatizar la escena, conviene actualizar el paquete.

---

## 8. Qué falta (siguiendo la propuesta original)

- [x] ~~Probar a mano el menú de pestañas y pintar apuntando de verdad con el rayo~~ — hecho, ver sección 10.
- [x] ~~Decidir qué hacer con el gesto de dos botones de `Activate`~~ — cambiado a un solo botón (sostener Select), ver sección 5.
- [ ] Probar con visor real (Quest u otro) — todo lo de abajo se probó con el XR Device Simulator (mouse/teclado) o disparando eventos por código, no con hardware VR real.
- [x] ~~Correr el bake de iluminación~~ — hecho (sección 12), 1 lightmap 512×512 + reflection probe horneado.
- [x] ~~Reemplazar primitivas de muebles por modelos reales~~ — Sofa/Mesa/TV con modelos reales (sección 11bis), rescatados de un pack existente. Falta ajustar orientación a mano y re-bakear luces.
- [ ] Reemplazar paredes/piso/puerta/ventana por el modelo real de Blender de YOSS/JAZMIN cuando esté listo — al hacerlo, marcar `Static` la geometría fija nueva igual que se hizo en la sección 12, y re-bakear.
- [ ] Reconocimiento de voz para el asistente IA — código implementado (sección 11ter), pero **no logró transcribir nada en varias pruebas con micrófono real** (probablemente permisos de Windows por-aplicación). Sin resolver todavía, y además falta confirmar que funcione sin internet una vez que sí transcriba.
- [ ] Conectar el flujo con el Marketplace Web + Backend (guardar diseño, cotización).
- [ ] App móvil (según el examen: debe usar sensores del dispositivo).
- [x] ~~Exportación a STL para impresión 3D~~ — implementado y probado (sección 12bis), sin necesidad de `pb_Stl`/`g3sharp`. Falta abrir el archivo en un slicer real para la validación final.
- [ ] Empaquetar como APK y probar en visor standalone (Opción 2 de despliegue).

---

## 10. Prueba manual real con el XR Device Simulator (mouse/teclado, no solo eventos automatizados)

> [!INFO] Qué se hizo distinto acá
> Todas las pruebas anteriores (secciones 5 y 6bis) disparaban el `UnityEvent` (`selectEntered.Invoke(...)`, `activated.Invoke(...)`, `button.onClick.Invoke()`) directamente desde código — válido para probar la lógica de nuestros scripts, pero no pasa por el sistema de Input real. Acá se usó control remoto real del mouse/teclado sobre la ventana de Unity (Play Mode, pestaña Game) para confirmar que el input físico también funciona de punta a punta.

### Hallazgo importante: el mapeo de botones NO es el que se documentó al principio

Con el XR Device Simulator en modo Controller (mano derecha), se inspeccionó por reflexión a qué acción de Input están enlazados los botones del **Right Controller / Near-Far Interactor** (el que usan `MaterialChangerVR`, `RayPaintVR` y `FurnitureSelectable`):

| Acción XRI | Botón físico real | Tecla/botón en el simulador |
|---|---|---|
| **Select** (cicla material, `RayPaintVR.TryPaint`) | **Grip** | Tecla `G` |
| **Activate** (abre menú de mover mueble) | **Trigger** | Click izquierdo del mouse |

Esto es el **inverso** de lo que se había asumido y escrito originalmente en las secciones 5 y 6bis ("Activate = grip"). Ya se corrigieron esas secciones más arriba.

### Además: `Activate` requiere `Select` sostenido al mismo tiempo

Se confirmó (leyendo el código de XRI vía reflexión y probando) que `Activate` solo se dispara si el interactor **ya tiene algo seleccionado en ese instante** — es decir, en un control real hay que **sostener Grip y, sin soltarlo, apretar el Trigger**. No son dos botones independientes.

> [!TIP] Resuelto — se cambió el diseño
> Por esto mismo se rediseñó el menú de mover muebles (sección 5) para usar un solo botón (`Select`/Grip sostenido) en vez de la combinación `Select`+`Activate`. Ver sección 5 para el diseño actual y sus pruebas.

### Lo que SÍ se confirmó con input 100% real (mouse/teclado físico, sin invocar UnityEvents desde código)

| Prueba | Botón usado | Resultado |
|---|---|---|
| Apuntar con el rayo a `Pared_Norte` y presionar `G` (Grip/Select) en modo Pintura, paleta en Gris | Tecla `G` real, sostenida 1s | `Pared_Norte` cambió de `Pared_Blanco` a `Pared_Gris` ✅ — confirma que `RayPaintVR.TryPaint()` funciona con input físico real, no solo simulado por código |
| Cambiar de dispositivo simulado (HMD → Controller → mano derecha) | Teclas `Tab`, `Y`, `U` | El overlay del simulador reflejó el cambio correctamente ✅ |
| Un solo click izquierdo (Trigger) apuntando a un mueble sin Grip sostenido | Click izquierdo | No pasa nada (esperado: `Activate` sin `Select` previo no hace nada) ✅ confirma el hallazgo de arriba |

### Nota técnica sobre el simulador (por si alguien más lo intenta automatizar)

El "look" del HMD/controlador en el XR Device Simulator es 100% delta de mouse acumulado frame a frame — no responde a mover el cursor a una posición absoluta (`SetCursorPos`) de forma predecible, y escribirle directamente los campos de rotación por reflexión (`m_RightControllerEuler`, `m_RightControllerState`) tampoco sirve porque se recalculan cada frame a partir del input real, no de esos campos. Para reposicionar la cámara/rayo durante pruebas automatizadas es mucho más confiable teletransportar el `XR Origin` por código (`rig.transform.position/rotation`) que tratar de "apuntar" moviendo el mouse.

---

## 11bis. Primitivas reemplazadas por modelos reales (Sofa, Mesa, TV)

> [!INFO] Origen
> Se encontró un proyecto viejo descargado (`D:\unity project\VR-Real-Estate`, GearVR ~2016) con un pack de muebles low-poly reutilizable en `Assets/HomeStuff/` (FBX + materiales + prefabs, ~1MB total). Los scripts de ese proyecto NO se usaron (input legacy de gaze/touchpad, incompatible con nuestro XRI); solo se copiaron los modelos 3D. Sin licencia explícita en el repo — está bien para el MVP del examen, pero no usar en algo comercial sin confirmar el origen.

Se copió `Assets/HomeStuff/` (Models, Materials, Prefabs) completo a `InmobiliariaVR` — hay más muebles disponibles ahí (cama, baño, cocina, libros, etc.) por si hacen falta más adelante.

### Qué se hizo

- **Sofa** → mesh de `Sofa.prefab` (1.8 × 0.63 × 0.57 m).
- **Mesa** → mesh de `SmallTable.prefab` (1.1 × 0.36 × 0.63 m, mesa ratona en vez de la mesa de comedor grande del pack).
- **TV** → mesh de `TV.prefab` (1.0 × 0.59 × 0.18 m), sigue montado en la pared a la misma altura de antes.
- En los tres casos se **reutilizó el mismo GameObject** (no se crearon objetos nuevos): se les cambió el `MeshFilter.sharedMesh`, el material inicial y el tamaño del `BoxCollider`, y se recalculó la escala/posición para que queden parados sobre el piso. Esto es clave — **todas las referencias existentes siguen funcionando sin tocarlas**: los botones `Btn_Sofa0/1/2` del menú, `OllamaAIController.sofa`, los eventos `Select`/`selectExited` de `FurnitureSelectable`, todo seguía apuntando al mismo GameObject.
- El material de las paredes/piso no se tocó — solo cambia lo que ya estaba planeado como placeholder (los muebles).

### Ajuste necesario: `MaterialChangerVR.cs`

Los modelos importados no traen el mesh en el mismo GameObject que el script en todos los casos posibles a futuro, así que se cambió `GetComponent<Renderer>()` por `GetComponentInChildren<Renderer>()` en [`MaterialChangerVR.cs`](../Assets/Scripts/MaterialChangerVR.cs) — cambio de una línea, compatible hacia atrás (sigue encontrando el Renderer aunque esté en el mismo objeto).

### Ajuste necesario: conversión de material a URP

El material del pack (`MainMaterial.mat`) traía shader `Standard` (Built-in Render Pipeline) y se veía **magenta/rosa** (shader roto) en nuestro proyecto, que usa URP. Se convirtió en el editor por código: mismo shader `Universal Render Pipeline/Lit`, con la textura y el color originales reasignados a `_BaseMap`/`_BaseColor`.

### Pruebas realizadas (Play Mode)

| Prueba | Resultado |
|---|---|
| Ciclar color del Sofa (`SetMaterialByIndex(2)`) | Cambió a `Sofa_Tela_Verde` sobre el modelo real ✅ |
| Sostener Select sobre la Mesa (modelo real) | Menú de mover/rotar se abrió con título "Mesa" ✅ |
| Mover y rotar la Mesa desde el menú | Posición y rotación cambiaron correctamente ✅ |

> [!TIP] Orientación ya ajustada
> El Sofa había quedado con el respaldo mirando al centro del cuarto y el asiento contra la pared (se notó comparando capturas de cerca desde distintos ángulos). Se giró 180° en Y — ahora el asiento mira hacia el TV, con la mesa ratona en el medio, un living coherente. TV y Mesa no necesitaron ajuste (el TV ya mostraba la pantalla hacia adentro del cuarto; la mesa es simétrica). No hizo falta re-bakear: los muebles no son `Static`, así que no tienen lightmap propio — solo reciben luz por Light Probes, que no dependen de su rotación exacta.

---

## 11ter. Reconocimiento de voz para el asistente IA

Se agregó [`VoiceCommandController.cs`](../Assets/Scripts/VoiceCommandController.cs), usando `UnityEngine.Windows.Speech.DictationRecognizer` (Windows Speech Recognition). Botón nuevo "Hablar" al lado de "Enviar" en `IA_Panel` → escucha, transcribe, y manda el texto directo a `OllamaAIController.EnviarComando()` (mismo parser que ya interpreta lenguaje natural).

### Limitación de plataforma (importante)

`UnityEngine.Windows.Speech` **solo compila y funciona en Windows** (Editor o build standalone/UWP). El código está protegido con `#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN` — en un APK de Quest standalone (Opción 2 de despliegue de la propuesta) el botón "Hablar" simplemente avisa que no está disponible, no rompe nada, pero **no sirve para esa opción de despliegue**. Para Quest standalone habría que integrar un STT on-device para Android (ej. Vosk) — no se hizo, queda fuera de alcance por ahora. Para Opción 1 (PC VR con Quest Link) sí sirve tal cual.

> [!WARNING] Sin confirmar todavía: ¿esto es realmente "sin internet"?
> Al probarlo (Play Mode, sin hablarle todavía) Windows tiró este error: `"Dictation support is not enabled on this device (see 'Get to know me' in Settings > Privacy > Speech, inking, & typing)"`. Ese mismo panel de configuración de Windows (**Configuración → Privacidad → Voz**, "reconocimiento de voz en línea") es el que controla si el dictado usa el servicio de Microsoft **en la nube** o el motor local. En muchas versiones de Windows, `DictationRecognizer` (a diferencia de `GrammarRecognizer`/`KeywordRecognizer`, que sí son 100% locales) depende del servicio online de Microsoft para el reconocimiento de dictado libre — lo cual **podría no cumplir** el requisito del examen ("IA local, sin conexión a servidor externo").
>
> **Falta verificar antes del examen:** activar "Dictation support" en Windows, después **desconectar internet** (modo avión) y probar si sigue transcribiendo. Si deja de funcionar sin internet, la alternativa es cambiar a `GrammarRecognizer`/`PhraseRecognizer` con una lista fija de frases esperadas (ej. "pared blanca", "sofá azul") — es 100% local siempre, a costa de no aceptar lenguaje 100% libre como ahora.

### Cómo probarlo (además de la nota de arriba)

1. Windows: Configuración → Hora e idioma → Voz → activar reconocimiento de voz / descargar el paquete de idioma si hace falta.
2. Play Mode → botón "Hablar" en el panel de IA → decir un pedido (ej. "poné la pared gris") → se transcribe y se manda solo, como si se hubiera escrito y apretado "Enviar".

### Sesión de pruebas con micrófono real — sin resultado, pendiente de resolver

Se probó varias veces en Play Mode, con Unity al frente y en foco, micrófono real (no simulado). Se agregó además el evento `DictationHypothesis` al script para mostrar en vivo el texto parcial mientras se habla (útil para saber si el audio está llegando, sin esperar al resultado final) — buena sugerencia del equipo durante la prueba.

**Resultado: nunca se recibió ni un solo fragmento de texto (`DictationHypothesis` nunca se disparó), en ningún intento.** Lo que sí fue cambiando entre intentos:

| Intento | Causa reportada | Contexto |
|---|---|---|
| 1 | `"Dictation support is not enabled on this device"` | Windows no tenía el paquete de voz instalado |
| 2-3 (tras instalar el paquete) | `TimeoutExceeded` | Paquete instalado pero 0 audio detectado |
| 4 | `Canceled` | Probablemente por conflicto con la barra de dictado de Windows (Win+H) que había quedado abierta en el Bloc de notas |
| 5 (tras cerrar todo, reiniciar Play Mode) | `TimeoutExceeded` de nuevo | Sin explicación clara |

**Diagnóstico paralelo con Win+H (dictado nativo de Windows) fuera de Unity:**
- Primer intento: error genérico `"Something went wrong. Try again in a little while"` (típico justo después de activar el servicio).
- Confirmado luego que **si funciona en otras aplicaciones** — descarta que sea un problema de hardware/driver del micrófono o de que el servicio de Windows esté roto en general.

**Conclusión: el problema parece específico de cómo Unity Editor accede al micrófono**, no del micrófono en sí ni de Windows en general (ya que Win+H funciona en otras apps). Se sugirió como próximo paso revisar **Configuración → Privacidad y seguridad → Micrófono → lista de aplicaciones específicas** (no el toggle general) para ver si Unity aparece bloqueado ahí puntualmente — quedó sin confirmar.

> [!WARNING] Estado: no funcional todavía, pendiente para el equipo
> Después de ~6 intentos con distintos ajustes (paquete de idioma, cerrar apps en conflicto, reiniciar Play Mode) el micrófono real nunca llegó a transcribir nada dentro de Unity. El código está bien armado (confirmado que la lógica funciona vía pruebas automatizadas y que el flujo texto→IA funciona con DeepSeek en un script aparte), pero la integración real con el micrófono de Windows dentro del Editor quedó **sin resolver**. Es probablemente un tema de permisos por-aplicación de Windows que alguien del equipo tiene que revisar con calma en su propia máquina (y probablemente probar en la máquina que se use el día del examen, porque esto puede variar de PC a PC). No se pudo seguir iterando a distancia sin poder escuchar el resultado en tiempo real.

---

## 12. Sala lista para baked lighting y reflection probes

> [!INFO] Por qué
> El equipo vio un ejemplo de un proyecto archviz (nivel HDRP con lightmaps, reflection probes y post-processing) y preguntó qué haría falta para acercarse a esa calidad visual. Migrar a HDRP o modelar ese nivel de detalle no tiene sentido para el plazo del examen, pero dejar la sala preparada para bake sí es rápido y da un salto de calidad real cuando llegue el modelo definitivo de Blender.

### Qué se hizo

- **Geometría fija marcada `Static`** (`ContributeGI` + `Occluder/OccludeeStatic` + `BatchingStatic` + `ReflectionProbeStatic`): `Piso`, las 4 paredes y `Puerta`. `Ventana` quedó estática pero **sin** `ContributeGI` (para evitar artefactos de lightmap si el material final es transparente).
- **Sofa/Mesa/TV NO se marcaron estáticos a propósito** — `FurnitureManipulatorVR` los mueve en runtime (sección 5); un objeto lightmap-static que se mueve queda con la iluminación "pegada" a su posición original. Reciben luz vía Light Probes en su lugar.
- **Reflection Probe** (heredado del VR Template, tenía tamaño 20×10×20 centrado fuera de la sala): reposicionado y redimensionado a los límites reales de `Sala` (4.2×2.6×4.2, centrado), modo `Baked`, resolución 256.
- **Light Probe Group** (también heredado, 8 probes mal ubicados): regenerado con 27 probes en grilla 3×3×3 (piso, altura media, cerca del techo) cubriendo toda el área caminable — así los muebles y el jugador reciben iluminación indirecta correcta se muevan donde se muevan.
- **Lighting Settings** (ya venían del template, se verificaron y quedaron como están): `Baked Global Illumination` activado, `Mixed Lighting` en modo `Shadowmask`, lightmapper `Progressive GPU`, resolución 40 texels/unidad — razonable para una sala de este tamaño.

### Bake corrido

> [!TIP] Ya se corrió (después de reemplazar Sofa/Mesa/TV por modelos reales, sección 11bis)
> Se disparó con `UnityEditor.Lightmapping.BakeAsync()` — tardó ~30s para esta sala. Resultado: 1 lightmap de 512×512 y el Reflection Probe con su textura horneada (`ReflectionProbe-0`). Se nota en las sombras suaves alrededor de la base de los muebles y las paredes. Guardado en la escena.
>
> **Ojo:** este bake quedó pegado a las posiciones actuales de Sofa/Mesa/TV. Si alguien los mueve a mano en el Editor (no en Play Mode — ahí no se guarda) hay que volver a bakear. Y cuando llegue el modelo final de Blender para paredes/piso, bakear de nuevo desde cero.

---

## 12bis. Exportación a STL para impresión 3D

> [!INFO] Por qué
> Es la mitad del tema asignado al grupo ("Realidad Virtual **e impresión 3D**"). El usuario personaliza la sala en VR y, cuando está conforme, exporta una maqueta imprimible — tal como plantea la propuesta.

### Qué se hizo

- [`StlExporter.cs`](../Assets/Scripts/StlExporter.cs) — utilidad estática que recorre los `MeshFilter` de una jerarquía y escribe un **STL binario** válido.
- [`StlExportController.cs`](../Assets/Scripts/StlExportController.cs) — conecta el botón de UI con el exportador, guarda en `Application.persistentDataPath/Exports/InmobiliariaVR_<fecha>.stl` y muestra el resultado (cantidad de triángulos) en un texto de estado.
- Nueva **4ta pestaña "Exportar"** en el menú principal (`MenuTabs`), al lado de Recorrido/Personalizar/Pintar — panel con un botón "Exportar a STL" y texto de estado, mismo patrón visual que el resto.

### Decisiones técnicas

- **No hizo falta la parte más pesada de la investigación previa** ([`investigacion_VR_impresion3D.md`](investigacion_VR_impresion3D.md) menciona `g3sharp` para darle espesor a paredes escaneadas sin volumen). Nuestras paredes/piso son cubos con volumen real desde el vamos, y los muebles importados también son sólidos — no hace falta "engrosar" nada, se exportan tal cual.
- **Conversión de ejes:** Unity es zurdo (Y arriba), STL/impresión 3D asume diestro (Z arriba). Se remapea `(x,y,z) → (x,z,y)` y se invierte el orden de los vértices de cada triángulo (compensa el cambio de lateralidad — si no, las normales quedan invertidas y algunos slicers imprimen la pieza "por dentro").
- **Escala:** 20mm impresos por cada metro de escena (**1:50**), configurable en `StlExporter.MilimetrosPorMetro`. Da una maqueta de escritorio en vez de un inmueble a tamaño real.
- Se usa `MeshFilter` en vez de `pb_Stl` (la librería que mencionaba la investigación) — no estaba instalada en el proyecto y para nuestro caso (mallas ya cerradas, sin necesidad de fusionar/booleanas) alcanza con un exportador propio simple.

### Pruebas realizadas (Play Mode, real — botón real, no solo el método directo)

| Prueba | Resultado |
|---|---|
| Abrir menú → pestaña "Exportar" → botón "Exportar a STL" | Archivo generado, texto de estado actualizado: "Exportado: ...stl (596 triángulos)" ✅ |
| Validación binaria del archivo (bytes totales vs. fórmula `84 + triángulos×50`) | Coincide exacto ✅ — el archivo no está corrupto |
| Bounding box de la geometría exportada | 55×77×30mm — maqueta de escritorio razonable, entra en cualquier impresora casera ✅ |
| Triángulos degenerados / valores `NaN` | 5 de 596 degenerados (0.8%, insignificante), 0 `NaN` ✅ |

> [!WARNING] Falta abrir el archivo en un slicer de verdad
> Se validó el archivo a nivel binario (estructura correcta, sin NaN, tamaño razonable) pero no se abrió en PrusaSlicer/OrcaSlicer para confirmar que "loopea" bien visualmente y que el slicer no se queja de geometría non-manifold. Se mandó el archivo generado por chat — alguien del equipo debería abrirlo en un slicer para la validación final antes de imprimir de verdad.

---

## 13. Cómo probarlo ustedes (Sala_MVP, VR)

1. Abrir `Assets/Scenes/Sala_MVP.unity` en el Editor.
2. Asegurarse de tener `ollama serve` corriendo (`ollama list` para ver modelos disponibles; usamos `gemma2:2b`).
3. Play Mode → con el visor o el simulador de XR, tocar paredes/sofá para ciclar color, o usar el botón azul para abrir el menú de personalización.
4. Escribir un pedido en el panel "Asistente IA Local" (ej. "cambia la pared a gris") y presionar Enviar.
5. Con `AppState.Personalizacion` activo, apuntar al Sofa/Mesa/TV y **sostener Select (Grip, tecla `G` en el simulador) ~0.35s** para abrir el menú de mover/rotar. Un toque corto en el Sofa solo cicla el color, como antes.
6. Para pintar: pestaña "Pintar" del menú principal, elegir color, apuntar a una pared/piso/techo y apretar **Grip** (`G` en el simulador).
7. Para exportar una maqueta: pestaña "Exportar" del menú principal → botón "Exportar a STL". El archivo queda en `%USERPROFILE%\AppData\LocalLow\DefaultCompany\InmobiliariaVR\Exports\`.

---

## 14. App móvil complementaria (AR + sensores) — requisito obligatorio del examen

> [!WARNING] Requisito eliminatorio
> El examen exige una app móvil que use sensores del dispositivo (cámara, micrófono). Hasta este punto el proyecto era 100% VR/PC — esta escena nueva cubre ese requisito reusando la mayor parte de la lógica ya escrita (mismo `OllamaAIController`, mismo `MaterialChangerVR`) en vez de armar un proyecto aparte.

### Qué se hizo

Escena nueva **`Assets/Scenes/AppMovil_AR.unity`**, agregada a Build Settings junto a `SampleScene` y `Sala_MVP`. Se armó con AR Foundation 6.6.2 (ya estaba instalado) + **ARCore XR Plugin** (se instaló ahora), apuntando a celulares Android:

```
AR Session          (ARSession)
XR Origin           (XROrigin + ARPlaneManager + ARRaycastManager)
 └── Camera Offset
      └── AR Camera (Camera + ARCameraManager + ARCameraBackground + TrackedPoseDriver)
EventSystem          (InputSystemUIInputModule, para que los botones respondan con el nuevo Input System)
Canvas_AppMovil       (UI: texto de estado, botones Sofa/Mesa/TV, campo de comando + Enviar, botón Hablar)
AppMovilManager       (OllamaAIController + ArFurniturePlacer + MobileVoiceCommandController)
```

- [`ArFurniturePlacer.cs`](../Assets/Scripts/ArFurniturePlacer.cs) — usa la **cámara** del celular (sensor 1, vía `ARRaycastManager`) para detectar el piso; al tocar la pantalla sobre una superficie detectada, instancia el mueble elegido (mismos prefabs de `Assets/HomeStuff/Prefabs/` que ya usa `Sala_MVP`: `Sofa`, `SmallTable`, `TV`). Al Sofá colocado se le agrega un `MaterialChangerVR` con la misma paleta de colores (`Sofa_Cuero/Tela_Azul/Tela_Verde`) y se lo conecta como `sofa` del `OllamaAIController` de la escena — así el asistente de IA puede cambiarle el color sin duplicar el parser de comandos.
- [`MobileVoiceCommandController.cs`](../Assets/Scripts/MobileVoiceCommandController.cs) — usa el **micrófono** del celular (sensor 2): pide el permiso en runtime y graba audio real con `Microphone.Start`. **Con una salvedad documentada a propósito** (mismo criterio que la limitación de voz en Windows, sección 11ter): Unity no trae un motor de speech-to-text para Android, así que todavía no transcribe lo grabado a texto. Mientras se resuelve (con un plugin nativo o un STT on-device como Vosk), el pedido en lenguaje natural se escribe en el campo de texto del panel, que ya manda al mismo `OllamaAIController.EnviarComandoDesdeInput()` que funciona en la versión de escritorio.
- Prefab de visualización de plano `Assets/Prefabs/AR_DefaultPlane.prefab` (mesh celeste semitransparente) para que el jugador vea qué superficie detectó la cámara antes de tocar.
- Se agregó el **AR Background Renderer Feature** al Renderer URP usado en Android (`Assets/Settings/Project Configuration/Android Preset.asset`) — sin este paso la imagen de la cámara no se muestra de fondo en un proyecto URP, es un requisito de AR Foundation fácil de pasar por alto.

### Decisiones técnicas

- **Misma escena/proyecto que la VR**, no un proyecto Unity aparte — se decidió así para reusar `OllamaAIController` y `MaterialChangerVR` tal cual, sin mantener dos copias de la lógica de IA. El costo es que compilar esta escena para celular requiere cambiar la configuración de XR del proyecto (ver "Cómo probarla" abajo), distinto de compilar `Sala_MVP` para Quest.
- **Reuso de prefabs y paleta de colores** de `Sala_MVP` en vez de modelos nuevos — consistente con el resto del proyecto y sin trabajo de arte adicional.
- **Solo el Sofá tiene cambio de color** (igual que en la versión VR, donde Mesa/TV tampoco lo tienen) — mantiene el vocabulario de `OllamaAIController` (`pared`/`sofa`) sin tener que extenderlo todavía.
- `ArFurniturePlacer.Awake()` fuerza `StateManager.Instance.ChangeState(AppState.Personalizacion)` al arrancar la escena, porque `MaterialChangerVR.SetMaterialByIndex` solo actúa en ese estado (guard heredado de la versión VR) y esta app no tiene menú de modos.

### Qué falta / pendiente de probar

- [ ] **Habilitar el proveedor ARCore en Android.** Antes de compilar esta escena para un celular: `Project Settings → XR Plug-in Management → Android` → desmarcar OpenXR (el que usa el build de Quest) y marcar **ARCore**. Hay que revertir este cambio antes de volver a compilar `Sala_MVP` para Quest — el proyecto no puede tener ambos proveedores activos a la vez para Android.
- [ ] **Probar en un celular Android real con ARCore certificado** — se intentó con un Redmi 15, un Galaxy A12 y un Nubia Neo 3 5G, ninguno está en la lista oficial de dispositivos soportados por ARCore (developers.google.com/ar/devices). Falta conseguir un equipo certificado (ej. cualquier Redmi Note, Galaxy A5x/A7x/S, Pixel, Moto G reciente).
- [x] ~~XR Simulation como respaldo~~ — agregado el `SimulationLoader` como segundo loader de Standalone (después de OpenXR) en `XRGeneralSettingsPerBuildTarget.asset`, usando el entorno de prueba por defecto de AR Foundation. Con el Quest desconectado, Play Mode en `AppMovil_AR` cae automáticamente al simulador (cuarto genérico con piso detectable, click de mouse = tap de pantalla) — no reemplaza la prueba en hardware real que pide el examen, pero permite validar la lógica de `ArFurniturePlacer` mientras se consigue un celular compatible.
- [ ] **Transcripción de voz a texto** — sin resolver, ver limitación documentada en `MobileVoiceCommandController.cs`. Mientras tanto, usar el campo de texto del panel.
- [ ] Agregar los permisos de Android (cámara ya la gestiona AR Foundation automáticamente; micrófono debería agregarse solo al usar la clase `Microphone`, pero confirmar en el manifest generado tras el primer build).

### Cómo probarla (celular Android)

1. `Project Settings → XR Plug-in Management → Android` → activar **ARCore**, desactivar OpenXR.
2. `File → Build Settings → Android`, dejar solo `AppMovil_AR` marcada (o todas, pero asegurarse que sea la escena de inicio si se compila un APK aparte para probar).
3. Compilar e instalar el APK en un celular Android con soporte ARCore.
4. Apuntar la cámara al piso hasta ver el plano celeste, tocar la pantalla para colocar el Sofá (o elegir Mesa/TV con los botones de arriba).
5. Escribir un pedido de color en el campo de texto (ej. "poné el sillón azul") y tocar "Enviar" — necesita el mismo servidor `ollama serve` local corriendo y accesible en la red del celular (ajustar `ollamaUrl` en el `OllamaAIController` de esta escena si el celular no usa `localhost`, ya que ahí `localhost` sería el propio celular, no la PC del servidor).
