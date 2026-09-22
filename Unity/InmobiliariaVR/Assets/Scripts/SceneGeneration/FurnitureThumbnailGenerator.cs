#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// EDITOR SCRIPT - Genera un thumbnail PNG por cada prefab único del PrefabDatabase, usando
/// el generador de previews nativo de Unity (AssetPreview.GetAssetPreview), y los deja
/// importados como Sprite en Assets/Resources/FurnitureThumbnails/&lt;NombrePrefab&gt;.png.
///
/// FurnitureCatalogController los busca automáticamente por nombre de prefab
/// (Resources.Load&lt;Sprite&gt;("FurnitureThumbnails/" + prefab.name)); si un prefab no tiene
/// thumbnail generado, el catálogo simplemente muestra un botón liso con el nombre -- no rompe
/// nada, así que este generador se puede correr en cualquier momento (y volver a correr cada
/// vez que se agreguen prefabs nuevos al Furniture Mega Pack / se reconstruya PrefabDatabase).
///
/// Ejecutar: Menu > InmobiliariaVR > Generar Thumbnails de Muebles
/// (Editor únicamente -- AssetPreview no existe en builds, por eso este archivo está envuelto
/// en #if UNITY_EDITOR y los PNG se generan una sola vez en el Editor, no en el Quest).
/// </summary>
public static class FurnitureThumbnailGenerator
{
    private const string OUTPUT_FOLDER = "Assets/Resources/FurnitureThumbnails";
    private const string DATABASE_PATH = "Assets/Resources/PrefabDatabase.asset";
    private const int TEXTURE_SIZE = 256;
    private const int MAX_INTENTOS_POR_PREVIEW = 60; // ~1 segundo a 60fps de EditorApplication.update

    [MenuItem("InmobiliariaVR/Generar Thumbnails de Muebles")]
    public static void GenerarThumbnails()
    {
        PrefabDatabase database = AssetDatabase.LoadAssetAtPath<PrefabDatabase>(DATABASE_PATH);
        if (database == null)
        {
            Debug.LogError($"FurnitureThumbnailGenerator: No se encontró {DATABASE_PATH}. " +
                            "Ejecuta Menu > InmobiliariaVR > Build Prefab Database primero.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OUTPUT_FOLDER))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            AssetDatabase.CreateFolder("Assets/Resources", "FurnitureThumbnails");
        }

        // Recolectar todos los prefabs únicos (varias keys pueden compartir el mismo prefab,
        // ej. "Sofas" y no tiene sub-key, pero "Kitchen" y "Kitchen/Refrigerator" sí se solapan).
        var prefabsUnicos = new Dictionary<string, GameObject>();
        foreach (var entry in database.entries)
        {
            if (entry.variants == null) continue;
            foreach (var prefab in entry.variants)
            {
                if (prefab != null && !prefabsUnicos.ContainsKey(prefab.name))
                    prefabsUnicos[prefab.name] = prefab;
            }
        }

        Debug.Log($"=== Generando thumbnails para {prefabsUnicos.Count} prefabs únicos ===");
        EditorCoroutineRunner.Start(GenerarTodosCoroutine(new List<GameObject>(prefabsUnicos.Values)));
    }

    private static IEnumerator GenerarTodosCoroutine(List<GameObject> prefabs)
    {
        int generados = 0;
        int saltados = 0;

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            string outputPath = $"{OUTPUT_FOLDER}/{prefab.name}.png";

            EditorUtility.DisplayProgressBar("Generando thumbnails de muebles",
                $"{prefab.name} ({i + 1}/{prefabs.Count})", (float)i / prefabs.Count);

            Texture2D preview = null;
            int intentos = 0;

            // AssetPreview.GetAssetPreview es asíncrono: la primera llamada dispara la
            // generación y devuelve null hasta que está lista, así que hay que reintentar
            // durante unos frames (AssetPreview.IsLoadingAssetPreview indica si sigue en curso).
            while (intentos < MAX_INTENTOS_POR_PREVIEW)
            {
                preview = AssetPreview.GetAssetPreview(prefab);
                // Unity reemplazó los overloads basados en int instanceID por EntityId
                // (GetInstanceID()/IsLoadingAssetPreview(int) quedaron obsoletos como error
                // en esta versión de Unity 6) -- se usa GetEntityId() en su lugar.
                if (preview != null && !AssetPreview.IsLoadingAssetPreview(prefab.GetEntityId()))
                    break;

                intentos++;
                yield return null;
            }

            if (preview == null)
            {
                Debug.LogWarning($"FurnitureThumbnailGenerator: no se pudo generar preview para '{prefab.name}' " +
                                  "(se agotaron los reintentos). El catálogo mostrará un botón liso para este mueble.");
                saltados++;
                continue;
            }

            byte[] png = RedimensionarYCodificar(preview, TEXTURE_SIZE);
            File.WriteAllBytes(outputPath, png);
            generados++;
        }

        EditorUtility.ClearProgressBar();
        AssetDatabase.Refresh();

        // Configurar cada PNG importado como Sprite (Texture Type = Sprite) para que
        // Resources.Load<Sprite> funcione.
        foreach (var prefab in prefabs)
        {
            string outputPath = $"{OUTPUT_FOLDER}/{prefab.name}.png";
            TextureImporter importer = AssetImporter.GetAtPath(outputPath) as TextureImporter;
            if (importer == null) continue;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        Debug.Log($"=== Thumbnails generados: {generados}, saltados: {saltados} ===");
        Debug.Log($"Guardados en: {OUTPUT_FOLDER}");
    }

    /// <summary>
    /// AssetPreview.GetAssetPreview devuelve texturas no siempre del mismo tamaño (y no
    /// necesariamente legibles/comprimibles directamente); se redibuja sobre una RenderTexture
    /// cuadrada del tamaño deseado antes de codificar a PNG, para tener thumbnails consistentes.
    /// </summary>
    private static byte[] RedimensionarYCodificar(Texture2D origen, int tamano)
    {
        RenderTexture rt = RenderTexture.GetTemporary(tamano, tamano);
        RenderTexture previo = RenderTexture.active;

        Graphics.Blit(origen, rt);
        RenderTexture.active = rt;

        Texture2D resultado = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false);
        resultado.ReadPixels(new Rect(0, 0, tamano, tamano), 0, 0);
        resultado.Apply();

        RenderTexture.active = previo;
        RenderTexture.ReleaseTemporary(rt);

        byte[] png = resultado.EncodeToPNG();
        Object.DestroyImmediate(resultado);
        return png;
    }
}

/// <summary>
/// Mini-runner para poder usar una IEnumerator como si fuera una coroutine, fuera de Play mode
/// (donde MonoBehaviour.StartCoroutine no está disponible). Se apoya en EditorApplication.update.
/// </summary>
public static class EditorCoroutineRunner
{
    public static void Start(IEnumerator rutina)
    {
        EditorApplication.CallbackFunction actualizar = null;
        actualizar = () =>
        {
            try
            {
                if (!rutina.MoveNext())
                    EditorApplication.update -= actualizar;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"EditorCoroutineRunner: error ejecutando rutina: {ex}");
                EditorApplication.update -= actualizar;
            }
        };
        EditorApplication.update += actualizar;
    }
}
#endif
