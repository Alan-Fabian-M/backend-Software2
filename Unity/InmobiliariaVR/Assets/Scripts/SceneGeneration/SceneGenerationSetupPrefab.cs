using UnityEngine;

/// <summary>
/// Prefab auto-configurador para Scene Generation Pipeline.
///
/// Instrucciones:
/// 1. Descarga el Prefab: Assets/Prefabs/SceneGenerationManager.prefab
/// 2. Drag-drop en la escena Sala_MVP.unity
/// 3. ¡Listo! Todo está configurado automáticamente
///
/// Este script se ejecuta automáticamente OnEnable para configurar todo.
/// </summary>
public class SceneGenerationSetupPrefab : MonoBehaviour
{
    private void Awake()
    {
        // Auto-setup cuando el prefab se instantia
        SetupSceneGeneration();
    }

    private void SetupSceneGeneration()
    {
        Debug.Log("🎯 Scene Generation Pipeline - Auto Setup iniciado");

        // Step 1: Asegurar que existe SceneGenerator
        SceneGenerator generator = GetComponent<SceneGenerator>();
        if (generator == null)
        {
            generator = gameObject.AddComponent<SceneGenerator>();
            Debug.Log("✓ SceneGenerator agregado a este GameObject");
        }

        // Step 2: Crear o encontrar PresetLoaderUI
        PresetLoaderController loader = Object.FindAnyObjectByType<PresetLoaderController>();
        if (loader == null)
        {
            GameObject loaderGO = new GameObject("PresetLoaderUI");
            loaderGO.transform.SetParent(null);
            loader = loaderGO.AddComponent<PresetLoaderController>();
            Debug.Log("✓ PresetLoaderUI creado");
        }

        // Step 3: Crear o encontrar ToastNotificationUI
        ToastNotificationUI toastUI = Object.FindAnyObjectByType<ToastNotificationUI>();
        if (toastUI == null)
        {
            GameObject toastGO = new GameObject("ToastNotificationManager");
            toastGO.transform.SetParent(null);
            toastUI = toastGO.AddComponent<ToastNotificationUI>();
            Debug.Log("✓ ToastNotificationUI creado");
        }

        Debug.Log("✅ Scene Generation Pipeline - LISTO PARA USAR");
        Debug.Log("Presiona Play para ver el menú de presets");

        // Auto-destroy este componente después de setup
        Destroy(this);
    }
}
