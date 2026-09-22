# Plan: Foto desde el visor, guardado de escenas y catálogo de muebles

**Última actualización:** 2026-09-16
**Estado:** Ejecutado — ver el resumen de qué quedó implementado y qué falta verificar al principio de este documento. El resto del archivo (debajo de la línea `---`) es el plan original, sin tocar, como referencia de las decisiones de diseño.

---

## Resumen de ejecución (2026-09-16)

Se implementaron las 3 funcionalidades del plan, en el orden sugerido (guardado de escenas → galería → catálogo), más la corrección de un problema encontrado en el camino. Todo el código nuevo/modificado ya está commiteado en el proyecto de Unity real (`InmobiliariaVR/Assets/Scripts/...`).

### 0. Corrección encontrada al empezar: `CroquisSceneCompilerController.cs` no estaba realmente actualizado

Al revisar el archivo real en el dispositivo (no una copia en caché de esta sesión) se detectó que la unificación de schema decidida en la sesión anterior ("transforma ese backend...") se había documentado como hecha pero el archivo de Unity seguía con la versión vieja (schema `class_label`/`prefab_id`, 5 Transforms fijos `sofa/mesa/tv/cama/silla`, sin `servidorIP` configurable). Se corrigió ahora: el script quedó con `servidorIP`/`servidorPuerto`/`endpoint` configurables (con `PlayerPrefs` para recordar la IP entre sesiones), apuntando a `POST /api/v1/compilar-sala`, y la respuesta del servidor va directo a `SceneGenerator.GenerateSceneAsync(json)` — mismo camino que un preset o una escena guardada. Los campos viejos (`sofa/mesa/tv/cama/silla`) quedan de solo lectura/compatibilidad, marcados `[LEGACY]`, sin usarse.

### 1. Guardado de escenas — ✅ implementado

- `SceneGraphSaveSystem.cs` (nuevo): guarda/carga/lista/borra escenas como JSON en `Application.persistentDataPath/Escenas/`, con el mismo schema de Scene Graph de siempre. No usa SQLite ni servidor.
- `SceneGenerator.cs`: se agregó `ExportCurrentSceneToJson(nombre)`, que recorre el estado ACTUAL de los objetos en escena (posición/rotación/color reales, no el JSON original) y arma el Scene Graph JSON — el "exportador inverso" que pedía el plan. También se agregó `GeneratedObjects` (accessor de solo lectura) y `RegisterExternalObject(obj)` para que objetos agregados fuera del pipeline normal (ej. desde el catálogo) cuenten en el guardado/exportado.
- `SceneSaveLoadController.cs` (nuevo): UI completa — un botón flotante "💾 Mis Escenas" (mismo patrón que el resto de menús del proyecto) que abre un panel con un botón "Guardar escena actual" (nombra automático por fecha/hora, sin pedir texto por teclado) y una lista con botones "Cargar"/"Borrar" por cada escena guardada. Cargar reutiliza `SceneGenerator.GenerateSceneAsync`, igual que un preset.
- Esto también resuelve "cambiar de escena": elegís un preset, generás desde croquis, o cargás una guardada — las tres opciones usan el mismo `GenerateSceneAsync` y quedan disponibles al mismo tiempo.

### 2. Selección desde galería (Fase 1 del plan) — 🟡 implementado pero NO verificado

En vez del plugin de Asset Store NativeGallery (no se pudo descargar: el acceso a GitHub estaba bloqueado en esta sesión), se escribió un plugin nativo Android propio, más simple porque el proyecto ya apunta a `minSdk 34` (Android 13+): el *Photo Picker* del sistema (`MediaStore.ACTION_PICK_IMAGES`) no pide ningún permiso de almacenamiento.

- `Assets/Plugins/Android/GalleryPickerActivity.java` (nuevo): una Activity que reemplaza a la Activity principal de Unity, solo para poder recibir el resultado del picker nativo (`onActivityResult`) y copiar la imagen elegida a un archivo temporal legible antes de avisarle a Unity.
- `Assets/Plugins/Android/AndroidManifest.xml` (nuevo): declara esa Activity como la principal.
- `CroquisSceneCompilerController.cs`: la rama Android de `CargarCroquisDesdeArchivo()` ahora llama al plugin nativo (antes decía literalmente "no disponible en un build").

