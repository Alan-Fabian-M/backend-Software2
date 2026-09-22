using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// EDITOR SCRIPT - Auto-setup para la generación de escenas.
/// Ejecutar desde Unity Editor: Menu > InmobiliariaVR > Setup Scene Generation
///
/// IMPORTANTE: Ejecutar esto SOLO en modo Edición (con el juego detenido, sin Play activo).
/// Los objetos creados durante Play mode son temporales y desaparecen al presionar Stop.
///
/// Esto crea automáticamente:
/// 1. GameObject "SceneGeneratorManager" con SceneGenerator
/// 2. GameObject "PresetLoaderUI" con PresetLoaderController
/// 3. Configura todos los parámetros necesarios
/// </summary>

#if UNITY_EDITOR
public class SceneGenerationSetup : MonoBehaviour
{
    [MenuItem("InmobiliariaVR/Setup Scene Generation")]
    public static void SetupSceneGeneration()
    {
        // Protección: no permitir ejecutar esto durante Play mode.
        // Los objetos creados en Play mode son temporales y EditorSceneManager.MarkSceneDirty
        // lanza una excepción si se llama mientras el juego está corriendo.
        if (EditorApplication.isPlaying)
        {
            Debug.LogError("⛔ No puedes ejecutar 'Setup Scene Generation' mientras el juego está en Play mode. " +
                            "Presiona el botón Stop (■) arriba primero, y vuelve a intentarlo en modo Edición.");
            EditorUtility.DisplayDialog(
                "Detén el Play mode primero",
                "Setup Scene Generation debe ejecutarse en modo Edición (sin Play activo).\n\n" +
                "Presiona Stop (■) y vuelve a intentarlo.",
                "Entendido"
            );
            return;
        }

        Debug.Log("=== Iniciando Setup de Generación de Escenas ===");

        // Step 1: Create SceneGeneratorManager (evita duplicados si ya existe)
        SceneGenerator generator = Object.FindAnyObjectByType<SceneGenerator>();
        if (generator == null)
        {
            GameObject managerGO = new GameObject("SceneGeneratorManager");
            generator = managerGO.AddComponent<SceneGenerator>();
            Debug.Log("✓ SceneGenerator creado");
        }
        else
        {
            Debug.Log("✓ SceneGenerator ya existía, no se duplicó");
        }

        // Step 2: Create PresetLoaderUI (evita duplicados si ya existe)
        PresetLoaderController loader = Object.FindAnyObjectByType<PresetLoaderController>();
        if (loader == null)
        {
            GameObject uiGO = new GameObject("PresetLoaderUI");
            loader = uiGO.AddComponent<PresetLoaderController>();
            Debug.Log("✓ PresetLoaderController creado");
        }
        else
        {
            Debug.Log("✓ PresetLoaderController ya existía, no se duplicó");
        }

        // Step 3: Create ToastNotificationManager if it doesn't exist
        if (Object.FindAnyObjectByType<ToastNotificationUI>() == null)
        {
            GameObject toastGO = new GameObject("ToastNotificationManager");
            toastGO.AddComponent<ToastNotificationUI>();
            Debug.Log("✓ ToastNotificationUI creado");
        }
        else
        {
            Debug.Log("✓ ToastNotificationUI ya existía, no se duplicó");
        }

        // Step 4: Create FurnitureCatalogController if it doesn't exist (botón "Catálogo"
        // flotante + panel con pestañas para agregar muebles no presentes en el croquis)
        if (Object.FindAnyObjectByType<FurnitureCatalogController>() == null)
        {
            GameObject catalogGO = new GameObject("FurnitureCatalogManager");
            catalogGO.AddComponent<FurnitureCatalogController>();
            Debug.Log("✓ FurnitureCatalogController creado");
        }
        else
        {
            Debug.Log("✓ FurnitureCatalogController ya existía, no se duplicó");
        }

        // Step 5: Create SceneSaveLoadController if it doesn't exist (botón "Mis Escenas"
        // flotante para guardar/cargar/borrar escenas localmente en el Quest)
        if (Object.FindAnyObjectByType<SceneSaveLoadController>() == null)
        {
            GameObject saveLoadGO = new GameObject("SceneSaveLoadManager");
            saveLoadGO.AddComponent<SceneSaveLoadController>();
            Debug.Log("✓ SceneSaveLoadController creado");
        }
        else
        {
            Debug.Log("✓ SceneSaveLoadController ya existía, no se duplicó");
        }

        // Step 6: Save scene (ahora seguro porque ya verificamos que NO estamos en Play mode)
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("✓ Escena marcada como modificada (guarda manualmente con Ctrl+S)");

        Debug.Log("=== Setup Completado ===");
        Debug.Log("Próximos pasos:");
        Debug.Log("1. Guarda la escena con Ctrl+S");
        Debug.Log("2. Presiona Play en el editor");
        Debug.Log("3. Verás el menú de presets flotante");
        Debug.Log("4. Haz click en un preset para generar la escena");
        Debug.Log("5. Interactúa con los objetos: toca, mueve, elimina (doble-click)");
    }
}
#endif
