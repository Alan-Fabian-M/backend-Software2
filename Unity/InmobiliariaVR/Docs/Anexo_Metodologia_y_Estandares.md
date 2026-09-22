# Anexo: Metodología de desarrollo y estándares de codificación

Responde al punto 5 de la rúbrica del examen (ver [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md)). Describe lo que **efectivamente se hizo** en el proyecto `InmobiliariaVR`, con evidencia verificable en el código y en los registros de avance — no es un framework aspiracional, es la metodología real seguida hasta ahora.

---

## 1. Metodología de desarrollo

**Iterativo-incremental, organizado como un Kanban informal basado en documentos.**

En vez de sprints con fechas fijas, el equipo trabajó por **features** (una funcionalidad completa por vez: menú de personalización, mover muebles, IA local, menú de pestañas, exportación STL, etc.), cada una llevada de punta a punta antes de pasar a la siguiente. El "tablero" del proyecto son dos documentos que actúan como backlog y como registro de estado:

- [`Estado_Avance_Examen_ISW2.md`](Estado_Avance_Examen_ISW2.md) — checklist general con casillas `[x]` / `[ ]` por criterio de evaluación, que funciona como vista de alto nivel de qué está "Hecho" / "En curso" / "Pendiente" (columnas implícitas de un Kanban).
- [`Progreso_MVP_Unity.md`](Progreso_MVP_Unity.md) — bitácora numerada por feature (secciones 1 a 13), donde cada sección documenta: qué había antes, qué se implementó, qué decisiones de diseño se tomaron y por qué, y qué pruebas se corrieron. La sección 8 ("Qué falta") funciona como backlog vivo, con items que se van tachando (`~~texto~~`) a medida que se completan.

### Ciclo seguido por cada feature

1. **Investigar/adaptar una referencia** cuando existía una solución similar en un proyecto anterior del equipo (Room Designer, TeamFWS) — se adapta el patrón, no se copia tal cual (ver ejemplos en el punto 3).
2. **Implementar** el script o cambio en Unity.
3. **Probar en Play Mode**, en dos niveles crecientes de fidelidad:
   - Automatizado, disparando los `UnityEvent` reales (`selectEntered.Invoke(...)`, `button.onClick.Invoke()`) en vez de llamar a los métodos directo por código — valida el cableado, no solo la lógica.
   - Manual con input físico real (mouse/teclado sobre el XR Device Simulator, o visor real cuando estuvo disponible) — detectó al menos un caso donde el comportamiento real difería de lo asumido (ver sección 10 de `Progreso_MVP_Unity.md`: el mapeo de botones `Select`/`Activate` no era el esperado).
4. **Documentar el resultado** en `Progreso_MVP_Unity.md` (qué se probó, qué pasó, qué quedó pendiente) y **actualizar el checklist** en `Estado_Avance_Examen_ISW2.md`.

### Por qué este enfoque y no Scrum/sprints formales

El equipo es de 5 personas trabajando part-time sobre una materia, sin roles de Scrum Master/Product Owner ni ceremonias formales. Un Kanban informal con backlog documentado se ajusta mejor a ese contexto: permite ver en todo momento qué está terminado y qué falta (ambos documentos están versionados en git, así que también sirven de historial), sin la sobrecarga de organizar sprints para un equipo de este tamaño.

---

## 2. Estándares de codificación

Evidencia observable directamente en `Assets/Scripts/`.

### 2.1 Nomenclatura

| Elemento | Convención | Ejemplo |
|---|---|---|
| Clases MonoBehaviour de interacción VR | `PascalCase` en inglés + sufijo `VR` | `FurnitureManipulatorVR`, `MaterialChangerVR`, `RayPaintVR`, `ToggleMenuVR`, `SideMenuVR` |
| Clases de datos/infraestructura (sin sufijo `VR`) | `PascalCase` en inglés | `StateManager`, `SaveDataSerializer`, `DesignLayoutData` |
| Métodos públicos pensados para conectarse desde el Inspector (botones, eventos XRI) | `PascalCase` **en español** | `Mover()`, `Rotar()`, `AbrirMenuPara()`, `CerrarMenu()`, `ExportarInmueble()`, `ToggleEscucha()` |
| Campos privados / variables locales | `camelCase` **en español** | `muebleActual`, `seleccionActiva`, `momentoInicioSeleccion` |
| Campos públicos expuestos en el Inspector | `camelCase`, con `[Tooltip("...")]` explicando su uso | `pasoMovimiento`, `paintableTags`, `tiempoParaAbrir` |

La mezcla inglés (nombres de clase/infraestructura) + español (métodos y variables de dominio) no es inconsistencia: los métodos en español son justamente los que el equipo conecta a mano desde el Inspector de Unity (Nombre método → evento `OnClick`/`Select Entered`), y mantenerlos en el idioma en que se piensa el dominio (mover, rotar, pintar, abrir menú) reduce errores de cableado entre quien programa y quien arma la escena.

### 2.2 Patrón de guarda de estado

Todo método que modifica la escena en respuesta a una interacción del jugador arranca chequeando `StateManager.Instance.CurrentState` y retorna sin hacer nada si no corresponde — nunca lanza excepción para esto, es un guard silencioso porque puede dispararse por input normal del usuario en el estado "equivocado" (no es un caso de error):