**Riesgos reales, sin verificar (no hay Android SDK/Gradle en este entorno para compilar, y el Editor de Unity tampoco estuvo abierto/conectado en ningún momento de esta sesión):**

1. Este manifest **no hace nada solo, hasta que en Player Settings > Publishing Settings tildes "Custom Main Manifest"** — es intencional, así no se activa sin que lo revises primero.
2. Asume que el proyecto usa la Activity "Classic" de Unity, no "Game Activity" (Player Settings > Android > Configuration > Activity Type). Si tenés tildado Game Activity, este plugin no va a andar y hay que adaptarlo — avisame qué tenés configurado y lo ajusto.
3. Nunca se compiló ni se probó en un dispositivo real. Antes de confiar en esto, generá el proyecto Gradle (Build Settings > Export Project) y fijate que compile, o hacé una build de prueba en el Quest.
4. La Fase 2 del plan (sacar foto en vivo con la cámara passthrough del Quest 3, vía Meta XR Core SDK) sigue **sin implementar**, tal cual se había decidido dejar para el final por depender de un SDK nuevo — no se tocó nada de eso.

### 3. Catálogo de muebles — ✅ implementado (con un pendiente: las miniaturas se generan aparte)

- `PrefabMapper.cs`: se agregaron `GetTopLevelCategories()` (lista las categorías de nivel superior del `PrefabDatabase`, ej. "Sofas", "Kitchen", "Bathroom") y `GetVariants(categoryKey)` (todas las variantes de una categoría). También se agregó un *fallback*: si el "type" no está en el vocabulario corto de YOLO (sofa/mesa/cama/...), se prueba como key directa del `PrefabDatabase` — necesario porque el catálogo guarda como "type" la key real de la categoría, y ese mismo valor hay que poder re-leerlo al cargar una escena guardada.
- `SceneGenerator.cs`: se extrajo la interactividad por-objeto a un método público reusable, `HacerInteractivo(GameObject obj, string type)`, que ahora usan tanto `GenerateSceneAsync` (muebles del JSON) como el catálogo (muebles agregados a mano).
- `FurnitureCatalogController.cs` (nuevo): un botón flotante "🛋 Catálogo" (mismo patrón que el resto de menús) que abre un panel con una pestaña por categoría del `PrefabDatabase` y, dentro, una grilla de botones (uno por variante). Al elegir uno, se instancia frente al usuario, se registra en el `SceneGenerator` y queda completamente interactivo (mover/rotar/color/borrar), listo para guardarse con el sistema de guardado de arriba.
- `FurnitureThumbnailGenerator.cs` (nuevo, Editor-only): `Menu > InmobiliariaVR > Generar Thumbnails de Muebles` — genera un PNG por cada prefab único usando el generador de previews nativo de Unity (`AssetPreview.GetAssetPreview`), y los deja en `Assets/Resources/FurnitureThumbnails/`. **Pendiente: correr este menú una vez desde el Editor** (no se pudo ejecutar en esta sesión porque el Editor no estuvo conectado) — hasta entonces, el catálogo funciona igual pero muestra un botón liso con el nombre del mueble en vez de una imagen.
- `SETUP_SceneGeneration.cs`: se agregaron los pasos para crear automáticamente `FurnitureCatalogController` y `SceneSaveLoadController` (mismo patrón anti-duplicados que ya tenía para `SceneGenerator`/`PresetLoaderController`).

### Cómo probar todo esto (para cuando abras el Editor)

1. `Menu > InmobiliariaVR > Build Prefab Database` (si no lo corriste después de agregar muebles nuevos).
2. `Menu > InmobiliariaVR > Generar Thumbnails de Muebles` (nuevo — para que el catálogo tenga imágenes).
3. `Menu > InmobiliariaVR > Setup Scene Generation` (ahora también crea los managers del catálogo y de guardado).
4. Play: deberías ver 2 botones flotantes nuevos además del menú de presets: "🛋 Catálogo" y "💾 Mis Escenas".
5. Para el picker de galería en Android, hace falta una build real en el Quest (no se puede probar desde el Editor) — ver los riesgos listados arriba antes de intentarlo.

