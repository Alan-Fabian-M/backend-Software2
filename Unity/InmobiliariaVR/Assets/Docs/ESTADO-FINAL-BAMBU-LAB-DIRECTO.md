# Estado final: conexión directa Quest 3 → Bambu Lab (WiFi, sin PC intermediario)

**Fecha:** 2026-09-19 (actualizado tras abrir Unity Editor el mismo día)
**Estado:** Implementado. Unity Editor ya reportó y se corrigieron errores reales de compilación
(ver sección 9) que la verificación local con Mono no había podido detectar. Pendiente: recompilar
en Unity y confirmar consola limpia, luego generar la UI con "Generar UI Bambu Lab".

Este documento reemplaza en la práctica a `GUIA-COMPLETA-QUEST3-BAMBU.md` e
`INTEGRACION-COMPLETA-REALIZADA.md` (generados antes en la misma sesión de hoy), que describen
una versión del protocolo con errores que nunca hubiera funcionado contra una impresora real.
Es también un complemento al análisis `analisis-exportacion-3d-impresion.md`: ese documento
asumía un flujo "exportar STL → USB a PC → Bambu Studio → imprimir"; esta sección describe el
flujo final realmente implementado, que envía el archivo directo por WiFi desde el Quest 3.

---

## 1. Qué cambió respecto a la versión anterior

La implementación previa (de antes de esta sesión) tenía tres errores de protocolo que la
hubieran hecho fallar siempre contra una impresora Bambu Lab real, más una limitación física
que ningún código puede resolver:

| Problema | Versión anterior (rota) | Versión corregida (actual) |
|---|---|---|
| Usuario MQTT | `"bic"` | `"bblp"` (fijo, así lo exige el firmware de Bambu Lab) |
| Seguridad MQTT | Sin TLS (`MqttSslProtocols.None`) | TLS 1.2 sobre puerto 8883 (obligatorio) |
| Topic MQTT | Wildcard `"+"` para publish | `device/{SERIAL}/report` (subscribe) y `device/{SERIAL}/request` (publish) con el Serial Number real de la impresora |
| Transferencia de archivo | Base64 truncado embebido en un mensaje MQTT | FTPS real (puerto 990, TLS implícito) con cliente propio (`BambuLabFTPS.cs`) |
| Formato que puede imprimir la impresora | Se asumía que aceptaba el STL crudo | **Las impresoras Bambu Lab no imprimen STL directamente** — necesitan G-code ya laminado (slicing) |

