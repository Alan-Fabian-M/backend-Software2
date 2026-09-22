using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Maps YOLO detection types to prefab references from the PrefabDatabase.
/// Handles prefab variant selection based on detection confidence scores.
///
/// Requiere que exista Assets/Resources/PrefabDatabase.asset, generado con
/// Menu > InmobiliariaVR > Build Prefab Database (ver PrefabDatabaseBuilder.cs).
/// </summary>
public class PrefabMapper : MonoBehaviour
{
    private static PrefabDatabase _database;

    private static PrefabDatabase Database
    {
        get
        {
            if (_database == null)
                _database = Resources.Load<PrefabDatabase>("PrefabDatabase");
            return _database;
        }
    }

    // type detectado -> posibles keys en el PrefabDatabase, en orden de prioridad.
    // La primera key que tenga variantes disponibles es la que se usa.
    private static Dictionary<string, string[]> typeToKeys = new Dictionary<string, string[]>()
    {
        { "sofa", new[] { "Sofas" } },
        { "mesa", new[] { "Tables" } },
        { "cama", new[] { "Beds" } },
        { "silla", new[] { "Chairs" } },
        { "estanteria", new[] { "Closets" } },
        { "armario", new[] { "Closets" } },
        { "tv", new[] { "Tables" } },
        { "escritorio", new[] { "Tables" } },
        { "cajon", new[] { "Drawers" } },
        { "cojin", new[] { "Cushioins", "Cushions" } }, // "Cushioins" = typo real de la carpeta del asset pack

        { "refrigerador", new[] { "Kitchen/Refrigerator", "Kitchen" } },
        { "horno", new[] { "Kitchen/KitchenOven", "Kitchen" } },
        { "microondas", new[] { "Kitchen/MicrowaveOven", "Kitchen" } },
        { "estufa", new[] { "Kitchen/GasStove", "Kitchen" } },

        { "bano_completo", new[] { "Bathroom" } },
        { "bañera", new[] { "Bathroom/BathTub", "Bathroom" } },
        { "inodoro", new[] { "Bathroom/Toilet", "Bathroom" } },
        { "lavabo", new[] { "Bathroom/WashBasin", "Bathroom" } }
    };

    public struct PrefabInfo
    {
        public GameObject prefab;
        public string category;
        public int variantNumber;
        public bool isValid;
    }

    /// <summary>
    /// Resuelve una detección YOLO (o un "type" ya guardado de una escena exportada) a un
    /// GameObject prefab concreto, listo para instanciar.
    ///
    /// Primero intenta el vocabulario corto en español (sofa, mesa, cama...). Si eso falla,
    /// intenta usar el propio "detectionType" directamente como key del PrefabDatabase
    /// (ej: "Sofas", "Kitchen/Refrigerator"). Este segundo camino es necesario porque el
    /// catálogo de muebles (FurnitureCatalogController) guarda como "type" la key real de la
    /// categoría elegida por el usuario -- no siempre corresponde a una palabra del vocabulario
    /// YOLO -- y ese mismo valor es el que se vuelve a leer al recargar una escena guardada con
    /// SceneGenerator.ExportCurrentSceneToJson / SceneGraphSaveSystem.
    /// </summary>
    public static PrefabInfo ResolvePrefab(string detectionType, float confidence)
    {
        var info = new PrefabInfo { isValid = false };

        if (string.IsNullOrEmpty(detectionType))
        {
            Debug.LogWarning("PrefabMapper: detectionType vacío o nulo");
            return info;
        }

        if (Database == null)
        {
            Debug.LogError("PrefabMapper: No se encontró PrefabDatabase en Resources. " +
                            "Ejecuta Menu > InmobiliariaVR > Build Prefab Database primero.");
            return info;
        }

        string normalizedType = detectionType.ToLower().Trim();
        string[] candidateKeys;

        if (typeToKeys.TryGetValue(normalizedType, out var mappedKeys))
        {
            candidateKeys = mappedKeys;
        }
        else if (Database.HasCategory(detectionType))
        {
            // Fallback: detectionType ya ES una key directa del PrefabDatabase.
            candidateKeys = new[] { detectionType };
        }
        else
        {
            Debug.LogWarning($"PrefabMapper: Tipo de detección desconocido '{detectionType}'");
            return info;
        }

        // Buscar la primera key candidata que tenga variantes disponibles
        string resolvedKey = null;
        int variantCount = 0;
        foreach (var key in candidateKeys)
        {
            int count = Database.GetVariantCount(key);
            if (count > 0)
            {
                resolvedKey = key;
                variantCount = count;
                break;
            }
        }

        if (resolvedKey == null)
        {
            Debug.LogWarning($"PrefabMapper: No hay prefabs para el tipo '{detectionType}' " +
                              $"(keys probadas: {string.Join(", ", candidateKeys)}). " +
                              $"¿Ejecutaste Build Prefab Database?");
            return info;
        }

        int variantNumber = SelectVariant(variantCount, confidence);
        GameObject prefab = Database.GetPrefab(resolvedKey, variantNumber);

        if (prefab == null)
        {
            Debug.LogWarning($"PrefabMapper: No se pudo obtener el prefab de '{resolvedKey}' variante {variantNumber}");
            return info;
        }

        info.prefab = prefab;
        info.category = resolvedKey;
        info.variantNumber = variantNumber;
        info.isValid = true;
        return info;
    }

