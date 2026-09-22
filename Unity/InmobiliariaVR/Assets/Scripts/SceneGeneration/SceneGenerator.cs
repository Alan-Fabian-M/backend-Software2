using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.XR.CoreUtils;
using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json.Linq;

/// <summary>
/// Main orchestrator for scene generation from JSON data.
/// Coordinates validation, object instantiation, transform application, and interactivity binding.
/// Supports both preset loading and croquis-based generation with progress tracking.
///
/// También permite registrar objetos "externos" (agregados en runtime desde el catálogo de
/// muebles) para que participen del guardado/exportado de la escena, y exportar el estado
/// actual de la escena de vuelta a JSON (mismo schema que se recibe del backend/presets) para
/// guardarla localmente con SceneGraphSaveSystem.
/// </summary>
public class SceneGenerator : MonoBehaviour
{
    [System.Serializable]
    public class GenerationConfig
    {
        public bool showWireframes = true;
        public float wireframeDuration = 0.3f;
        public float fadeInDuration = 0.5f;
        public bool progressiveInstantiation = true;
        public Transform instantiationParent;
    }

    public delegate void OnGenerationProgressDelegate(int current, int total, string message);
    public delegate void OnGenerationCompleteDelegate(bool success, string message);
    public delegate void OnGenerationErrorDelegate(string errorMessage);

    public event OnGenerationProgressDelegate OnProgress;
    public event OnGenerationCompleteDelegate OnComplete;
    public event OnGenerationErrorDelegate OnError;

    private GenerationConfig config;
    private List<GameObject> generatedObjects = new List<GameObject>();
    private bool isGenerating = false;

    /// <summary>
    /// Lista de solo lectura de todos los objetos actualmente en la escena generada
    /// (incluye tanto los que vinieron del JSON original como los agregados luego desde
    /// el catálogo de muebles vía RegisterExternalObject). Usado por ExportCurrentSceneToJson
    /// y por cualquier UI que necesite saber cuántos/cuáles objetos hay.
    /// </summary>
    public IReadOnlyList<GameObject> GeneratedObjects => generatedObjects;

    // Menú de mover/rotar compartido por todos los muebles generados (ver ManipulatorMenuFactory)
    private FurnitureManipulatorVR sharedManipulator;

    // Nombre del GameObject raíz que agrupa la sala/muebles fijos y hardcodeados de la escena
    // Sala_MVP (ver "Sala" en Sala_MVP.unity: Piso, Sofa, Mesa, TV y las paredes originales).
    // No se instancia dinámicamente ni lo conoce ClearPreviousScene, así que sin este apagado
    // explícito queda superpuesto (y a veces literalmente adentro) de cualquier preset o
    // croquis que se genere después, dando el efecto de "la sala vieja nunca desaparece".
    private const string STATIC_ROOM_ROOT_NAME = "Sala";
    private bool staticRoomHidden = false;

    // Un multiplicador fijo (probado antes: 0.11, para compensar el hijo "_model" a escala 220
    // que traen horneados los prefabs de Assets/Furniture Mega Pack) resultó ser una
    // sobrecorrección: los muebles quedaron chicos comparados con el jugador. En vez de seguir
    // adivinando un número a ciegas (no tengo forma de abrir el Editor para medirlo a ojo),
    // este enfoque MIDE el tamaño real del mueble ya instanciado (Renderer.bounds) y lo
    // reescala para que su ancho/profundidad coincida con un tamaño realista aproximado en
    // metros -- así se autocorrige sea cual sea la escala que el prefab tenga horneada adentro.
    // Los valores son estimaciones de sentido común (no medidos contra el pack real); ajustalos
    // si algún tipo en particular todavía se ve mal.
    private static readonly Dictionary<string, float> FURNITURE_TARGET_FOOTPRINT_M = new Dictionary<string, float>
    {
        { "sofa", 2.0f },
        { "mesa", 1.0f },
        { "cama", 2.0f },
        { "silla", 0.55f },
        { "estanteria", 1.0f },
        { "armario", 1.0f },
        { "tv", 1.0f },
        { "escritorio", 1.2f },
        { "cajon", 0.6f },
        { "cojin", 0.45f },
        { "refrigerador", 0.75f },
        { "horno", 0.6f },
        { "microondas", 0.5f },
        { "estufa", 0.6f },
        { "bano_completo", 1.5f },
        { "bañera", 1.6f },
        { "inodoro", 0.4f },
        { "lavabo", 0.5f }
    };
    private const float FURNITURE_TARGET_FOOTPRINT_DEFAULT_M = 1.0f;

    // Dimensiones (X=ancho, Z=profundidad) de la última sala generada, en metros. Se usan para
    // que un mueble movido a mano no termine atravesando una pared: la sala generada siempre
    // ocupa desde (0,0) hasta (roomWidth, roomDepth) en el plano XZ (mismo supuesto que ya usaba
    // RepositionPlayerInRoom para ubicar al jugador adentro). Default 4x4m si no hay una escena
    // generada todavía (ej. agregando desde el catálogo antes de elegir una sala).
    private float roomWidth = 4f;
    private float roomDepth = 4f;

    // "Matriz"/grilla de ayuda para colocar muebles: al soltar un mueble movible, su posición
    // X/Z se redondea a pasos de esta distancia y su rotación (eje Y) a pasos de este ángulo,
    // para que quede prolijo en vez de en cualquier posición/ángulo arbitrario.
    private const float PLACEMENT_GRID_SIZE_M = 0.25f;
    private const float PLACEMENT_ROTATION_STEP_DEG = 15f;

    // Cuarta ronda (2026-09-19, pedido de Alan: "quisiera que amplies mas el tema de la matriz
    // ampliarlo a todo el mapa ya que asi poder sacarlo fuera de la cada sin problemas"): antes la
    // grilla/matriz y el clamp de FinalizePlacement solo cubrían el rectángulo exacto de la sala
    // (0,0)-(roomWidth,roomDepth), así que un mueble no se podía soltar fuera de esas paredes. Este
    // margen extiende AMBOS (la grilla dibujada en el piso y los límites de colocación) más allá de
    // la sala en las 4 direcciones, para poder sacar muebles fuera de la casa sin que se claven
    // clampeados en el borde. 15m es un valor supuesto/de arranque -- avisar si Alan quiere más o
    // menos área "afuera".
    private const float MARGEN_MATRIZ_FUERA_SALA_M = 15f;

    // Dibujo de la grilla de arriba en el piso, visible solo mientras se sostiene un mueble (ver
    // ShowFloorGrid/HideFloorGrid, enganchados al selectEntered/selectExited del
    // XRGrabInteractable en HacerInteractivo).
    private GameObject floorGridVisual;
    private float gridBuiltForWidth = -1f;
    private float gridBuiltForDepth = -1f;

    // --- Huella del mueble en el piso (punto 3 del plan de edición avanzada, 2026-09-17) ---
    // Rectángulo dibujado en el piso mientras se sostiene un mueble, mostrando exactamente el
    // espacio que va a ocupar (respeta la rotación actual: si el mueble está en diagonal, el
    // rectángulo también queda en diagonal). Es puramente visual/orientativo -- se pone rojo si
    // se superpone con un muro o con otro mueble ya colocado, pero eso NO bloquea soltar el
    // mueble ahí igual; es solo un aviso para el usuario.
    private GameObject footprintOutlineVisual;
    private LineRenderer footprintLineRenderer;
    private Material footprintMaterialGreen;
    private Material footprintMaterialRed;
    private GameObject currentlyHeldForFootprint;
    // Medio-ancho/medio-profundidad del mueble sostenido actualmente, en su propio espacio local
    // (calculado una vez al agarrarlo, no en cada frame -- ver ComputeLocalFootprint), más el
    // desfasaje de su centro real respecto al pivot (algunos prefabs traen el mesh descentrado).
    private Vector2 heldFootprintHalfExtents;
    private Vector2 heldFootprintLocalCenterOffset;

    /// <summary>
    /// Mide el tamaño real (footprint horizontal X/Z, vía Renderer.bounds) del mueble ya
    /// instanciado y lo reescala para que coincida con un tamaño realista aproximado en metros
    /// según su tipo (FURNITURE_TARGET_FOOTPRINT_M). Reemplaza cualquier escala previa: para
    /// los muebles de presets/catálogo el campo "scale" del JSON en la práctica siempre es
    /// {1,1,1} y no aporta información real de tamaño, así que no tiene sentido combinarlo con
    /// esto. Público y estático para poder llamarse también desde FurnitureCatalogController.
    /// </summary>
    public static void AutoScaleFurnitureToRealSize(GameObject obj, string type)
    {
        string normalizedType = (type ?? "").ToLower().Trim();
        float targetFootprint = FURNITURE_TARGET_FOOTPRINT_M.TryGetValue(normalizedType, out float target)
            ? target
            : FURNITURE_TARGET_FOOTPRINT_DEFAULT_M;
        AutoScaleFurnitureToFootprint(obj, targetFootprint);
    }

    /// <summary>
    /// Igual que AutoScaleFurnitureToRealSize, pero recibe el tamaño objetivo directamente en
    /// vez de buscarlo por tipo (para llamadores que no usan las keys en español de
    /// PrefabMapper, como FurnitureCatalogController con las categorías del PrefabDatabase).
    /// </summary>
    public static void AutoScaleFurnitureToFootprint(GameObject obj, float targetFootprintMeters)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        float currentFootprint = Mathf.Max(combined.size.x, combined.size.z);
        if (currentFootprint <= 0.0001f) return; // bounds inválidos (mesh vacía?): no tocar