El protocolo se verificó contra la documentación de referencia de la comunidad
(https://github.com/Doridian/OpenBambuAPI) antes de escribir el código.

## 2. La limitación de slicing (por qué no se puede evitar)

Ninguna impresora Bambu Lab ejecuta un STL directamente: necesita G-code, que resulta de
"laminar" (slice) el modelo 3D — decidir capa por capa, velocidades, soportes, temperatura, etc.
Esa lógica vive en el slicer (Bambu Studio), no en el firmware de la impresora ni en el
protocolo de red. Implementar un slicer dentro de la app de Unity/Quest está fuera de alcance
(es un problema de ingeniería en sí mismo, similar en tamaño a todo el resto del proyecto).

**Decisión tomada (confirmada por Alan):** documentar la limitación y resolverla con un paso
manual de una sola vez por objeto, no automatizarla. El flujo queda así:

```
Primera vez que se imprime un objeto:
  Quest 3 exporta el STL → PC (una vez) abre Bambu Studio → lamina → guarda como
  {nombre_objeto}.gcode.3mf en una carpeta fija → LISTO, no se repite para ese objeto

Cada vez después (para ese mismo objeto):
  Quest 3 detecta que ya existe el .gcode.3mf → lo sube por FTPS directo a la impresora →
  envía comando de impresión por MQTT → sin PC, sin Bambu Studio, sin intervención manual
```

Esto es exactamente lo que implementa `CrudPanelController.EnviarABambuLab()`.

## 3. Estructura de carpetas (en el PC de Alan, vía sincronización de la impresora o similar)

```
Documents/InmobiliariaVR_Exports/
├── STL_para_slicear/          ← el STL crudo cae acá si todavía no fue laminado
│   └── {nombre_objeto}.stl
└── Listos_para_imprimir/      ← acá va el resultado de laminar en Bambu Studio
    └── {nombre_objeto}.gcode.3mf
```

El nombre base (`{nombre_objeto}`) sale de `NombreLegible(objeto)` con espacios reemplazados
por guiones bajos, así que el nombre del `.gcode.3mf` que hay que guardar en Bambu Studio debe
coincidir exactamente con eso para que la app lo detecte.

## 4. Qué necesita el usuario ahora en el formulario de configuración

El formulario (`BambuLabConfigPanel`, generado por `BambuLabConfigFormBuilder`) ahora pide
**tres** valores, no dos:

1. **IP** de la impresora en la red local (ej. `192.168.1.100`)
2. **Access Code**: en la pantalla de la impresora → Configuración → Red → WLAN (código
   alfanumérico, longitud variable según el modelo — ya no se asume que son 5 dígitos)
3. **Serial Number / Device ID** (nuevo campo, obligatorio): en la pantalla de la impresora →
   Configuración → Acerca de. Es imprescindible porque los topics MQTT lo necesitan literal, un
   wildcard no sirve.

## 5. Archivos modificados o creados en esta sesión

Todos ya están escritos directamente en el proyecto real de Alan vía el puente a su
computadora (no quedaron solo como archivos para copiar a mano):

- `Assets/Plugins/M2Mqtt/**` (44 archivos, nuevo) — código fuente de M2Mqtt (cliente MQTT),
  tomado de `github.com/gpvigano/M2MqttUnity` porque el repo oficial de Eclipse Paho no se puede
  instalar como paquete UPM (no tiene `package.json`).
- `Assets/Scripts/SceneGeneration/BambuLabMQTT.cs` — reescrito completo: conexión MQTT+TLS real,
  ya no tiene el patrón singleton destructivo que podía borrar el GameObject del panel de
  configuración.
- `Assets/Scripts/SceneGeneration/BambuLabFTPS.cs` (nuevo) — cliente FTPS implícito (TLS) escrito
  a mano con `TcpClient` + `SslStream`, porque `FtpWebRequest` de .NET no soporta FTPS implícito.
- `Assets/Scripts/SceneGeneration/BambuLabConfigPanel.cs` — agregado el campo Serial Number,
  validaciones corregidas (Access Code ya no exige 5 dígitos fijos), expone `BambuMqtt` para que
  `CrudPanelController` no cree su propia instancia.
- `Assets/Scripts/SceneGeneration/CrudPanelController.cs` — eliminada la creación de una segunda
  instancia "fantasma" de `BambuLabConfigPanel`/`BambuLabMQTT` (ver bug crítico abajo);
  `EnviarABambuLab()` reescrito para el flujo de detección de `.gcode.3mf`.
- `Assets/Scripts/SceneGeneration/BambuLabConfigFormBuilder.cs` — agrega el campo Serial Number
  al formulario generado; envuelto en `#if UNITY_EDITOR` (ver nota técnica abajo).
- `ProjectSettings/ProjectSettings.asset` — agregado el símbolo de compilación `SSL` (necesario
  para que M2Mqtt compile su código TLS) y activado `ForceInternetPermission` (permiso de
  Internet en Android, necesario para MQTT/FTPS).

## 6. Bug crítico encontrado y corregido: instancia fantasma del panel

`CrudPanelController.BuildUI()` creaba su **propia** copia de `BambuLabConfigPanel` y
`BambuLabMQTT` sobre un GameObject sin UI (`AddComponent` sobre "CrudPanelManager"), totalmente
separada del panel real con inputs y botones que crea `BambuLabConfigFormBuilder` bajo el
Canvas. Combinado con el patrón singleton que tenía `BambuLabMQTT.Awake()` (que hacía
`Destroy(gameObject)` — el GameObject completo, no solo el componente — al detectar una segunda
instancia), esto podía **borrar el panel de configuración real** la primera vez que corrieran
los `Awake()` en el orden "equivocado" (Unity no garantiza orden entre objetos distintos). Y aun
sin ese borrado, el botón "⚙️ Bambu Config" hubiera lanzado una `NullReferenceException` porque
la instancia fantasma nunca tiene `panelBackground` asignado.

Se corrigió eliminando la creación fantasma, quitando el singleton destructivo de
`BambuLabMQTT.Awake()`, y agregando una búsqueda perezosa (`FindAnyObjectByType`, cacheada) del
panel real desde `CrudPanelController`.

## 7. Verificación realizada (sin Unity Editor abierto)

Unity Editor no estaba corriendo en la máquina de Alan durante esta sesión, así que no fue
posible usar las herramientas de automatización del Editor (MCP for Unity) para compilar de
verdad ni generar la UI en la escena. Como verificación alternativa se instaló el compilador
Mono (`mono-mcs`) en este entorno y se compilaron localmente, con stubs mínimos que imitan las
APIs de `UnityEngine`/`UnityEngine.UI`/`TMPro`/`UnityEditor` que estos scripts usan:

- `BambuLabMQTT.cs` + `BambuLabFTPS.cs` + las 44 fuentes reales de M2Mqtt → compilación exitosa.
- `BambuLabConfigPanel.cs` + `BambuLabConfigFormBuilder.cs` + los dos archivos anteriores, todos
  juntos, con `-define:SSL -define:UNITY_EDITOR` → compilación exitosa (solo warnings menores,
  ningún error).

Un chequeo previo, más tosco (conteo de llaves/paréntesis por script), había marcado a
`BambuLabConfigFormBuilder.cs` con un desbalance de 3 paréntesis de más. Se investigó
compilando ese archivo de verdad con el símbolo `UNITY_EDITOR` definido (el chequeo anterior
había compilado en falso "sin errores" porque, sin ese símbolo, todo el archivo — que está
envuelto en `#if UNITY_EDITOR` — se descartaba antes de siquiera analizarse). Con la compilación
real no aparece ningún error de paréntesis: el desbalance del conteo tosco era un falso positivo
(los caracteres `(`/`)` dentro de comentarios y strings del archivo, como las descripciones en
español, no están necesariamente balanceados entre sí aunque el código sí lo esté). **No hizo
falta ningún cambio en este archivo.**

Esto da bastante confianza de que el código compila, pero no reemplaza abrir el proyecto real en
Unity: `CrudPanelController.cs` (el archivo más grande y más modificado) no se pudo compilar de
verdad porque referencia muchas otras clases del proyecto (`SceneGenerator`, `ToastNotificationUI`,
etc.) que no tiene sentido stubear todas; solo se verificó por revisión manual y balance de
llaves/paréntesis (resultó balanceado: 88/88 llaves, 380/380 paréntesis).

## 8. Pendiente para Alan

1. **Abrir Unity Editor** en el proyecto (esto habilita además las herramientas de automatización
   MCP for Unity, que en esta sesión no pudieron usarse porque el Editor estaba cerrado).
2. Mirar la consola de Unity apenas recompile — si algo de lo de arriba tiene un error real que
   la verificación con Mono no pudo detectar (por ejemplo, un typo en el nombre de un método de
   una clase del proyecto que no se stubeó), va a aparecer ahí.
3. Crear un GameObject vacío en el Canvas, agregarle el script `BambuLabConfigFormBuilder`, y en
   el Inspector usar el menú contextual (⋮) → **"Generar UI Bambu Lab"**. Esto crea todo el panel
   (IP, Access Code, Serial Number, botones) automáticamente.
4. Obtener de la impresora real: IP, Access Code y Serial Number (ver sección 4).
5. Para cada mueble/objeto que se quiera imprimir por primera vez: exportar el STL desde la app,
   abrirlo en Bambu Studio en la PC, laminar, y guardar el resultado como
   `{nombre_objeto}.gcode.3mf` en `Documents/InmobiliariaVR_Exports/Listos_para_imprimir/`
   (ver sección 3 para el nombre exacto). Después de eso, "Imprimir" desde el Quest ya lo envía
   solo, por WiFi, sin PC.

## 9. Segunda ronda: errores reales de compilación en Unity (y su corrección)

Alan abrió Unity Editor y compartió la consola con errores reales. Esto sirvió como la
verificación de verdad que la sección 7 no pudo hacer sin el Editor abierto, y encontró dos
categorías de problemas:

### 9.1 Descubrimiento importante: dos archivos NO tenían la corrección de verdad en disco

Al revisar el estado actual real de `BambuLabConfigPanel.cs` y `BambuLabMQTT.cs` en la máquina
de Alan (no la copia local usada para verificar), resultó que **todavía tenían la versión rota**
descrita en la sección 1 (usuario MQTT `"bic"`, sin TLS, el singleton destructivo con
`Destroy(gameObject)`, sin el método `BambuMqtt`). La sesión anterior había preparado la
corrección pero, por lo visto, nunca llegó a escribirse en el proyecto real -- solo
`BambuLabFTPS.cs` sí estaba correcto. Ya se volvieron a escribir las versiones corregidas de los
dos archivos. Esto es la explicación real de por qué reapareció el bug de la instancia fantasma
(ver 9.3): la corrección de `CrudPanelController.cs` de la sesión anterior tampoco se había
guardado.

### 9.2 `BambuLabConfigFormBuilder.cs`: mezcla de InputField/Text legacy con TMP_InputField/TMP_Text

Error real: `CS0029: Cannot implicitly convert type 'TMPro.TextMeshProUGUI' to
'UnityEngine.UI.Text'`. El formulario generado creaba campos `UnityEngine.UI.InputField`
(legacy) pero les asignaba texto `TextMeshProUGUI` (TextMeshPro) -- son dos sistemas de UI de
texto distintos y no compatibles entre sí (`TextMeshProUGUI` no hereda de `UnityEngine.UI.Text`).
Además `BambuLabConfigPanel.cs` espera justamente `TMP_InputField` en sus campos
`[SerializeField]`, así que aunque hubiera compilado, la asignación por `SerializedObject` habría
fallado en silencio en tiempo real.

**Corrección:** todo el formulario ahora usa `TMP_InputField` (no el `InputField` legacy), y cada
campo tiene DOS objetos de texto separados -- uno para lo que el usuario escribe
(`textComponent`, empieza vacío) y otro para el placeholder (`placeholder`, se oculta solo al
escribir) -- que es como TMP_InputField espera que funcione un placeholder de verdad.

La verificación local con Mono había dado una falsa confianza acá: el stub usado antes modelaba
`TextMeshProUGUI` heredando de `Text` (UnityEngine.UI), lo cual no es así en la API real de
Unity/TextMeshPro. Se corrigió el stub para reflejar la jerarquía real (`TMP_Text` no hereda de
`Text`) y, apenas se corrigió, el mismo compilador local hubiera detectado este error también.
Lección: un stub que simplifica de más puede ocultar justo el tipo de error que se busca atrapar.

### 9.3 Reapareció el bug de la "instancia fantasma" en `CrudPanelController.cs`

Por la razón explicada en 9.1 (el archivo nunca se había corregido de verdad), `CrudPanelController`
seguía creando su propia copia de `BambuLabConfigPanel`/`BambuLabMQTT` con `AddComponent` sobre el
GameObject "CrudPanelManager" (sin UI), separada del panel real. Se volvió a aplicar la misma
corrección descrita en la sección 6 de este documento -- ahora sí confirmada en el archivo
committeado: `CrudPanelController` ya no crea nada, busca perezosamente
(`ObtenerBambuConfigPanel()`, cacheado) el panel real de la escena y llega a la única instancia
de `BambuLabMQTT` a través de `panel.BambuMqtt`.

### 9.4 `Environment.SpecialFolder.Documents` no existe

Error real: `CS0117: 'Environment.SpecialFolder' does not contain a definition for 'Documents'`.
El enum de .NET no tiene un miembro llamado `Documents` -- el nombre correcto es `MyDocuments`.
Este error estaba en dos archivos: `CrudPanelController.cs` (línea con `carpetaBase`) y también en
`ExportPanelUI.cs` (un panel de exportación de una fase anterior, no escrito en esta sesión).
Corregido en ambos.

### 9.5 `ExportPanelUI.cs` refería a dos clases que nunca llegaron a existir

`Model3MFExporter` y `GCodeExporter` no existen en ningún lado del proyecto (`CS0103`/`CS0246`)
-- los botones "Exportar a 3MF" y "Exportar a GCODE" de ese panel llamaban a clases que se
planearon en algún momento pero nunca se implementaron. Esto coincide, además, con la decisión ya
documentada en `analisis-exportacion-3d-impresion.md`: exportar **solo STL** y dejar que Bambu
Studio genere el G-code real (la app no necesita un exportador de G-code propio). Se dejaron esos
dos botones con un mensaje informativo en vez de eliminarlos (por si la UI de la escena ya los
tiene conectados a un `Button.onClick`), en vez de crear clases nuevas que reimplementarían algo
que la decisión del proyecto ya descartó.

### 9.6 Qué NO se tocó

Los warnings de `PlayerFlightController.cs` (`FindObjectsSortMode`/`FindObjectsByType` obsoletos)
y el warning de `Object.FindObjectOfType<T>()` obsoleto no son errores -- no bloquean la
compilación. Se corrigió el de `BambuLabConfigFormBuilder.cs` de paso (cambiado a
`Object.FindAnyObjectByType<Canvas>()`) porque ya se estaba tocando ese archivo, pero no se salió
a corregir warnings sueltos en archivos no relacionados con Bambu Lab.

### 9.7 Estado tras esta ronda

Los 5 archivos corregidos (`BambuLabConfigPanel.cs`, `BambuLabMQTT.cs`,
`BambuLabConfigFormBuilder.cs`, `CrudPanelController.cs`, `ExportPanelUI.cs`) ya están escritos en
el proyecto real de Alan. Los 4 relacionados con Bambu Lab se volvieron a compilar juntos con Mono
(con el stub corregido de TMP_Text) sin errores. `CrudPanelController.cs` y `ExportPanelUI.cs` no
se pudieron recompilar de verdad localmente (dependen de XR Interaction Toolkit y de muchas otras
clases del proyecto que no tiene sentido simular todas) -- quedan verificados por revisión manual
y balance de llaves/paréntesis. La verificación real y definitiva sigue siendo que Alan recompile
en Unity Editor y revise la consola.

## 10. Tercera ronda: el panel "existía" pero el botón nunca lo encontraba

Alan generó el panel correctamente (aparecía en la Hierarchy) pero al presionar "⚙️ Bambu Config"
en el juego seguía diciendo "No se encontró el panel de configuración de Bambu Lab en la escena".

**Causa real:** `BambuLabConfigPanel.Start()` oculta `panelBackground` apenas arranca el juego
(`panelBackground.SetActive(false)`, para que el panel empiece cerrado). El problema es que
`BambuLabConfigFormBuilder` asignaba `panelBackground` al MISMO GameObject que lleva el
componente `BambuLabConfigPanel` -- así que ese `SetActive(false)` desactivaba el GameObject
COMPLETO, incluido el propio componente, desde el primer frame. Y `Object.FindAnyObjectByType<T>()`
sin el parámetro `FindObjectsInactive.Include` **ignora objetos inactivos por default** -- por eso
`CrudPanelController` nunca lo encontraba: no es que el panel no existiera, es que estaba oculto y
la búsqueda no mira objetos ocultos a menos que se le pida explícitamente.

**Corrección (dos partes):**
1. `CrudPanelController.ObtenerBambuConfigPanel()` ahora busca con
   `Object.FindAnyObjectByType<BambuLabConfigPanel>(FindObjectsInactive.Include)` -- esto solo ya
   resuelve el problema con el panel que Alan ya tenía generado, sin necesitar regenerar nada.
2. Además, para que esto no se repita si alguna vez se regenera el panel desde cero:
   `BambuLabConfigFormBuilder` ahora separa la raíz (`BambuLabConfigPanel`, que lleva el
   componente y NUNCA se desactiva a sí misma) de un hijo nuevo llamado `Contenido` (que tiene el
   fondo, textos, inputs y botones, y es lo único que se muestra/oculta). `panelBackground` ahora
   apunta a `Contenido`, no a la raíz.

No hace falta borrar ni regenerar el panel que Alan ya creó -- el fix del punto 1 alcanza para que
funcione tal cual está. El punto 2 es solo para dejar bien hecho el generador de cara al futuro.