### Preguntas que quedaron abiertas (del plan original, todavía sin responder)

Las 4 preguntas de la sección "Dudas que me quedaron" al final de este documento ya no bloquean nada (se tomaron decisiones razonables por default: catálogo como botón flotante independiente, todas las categorías del Furniture Mega Pack incluidas, nombres de escena por fecha/hora sin miniatura). Si preferís algo distinto en cualquiera de esos puntos, es un cambio chico sobre lo ya construido.

---

## Documento original (plan, previo a la ejecución)

**Última actualización:** 2026-09-16
**Estado:** Plan a revisar — nada de esto está implementado todavía, salvo lo que ya existía (ver cada sección).

Este documento junta las 3 funcionalidades nuevas que pediste agregar al flujo del Quest 3, con una propuesta de cómo construirlas, en qué orden, y qué depende de qué. Todo está pensado para enchufarse sobre lo que ya existe (`SceneGenerator`, `PrefabDatabase`, `CroquisSceneCompilerController`) sin duplicar lógica.

---

## 1. Sacar foto / elegir imagen desde el visor

Hoy el botón "Cargar Croquis..." (`CroquisSceneCompilerController.CargarCroquisDesdeArchivo()`) solo funciona en el Editor (`EditorUtility.OpenFilePanel` no existe en un build de Android). En el Quest instalado, ese botón hoy no hace nada. Esto se resuelve en dos fases independientes — se puede entregar la Fase 1 sola y dejar la Fase 2 para después.

### Fase 1 — Elegir foto de la galería del visor (bajo riesgo, primero)

Cubre tu punto 2: "que salga de la galería del visor, si se sacó foto o si quiere el usuario [cualquier imagen]".

- **Qué hace falta:** un selector de archivos nativo de Android, porque `OpenFilePanel` de Unity no anda en un build. La forma estándar y más rápida es el plugin de Asset Store **"Native Gallery for Android & iOS"** (yasirkula, gratuito) — expone algo como `NativeGallery.GetImageFromGallery(callback)`, que abre el picker nativo de imágenes de Android (el mismo que usa cualquier app del Quest) y devuelve el path del archivo elegido. Es Android puro, no depende de nada específico de Meta, así que no debería chocar con OpenXR ni con los paquetes de XR ya instalados.
- **Cambio en código:** en `CroquisSceneCompilerController.cs`, reemplazar el bloque `#if UNITY_EDITOR / #else` de `CargarCroquisDesdeArchivo()` por la llamada al plugin cuando **no** estemos en el Editor, leyendo los bytes del archivo elegido igual que ya hace con `File.ReadAllBytes(ruta)`. El resto del flujo (`EnviarCroquis` → backend → `SceneGenerator`) no cambia nada.
- **Efort estimado:** bajo (1 plugin + ~15 líneas de código nuevas). Es lo primero que recomiendo hacer.

### Fase 2 — Sacar foto en vivo con la cámara del Quest 3 (más esfuerzo, depende de Meta)

Cubre la parte de "sacar foto" en el momento, con el visor puesto.

- **Por qué es más complejo:** el Quest 3 no tiene una "app de Cámara" del sistema que guarde fotos en una galería como un celular — el acceso a la cámara de passthrough es una API que cada app tiene que pedir explícitamente (**Passthrough Camera API**, específica de Meta, solo existe en Quest 3/3S, no en Quest 2). Esto es distinto de OpenXR genérico: requiere agregar el **Meta XR Core SDK** al proyecto (paquete separado de `com.unity.xr.openxr`), pedir el permiso de Android `HEADSET_CAMERA` en tiempo de ejecución (el usuario tiene que aceptarlo con el visor puesto, como cualquier permiso de Android), y que el Quest tenga una versión de Horizon OS reciente.
- **Qué habría que construir:**
  1. Agregar el SDK de Meta al proyecto (además de OpenXR — hay que revisar que convivan bien con los paquetes de Android XR que ya están instalados, que son de Google y no de Meta).
  2. Pedir permiso de cámara en runtime.
  3. Una pantalla simple de "encuadre" (un marco en el HUD + un botón de "Capturar", enganchado al gatillo del control) que muestre el feed de la cámara passthrough.
  4. Capturar un frame como `Texture2D`, convertirlo a PNG, y de ahí en adelante usar el mismo camino que ya existe (`EnviarCroquis`).