        float factor = targetFootprintMeters / currentFootprint;
        obj.transform.localScale *= factor;
    }

    private void Start()
    {
        // Default configuration
        config = new GenerationConfig
        {
            showWireframes = true,
            wireframeDuration = 0.3f,
            fadeInDuration = 0.5f,
            progressiveInstantiation = true,
            instantiationParent = transform
        };

        MakeExistingFloorsInteractive();
    }

    private void Update()
    {
        // Huella visual del mueble sostenido actualmente (ver bloque de campos arriba). Se
        // actualiza cada frame porque la posición/rotación del mueble cambian mientras el
        // usuario lo mueve con la mano.
        if (currentlyHeldForFootprint != null)
            UpdateFootprintOutline(currentlyHeldForFootprint);
    }

    /// <summary>
    /// Main async method to generate a scene from JSON string.
    /// Handles the complete pipeline: validation, instantiation, animation.
    /// </summary>
    public void GenerateSceneAsync(string jsonString, GenerationConfig generationConfig = null)
    {
        if (isGenerating)
        {
            OnError?.Invoke("Scene generation already in progress");
            return;
        }

        if (generationConfig != null)
            config = generationConfig;

        if (config == null)
            config = new GenerationConfig { instantiationParent = transform };

        StartCoroutine(GenerateSceneCoroutine(jsonString));
    }

    private IEnumerator GenerateSceneCoroutine(string jsonString)
    {
        isGenerating = true;
        OnProgress?.Invoke(0, 0, "Validating scene data...");

        // Phase 1: Validation
        var validationResult = SceneValidator.ValidateSceneJSON(jsonString);
        if (!validationResult.isValid)
        {
            string errorMsg = $"Scene validation failed with {validationResult.errors.Count} errors";
            OnError?.Invoke(errorMsg);
            SceneValidator.LogValidationResult(validationResult);
            isGenerating = false;
            yield break;
        }

        // Phase 2: Parse JSON
        JObject sceneData = null;
        try
        {
            sceneData = JObject.Parse(jsonString);
        }
        catch (System.Exception ex)
        {
            OnError?.Invoke($"Failed to parse JSON: {ex.Message}");
            isGenerating = false;
            yield break;
        }

        // Phase 3: Clear previous scene
        OnProgress?.Invoke(0, 0, "Clearing previous scene...");
        ClearPreviousScene();
        yield return new WaitForSeconds(0.1f);

        // Ocultar (una sola vez) la sala/muebles fijos hardcodeados de Sala_MVP.unity, para que
        // no queden superpuestos con lo que se genera dinámicamente a partir de acá.
        HideStaticRoomOnce();

        // Reubicar al jugador (XR Origin) dentro de la sala que se está por generar. La escena
        // Sala_MVP trae al XR Rig en una posición fija (pensada para un layout hardcodeado que
        // ya no existe); si no se corrige acá, con cualquier preset/croquis el jugador termina
        // spawneando afuera de las paredes generadas dinámicamente (del lado de afuera de un
        // muro), lo cual explica que "la sala no dé" / no se sienta como una sala real.
        RepositionPlayerInRoom(sceneData);

        // Phase 4: Extract scene elements
        JArray elements = sceneData["scene_elements"] as JArray;
        if (elements == null || elements.Count == 0)
        {
            OnError?.Invoke("No scene elements found in JSON");
            isGenerating = false;
            yield break;
        }

        OnProgress?.Invoke(0, elements.Count, $"Generating {elements.Count} objects...");

        // Phase 5: Instantiate objects with progressive feedback
        for (int i = 0; i < elements.Count; i++)
        {
            JObject elementData = elements[i] as JObject;
            if (elementData == null) continue;

            string elementId = elementData["id"]?.ToString() ?? $"Element_{i}";
            OnProgress?.Invoke(i + 1, elements.Count, $"Creating {elementId}...");

            GameObject obj = InstantiateSceneElement(elementData);
            if (obj != null)
            {
                generatedObjects.Add(obj);

                // Show wireframe briefly
                if (config.showWireframes)
                {
                    StartCoroutine(ShowWireframePreview(obj));
                    yield return new WaitForSeconds(config.wireframeDuration);
                }
            }

            yield return null; // Spread instantiation across frames
        }

        // Phase 5.5: apoyar los muebles en el piso. Los prefabs del Furniture Mega Pack tienen
        // el pivot horneado en distintos lugares (no siempre en la base del mesh), y el JSON
        // solo manda la posición X/Z del mueble con y=0 (pensado como "nivel de piso") -- por
        // eso, sin esto, algunos muebles quedan flotando en el aire o hundidos en el piso. Se
        // hace con un raycast hacia abajo desde arriba del mueble hasta el primer collider que
        // encuentre (el piso u otro mueble debajo), y se lo sube/baja para que la base de su
        // bounding box quede apoyada justo ahí.
        SnapFurnitureToFloor();

        // Phase 6: Apply animations
        OnProgress?.Invoke(elements.Count, elements.Count, "Applying animations...");
        if (config.progressiveInstantiation)
        {
            yield return StartCoroutine(FadeInAllObjects());
        }

        // Phase 7: Bind interactivity
        OnProgress?.Invoke(elements.Count, elements.Count, "Binding interactivity...");
        BindInteractivity();
        yield return new WaitForSeconds(0.1f);

        // Phase 8: Complete
        isGenerating = false;
        OnProgress?.Invoke(elements.Count, elements.Count, "Scene generation complete!");
        OnComplete?.Invoke(true, $"Successfully generated scene with {generatedObjects.Count} objects");

        // Sin esto, mover/rotar muebles (FurnitureManipulatorVR/MaterialChangerVR) queda
        // bloqueado: ambos exigen AppState.Personalizacion, y el único lugar del proyecto que
        // cambiaba a ese estado era el menú viejo de Sala_MVP (MenuControllerVR/SideMenuVR,
        // que además acabamos de ocultar en HideStaticRoomOnce). Sin este cambio de estado acá,
        // el flujo de presets/croquis nunca podía entrar a Personalizacion y por eso "no se
        // podían mover los muebles" aunque el resto (raycaster, mando, etc.) ya funcionara.
        StateManager.Instance.ChangeState(AppState.Personalizacion);
    }

    /// <summary>
    /// Apaga (una sola vez) la sala/muebles fijos hardcodeados de Sala_MVP.unity (GameObject
    /// raíz "Sala": Piso, Sofa, Mesa, TV, paredes originales, y el menú viejo
    /// MenuControllerVR/SideMenuVR que vivía adentro). Se llama al generar la primera escena
    /// dinámica (preset o croquis) para que no quede superpuesta con la sala vieja.
    /// </summary>
    private void HideStaticRoomOnce()
    {
        if (staticRoomHidden) return;

        GameObject staticRoom = GameObject.Find(STATIC_ROOM_ROOT_NAME);
        if (staticRoom != null)
        {
            staticRoom.SetActive(false);
            staticRoomHidden = true;
            Debug.Log($"SceneGenerator: sala estática '{STATIC_ROOM_ROOT_NAME}' de Sala_MVP ocultada " +
                       "para no superponerse con la sala generada.");
        }
        else
        {
            // Puede que ya esté oculta, o que esta escena no tenga ese objeto (croquis en otra
            // escena base, por ejemplo). No es un error bloqueante.
            staticRoomHidden = true;
        }
    }

    private GameObject InstantiateSceneElement(JObject elementData)
    {
        try
        {
            string type = elementData["type"]?.ToString() ?? "unknown";
            string id = elementData["id"]?.ToString() ?? "unnamed";

            GameObject obj = null;

            // Handle built-in geometry types ("muro_interior" = "Muro divisorio" del catálogo,
            // Sección A -- si una escena guardada lo trae de vuelta, se recrea igual que un muro
            // normal: cubo primitivo, no busca un prefab del Furniture Mega Pack).
            if (type == "muro" || type == "muro_interior" || type == "piso" || type == "puerta" || type == "ventana")
            {
                obj = CreateDynamicGeometry(elementData);
            }
            else
            {
                // Handle prefab-based furniture
                obj = InstantiatePrefab(elementData);
            }

            if (obj != null)
            {
                obj.name = id;

                // Apply transforms
                ApplyTransforms(obj, elementData);

                // Apply materials
                ApplyMaterials(obj, elementData);

                // Store metadata
                var meta = obj.AddComponent<SceneElementMetadata>();
                meta.elementId = id;
                meta.elementType = type;
                meta.jsonData = elementData.ToString();
            }

            return obj;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error instantiating element: {ex.Message}");
            return null;
        }
    }

    private GameObject CreateDynamicGeometry(JObject elementData)
    {
        string type = elementData["type"]?.ToString() ?? "cube";

        // Create primitive cube
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // GameObject.CreatePrimitive asigna el material default que tenga configurado el
        // pipeline (GraphicsSettings > URP Global Settings > Default Material). Si esa
        // referencia está vacía o mal configurada, Unity puede caer al "Default-Material"
        // clásico (shader "Standard", del Built-in Render Pipeline), que NO es compatible con
        // URP y se ve rosa/magenta -- esto explica las "paredes rosadas". Para no depender de
        // esa configuración, se le asigna acá un material propio con el shader de URP
        // explícitamente, garantizando que compile e incluya en el build sin importar cómo
        // esté esa referencia global.
        Renderer primitiveRenderer = obj.GetComponent<Renderer>();
        if (primitiveRenderer != null)
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLitShader != null)
                primitiveRenderer.sharedMaterial = new Material(urpLitShader);
            else
                Debug.LogWarning("SceneGenerator: no se encontró el shader 'Universal Render Pipeline/Lit'. " +
                                  "Los muros/piso pueden verse rosados (shader faltante).");
        }

        // Remove default collider and add a proper one
        Collider col = obj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        obj.AddComponent<BoxCollider>();

        // Set parent
        if (config.instantiationParent != null)
            obj.transform.SetParent(config.instantiationParent);

        return obj;
    }

    private GameObject InstantiatePrefab(JObject elementData)
    {
        string type = elementData["type"]?.ToString() ?? "";
        float confidence = elementData["confidence"]?.Value<float>() ?? 0.85f;

        // Get prefab reference directly from the mapper (backed by PrefabDatabase)
        var prefabInfo = PrefabMapper.ResolvePrefab(type, confidence);

        if (!prefabInfo.isValid || prefabInfo.prefab == null)
        {
            Debug.LogWarning($"Could not resolve prefab for type: {type}");
            return null;
        }

        GameObject obj = Instantiate(prefabInfo.prefab, config.instantiationParent);
        return obj;
    }

    private void ApplyTransforms(GameObject obj, JObject elementData)
    {
        if (elementData.ContainsKey("position"))
        {
            var pos = ParseVector3(elementData["position"]);
            obj.transform.position = pos;
        }

        if (elementData.ContainsKey("rotation"))
        {
            var rot = ParseVector3(elementData["rotation"]);
            obj.transform.eulerAngles = rot;
        }

        string type = elementData["type"]?.ToString()?.ToLower() ?? "";
        bool esEstructural = (type == "muro" || type == "muro_interior" || type == "piso" || type == "puerta" || type == "ventana");

        if (esEstructural)
        {
            // Los muros/piso/puertas/ventanas son cubos primitivos de 1 unidad: la escala del
            // JSON ES directamente su tamaño real en metros, se aplica tal cual.
            if (elementData.ContainsKey("scale"))
                obj.transform.localScale = ParseVector3(elementData["scale"]);

            // BUG reportado por Alan (2026-09-19, tercera ronda: "los muros que hay en la parte de
            // los monoambientes... traspasan el piso y se ven muy cortos"): el pivot de un cubo
            // primitivo de Unity queda en su CENTRO. Si el JSON de la escena manda la posición Y
            // del muro pensando en su BASE (Y=0) en vez de en su centro, la mitad de abajo del
            // muro queda hundida bajo el piso y solo asoma la mitad de arriba -- exactamente
            // "atraviesa el piso y se ve corto" (se ve la mitad de su altura real). Se corrige acá
            // forzando que la BASE de todo "muro"/"muro_interior" quede pegada al piso (Y=0) sin
            // importar qué Y haya mandado el JSON -- un muro ya bien posicionado no cambia (esta
            // cuenta ya da su mismo Y), así que es seguro para los que ya funcionaban bien. No se
            // toca "puerta"/"ventana": esas sí pueden llevar una Y intencional distinta de la base
            // (por ejemplo una ventana a media altura del muro).
            if (type == "muro" || type == "muro_interior")
            {
                Vector3 pos = obj.transform.position;
                pos.y = obj.transform.localScale.y * 0.5f;
                obj.transform.position = pos;
            }
        }
        else
        {
            // Los muebles (prefabs de Furniture Mega Pack) no usan el "scale" del JSON (en la
            // práctica siempre es {1,1,1} y no refleja el tamaño real de cada prefab, que varía
            // según cómo esté armado el pack). En vez de eso, se mide el tamaño real ya
            // instanciado y se reescala a un tamaño realista (ver AutoScaleFurnitureToRealSize).
            // Necesita la position ya aplicada arriba para que Renderer.bounds sea correcto.
            AutoScaleFurnitureToRealSize(obj, type);
        }
    }

    private void ApplyMaterials(GameObject obj, JObject elementData)
    {
        if (!elementData.ContainsKey("material"))
            return;

        var materialData = elementData["material"] as JObject;
        if (materialData == null) return;

        // Get renderer
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            renderer = obj.GetComponentInChildren<Renderer>();

        if (renderer != null && materialData.ContainsKey("color"))
        {
            string colorStr = materialData["color"]?.ToString() ?? "#FFFFFF";
            if (ColorUtility.TryParseHtmlString(colorStr, out Color color))
            {
                Material mat = new Material(renderer.material);
                mat.color = color;
                renderer.material = mat;
            }
        }
    }

    private IEnumerator ShowWireframePreview(GameObject obj)
    {
        // Draw wireframe bounding box
        WireframeDrawer.DrawBounds(obj, Color.green, config.wireframeDuration);
        yield return new WaitForSeconds(config.wireframeDuration);
    }

    private IEnumerator FadeInAllObjects()
    {
        foreach (var obj in generatedObjects)
        {
            if (obj != null)
            {
                StartCoroutine(FadeInAnimator.FadeIn(obj, config.fadeInDuration));
            }
        }

        yield return new WaitForSeconds(config.fadeInDuration);
    }

    /// <summary>
    /// Conecta la interactividad real de cada mueble generado en esta pasada (llamado al final
    /// de GenerateSceneCoroutine). Delega en HacerInteractivo, que también puede llamarse
    /// individualmente para un mueble agregado luego desde el catálogo.
    /// </summary>
    private void BindInteractivity()
    {
        EnsureSharedManipulator();

        foreach (var obj in generatedObjects)
        {
            if (obj == null) continue;

            var metadata = obj.GetComponent<SceneElementMetadata>();
            if (metadata == null) continue;

            HacerInteractivo(obj, metadata.elementType);
        }
    }

    /// <summary>
    /// Conecta la interactividad real (seleccionar/mover/rotar, cambiar color, borrar) de UN
    /// mueble puntual, según lo que PrefabMapper.GetMetadata diga para su tipo. Es idempotente
    /// (usa GetComponent == null antes de agregar cada componente) para poder llamarse tanto
    /// desde BindInteractivity (todos los muebles del JSON) como desde FurnitureCatalogController
    /// (un mueble nuevo agregado en runtime desde el catálogo).
    /// </summary>
    public void HacerInteractivo(GameObject obj, string type)
    {
        if (obj == null) return;

        // Los muros tienen su propio sistema de interacción (Fase 3 del plan de edición
        // avanzada, 2026-09-17: seleccionar con el rayo + panel de pintado con la opción de
        // aplicar el color a todos los muros de la sala o solo a este) en vez del genérico de
        // muebles de abajo -- antes de esto los muros no tenían NINGUNA interactividad
        // (PrefabMapper los marcaba "selectable = false"). Ver MuroEditable/WallPaintPanelController.
        // "muro" = muro exterior/fijo de la sala (JSON/preset): solo seleccionar + pintar/altura.
        // "muro_interior" = "Muro divisorio" agregado desde el catálogo (Sección A): además se
        // puede mover/rotar (paso de 90°) y su longitud es ajustable. Ver CreateInteriorWallDivider.
        string normalizedType = (type ?? "").ToLower().Trim();
        if (normalizedType == "piso")
        {
            HacerPisoInteractivo(obj);
            return;
        }
        if (normalizedType == "muro")
        {
            HacerMuroInteractivo(obj, esDivisorInterior: false);
            return;
        }
        if (normalizedType == "muro_interior")
        {
            HacerMuroInteractivo(obj, esDivisorInterior: true);
            return;
        }
        if (normalizedType.StartsWith("puerta") || normalizedType.StartsWith("ventana") || obj.GetComponent<AberturaInteractivaVR>() != null || obj.GetComponentInChildren<AberturaInteractivaVR>() != null)
        {
            HacerAberturaInteractiva(obj, normalizedType);
            return;
        }

        var furnitureMetadata = PrefabMapper.GetMetadata(type);
        if (!furnitureMetadata.selectable) return;

        // Los prefabs del Furniture Mega Pack no traen Collider (verificado leyendo varios .prefab
        // directamente): sin uno, ningún interactable de abajo puede recibir hover/select del
        // rayo del control -- por eso el mueble "flota" (sin colisión física) y el rayo lo
        // atraviesa como si no estuviera (se achica al pasar detrás, pero nunca selecciona nada).
        EnsureFurnitureCollider(obj);

        XRBaseInteractable interactable;

        if (furnitureMetadata.movable)
        {
            // Agarrar y mover directo con la mano (en vez del menú de flechas "Adelante/Atras/
            // Izquierda/Derecha" que había antes, que además movía el mueble en ejes fijos del
            // mundo en vez de a donde el usuario realmente quisiera). Requiere un Rigidbody:
            // se deja kinemático y sin gravedad porque el "apoyado en el piso" ya lo resuelve
            // SnapToFloor una sola vez al generar/agregar el mueble -- no hace falta simular
            // caída física, y evita que el mueble tiemble o se cuele por un collider imperfecto.
            // Para rotarlo: girar la muñeca mientras se sostiene el grip, como agarrar un objeto
            // real (XRGrabInteractable ya sigue la rotación del control automáticamente).
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb == null) rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            XRGrabInteractable grabInteractable = obj.GetComponent<XRGrabInteractable>();
            if (grabInteractable == null)
            {
                grabInteractable = obj.AddComponent<XRGrabInteractable>();
                grabInteractable.movementType = XRGrabInteractable.MovementType.Kinematic;
                grabInteractable.throwOnDetach = false;

                // Grilla visible en el piso mientras se sostiene el mueble (ayuda visual para
                // ver dónde van a caer los "escalones" de posición al soltarlo), más la huella
                // real del mueble (rectángulo verde/rojo, ver punto 3 del plan de edición
                // avanzada) -- y al soltarlo: si lo soltaste sobre la papelera de la entrada, se
                // pregunta antes de borrar (punto 4); si no, se clampea adentro de la sala, se
                // redondea su posición/rotación a la grilla, y se lo vuelve a apoyar en el piso.
                grabInteractable.selectEntered.AddListener((args) =>
                {
                    ShowFloorGrid();
                    StartFootprintTracking(obj);
                });
                grabInteractable.selectExited.AddListener((args) =>
                {
                    HideFloorGrid();

                    // Ajuste pedido por Alan (2026-09-19, puntos 5 y 8): si este mueble está
                    // siendo manejado por "Edición flotante" o "Edición con objetivo fijo"
                    // (UpdateModesController), agarrarlo con la mano para acomodarlo mejor y
                    // soltarlo NO debe dispararle el clamp-a-los-límites-de-la-sala-real ni el
                    // apoyo forzado en el piso de acá abajo -- eso era lo que hacía que "pareciera
                    // desaparecer" (se re-clampeaba a las coordenadas de la sala real, lejos de
                    // donde estás parado en el escenario aislado) o que "no se pudiera dejar
                    // flotando" (SnapToFloor lo pegaba al piso apenas lo soltabas). El propio modo
                    // especial decide cuándo y cómo terminar (sus botones Confirmar/Cancelar/
                    // Volver).
                    //
                    // BUG encontrado esta ronda ("se pone en verde y no deja de estar en verde,
                    // como si nunca lo soltaras"): la versión anterior de este fix directamente
                    // salía (return) ANTES de llamar a StopFootprintTracking, así que la piel
                    // verde temporal (activada en StartFootprintTracking, al agarrarlo) nunca se
                    // apagaba al soltar -- se quedaba pegada para siempre, y ni siquiera "Volver"/
                    // "Cancelar" la sacaban porque asumían que ya se había apagado sola. Ahora
                    // StopFootprintTracking SIEMPRE se llama (apaga huella + piel verde), y si el
                    // objeto sigue en un modo especial apenas soltado, se le vuelve a poner la piel
                    // verde de inmediato (sigue "en edición" hasta Confirmar/Cancelar/Volver) --
                    // así nunca queda ni pegada para siempre ni parpadeando a su color real entre
                    // agarrones.
                    StopFootprintTracking(obj);

                    if (UpdateModesController.EstaGestionadoPorModoEspecial(obj))
                    {
                        // Quinta ronda (2026-09-19, "la edicion flotante sigue apareciendo verde
                        // ... deberia aparecer normal"): la piel verde solo se vuelve a poner para
                        // "Edición con objetivo fijo" (el escenario aislado) -- ahí sigue teniendo
                        // sentido, marca que seguís "en edición" hasta apretar Volver. Para
                        // Edición flotante, Alan pidió que se vea con su color real en cuanto lo
                        // soltás, no solo mientras lo tenés agarrado con la mano.
                        if (UpdateModesController.EstaGestionadoPorEscenario(obj))
                            TemporaryGhostVisual.Activar(obj);
                        return;
                    }

                    ResolvePlacementOrTrash(obj);
                });
            }

            interactable = grabInteractable;
        }
        else
        {
            XRSimpleInteractable simpleInteractable = obj.GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
                simpleInteractable = obj.AddComponent<XRSimpleInteractable>();

            interactable = simpleInteractable;
        }

        // Punto 2 del plan de edición avanzada (2026-09-18): el modo de edición universal se
        // activa manteniendo Y sobre CUALQUIER objeto (mueble o pared) -- para eso hace falta
        // saber en todo momento a qué objeto está apuntando/tocando el control, y el hover del
        // propio interactable (que la Toolkit ya calcula para resaltarlo) es justo esa señal, sin
        // necesitar un raycast propio aparte. Se agrega acá, fuera de las ramas de arriba, porque
        // aplica igual esté el mueble agarrable o no.
        UniversalEditModeController.EnsureExists();
        interactable.hoverEntered.AddListener((args) => UniversalEditModeController.SetHoverTarget(obj));
        interactable.hoverExited.AddListener((args) => UniversalEditModeController.ClearHoverTarget(obj));

        if (furnitureMetadata.colorable && obj.GetComponent<MaterialChangerVR>() == null)
        {
            var colorChanger = obj.AddComponent<MaterialChangerVR>();
            colorChanger.materials = GenerateColorPalette(obj);

            if (furnitureMetadata.movable)
            {
                // Ya no cambia de color solo por agarrarlo (era molesto: cada vez que
                // levantabas el sofá para moverlo, de paso le cambiaba el color sin querer).
                // Ahora cambia a pedido: apretando el botón X del control izquierdo mientras se
                // sostiene el mueble con la mano derecha (ver FurnitureColorButtonController).
                FurnitureColorButtonController.EnsureExists();
                interactable.selectEntered.AddListener((args) => FurnitureColorButtonController.SetHeldColorChanger(colorChanger));
                interactable.selectExited.AddListener((args) => FurnitureColorButtonController.ClearHeldColorChanger(colorChanger));
            }
            else
            {
                interactable.selectEntered.AddListener((args) => colorChanger.ChangeToNextMaterial());
            }
        }

        if (furnitureMetadata.deletable && obj.GetComponent<FurnitureDeleterVR>() == null)
        {
            obj.AddComponent<FurnitureDeleterVR>();
        }
    }

    /// <summary>
    /// Conecta la interactividad de un muro (Fase 3 del plan de edición avanzada): seleccionable
    /// con el rayo del control, y al seleccionarlo abre el panel de pintado
    /// (WallPaintPanelController) en vez del ciclo de color automático que usan los muebles --
    /// porque acá hace falta elegir además el alcance (todos los muros o solo este).
    ///
    /// Un muro exterior (esDivisorInterior=false, viene del JSON/preset) solo se puede
    /// seleccionar + pintar/ajustar altura, no se mueve. Un "Muro divisorio" (Sección A, agregado
    /// desde el catálogo) además se puede agarrar y mover/rotar como un mueble -- con un paso de
    /// rotación de 90° en vez de 15° (tiene más sentido para una pared recta) -- y abre el mismo
    /// panel apretando el botón X mientras se sostiene (igual que el color de los muebles), ya
    /// que agarrarlo ya usa el evento de "seleccionar" para moverlo.
    /// </summary>
    private void HacerMuroInteractivo(GameObject obj, bool esDivisorInterior)
    {
        EnsureFurnitureCollider(obj); // no-op si ya tiene collider (CreateDynamicGeometry le puso uno)

        MuroEditable muro = obj.GetComponent<MuroEditable>();
        if (muro == null)
            muro = obj.AddComponent<MuroEditable>();
        muro.EsDivisorInterior = esDivisorInterior;

        UniversalEditModeController.EnsureExists();

        if (!esDivisorInterior)
        {
            XRSimpleInteractable simpleInteractable = obj.GetComponent<XRSimpleInteractable>();
            if (simpleInteractable == null)
                simpleInteractable = obj.AddComponent<XRSimpleInteractable>();

            simpleInteractable.selectEntered.AddListener((args) => WallPaintPanelController.ShowFor(muro));

            // Punto 2 (edición universal, ver HacerInteractivo para el detalle de por qué se usa
            // el hover en vez de un raycast propio): un muro exterior/fijo también puede activar
            // el modo de edición universal manteniendo Y, aunque no se pueda mover/duplicar/borrar
            // (eso lo filtra CrudPanelController mirando MuroEditable.EsDivisorInterior).
            simpleInteractable.hoverEntered.AddListener((args) => UniversalEditModeController.SetHoverTarget(obj));
            simpleInteractable.hoverExited.AddListener((args) => UniversalEditModeController.ClearHoverTarget(obj));
            return;
        }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = obj.AddComponent<XRGrabInteractable>();
            grab.movementType = XRGrabInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;

            WallDividerToolButtonController.EnsureExists();

            grab.selectEntered.AddListener((args) =>
            {
                ShowFloorGrid();
                StartFootprintTracking(obj);
                WallDividerToolButtonController.SetHeldWall(muro);
            });
            grab.selectExited.AddListener((args) =>
            {
                HideFloorGrid();
                WallDividerToolButtonController.ClearHeldWall(muro);

                // Mismo ajuste que para muebles (puntos 5 y 8, 2026-09-19, y el mismo bug de la
                // piel verde pegada para siempre corregido esta ronda) -- ver el comentario largo
                // en la rama de muebles más arriba.
                StopFootprintTracking(obj);

                if (UpdateModesController.EstaGestionadoPorModoEspecial(obj))
                {
                    // Ver el comentario largo en la rama de muebles de arriba (quinta ronda):
                    // solo el escenario aislado vuelve a poner la piel verde al soltar.
                    if (UpdateModesController.EstaGestionadoPorEscenario(obj))
                        TemporaryGhostVisual.Activar(obj);
                    return;
                }

                ResolvePlacementOrTrash(obj, rotationStepDegOverride: 90f);
            });

            grab.hoverEntered.AddListener((args) => UniversalEditModeController.SetHoverTarget(obj));
            grab.hoverExited.AddListener((args) => UniversalEditModeController.ClearHoverTarget(obj));
        }
    }

    /// <summary>
    /// Configura interactividad VR para puertas y ventanas funcionales.
    /// Permite agarrar con Grip para reposicionar (con auto-acople magnético a muros al soltar),
    /// pulsar Trigger con el rayo para abrir/cerrar inmediatamente, o abrir el menú CRUD con el botón Y.
    /// </summary>
    private void HacerAberturaInteractiva(GameObject obj, string type)
    {
        if (obj == null) return;

        EnsureFurnitureCollider(obj);

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb == null) rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        AberturaInteractivaVR abertura = obj.GetComponent<AberturaInteractivaVR>();
        if (abertura == null) abertura = obj.GetComponentInChildren<AberturaInteractivaVR>();
        if (abertura == null)
        {
            abertura = obj.AddComponent<AberturaInteractivaVR>();
            if (type.Contains("doble") && type.Contains("puerta"))
                abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaDoble;
            else if (type.Contains("puerta"))
                abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaSimple;
            else if (type.Contains("ventana"))
                abertura.tipo = AberturaInteractivaVR.TipoAbertura.Ventana;
            abertura.Inicializar();
        }

        XRGrabInteractable grab = obj.GetComponent<XRGrabInteractable>();
        if (grab == null)
        {
            grab = obj.AddComponent<XRGrabInteractable>();
            grab.movementType = XRGrabInteractable.MovementType.Kinematic;
            grab.throwOnDetach = false;

            grab.selectEntered.AddListener((args) =>
            {
                ShowFloorGrid();
                StartFootprintTracking(obj);
                var hole = obj.GetComponent<AberturaWallHole>();
                if (hole != null) hole.RestaurarMuro();
            });
            grab.selectExited.AddListener((args) =>
            {
                HideFloorGrid();
                StopFootprintTracking(obj);

                if (UpdateModesController.EstaGestionadoPorModoEspecial(obj))
                {
                    if (UpdateModesController.EstaGestionadoPorEscenario(obj))
                        TemporaryGhostVisual.Activar(obj);
                    return;
                }

                ResolvePlacementOrTrash(obj);
            });

            // Al pulsar Trigger (Activate) apuntando a la abertura con el rayo, se abre/cierra en VR
            grab.activated.AddListener((args) =>
            {
                if (abertura != null) abertura.Toggle();
            });
        }

        UniversalEditModeController.EnsureExists();
        grab.hoverEntered.AddListener((args) => UniversalEditModeController.SetHoverTarget(obj));
        grab.hoverExited.AddListener((args) => UniversalEditModeController.ClearHoverTarget(obj));

        if (obj.GetComponent<FurnitureDeleterVR>() == null)
            obj.AddComponent<FurnitureDeleterVR>();
    }

    /// <summary>
    /// Comprueba si un GameObject pertenece al entorno virtual de VR (grilla 25x25, plataforma,
    /// teleportation area, cielo, cámaras, rigs de XR, iluminación global o UI) para evitar que se
    /// confunda con el piso o mobiliario del departamento y se exporte en la maqueta 3D.
    /// </summary>
    public static bool EsElementoEntornoVirtual(GameObject go)
    {
        if (go == null) return false;

        // UI y Canvas
        if (go.GetComponent<CanvasRenderer>() != null || go.GetComponentInParent<Canvas>() != null) return true;

        // Rigs de XR y Jugador
        if (go.GetComponentInParent<Unity.XR.CoreUtils.XROrigin>() != null) return true;
        if (go.CompareTag("MainCamera") || go.CompareTag("EditorOnly")) return true;

        // Inspeccionar el objeto y toda su cadena de ancestros
        Transform curr = go.transform;
        while (curr != null)
        {
            string n = curr.name;
            if (n.IndexOf("Environment", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Grid", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("XROrigin", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("XR Origin", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("XR Interaction", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Controller", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Ray", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Hand", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Ghost", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Handle", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Gizmo", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Trash", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Simulator", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Directional Light", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Global Volume", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Sky", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Reflection Probe", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.IndexOf("Light Probe", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (n.Equals("Camera", System.StringComparison.OrdinalIgnoreCase)) return true;
            if (n.Equals("GLTF_SceneRootNode", System.StringComparison.OrdinalIgnoreCase)) return true;

            curr = curr.parent;
        }

        return false;
    }

    /// <summary>
    /// Encuentra el contenedor raíz del inmueble/apartamento (ej: Sala, Modelo_Monoambiente)
    /// al que pertenece el piso seleccionado, evitando subir hasta el entorno virtual de VR.
    /// </summary>
    public static Transform EncontrarRaizInmueble(GameObject floorObj)
    {
        if (floorObj == null) return null;
        Transform curr = floorObj.transform;
        Transform bestRoot = curr;

        while (curr.parent != null)
        {
            Transform p = curr.parent;
            if (EsElementoEntornoVirtual(p.gameObject)) break;

            string pName = p.name.ToLower();
            if (pName.Contains("sala") || pName.Contains("monoambiente") || pName.Contains("apartamento") ||
                pName.Contains("apartment") || pName.Contains("inmueble") || pName.Contains("departamento") ||
                pName.Contains("house") || pName.Contains("scene_"))
            {
                bestRoot = p;
                break;
            }

            bestRoot = p;
            curr = p;
        }

        return bestRoot;
    }

    /// <summary>
    /// Calcula los límites espaciales (Bounds) del piso completo del inmueble.
    /// Si el inmueble está compuesto de múltiples baldosas o submódulos de piso (como monoambiente.fbx
    /// con LR_Floor_105, BR_Floor_27, etc.), combina la envolvente de todos ellos, excluyendo mallas del entorno VR.
    /// </summary>
    public static Bounds CalcularBoundsPisoCompleto(GameObject floorObj)
    {
        Bounds bounds = default;
        bool inicializado = false;

        if (floorObj != null && !EsElementoEntornoVirtual(floorObj))
        {
            Collider col = floorObj.GetComponent<Collider>();
            Renderer rend = floorObj.GetComponent<Renderer>();
            if (col != null)
            {
                bounds = col.bounds;
                inicializado = true;
            }
            else if (rend != null)
            {
                bounds = rend.bounds;
                inicializado = true;
            }
        }

        Transform raiz = EncontrarRaizInmueble(floorObj);
        if (raiz != null)
        {
            Renderer[] rends = raiz.GetComponentsInChildren<Renderer>(false);
            foreach (var r in rends)
            {
                if (r == null || EsElementoEntornoVirtual(r.gameObject)) continue;
                string n = r.gameObject.name.ToLower();
                bool esSubPiso = r.gameObject.CompareTag("Floor") || n.Contains("floor") || n.Contains("piso");
                if (esSubPiso)
                {
                    // Descartar si el renderer es sospechosamente grande (> 30m, como la grilla VR)
                    if (r.bounds.size.x > 30f || r.bounds.size.z > 30f) continue;

                    if (!inicializado)
                    {
                        bounds = r.bounds;
                        inicializado = true;
                    }
                    else
                    {
                        bounds.Encapsulate(r.bounds);
                    }
                }
            }
        }

        if (!inicializado)
        {
            bounds = (floorObj != null)
                ? new Bounds(floorObj.transform.position, floorObj.transform.lossyScale)
                : new Bounds(Vector3.zero, Vector3.one);
        }

        return bounds;
    }

    /// <summary>
    /// Hace interactivo el Piso de la sala para que el rayo del control pueda seleccionarlo
    /// y abrir el panel CRUD de configuración de sala / exportación de maqueta completa.
    /// </summary>
    public void HacerPisoInteractivo(GameObject obj)
    {
        if (obj == null) return;
        if (EsElementoEntornoVirtual(obj)) return;
        EnsureFurnitureCollider(obj);
        UniversalEditModeController.EnsureExists();

        XRSimpleInteractable simpleInteractable = obj.GetComponent<XRSimpleInteractable>();
        if (simpleInteractable == null)
            simpleInteractable = obj.AddComponent<XRSimpleInteractable>();

        simpleInteractable.hoverEntered.AddListener((args) => UniversalEditModeController.SetHoverTarget(obj));
        simpleInteractable.hoverExited.AddListener((args) => UniversalEditModeController.ClearHoverTarget(obj));
    }

    /// <summary>
    /// Busca pisos preexistentes en la escena (ej: Sala_MVP, Monoambiente_Jazmin) y les agrega interactividad,
    /// excluyendo explícitamente cualquier elemento del entorno virtual VR (Grid, Teleport, Template).
    /// </summary>
    public void MakeExistingFloorsInteractive()
    {
        var floors = GameObject.FindGameObjectsWithTag("Floor");
        foreach (var f in floors)
        {
            if (f != null && !EsElementoEntornoVirtual(f))
                HacerPisoInteractivo(f);
        }

        GameObject pisoPorNombre = GameObject.Find("Piso");
        if (pisoPorNombre != null && !EsElementoEntornoVirtual(pisoPorNombre))
            HacerPisoInteractivo(pisoPorNombre);

        // Buscar pisos en modelos importados como monoambiente.fbx (ej: LR_Floor_105, BR_Floor_27)
        MeshRenderer[] allRends = Object.FindObjectsByType<MeshRenderer>();
        foreach (var rend in allRends)
        {
            if (rend == null) continue;
            GameObject go = rend.gameObject;
            if (EsElementoEntornoVirtual(go)) continue;

            string n = go.name.ToLower();
            if (n.Contains("floor") || n.Contains("piso"))
            {
                HacerPisoInteractivo(go);
            }
        }
    }

    /// <summary>
    /// Devuelve todos los GameObjects de la sala que están contenidos dentro de los límites
    /// horizontales del piso (el piso mismo, muros perimetrales, muros divisorios y todos los
    /// muebles), excluyendo al jugador (XR Origin), UI, gizmos y elementos del entorno virtual VR (Grid, Template Environment).
    /// </summary>
    public List<GameObject> GetObjectsInsideFloor(GameObject floorObj)
    {
        var resultado = new List<GameObject>();
        if (floorObj == null || EsElementoEntornoVirtual(floorObj)) return resultado;

        // 1. Obtener los límites horizontales (X, Z) del piso completo
        Bounds floorBounds = CalcularBoundsPisoCompleto(floorObj);

        // Margen de tolerancia de 0.25m para capturar muros exteriores montados sobre el borde
        float minX = floorBounds.min.x - 0.25f;
        float maxX = floorBounds.max.x + 0.25f;
        float minZ = floorBounds.min.z - 0.25f;
        float maxZ = floorBounds.max.z + 0.25f;
        float minY = floorBounds.min.y - 0.20f;
        float maxY = floorBounds.max.y + 4.50f;

        HashSet<GameObject> agregados = new HashSet<GameObject>();
        Transform raizInmueble = EncontrarRaizInmueble(floorObj);

        // Si el piso forma parte de un inmueble compuesto (ej: Monoambiente con subpisos),
        // incluir todos los submódulos de piso del inmueble
        if (raizInmueble != null)
        {
            foreach (var r in raizInmueble.GetComponentsInChildren<Renderer>(false))
            {
                if (r == null || EsElementoEntornoVirtual(r.gameObject)) continue;
                string n = r.gameObject.name.ToLower();
                if (r.gameObject.CompareTag("Floor") || n.Contains("floor") || n.Contains("piso"))
                {
                    if (agregados.Add(r.gameObject))
                        resultado.Add(r.gameObject);
                }
            }
        }

        if (agregados.Add(floorObj))
            resultado.Add(floorObj);

        // 2. Si la escena fue generada procedimentalmente, revisar generatedObjects
        if (generatedObjects != null)
        {
            foreach (var go in generatedObjects)
            {
                if (go == null || agregados.Contains(go)) continue;
                if (EsElementoEntornoVirtual(go)) continue;
                Vector3 p = go.transform.position;
                if (p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ && p.y >= minY && p.y <= maxY)
                {
                    agregados.Add(go);
                    resultado.Add(go);
                }
            }
        }

        // 3. Revisar todos los Renderers de la escena para capturar objetos estáticos o agregados dinámicamente
        float maxDimPiso = Mathf.Max(floorBounds.size.x, floorBounds.size.z);
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>();
        foreach (var rend in allRenderers)
        {
            if (rend == null) continue;
            GameObject go = rend.gameObject;

            // Filtrar elementos de sistema / UI / jugador / entorno virtual
            if (EsElementoEntornoVirtual(go)) continue;

            // Descartar mallas desproporcionadamente grandes (ej: Grid de 25m, terrenos)
            Vector3 size = rend.bounds.size;
            if (size.x > maxDimPiso * 1.5f || size.z > maxDimPiso * 1.5f)
            {
                continue;
            }

            // Comprobar posición o bounds dentro del perímetro del piso
            Vector3 center = rend.bounds.center;
            if (center.x < minX || center.x > maxX || center.z < minZ || center.z > maxZ || center.y < minY || center.y > maxY)
            {
                continue;
            }

            // Encontrar el ancestro relevante más alto antes de la raíz de la escena (ej. el mueble padre)
            Transform rootCandidate = rend.transform;
            while (rootCandidate.parent != null &&
                   rootCandidate.parent.parent != null &&
                   !rootCandidate.parent.name.Equals("Sala", System.StringComparison.OrdinalIgnoreCase) &&
                   !rootCandidate.parent.name.Equals("SceneGeneratorManager", System.StringComparison.OrdinalIgnoreCase) &&
                   rootCandidate.parent != raizInmueble)
            {
                if (EsElementoEntornoVirtual(rootCandidate.parent.gameObject))
                    break;
                rootCandidate = rootCandidate.parent;
            }

            if (EsElementoEntornoVirtual(rootCandidate.gameObject)) continue;

            GameObject item = rootCandidate.gameObject;
            if (agregados.Add(item))
            {
                resultado.Add(item);
            }
        }

        Debug.Log($"[SceneGenerator] GetObjectsInsideFloor encontró {resultado.Count} objetos dentro del piso del departamento.");
        return resultado;
    }

    /// <summary>
    /// Crea un "Muro divisorio" nuevo (Sección A del plan de edición avanzada, 2026-09-17): un
    /// segmento de muro recto para separar ambientes dentro de un monoambiente (ej. baño/cocina
    /// sin nada que los separe), pensado para agregarse desde el catálogo de muebles
    /// (FurnitureCatalogController), igual que cualquier mueble.
    ///
    /// Mismo enfoque que CreateDynamicGeometry para los muros del JSON (cubo primitivo + material
    /// URP explícito, para no depender del material default del proyecto), pero con longitud
    /// ajustable después de creado (ver MuroEditable.AdjustLength). Convención de ejes local:
    /// X = longitud (ajustable), Y = altura, Z = espesor.
    /// </summary>
    public GameObject CreateInteriorWallDivider(Vector3 posicion,
        float longitudInicialM = 1.5f, float alturaM = 2.5f, float espesorM = 0.12f)
    {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);

        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
            renderer.sharedMaterial = new Material(urpLitShader != null ? urpLitShader : renderer.sharedMaterial.shader);
        }

        Collider col = obj.GetComponent<Collider>();
        if (col != null) DestroyImmediate(col);
        obj.AddComponent<BoxCollider>();

        obj.transform.localScale = new Vector3(longitudInicialM, alturaM, espesorM);
        obj.transform.position = new Vector3(posicion.x, alturaM * 0.5f, posicion.z);
        obj.name = $"MuroDivisorio_{generatedObjects.Count + 1}";

        var meta = obj.AddComponent<SceneElementMetadata>();
        meta.elementId = obj.name;
        meta.elementType = "muro_interior";
        meta.jsonData = "";

        RegisterExternalObject(obj);
        HacerInteractivo(obj, "muro_interior");

        return obj;
    }

    /// <summary>
    /// Centra y escala el modelo visual de una abertura dentro de su objeto raíz para que:
    /// 1. El centro horizontal (ejes X y Z) quede exactamente en (0, 0)
    /// 2. La base inferior quede exactamente en Y = 0
    /// 3. Las dimensiones encajen perfectamente en los muros residenciales estándar
    /// </summary>
    public static void ConfigurarAberturaVisual(GameObject root, GameObject model, float escalaUniforme)
    {
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * escalaUniforme;

        Renderer[] rends = model.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return;

        Bounds bTotal = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
        {
            if (rends[i].enabled) bTotal.Encapsulate(rends[i].bounds);
        }

        Vector3 localCenter = root.transform.InverseTransformPoint(bTotal.center);
        Vector3 localMin = root.transform.InverseTransformPoint(bTotal.min);

        // Desplazar el modelo para cancelar desfases internos del prefab (como los offsets modulares de 1.244m y 1.07m)
        model.transform.localPosition = new Vector3(-localCenter.x, -localMin.y, -localCenter.z);
    }

    /// <summary>
    /// Crea una puerta simple interactiva frente al usuario y la acopla al muro más cercano.
    /// Funciona en Meta Quest 3 abriendo y cerrando con el gatillo (Activate) o desde el menú CRUD.
    /// </summary>
    public GameObject CreateSingleDoor(Vector3 posicion, Quaternion rotacion)
    {
        GameObject obj = new GameObject($"PuertaSimple_{generatedObjects.Count + 1}");
        obj.transform.position = posicion;
        obj.transform.rotation = rotacion;

        GameObject prefab = Resources.Load<GameObject>("Aberturas/Puerta_Simple");
        if (prefab != null)
        {
            GameObject model = Instantiate(prefab);
            model.name = "ModeloPuerta";
            ConfigurarAberturaVisual(obj, model, 0.72f);
            EliminarScriptPorNombre(model, "opencloseDoor");

            var abertura = obj.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaSimple;
            abertura.hojaIzquierda = BuscarSubobjeto(model, "door");
            abertura.anguloApertura = 90f;
            abertura.Inicializar();
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(obj.transform, false);
            cube.transform.localScale = new Vector3(0.15f, 2.2f, 1.2f);
            cube.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var abertura = obj.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaSimple;
            abertura.Inicializar();
        }

        BoxCollider boxCol = obj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.35f, 2.25f, 1.25f);
        boxCol.center = new Vector3(0f, 1.125f, 0f);

        var meta = obj.AddComponent<SceneElementMetadata>();
        meta.elementId = obj.name;
        meta.elementType = "puerta";
        meta.jsonData = "";

        RegisterExternalObject(obj);
        HacerInteractivo(obj, "puerta");
        SnapToWall(obj, esVentana: false, 3.0f);

        return obj;
    }

    /// <summary>
    /// Crea una puerta doble interactiva simétrica con dos hojas que abren simultáneamente
    /// en sentidos opuestos (-90° y +90°).
    /// </summary>
    public GameObject CreateDoubleDoor(Vector3 posicion, Quaternion rotacion)
    {
        GameObject root = new GameObject($"PuertaDoble_{generatedObjects.Count + 1}");
        root.transform.position = posicion;
        root.transform.rotation = rotacion;

        GameObject prefab = Resources.Load<GameObject>("Aberturas/Puerta_Simple");
        if (prefab != null)
        {
            GameObject modelGroup = new GameObject("ModeloPuertaDoble");

            GameObject left = Instantiate(prefab, modelGroup.transform);
            left.name = "Hoja_Izquierda";
            EliminarScriptPorNombre(left, "opencloseDoor");

            GameObject right = Instantiate(prefab, modelGroup.transform);
            right.name = "Hoja_Derecha";
            right.transform.localScale = new Vector3(1f, 1f, -1f);
            EliminarScriptPorNombre(right, "opencloseDoor");

            // Separar hojas simétricamente a lo largo del ancho (Z local)
            left.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            right.transform.localPosition = new Vector3(0f, 0f, 0.6f);

            ConfigurarAberturaVisual(root, modelGroup, 0.72f);

            foreach (var anim in root.GetComponentsInChildren<Animator>())
                anim.enabled = false;

            var abertura = root.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaDoble;
            abertura.hojaIzquierda = BuscarSubobjeto(left, "door");
            abertura.hojaDerecha = BuscarSubobjeto(right, "door");
            abertura.anguloApertura = 90f;
            abertura.Inicializar();
        }
        else
        {
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.transform.SetParent(root.transform, false);
            frame.transform.localScale = new Vector3(0.15f, 2.2f, 2.0f);
            frame.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var abertura = root.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.PuertaDoble;
            abertura.Inicializar();
        }

        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.35f, 2.25f, 2.45f);
        boxCol.center = new Vector3(0f, 1.125f, 0f);

        var meta = root.AddComponent<SceneElementMetadata>();
        meta.elementId = root.name;
        meta.elementType = "puerta_doble";
        meta.jsonData = "";

        RegisterExternalObject(root);
        HacerInteractivo(root, "puerta_doble");
        SnapToWall(root, esVentana: false, 3.0f);

        return root;
    }

    /// <summary>
    /// Crea una ventana interactiva de pared y la acopla al muro a altura estándar (~0.95m de antepecho).
    /// </summary>
    public GameObject CreateWindow(Vector3 posicion, Quaternion rotacion)
    {
        GameObject obj = new GameObject($"Ventana_{generatedObjects.Count + 1}");
        obj.transform.position = posicion;
        obj.transform.rotation = rotacion;

        GameObject prefab = Resources.Load<GameObject>("Aberturas/Ventana_Pared");
        if (prefab != null)
        {
            GameObject model = Instantiate(prefab);
            model.name = "ModeloVentana";
            ConfigurarAberturaVisual(obj, model, 0.60f);
            EliminarScriptPorNombre(model, "opencloseWindowApt");
            EliminarScriptPorNombre(model, "opencloseWindow");

            var abertura = obj.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.Ventana;
            abertura.Inicializar();
        }
        else
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(obj.transform, false);
            cube.transform.localScale = new Vector3(0.15f, 1.4f, 1.3f);
            cube.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var abertura = obj.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.Ventana;
            abertura.Inicializar();
        }

        BoxCollider boxCol = obj.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.35f, 1.55f, 1.35f);
        boxCol.center = new Vector3(0f, 0.775f, 0f);

        var meta = obj.AddComponent<SceneElementMetadata>();
        meta.elementId = obj.name;
        meta.elementType = "ventana";
        meta.jsonData = "";

        RegisterExternalObject(obj);
        HacerInteractivo(obj, "ventana");
        SnapToWall(obj, esVentana: true, 3.0f);

        return obj;
    }

    /// <summary>
    /// Crea una ventana doble interactiva de pared compuesta por dos módulos acoplados.
    /// </summary>
    public GameObject CreateDoubleWindow(Vector3 posicion, Quaternion rotacion)
    {
        GameObject root = new GameObject($"VentanaDoble_{generatedObjects.Count + 1}");
        root.transform.position = posicion;
        root.transform.rotation = rotacion;

        GameObject prefab = Resources.Load<GameObject>("Aberturas/Ventana_Pared");
        if (prefab != null)
        {
            GameObject modelGroup = new GameObject("ModeloVentanaDoble");

            GameObject left = Instantiate(prefab, modelGroup.transform);
            left.name = "Ventana_Izquierda";
            EliminarScriptPorNombre(left, "opencloseWindowApt");
            EliminarScriptPorNombre(left, "opencloseWindow");

            GameObject right = Instantiate(prefab, modelGroup.transform);
            right.name = "Ventana_Derecha";
            EliminarScriptPorNombre(right, "opencloseWindowApt");
            EliminarScriptPorNombre(right, "opencloseWindow");

            left.transform.localPosition = new Vector3(0f, 0f, -0.65f);
            right.transform.localPosition = new Vector3(0f, 0f, 0.65f);

            ConfigurarAberturaVisual(root, modelGroup, 0.60f);

            var abertura = root.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.Ventana;
            abertura.hojaIzquierda = BuscarSubobjeto(left, "window_01");
            abertura.hojaDerecha = BuscarSubobjeto(right, "window_01");
            abertura.ventanaEsDeslizante = true;
            abertura.Inicializar();
        }
        else
        {
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.transform.SetParent(root.transform, false);
            frame.transform.localScale = new Vector3(0.15f, 1.4f, 2.6f);
            frame.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            var abertura = root.AddComponent<AberturaInteractivaVR>();
            abertura.tipo = AberturaInteractivaVR.TipoAbertura.Ventana;
            abertura.Inicializar();
        }

        BoxCollider boxCol = root.AddComponent<BoxCollider>();
        boxCol.size = new Vector3(0.35f, 1.55f, 2.65f);
        boxCol.center = new Vector3(0f, 0.775f, 0f);

        var meta = root.AddComponent<SceneElementMetadata>();
        meta.elementId = root.name;
        meta.elementType = "ventana_doble";
        meta.jsonData = "";

        RegisterExternalObject(root);
        HacerInteractivo(root, "ventana_doble");
        SnapToWall(root, esVentana: true, 3.0f);

        return root;
    }

    private static void EliminarScriptPorNombre(GameObject go, string nombreScript)
    {
        if (go == null) return;
        foreach (var comp in go.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (comp != null && comp.GetType().Name.Equals(nombreScript, System.StringComparison.OrdinalIgnoreCase))
            {
                DestroyImmediate(comp);
            }
        }
    }

    private static Transform BuscarSubobjeto(GameObject go, string subcadena)
    {
        if (go == null) return null;
        string sub = subcadena.ToLowerInvariant();
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
        {
            if (t == go.transform) continue;
            string n = t.name.ToLowerInvariant();
            if (n.Contains(sub) && !n.Contains("frame") && !n.Contains("base") && !n.Contains("handle"))
                return t;
        }
        return null;
    }

    /// <summary>
    /// Registra un objeto creado fuera del pipeline normal de GenerateSceneAsync (por ejemplo,
    /// un mueble instanciado desde el catálogo) para que cuente en GetGeneratedObjectCount,
    /// se destruya al generar/cargar otra escena (ClearPreviousScene), y se incluya al exportar
    /// la escena actual a JSON (ExportCurrentSceneToJson).
    /// </summary>
    public void RegisterExternalObject(GameObject obj)
    {
        if (obj != null && !generatedObjects.Contains(obj))
            generatedObjects.Add(obj);
    }

    /// <summary>
    /// Recorre los objetos generados y apoya cada mueble (no estructural) sobre el piso/objeto
    /// que tenga debajo. Se llama una vez por escena, después de instanciar todos los elementos
    /// (así el piso ya existe sin importar en qué orden vino en el JSON).
    /// </summary>
    private void SnapFurnitureToFloor()
    {
        foreach (var obj in generatedObjects)
        {
            if (obj == null) continue;

            var metadata = obj.GetComponent<SceneElementMetadata>();
            if (metadata == null) continue;

            string type = (metadata.elementType ?? "").ToLower();
            bool esEstructural = (type == "muro" || type == "muro_interior" || type == "piso" || type == "puerta" || type == "ventana");
            if (esEstructural) continue;

            SnapToFloor(obj);
        }
    }

    /// <summary>
    /// Mide los Renderer combinados del objeto y lo desplaza verticalmente para que la base de
    /// su bounding box quede apoyada sobre el primer collider que encuentre debajo (típicamente
    /// el piso). Público y estático para poder llamarse también desde FurnitureCatalogController
    /// al agregar un mueble nuevo a mano.
    /// </summary>
    public static void SnapToFloor(GameObject obj)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        // BUG encontrado esta ronda: una vez que el mueble ya tiene su propio Collider (lo
        // agrega EnsureFurnitureCollider en HacerInteractivo, antes de que se pueda agarrar por
        // primera vez), el rayo hacia abajo -- que arranca ARRIBA del propio mueble -- golpeaba
        // primero el techo de SU PROPIO collider en vez de seguir hasta el piso real debajo.
        // Eso hacía "delta = altura del propio mueble" y lo empujaba hacia arriba esa distancia
        // cada vez que se soltaba (por eso "se iba elevando" cada vez que se lo volvía a
        // posicionar). Se desactivan temporalmente los colliders del propio objeto durante el
        // raycast para que solo pueda pegarle a algo externo (el piso, u otro mueble debajo).
        Collider[] ownColliders = obj.GetComponentsInChildren<Collider>();
        bool[] wasEnabled = new bool[ownColliders.Length];
        for (int i = 0; i < ownColliders.Length; i++)
        {
            wasEnabled[i] = ownColliders[i].enabled;
            ownColliders[i].enabled = false;
        }

        Vector3 rayStart = new Vector3(combined.center.x, combined.max.y + 5f, combined.center.z);
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 50f))
        {
            float delta = hit.point.y - combined.min.y;
            if (Mathf.Abs(delta) > 0.0001f)
                obj.transform.position += new Vector3(0f, delta, 0f);
        }

        for (int i = 0; i < ownColliders.Length; i++)
            ownColliders[i].enabled = wasEnabled[i];
    }

    /// <summary>
    /// Acopla magnéticamente una puerta o ventana al muro más cercano dentro de snapDistance metros.
    /// - Alinea la orientación en el mismo plano de la pared (nunca perpendicular).
    /// - Empotra la abertura en el espesor medio del muro.
    /// - Para ventanas: posiciona el antepecho a altura estándar (~0.95m sobre el piso).
    /// - Para puertas: apoya la base directamente en el piso.
    /// - Corta el muro en segmentos contiguos exactos dejando un hueco que rodea la abertura.
    /// Devuelve true si encontró un muro y se acopló con éxito.
    /// </summary>
    public static bool SnapToWall(GameObject obj, bool esVentana = false, float snapDistance = 2.5f)
    {
        if (obj == null) return false;

        // 1. Restaurar el muro original antes del raycast para no impactar segmentos cortados
        var holeMgr = obj.GetComponent<AberturaWallHole>();
        if (holeMgr != null)
        {
            holeMgr.RestaurarMuro();
        }

        // 2. Desactivar temporalmente colliders propios para evitar auto-impactos
        Collider[] ownColliders = obj.GetComponentsInChildren<Collider>();
        bool[] wasEnabled = new bool[ownColliders.Length];
        for (int i = 0; i < ownColliders.Length; i++)
        {
            wasEnabled[i] = ownColliders[i].enabled;
            ownColliders[i].enabled = false;
        }

        Vector3 pos = obj.transform.position;
        Vector3 rayOrigin = pos + Vector3.up * (esVentana ? 1.4f : 1.0f);

        RaycastHit bestHit = default;
        float minDistance = float.MaxValue;
        bool foundWall = false;

        const int directionsCount = 16;
        for (int i = 0; i < directionsCount; i++)
        {
            float angle = i * (360f / directionsCount) * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

            if (Physics.Raycast(rayOrigin, dir, out RaycastHit hit, snapDistance))
            {
                if (Mathf.Abs(hit.normal.y) > 0.35f) continue;
                if (hit.collider != null && hit.collider.transform.IsChildOf(obj.transform)) continue;

                if (hit.distance < minDistance)
                {
                    minDistance = hit.distance;
                    bestHit = hit;
                    foundWall = true;
                }
            }
        }

        for (int i = 0; i < ownColliders.Length; i++)
            ownColliders[i].enabled = wasEnabled[i];

        if (foundWall)
        {
            // Si el raycast impactó un segmento cortado de otra abertura, recuperar el muro original
            GameObject wallHitGO = bestHit.collider.gameObject;
            if (wallHitGO.name.Contains("_Segmento"))
            {
                foreach (var otherHole in Object.FindObjectsOfType<AberturaWallHole>())
                {
                    if (otherHole != holeMgr && otherHole.SegmentosRoot != null &&
                        wallHitGO.transform.IsChildOf(otherHole.SegmentosRoot.transform))
                    {
                        if (otherHole.MuroOriginal != null)
                        {
                            wallHitGO = otherHole.MuroOriginal;
                            break;
                        }
                    }
                }
            }

            Vector3 wallNormal = bestHit.normal;
            wallNormal.y = 0f;
            wallNormal.Normalize();

            // Tangente horizontal del muro: orientar el eje Z (ancho de la abertura)
            // a lo largo del plano de la pared (NUNCA perpendicular en cruz)
            Vector3 wallTangent = Vector3.Cross(Vector3.up, wallNormal).normalized;
            if (wallTangent.sqrMagnitude > 0.001f)
            {
                if (Vector3.Dot(obj.transform.forward, wallTangent) < 0f)
                    wallTangent = -wallTangent;

                obj.transform.rotation = Quaternion.LookRotation(wallTangent, Vector3.up);
            }

            // Empotrar la abertura centrada en el espesor del muro (plano medio de la pared)
            BoxCollider wallBox = bestHit.collider as BoxCollider;
            float halfThickness = 0.1f;
            if (wallBox != null)
            {
                Vector3 toCenter = wallBox.bounds.center - bestHit.point;
                float proj = Vector3.Dot(toCenter, -wallNormal);
                if (proj > 0.01f && proj < 0.8f)
                    halfThickness = proj;
            }
            Vector3 newPos = bestHit.point - wallNormal * halfThickness;

            // Detectar el piso bajo la abertura
            float floorY = 0f;
            bool foundFloor = false;

            for (int i = 0; i < ownColliders.Length; i++) ownColliders[i].enabled = false;
            if (Physics.Raycast(newPos + Vector3.up * 3f, Vector3.down, out RaycastHit floorHit, 20f))
            {
                floorY = floorHit.point.y;
                foundFloor = true;
            }
            for (int i = 0; i < ownColliders.Length; i++) ownColliders[i].enabled = wasEnabled[i];

            if (esVentana)
            {
                // Altura de antepecho estándar para ventana (~0.95m sobre el piso)
                newPos.y = foundFloor ? floorY + 0.95f : pos.y;
            }
            else
            {
                // Base de la puerta apoyada en el piso
                newPos.y = foundFloor ? floorY : pos.y;
            }

            obj.transform.position = newPos;

            // Cortar hueco real en el muro ajustado automáticamente al tamaño físico real de la abertura
            if (holeMgr == null) holeMgr = obj.AddComponent<AberturaWallHole>();

            if (holeMgr.AplicarHueco(wallHitGO, obj, newPos, esVentana, wallNormal, out Vector3 posCentrada, out Quaternion rotMuro))
            {
                obj.transform.position = posCentrada;
                obj.transform.rotation = rotMuro;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Wrapper público de FinalizePlacement para "Edición flotante" (Punto 2 del plan de edición
    /// avanzada, UpdateModesController): al confirmar la nueva posición de un objeto que estuvo
    /// flotando frente al jugador, hace falta el mismo clamp+grilla+apoyo en el piso que ya usa
    /// soltar un mueble a mano, sin duplicar esa lógica.
    /// </summary>
    public void FinalizarColocacionExterna(GameObject obj, float rotationStepDegOverride = -1f)
    {
        FinalizePlacement(obj, rotationStepDegOverride);
    }

    /// <summary>
    /// Se llama al soltar un mueble movible (selectExited del XRGrabInteractable). Antes, un
    /// mueble arrastrado a mano podía terminar atravesando una pared, colgado en el aire, o en
    /// cualquier posición/ángulo arbitrario -- esto lo deja en un lugar prolijo:
    /// 1) lo clampea para que no quede más allá de los límites conocidos de la sala actual
    ///    (roomWidth/roomDepth, seteados en RepositionPlayerInRoom al generar la escena),
    /// 2) redondea su posición X/Z y su rotación en Y a una "grilla" fija (una ayuda de
    ///    alineación tipo grid, sin necesidad de dibujarla en el piso), y
    /// 3) lo vuelve a apoyar en el piso con SnapToFloor (el clamp/redondeo de arriba pudo haber
    ///    cambiado qué queda debajo).
    /// </summary>
    private void FinalizePlacement(GameObject obj, float rotationStepDegOverride = -1f)
    {
        if (obj == null) return;

        // Ajuste pedido por Alan (2026-09-19, punto 3): por si el objeto todavía tenía puesta la
        // "piel" verde temporal (por ejemplo al confirmar una Edición flotante, que llega acá vía
        // FinalizarColocacionExterna sin pasar por StartFootprintTracking/StopFootprintTracking),
        // se le devuelve acá su color/material real antes de dejarlo colocado. No hace nada si el
        // objeto no la tenía activa.
        TemporaryGhostVisual.Desactivar(obj);

        AberturaInteractivaVR abertura = obj.GetComponent<AberturaInteractivaVR>();
        if (abertura == null) abertura = obj.GetComponentInChildren<AberturaInteractivaVR>();

        SceneElementMetadata metadata = obj.GetComponent<SceneElementMetadata>();
        string elemType = metadata != null ? (metadata.elementType ?? "").ToLower() : "";
        bool esAbertura = abertura != null || elemType == "puerta" || elemType == "ventana" || elemType.Contains("puerta") || elemType.Contains("ventana");
        bool esVentana = (abertura != null && abertura.tipo == AberturaInteractivaVR.TipoAbertura.Ventana) || elemType.Contains("ventana");

        if (esAbertura)
        {
            bool snappeado = SnapToWall(obj, esVentana, 2.5f);
            if (!snappeado && !esVentana)
            {
                SnapToFloor(obj);
            }
            return;
        }

        // Un "Muro divisorio" (Sección A) rota de a 90° en vez de 15° -- tiene más sentido para
        // una pared recta que se alinea contra las paredes existentes. -1 (default) = usar el
        // paso normal de los muebles.
        float rotationStep = rotationStepDegOverride > 0f ? rotationStepDegOverride : PLACEMENT_ROTATION_STEP_DEG;

        // BUG encontrado esta ronda ("se tambalea de una pata"): mientras se sostiene un mueble
        // con XRGrabInteractable, el objeto sigue la rotación COMPLETA del control (X/Y/Z), no
        // solo el giro en Y. Antes, acá solo se redondeaba euler.y y se dejaban X/Z tal cual
        // quedaron al soltarlo -- si lo agarrabas de costado o inclinado, quedaba apoyado sobre
        // un borde o una sola pata en vez de plano sobre su base, aunque técnicamente "tocara"
        // el piso (por eso SnapToFloor no lo detectaba como error: el raycast solo mide altura,
        // no si el objeto está derecho).
        //
        // Se endereza PRIMERO (antes de medir el bounding box para el clamp de posición, más
        // abajo), para que ese cálculo use el tamaño real del mueble ya parado, no el de como
        // estaba inclinado en la mano. Se asume que cada prefab de mueble viene modelado "parado"
        // en su rotación identidad (0 en X y Z) -- válido para los del Furniture Mega Pack -- así
        // que solo se conserva/ajusta el giro en Y (hacia dónde mira, que es justamente lo que el
        // usuario sí quiere poder elegir al colocarlo) y se fuerzan X/Z a 0. Con esto, el mueble
        // siempre queda con las 4 patas (o su base) apoyadas en el piso, mirando hacia donde lo
        // dejaste, sin importar en qué ángulo lo sostenías al soltarlo.
        // Sección C del plan de edición avanzada (2026-09-18, ajustado 2026-09-19 a un modo POR
        // OBJETO en vez de global -- ver el comentario de RotationModeController): en modo
        // "Avanzada" de ESTE objeto puntual se salta todo este enderezado/redondeo a propósito --
        // el usuario pidió poder dejar el mueble en cualquier ángulo libre de los 3 ejes, tal cual
        // lo soltó. En modo "Simple" (default) se mantiene el comportamiento de siempre.
        if (RotationModeController.ObtenerModo(obj) == ModoRotacion.Simple)
        {
            Vector3 eulerEnderezado = obj.transform.eulerAngles;
            eulerEnderezado.x = 0f;
            eulerEnderezado.z = 0f;
            eulerEnderezado.y = Mathf.Round(eulerEnderezado.y / rotationStep) * rotationStep;
            obj.transform.eulerAngles = eulerEnderezado;
        }

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);

            const float margin = 0.05f;
            float halfX = combined.extents.x;
            float halfZ = combined.extents.z;

            Vector3 pos = obj.transform.position;
            // El pivot del objeto no siempre coincide con el centro de su bounding box (algunos
            // prefabs traen el mesh hijo desplazado) -- se corrige el clamp con ese desfasaje.
            Vector3 centerOffset = combined.center - pos;

            // Cuarta ronda: el rango de colocación ya no termina en el borde de la sala -- se
            // extiende MARGEN_MATRIZ_FUERA_SALA_M metros para cada lado, para que se pueda soltar
            // un mueble afuera de la casa sin que quede clampeado contra la pared.
            float minX = -MARGEN_MATRIZ_FUERA_SALA_M + margin + halfX - centerOffset.x;
            float maxX = roomWidth + MARGEN_MATRIZ_FUERA_SALA_M - margin - halfX - centerOffset.x;
            float minZ = -MARGEN_MATRIZ_FUERA_SALA_M + margin + halfZ - centerOffset.z;
            float maxZ = roomDepth + MARGEN_MATRIZ_FUERA_SALA_M - margin - halfZ - centerOffset.z;

            // Si el mueble es más grande que la sala (minX > maxX), no clampear -- se dejaría
            // en una posición imposible. Mejor una sala chica con un mueble grande que un bug.
            if (minX <= maxX) pos.x = Mathf.Clamp(pos.x, minX, maxX);
            if (minZ <= maxZ) pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

            pos.x = Mathf.Round(pos.x / PLACEMENT_GRID_SIZE_M) * PLACEMENT_GRID_SIZE_M;
            pos.z = Mathf.Round(pos.z / PLACEMENT_GRID_SIZE_M) * PLACEMENT_GRID_SIZE_M;
            obj.transform.position = pos;
        }

        SnapToFloor(obj);
    }

    /// <summary>
    /// Muestra la grilla de ayuda dibujada en el piso (la "matriz" pedida para ver dónde van a
    /// caer los pasos de posición al soltar un mueble). La reconstruye solo si el tamaño de sala
    /// cambió desde la última vez (no hace falta recrear las líneas en cada agarre).
    /// </summary>
    private void ShowFloorGrid()
    {
        EnsureFloorGridVisual();

        if (!Mathf.Approximately(gridBuiltForWidth, roomWidth) || !Mathf.Approximately(gridBuiltForDepth, roomDepth))
        {
            RebuildFloorGridLines();
            gridBuiltForWidth = roomWidth;
            gridBuiltForDepth = roomDepth;
        }

        floorGridVisual.SetActive(true);
    }

    private void HideFloorGrid()
    {
        if (floorGridVisual != null) floorGridVisual.SetActive(false);
    }

    private void EnsureFloorGridVisual()
    {
        if (floorGridVisual != null) return;
        floorGridVisual = new GameObject("FurniturePlacementGrid");
        floorGridVisual.SetActive(false);
    }

    /// <summary>
    /// Arma la grilla como un conjunto de líneas (LineRenderer) sobre el piso, espaciadas cada
    /// PLACEMENT_GRID_SIZE_M metros. Cuarta ronda (2026-09-19, "ampliar la matriz a todo el
    /// mapa"): antes cubría solo el rectángulo exacto de la sala (roomWidth x roomDepth) -- ahora
    /// se extiende MARGEN_MATRIZ_FUERA_SALA_M metros más allá en las 4 direcciones, para que se
    /// vea grilla también afuera de la casa mientras se sostiene un mueble hacia esa zona.
    /// Se usa LineRenderer con el shader URP/Unlit (el mismo ya usado y verificado para
    /// paredes/piso en este proyecto) en vez de un quad con textura semitransparente: evitar
    /// depender de un material URP transparente armado a mano en runtime (superficie
    /// Opaque/Transparent, keywords, blend mode) que, sin poder verlo en el Editor esta sesión,
    /// es fácil que termine invisible o rosado -- el mismo problema que ya dio dolores de cabeza
    /// con las paredes en una ronda anterior. Líneas opacas de un color bien visible es una
    /// apuesta mucho más segura sin poder probarlo a ojo.
    /// </summary>
    private void RebuildFloorGridLines()
    {
        foreach (Transform child in floorGridVisual.transform)
            Destroy(child.gameObject);

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        Material lineMaterial = new Material(shader);
        lineMaterial.color = new Color(0.2f, 0.9f, 1f); // celeste, visible sobre cualquier piso

        const float y = 0.01f; // apenas arriba del piso, para evitar z-fighting
        const float lineWidth = 0.015f;

        float minX = -MARGEN_MATRIZ_FUERA_SALA_M;
        float maxX = roomWidth + MARGEN_MATRIZ_FUERA_SALA_M;
        float minZ = -MARGEN_MATRIZ_FUERA_SALA_M;
        float maxZ = roomDepth + MARGEN_MATRIZ_FUERA_SALA_M;

        int stepsX = Mathf.Max(1, Mathf.RoundToInt((maxX - minX) / PLACEMENT_GRID_SIZE_M));
        int stepsZ = Mathf.Max(1, Mathf.RoundToInt((maxZ - minZ) / PLACEMENT_GRID_SIZE_M));

        for (int i = 0; i <= stepsX; i++)
        {
            float x = minX + i * PLACEMENT_GRID_SIZE_M;
            if (x > maxX + 0.001f) break;
            CreateGridLine(new Vector3(x, y, minZ), new Vector3(x, y, maxZ), lineWidth, lineMaterial);
        }

        for (int i = 0; i <= stepsZ; i++)
        {
            float z = minZ + i * PLACEMENT_GRID_SIZE_M;
            if (z > maxZ + 0.001f) break;
            CreateGridLine(new Vector3(minX, y, z), new Vector3(maxX, y, z), lineWidth, lineMaterial);
        }
    }

    private void CreateGridLine(Vector3 from, Vector3 to, float width, Material material)
    {
        GameObject lineGO = new GameObject("GridLine");
        lineGO.transform.SetParent(floorGridVisual.transform, false);

        LineRenderer line = lineGO.AddComponent<LineRenderer>();
        line.material = material;
        line.positionCount = 2;
        line.SetPosition(0, from);
        line.SetPosition(1, to);
        line.startWidth = width;
        line.endWidth = width;
        line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    /// <summary>
    /// Se llama al soltar un mueble o "Muro divisorio" movible, tras StopFootprintTracking.
    ///
    /// Antes existía acá la lógica de la papelera de la entrada (soltar el mueble tocándola
    /// preguntaba si querías eliminarlo) -- la papelera se eliminó (ajuste pedido por Alan,
    /// 2026-09-19, punto 1): ahora eliminar un objeto se hace clickeándolo y usando la opción
    /// "🗑️ Eliminar" del menú (CrudPanelController), que ya pregunta "¿Estás seguro? Sí/No" con
    /// el mismo ConfirmDialogUI. Este método quedó como un simple wrapper de FinalizePlacement --
    /// se mantiene con su propio nombre en vez de reemplazar sus 2 llamadas por FinalizePlacement
    /// directo para no tener que tocar esos call sites.
    /// </summary>
    private void ResolvePlacementOrTrash(GameObject obj, float rotationStepDegOverride = -1f)
    {
        FinalizePlacement(obj, rotationStepDegOverride);
    }

    // --- Huella del mueble en el piso (punto 3 del plan de edición avanzada) ---

    private void StartFootprintTracking(GameObject obj)
    {
        currentlyHeldForFootprint = obj;
        ComputeLocalFootprint(obj, out heldFootprintHalfExtents, out heldFootprintLocalCenterOffset);

        // BUG encontrado esta ronda (pedido de Alan, tercera ronda: "se tiene que ajustar el
        // cuadro que hay debajo de cada mueble que se ajuste al tamaño del mueble si se agranda o
        // si achica"): ComputeLocalFootprint (como ComputeLocalBounds, del que sale) devuelve
        // medidas SIN ESCALAR -- el mismo espacio local "de fábrica" del mesh, sin importar el
        // localScale actual del objeto (ver el comentario largo de ComputeLocalBounds más abajo).
        // Eso está bien para ResizeHandlesController, que multiplica por lossyScale él mismo, pero
        // acá se usaba tal cual -- así que si el mueble ya había sido agrandado/achicado con las
        // esferitas de redimensionar, el rectángulo de huella se dibujaba con su tamaño ORIGINAL
        // de fábrica, no con el tamaño real actual. Se corrige multiplicando por el lossyScale
        // (X/Z, que son los dos ejes que le importan a la huella dibujada en el piso) antes de
        // guardarlo.
        Vector3 escala = obj.transform.lossyScale;
        heldFootprintHalfExtents = new Vector2(heldFootprintHalfExtents.x * escala.x, heldFootprintHalfExtents.y * escala.z);
        heldFootprintLocalCenterOffset = new Vector2(heldFootprintLocalCenterOffset.x * escala.x, heldFootprintLocalCenterOffset.y * escala.z);

        EnsureFootprintOutlineVisual();
        footprintOutlineVisual.SetActive(true);

        // Ajuste pedido por Alan (2026-09-19, punto 3): mientras se sostiene/reposiciona el
        // objeto, se cubre con un material verde temporal para dar mejor referencia visual --
        // ver TemporaryGhostVisual. Se restaura solo (color/material real, el de fábrica o el
        // que le hayas puesto vos) en StopFootprintTracking, apenas lo soltás.
        TemporaryGhostVisual.Activar(obj);
    }

    private void StopFootprintTracking(GameObject obj)
    {
        if (currentlyHeldForFootprint == obj)
            currentlyHeldForFootprint = null;
        if (footprintOutlineVisual != null)
            footprintOutlineVisual.SetActive(false);

        TemporaryGhostVisual.Desactivar(obj);
    }

    /// <summary>
    /// Calcula, una sola vez al agarrar el mueble (no en cada frame), el ancho/profundidad reales
    /// del mueble en su propio espacio local (independiente de a qué ángulo esté rotado ahora
    /// mismo). Es solo la proyección en X/Z de ComputeLocalBounds -- ver ese método para el
    /// detalle del bug que corrige (el rectángulo de huella aparecía "mucho más grande que el
    /// objeto", reportado por Alan el 2026-09-19, cuando el mueble estaba rotado en un ángulo no
    /// alineado a los ejes en el momento de agarrarlo).
    /// </summary>
    private static void ComputeLocalFootprint(GameObject obj, out Vector2 halfExtents, out Vector2 centerOffset)
    {
        ComputeLocalBounds(obj, out Vector3 halfExtents3D, out Vector3 centerOffset3D);
        halfExtents = new Vector2(halfExtents3D.x, halfExtents3D.z);
        centerOffset = new Vector2(centerOffset3D.x, centerOffset3D.z);
    }

    /// <summary>
    /// Mide el bounding box real de un objeto en su propio espacio LOCAL (X/Y/Z), sin importar en
    /// qué ángulo esté rotado ahora mismo -- usado por la huella del piso (ComputeLocalFootprint)
    /// y por ResizeHandlesController para ubicar las esquinas de sus esferitas de redimensionar.
    ///
    /// BUG encontrado y corregido acá (2026-09-19, reportado por Alan tras probar en el Quest:
    /// "he visto los límites mucho más grande que el objeto"): la versión anterior usaba
    /// Renderer.bounds (un AABB calculado en espacio MUNDO, a la rotación actual del objeto) y
    /// transformaba esas 8 esquinas ya infladas a espacio local -- si el mueble estaba rotado en
    /// un ángulo no alineado a los ejes (45°, por ejemplo) en el momento de agarrarlo, ese AABB
    /// mundial queda más grande que la caja real (la diagonal de una caja rotada es más larga que
    /// sus lados), y ese inflado quedaba "congelado" en la huella para siempre porque esto solo se
    /// calcula una vez al agarrar.
    ///
    /// La corrección: en vez de Renderer.bounds, se usa el bounds LOCAL de cada malla
    /// (Mesh.bounds -- tight y fijo, no depende de a qué ángulo esté rotado el objeto en este
    /// instante) y se transforman sus 8 esquinas a través del transform real de cada Renderer
    /// hasta llegar al espacio local del objeto raíz. El resultado es la huella/caja real del
    /// mueble, sin importar en qué ángulo esté parado.
    /// </summary>
    public static void ComputeLocalBounds(GameObject obj, out Vector3 halfExtents, out Vector3 centerOffset)
    {
        halfExtents = new Vector3(0.25f, 0.25f, 0.25f);
        centerOffset = Vector3.zero;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        bool huboAlgunaEsquina = false;

        foreach (var rend in renderers)
        {
            Bounds localBounds;
            MeshFilter mf = rend.GetComponent<MeshFilter>();
            SkinnedMeshRenderer smr = rend as SkinnedMeshRenderer;
            if (mf != null && mf.sharedMesh != null)
                localBounds = mf.sharedMesh.bounds;
            else if (smr != null && smr.sharedMesh != null)
                localBounds = smr.sharedMesh.bounds;
            else
                continue; // sin malla local conocida -- se ignora en vez de usar un AABB inflado

            Transform rendTransform = rend.transform;
            Vector3 center = localBounds.center;
            Vector3 ext = localBounds.extents;

            for (int dx = -1; dx <= 1; dx += 2)
            for (int dy = -1; dy <= 1; dy += 2)
            for (int dz = -1; dz <= 1; dz += 2)
            {
                Vector3 localCorner = center + Vector3.Scale(ext, new Vector3(dx, dy, dz));
                Vector3 worldCorner = rendTransform.TransformPoint(localCorner);
                Vector3 local = obj.transform.InverseTransformPoint(worldCorner);
                minX = Mathf.Min(minX, local.x); maxX = Mathf.Max(maxX, local.x);
                minY = Mathf.Min(minY, local.y); maxY = Mathf.Max(maxY, local.y);
                minZ = Mathf.Min(minZ, local.z); maxZ = Mathf.Max(maxZ, local.z);
                huboAlgunaEsquina = true;
            }
        }

        if (!huboAlgunaEsquina || minX > maxX || minY > maxY || minZ > maxZ) return;

        halfExtents = new Vector3((maxX - minX) * 0.5f, (maxY - minY) * 0.5f, (maxZ - minZ) * 0.5f);
        centerOffset = new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, (minZ + maxZ) * 0.5f);
    }

    private void EnsureFootprintOutlineVisual()
    {
        if (footprintOutlineVisual != null) return;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        footprintMaterialGreen = new Material(shader) { color = new Color(0.2f, 0.95f, 0.3f) };
        footprintMaterialRed = new Material(shader) { color = new Color(0.95f, 0.2f, 0.2f) };

        footprintOutlineVisual = new GameObject("FurnitureFootprintOutline");
        LineRenderer line = footprintOutlineVisual.AddComponent<LineRenderer>();
        line.material = footprintMaterialGreen;
        line.positionCount = 5; // rectángulo cerrado: 4 esquinas + volver a la primera
        line.startWidth = 0.02f;
        line.endWidth = 0.02f;
        line.useWorldSpace = true;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        footprintLineRenderer = line;

        footprintOutlineVisual.SetActive(false);
    }

    /// <summary>
    /// Dibuja el rectángulo de huella del mueble sostenido, usando el ancho/profundidad ya
    /// calculados en StartFootprintTracking y la posición/rotación (solo el giro en Y, que es el
    /// que importa para "en qué dirección queda parado en el piso") actuales del mueble. Se pone
    /// rojo si ese rectángulo se superpone con un muro o con otro mueble ya colocado -- solo como
    /// aviso visual, nunca bloquea soltar el mueble ahí.
    /// </summary>
    private void UpdateFootprintOutline(GameObject obj)
    {
        if (footprintLineRenderer == null) return;

        float yaw = obj.transform.eulerAngles.y;
        Quaternion yawRot = Quaternion.Euler(0f, yaw, 0f);

        Vector3 centerWorld = obj.transform.position
            + yawRot * new Vector3(heldFootprintLocalCenterOffset.x, 0f, heldFootprintLocalCenterOffset.y);
        centerWorld.y = 0.02f; // apenas arriba del piso, igual que la grilla

        Vector3 right = yawRot * Vector3.right * heldFootprintHalfExtents.x;
        Vector3 fwd = yawRot * Vector3.forward * heldFootprintHalfExtents.y;

        Vector3 c0 = centerWorld - right - fwd;
        Vector3 c1 = centerWorld + right - fwd;
        Vector3 c2 = centerWorld + right + fwd;
        Vector3 c3 = centerWorld - right + fwd;

        footprintLineRenderer.SetPosition(0, c0);
        footprintLineRenderer.SetPosition(1, c1);
        footprintLineRenderer.SetPosition(2, c2);
        footprintLineRenderer.SetPosition(3, c3);
        footprintLineRenderer.SetPosition(4, c0);

        bool superpuesto = IsFootprintOverlappingWallOrFurniture(obj, centerWorld, yawRot);
        footprintLineRenderer.material = superpuesto ? footprintMaterialRed : footprintMaterialGreen;
    }

    /// <summary>
    /// Chequeo puramente visual (nunca bloquea nada): tira una caja (Physics.OverlapBox) del
    /// tamaño de la huella, un poco elevada del piso para no engancharse con el piso mismo, y ve
    /// si golpea algún muro u otro mueble que no sea el propio objeto sostenido.
    /// </summary>
    private bool IsFootprintOverlappingWallOrFurniture(GameObject obj, Vector3 centerWorld, Quaternion yawRot)
    {
        Vector3 boxCenter = centerWorld + Vector3.up * 0.15f;
        Vector3 halfExtentsBox = new Vector3(
            Mathf.Max(0.02f, heldFootprintHalfExtents.x - 0.02f),
            0.12f,
            Mathf.Max(0.02f, heldFootprintHalfExtents.y - 0.02f));

        Collider[] hits = Physics.OverlapBox(boxCenter, halfExtentsBox, yawRot);
        foreach (var hit in hits)
        {
            if (hit == null) continue;
            Transform root = hit.transform;
            // Ignorar colliders que son del propio mueble sostenido (o hijos suyos).
            if (root.IsChildOf(obj.transform) || root == obj.transform) continue;

            var metadata = root.GetComponentInParent<SceneElementMetadata>();
            string tipo = (metadata != null ? metadata.elementType : "")?.ToLower() ?? "";
            // El piso no cuenta como "superposición" (siempre está ahí debajo, no es un aviso útil).
            if (tipo == "piso") continue;

            return true;
        }
        return false;
    }

    /// <summary>
    /// Agrega un BoxCollider ajustado a los Renderer combinados del mueble si no tiene ningún
    /// Collider propio ni en sus hijos. Los prefabs del Furniture Mega Pack no traen Collider,
    /// por lo que sin esto no hay colisión física (los muebles "flotan"/se atraviesan) y el
    /// XRSimpleInteractable nunca puede detectar el rayo del control (no hay nada que golpear).
    /// </summary>
    private void EnsureFurnitureCollider(GameObject obj)
    {
        if (obj.GetComponentInChildren<Collider>() != null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            combined.Encapsulate(renderers[i].bounds);

        BoxCollider box = obj.AddComponent<BoxCollider>();
        box.center = obj.transform.InverseTransformPoint(combined.center);

        Vector3 sizeWorld = combined.size;
        Vector3 lossyScale = obj.transform.lossyScale;
        box.size = new Vector3(
            lossyScale.x != 0 ? sizeWorld.x / lossyScale.x : sizeWorld.x,
            lossyScale.y != 0 ? sizeWorld.y / lossyScale.y : sizeWorld.y,
            lossyScale.z != 0 ? sizeWorld.z / lossyScale.z : sizeWorld.z
        );
    }

    /// <summary>
    /// Corrige una posición "deseada" (por ejemplo, un menú flotante calculado a una distancia fija
    /// frente a la cámara) cuando hay geometría en el medio -- una pared o un mueble cercano -- que
    /// haría que esa posición caiga adentro o detrás de esa geometría, dejando el menú tapado por
    /// el z-fighting/oclusión normal de profundidad. Si un raycast desde "from" hacia "desired"
    /// golpea algo antes de llegar, devuelve un punto justo delante de ese obstáculo.
    /// </summary>
    public static Vector3 ClampInFrontOfObstruction(Vector3 from, Vector3 desired)
    {
        Vector3 direction = desired - from;
        float distance = direction.magnitude;
        if (distance < 0.01f) return desired;

        if (Physics.Raycast(from, direction.normalized, out RaycastHit hit, distance))
            return hit.point - direction.normalized * 0.08f;

        return desired;
    }

    /// <summary>
    /// Busca (o crea, una sola vez) el manipulador compartido de mover/rotar con su menú de UI.
    /// </summary>
    private void EnsureSharedManipulator()
    {
        if (sharedManipulator == null)
            sharedManipulator = Object.FindAnyObjectByType<FurnitureManipulatorVR>();

        if (sharedManipulator == null)
        {
            GameObject managerGO = new GameObject("SharedFurnitureManipulator");
            sharedManipulator = managerGO.AddComponent<FurnitureManipulatorVR>();
            ManipulatorMenuFactory.BuildMenuFor(sharedManipulator);
        }
    }

    /// <summary>
    /// Genera una paleta de materiales de colores para MaterialChangerVR, usando el mismo
    /// shader que ya tiene el objeto (para mantener consistencia visual con URP).
    /// </summary>
    private Material[] GenerateColorPalette(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null)
            renderer = obj.GetComponentInChildren<Renderer>();

        Shader shader = (renderer != null && renderer.sharedMaterial != null && renderer.sharedMaterial.shader != null)
            ? renderer.sharedMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        Color[] palette = new Color[]
        {
            new Color(0.82f, 0.82f, 0.82f), // gris claro
            new Color(0.36f, 0.24f, 0.16f), // marron / cuero
            new Color(0.16f, 0.35f, 0.65f), // azul
            new Color(0.24f, 0.55f, 0.30f)  // verde
        };

        Material[] mats = new Material[palette.Length];
        for (int i = 0; i < palette.Length; i++)
        {
            Material mat = new Material(shader);
            mat.color = palette[i];
            mats[i] = mat;
        }
        return mats;
    }

    /// <summary>
    /// Mueve al jugador (XR Origin) a un punto dentro de las 4 paredes de la sala que se acaba
    /// de generar, usando las dimensiones de room_info.dimensions del JSON (o 4x4m si faltan).
    /// Lo ubica cerca de una esquina (con margen) en vez de en el centro exacto, para minimizar
    /// la chance de aparecer encima de un mueble en presets con el centro de la sala ocupado.
    /// No rota al jugador: en VR es más natural que gire la cabeza a que se le imponga una
    /// orientación fija.
    /// </summary>
    private void RepositionPlayerInRoom(JObject sceneData)
    {
        float width = 4f, depth = 4f;
        var roomInfo = sceneData["room_info"] as JObject;
        var dims = roomInfo?["dimensions"] as JObject;
        if (dims != null)
        {
            width = dims["x"]?.Value<float>() ?? width;
            depth = dims["z"]?.Value<float>() ?? depth;
        }

        // Guardado para FinalizePlacement (ver HacerInteractivo): así un mueble movido a mano no
        // puede terminar atravesando una pared, más allá de los límites reales de esta sala.
        roomWidth = width;
        roomDepth = depth;

        Transform rigTransform = null;
        XROrigin xrOrigin = Object.FindAnyObjectByType<XROrigin>();
        if (xrOrigin != null)
            rigTransform = xrOrigin.transform;

        if (rigTransform == null)
        {
            GameObject rigGO = GameObject.Find("XR Origin Hands (XR Rig)");
            if (rigGO != null) rigTransform = rigGO.transform;
        }

        if (rigTransform == null)
        {
            Debug.LogWarning("SceneGenerator: no se encontró el XR Origin; no se pudo reubicar al jugador dentro de la sala generada.");
            return;
        }

        float margin = 0.6f;
        float px = Mathf.Clamp(margin, 0.1f, Mathf.Max(0.1f, width - 0.1f));
        float pz = Mathf.Clamp(margin, 0.1f, Mathf.Max(0.1f, depth - 0.1f));

        rigTransform.position = new Vector3(px, 0f, pz);

        // La papelera de la entrada y el gatito ayudante (puntos 4 y 5 del plan de edición
        // avanzada) se eliminaron a pedido de Alan (2026-09-19, ajuste punto 1): eliminar un
        // objeto ahora se hace clickeándolo y usando "🗑️ Eliminar" en el menú que aparece
        // (CrudPanelController), y cambiar de perspectiva/rotación se maneja desde ahí mismo y
        // el botón flotante de RotationModeController -- ya no hace falta reubicar nada acá.
    }

    private void ClearPreviousScene()
    {
        // Destroy all previously generated objects
        foreach (var obj in generatedObjects)
        {
            if (obj != null)
                Destroy(obj);
        }
        generatedObjects.Clear();
    }

    private Vector3 ParseVector3(JToken token)
    {
        if (token is JObject obj)
        {
            float x = obj["x"]?.Value<float>() ?? 0;
            float y = obj["y"]?.Value<float>() ?? 0;
            float z = obj["z"]?.Value<float>() ?? 0;
            return new Vector3(x, y, z);
        }
        return Vector3.zero;
    }

    public int GetGeneratedObjectCount()
    {
        return generatedObjects.Count;
    }

    public bool IsGenerating()
    {
        return isGenerating;
    }

    /// <summary>
    /// Serializa el estado ACTUAL de la escena (posición/rotación/escala/color reales de cada
    /// objeto en generatedObjects, incluyendo los agregados desde el catálogo vía
    /// RegisterExternalObject) de vuelta al mismo schema de Scene Graph JSON que usa todo el
    /// resto del pipeline. Pensado para guardarse con SceneGraphSaveSystem.GuardarEscena y
    /// recargarse después con GenerateSceneAsync, cerrando el ciclo completo de "guardar mi
    /// escena tal cual la dejé" sin depender del servidor.
    /// </summary>
    public string ExportCurrentSceneToJson(string sceneName = "Mi escena")
    {
        var elements = new JArray();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        int count = 0;

        foreach (var obj in generatedObjects)
        {
            if (obj == null) continue;
            count++;

            var metadata = obj.GetComponent<SceneElementMetadata>();
            string type = (metadata != null && !string.IsNullOrEmpty(metadata.elementType))
                ? metadata.elementType
                : "misc_furniture";
            string id = (metadata != null && !string.IsNullOrEmpty(metadata.elementId))
                ? metadata.elementId
                : obj.name;

            Color color = Color.white;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                color = renderer.sharedMaterial.color;

            Vector3 pos = obj.transform.position;
            Vector3 rot = obj.transform.eulerAngles;
            Vector3 scale = obj.transform.localScale;

            var element = new JObject
            {
                ["id"] = id,
                ["type"] = type,
                ["confidence"] = 1.0f,
                ["position"] = new JObject { ["x"] = pos.x, ["y"] = pos.y, ["z"] = pos.z },
                ["rotation"] = new JObject { ["x"] = rot.x, ["y"] = rot.y, ["z"] = rot.z },
                ["scale"] = new JObject { ["x"] = scale.x, ["y"] = scale.y, ["z"] = scale.z },
                ["material"] = new JObject { ["color"] = "#" + ColorUtility.ToHtmlStringRGB(color) }
            };
            elements.Add(element);

            minX = Mathf.Min(minX, pos.x); maxX = Mathf.Max(maxX, pos.x);
            minZ = Mathf.Min(minZ, pos.z); maxZ = Mathf.Max(maxZ, pos.z);
        }

        float width = count > 0 ? Mathf.Max(maxX - minX, 1f) : 4f;
        float depth = count > 0 ? Mathf.Max(maxZ - minZ, 1f) : 4f;

        var sceneGraph = new JObject
        {
            ["metadata"] = new JObject
            {
                ["name"] = sceneName,
                ["description"] = "Escena guardada por el usuario desde el visor",
                ["dimensions"] = new JObject { ["width"] = width, ["depth"] = depth, ["height"] = 2.5f },
                ["furniture_count"] = count,
                ["created_date"] = System.DateTime.UtcNow.ToString("o"),
                ["processing_time_ms"] = 0,
                ["scale_confidence"] = 1.0f,
                ["scale_source"] = "manual"
            },
            ["room_info"] = new JObject
            {
                ["room_type"] = "guardado",
                ["dimensions"] = new JObject { ["x"] = width, ["y"] = 2.5f, ["z"] = depth },
                ["flooring_material"] = "wood",
                ["wall_color"] = "white"
            },
            ["scene_elements"] = elements
        };

        return sceneGraph.ToString();
    }
}