    /// <summary>
    /// Selecciona un número de variante según la confianza de YOLO.
    /// Alta confianza -> variante estándar (1)
    /// Media confianza -> variantes del rango medio (aleatorio)
    /// Baja confianza -> variantes del rango inicial (aleatorio, más variedad)
    /// </summary>
    private static int SelectVariant(int maxVariants, float confidence)
    {
        confidence = Mathf.Clamp01(confidence);

        if (maxVariants <= 1)
            return 1;

        if (confidence >= 0.85f)
        {
            return 1;
        }
        else if (confidence >= 0.70f)
        {
            int min = Mathf.Max(1, maxVariants / 3);
            int max = Mathf.Max(min + 1, (maxVariants * 2) / 3);
            return UnityEngine.Random.Range(min, max + 1);
        }
        else
        {
            int min = 1;
            int max = Mathf.Max(2, maxVariants / 3);
            return UnityEngine.Random.Range(min, max + 1);
        }
    }

    /// <summary>
    /// Gets metadata for a specific furniture type.
    /// </summary>
    public static FurnitureMetadata GetMetadata(string detectionType)
    {
        var metadata = new FurnitureMetadata();

        switch ((detectionType ?? "").ToLower())
        {
            case "piso":
                metadata.selectable = true;
                metadata.deletable = false;
                metadata.colorable = true;
                metadata.movable = false;
                break;

            case "muro":
            case "puerta":
            case "ventana":
                metadata.selectable = false;
                metadata.deletable = false;
                metadata.colorable = true;
                metadata.movable = false;
                break;

            case "sofa":
            case "mesa":
            case "cama":
            case "silla":
            case "estanteria":
            case "armario":
                metadata.selectable = true;
                metadata.deletable = true;
                metadata.colorable = true;
                metadata.movable = true;
                break;

            case "tv":
                metadata.selectable = true;
                metadata.deletable = true;
                metadata.colorable = false;
                metadata.movable = true;
                break;

            default:
                // Incluye los muebles agregados desde el catálogo (cuyo "type" es una key
                // cruda del PrefabDatabase, ej: "Sofas", "Kitchen/Refrigerator") -- por defecto
                // son completamente interactivos, igual que cualquier mueble desconocido.
                metadata.selectable = true;
                metadata.deletable = true;
                metadata.colorable = true;
                metadata.movable = true;
                break;
        }

        return metadata;
    }

    /// <summary>
    /// Lista las categorías de "nivel superior" del PrefabDatabase (las que corresponden
    /// directamente a una carpeta de Furniture Mega Pack, sin sub-prefijo: "Sofas", "Kitchen",
    /// "Bathroom", etc.) que tengan al menos un prefab. Pensado para armar las pestañas del
    /// catálogo de muebles (FurnitureCatalogController).
    /// </summary>
    public static List<string> GetTopLevelCategories()
    {
        var result = new List<string>();
        if (Database == null) return result;

        foreach (var entry in Database.entries)
        {
            if (!string.IsNullOrEmpty(entry.key) && !entry.key.Contains("/") && entry.variants != null && entry.variants.Count > 0)
                result.Add(entry.key);
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// Devuelve los prefabs (variantes) disponibles bajo una key exacta del PrefabDatabase
    /// (típicamente una de las categorías devueltas por GetTopLevelCategories). Pensado para
    /// que el catálogo muestre todas las variantes de una pestaña.
    /// </summary>
    public static List<GameObject> GetVariants(string categoryKey)
    {
        if (Database == null || string.IsNullOrEmpty(categoryKey))
            return new List<GameObject>();

        var entry = Database.entries.FirstOrDefault(e => e.key == categoryKey);
        return entry != null && entry.variants != null ? entry.variants : new List<GameObject>();
    }

    [System.Serializable]
    public class FurnitureMetadata
    {
        public bool selectable = true;
        public bool deletable = true;
        public bool colorable = true;
        public bool movable = true;
    }
}