- **Efort estimado:** medio/alto — es una integración nueva con un SDK que el proyecto no usa todavía. Recomiendo dejarla para después de tener la Fase 1 y el resto del flujo probado en el visor real.

### Menú de entrada unificado

Para que las dos fases (y el punto "elegir un default") vivan en un solo lugar en vez de estar repartidas como hoy (el panel de presets por un lado, la pestaña "Croquis" por otro), la idea es un único panel con 3 botones:

> **"Elegir sala predeterminada" | "Sacar foto" | "Elegir de la galería"**

Los 3 primeros dos ya casi existen (presets + croquis), falta juntarlos en una sola pantalla y agregar la Fase 1/2 de arriba.

---

## 2. ¿Se puede guardar escenas en una base de datos dentro del propio visor?

**Sí, es totalmente posible y no hace falta ningún servidor para esto** — el Quest tiene su propio almacenamiento interno por app (`Application.persistentDataPath`), que persiste entre sesiones (se borra solo si desinstalás la app).

Lo interesante es que **el proyecto ya tiene una primera versión de esto, sin usar**: `Assets/Scripts/SaveSystem/DesignLayoutData.cs` + `SaveDataSerializer.cs`, que guarda/carga un diseño como JSON local. El problema es que quedó pensado para el sistema viejo (una lista de muebles con `modelId` genérico + índice de material), no para el schema nuevo de `SceneGenerator` (`scene_elements[].type/position/rotation/scale/material`). Hay que actualizarlo, no crearlo de cero.

### Propuesta

- **No hace falta SQLite ni una base de datos "de verdad".** Con la cantidad de escenas que un usuario va a guardar (unas pocas, no miles), archivos JSON simples alcanzan y son mucho más simples de mantener que meter una dependencia nativa de SQLite (que además complica el build de Android con librerías `.so` extra).
- Cada escena guardada = un archivo `Application.persistentDataPath/Escenas/<nombre>.json`, con el **mismo schema del Scene Graph** que ya usa todo el pipeline (no uno nuevo).
- Hace falta un método nuevo, algo como `SceneExporter.ExportCurrentScene()`, que recorra los objetos actualmente generados (`SceneGenerator.generatedObjects`) y arme el JSON a partir de su estado ACTUAL en la escena (posición/rotación/color actuales — no el JSON original que vino del backend), para que guardar realmente capture los cambios que hizo el usuario (muebles movidos, recoloreados, agregados o borrados). Es literalmente el proceso inverso de `SceneGenerator.GenerateSceneAsync()`.
- Con esto guardado, "cambiar de escena" (lo que preguntaste en la conversación anterior) se resuelve solo: el menú de cambiar escena lista los archivos de `Escenas/` (con `SaveDataSerializer.GetSavedLayouts()`, que ya existe) y al elegir uno se llama a `SceneGenerator.GenerateSceneAsync()` con ese JSON — mismo camino que ya usan los presets y el croquis.

**Efort estimado:** medio-bajo. La infraestructura de guardado en disco ya existe (`SaveDataSerializer`), solo hay que adaptarla al schema nuevo y agregar el "exportador" que lee el estado actual de la escena.

---

## 3. Catálogo de muebles para agregar a la sala

Esto es una funcionalidad nueva — hoy el `PrefabDatabase` tiene todos los muebles indexados y disponibles, pero no hay ningún menú para que el usuario elija uno y lo agregue a mano.

### Cómo lo entendí de tu descripción

- Se abre con un botón (con las manos/control del visor) — un menú flotante en el mundo, con **pestañas por categoría** (Sofás, Mesas, Camas, Sillas, Placares, Cocina, Baño, etc. — las mismas categorías que ya existen en el Furniture Mega Pack / `PrefabDatabase`).
- Dentro de cada pestaña, una grilla de botones, cada uno con una **imagen de referencia** del mueble.
- Al tocar un mueble, aparece **enfrente del usuario**, para que lo mueva a mano con el menú de mover/rotar que ya existe.