```csharp
private bool PuedeManipular()
{
    return StateManager.Instance.CurrentState == AppState.Personalizacion;
}
```

Presente igual en `MaterialChangerVR.PuedeCambiarMaterial()`, `FurnitureManipulatorVR.PuedeManipular()` y el flag `isEnabled` de `RayPaintVR`. Ver [`Docs/README.md`](README.md) para el detalle de esta arquitectura de estados.

### 2.3 Comentarios: explican el *por qué*, no el *qué*

Cada script no trivial tiene un bloque de comentario al inicio explicando la decisión de diseño detrás del código, no una descripción de lo que ya es obvio leyendo los nombres. Cuando el código se adaptó de un proyecto anterior, se lo dice explícitamente y se explica qué cambió y por qué. Ejemplo real (`FurnitureSelectable.cs`):

```csharp
// Se probo primero con "Activate" (trigger) separado de "Select" (grip) para no
// pisar el ciclo de color del Sofa (que ya usa Select), pero probando a mano con
// el XR Device Simulator se descubrio que en XRI "Activate" solo se dispara si el
// objeto YA esta seleccionado en simultaneo (...) — un gesto de dos botones
// dificil de ejecutar y de probar. Ahora se usa un solo boton (Select) con
// umbral de tiempo (...)
```

No hay comentarios tipo `// incrementa el contador` sobre líneas autoexplicativas.

### 2.4 Organización de carpetas

```
Assets/Scripts/
  *.cs                 -- scripts de interacción VR, estado global, IA, voz, exportación (planos, un archivo por responsabilidad)
  SaveSystem/
    DesignLayoutData.cs      -- modelo de datos serializable
    SaveDataSerializer.cs    -- lógica de guardado/carga (I/O)
```

Separación por responsabilidad, no por tipo técnico: `SaveSystem/` agrupa todo lo relacionado a persistencia (datos + serializador) en vez de tener carpetas genéricas como `Models/` o `Utils/`.

### 2.5 Manejo de errores

- **I/O de archivos** (`SaveDataSerializer`, `StlExportController`): `try/catch` con `Debug.LogError` y, en el caso del guardado/carga, relanzar la excepción para que quien llama pueda decidir qué mostrar al usuario.
- **Entrada de usuario / estado inválido** (todo lo demás): guard silencioso (`if (!condicion) return;`), sin excepciones — porque no es un caso excepcional, es parte normal del flujo de interacción (ver 2.2).
- **Operaciones asíncronas de archivo** (`SaveDataSerializer`): `async/await` con `Task`, no coroutines, porque no dependen del ciclo de frames de Unity.
- **Llamadas de red** (`OllamaAIController`): coroutine + `UnityWebRequest`, con chequeo explícito de `www.result != UnityWebRequest.Result.Success` antes de intentar parsear la respuesta.

### 2.6 Patrones de diseño usados

- **Singleton** (`StateManager.Instance`) — único caso en el proyecto; se reservó para el estado global porque necesita sobrevivir entre pantallas/escenas (`DontDestroyOnLoad`) y ser accedido desde scripts sin referencia directa entre sí.
- **Observer / evento de C#** (`StateManager.StateChanged`) — para que scripts como `RayPaintVR` reaccionen a cambios de estado sin que `StateManager` conozca a sus suscriptores.
- **Utilidad estática sin estado** (`StlExporter`) — funciones puras de transformación de datos (mallas → triángulos → bytes STL), sin necesidad de ser un `MonoBehaviour` ni de guardar estado entre llamadas.

---

## 3. Trazabilidad de la adaptación de código externo

Cuando un script se basó en código de otro proyecto del equipo (Room Designer, TeamFWS — proyecto de Realidad Mixta con Meta Quest), el comentario de cabecera del archivo lo declara explícitamente junto con qué se sacó/cambió y por qué. Ejemplos verificables:

| Script actual | Adaptado de | Cambio principal y motivo |
|---|---|---|
| `StateManager.cs` | `StateManager` de Room Designer | Se quitaron dependencias de Meta XR |
| `FurnitureManipulatorVR.cs` | `FurnitureManipulator` | Slider de rotación → botones ±15°, más confiable en VR que arrastrar con un rayo |
| `RayPaintVR.cs` | `RayPaint.cs` | Se sacó la dependencia del `FlexibleColorPicker` propio del otro proyecto, reemplazado por paleta de materiales en el Inspector |
| `SaveDataSerializer.cs` | `SaveDataSerializer` de Room Designer | Se quitó `OVRSpatialAnchor` (el inmueble ya viene modelado, no se escanea un cuarto real) |
| `ToggleMenuVR.cs` / `SideMenuVR.cs` | `ToggleMenu.cs` / `SideMenu.cs` | Polling de input legacy por frame → evento `Select Entered`; `ToggleGroup` con resaltado manual → botones simples |

Esto deja evidencia de que el código no se escribió "a ciegas": cada adaptación tiene una razón técnica documentada, verificable contra el comentario del propio archivo fuente.