### Piezas que hacen falta construir

1. **Miniaturas de cada mueble.** Hoy no existe ninguna imagen de preview generada. La forma más práctica: extender la herramienta de Editor que ya existe (`PrefabDatabaseBuilder.cs`, el menú "InmobiliariaVR > Build Prefab Database") para que, además de armar la base de datos, genere una miniatura PNG de cada prefab (renderizando cada uno en una mini escena de preview con cámara + luz, tipo "estudio fotográfico" — es una técnica estándar de Unity) y las guarde en `Assets/Resources/FurnitureThumbnails/`. Se corre una sola vez (y de nuevo cada vez que se agreguen muebles nuevos al pack).
2. **Panel de catálogo con pestañas.** Un Canvas World Space nuevo (mismo patrón que ya usan `ManipulatorMenuFactory` y `PresetLoaderController`), con una pestaña por categoría y, dentro, una grilla de botones armada dinámicamente a partir de `PrefabDatabase.entries` (ya tiene la lista completa, no hay que hardcodear nada).
3. **Instanciar y conectar el mueble elegido.** Al tocar un botón del catálogo: instanciar ese prefab enfrente del jugador (mismo cálculo de posición que ya usa `PresetLoaderController.PositionInFrontOfCamera`), y conectarle la misma interactividad que ya tienen los muebles generados por croquis (mover/rotar/color/borrar). Para no duplicar esa lógica, conviene sacar el bloque de `SceneGenerator.BindInteractivity()` que arma esto por-objeto a un método reusable (algo como `SceneGenerator.HacerInteractivo(GameObject obj, string tipo)`), y que tanto la generación por JSON como el catálogo lo llamen igual.
4. **Botón para abrir el catálogo.** Puede ser una pestaña más del menú principal que ya existe (`SideMenuVR`), consistente con cómo están armadas "Personalizar"/"Pintar"/"Croquis" hoy — evita depender de gestos de mano libre (hand tracking), que el proyecto no usa en ningún otro lado todavía y sería un riesgo técnico nuevo sin probar.

**Efort estimado:** medio — la parte más nueva es la generación de miniaturas (paso 1) y el panel con pestañas (paso 2); el resto reusa piezas que ya existen.

---

## 4. Orden sugerido para encarar esto

1. **Guardado de escenas** (sección 2) — bajo esfuerzo, y destraba "cambiar de escena" que ya habías pedido antes.
2. **Selección desde galería** (sección 1, Fase 1) — bajo esfuerzo, resuelve la mitad de "mandar imagen" ya en el visor real.
3. **Catálogo de muebles** (sección 3) — esfuerzo medio, es la funcionalidad más nueva.
4. **Captura de foto en vivo con la cámara del Quest 3** (sección 1, Fase 2) — se deja para el final por ser lo que más depende de SDKs nuevos (Meta) y de verificar que convivan con lo que ya está instalado.

Esto además coincide con ir de menor a mayor riesgo técnico, así que si el tiempo aprieta antes de la entrega, lo importante queda cubierto primero.

---

## Dudas que me quedaron

1. Para el botón del catálogo — ¿lo agrego como una pestaña más del menú que ya existe (Recorrido/Personalizar/Pintar/Croquis/**Catálogo**), o preferís un botón físico aparte en la sala?
2. ¿Las categorías del Furniture Mega Pack que ya existen (Sofás, Mesas, Camas, Sillas, Placares, Cajones, Cocina, Baño) alcanzan tal cual, o hay alguna que no quieras mostrar en el catálogo?
3. Para el guardado de escenas — ¿alcanza con una lista simple de nombres (texto), o te gustaría que cada escena guardada muestre también una miniatura de cómo quedó?
4. Para la Fase 2 de la cámara (sección 1) — ¿confirmamos que se puede dejar para el final, o es un requisito que tiene que estar sí o sí para la entrega del parcial?
