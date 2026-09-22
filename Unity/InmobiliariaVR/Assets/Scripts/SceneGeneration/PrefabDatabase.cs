using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject que almacena referencias directas a los prefabs de muebles del proyecto,
/// agrupados por categoría (nombre de carpeta) y sub-categoría (prefijo del nombre de archivo).
/// Se genera automáticamente con PrefabDatabaseBuilder (Editor > InmobiliariaVR > Build Prefab
/// Database), que desde la sexta ronda (2026-09-19) escanea VARIOS asset packs a la vez (el
/// "Furniture Mega Pack" original y "Brick Project Studio/Apartment Kit") y los combina acá --
/// para este ScriptableObject en sí no hay ninguna diferencia entre un pack y otro, todos los
/// prefabs terminan mezclados de la misma forma bajo sus categorías.
/// Vive en Assets/Resources/ para poder cargarse en runtime con Resources.Load, pero las
/// referencias a los prefabs funcionan igual en Editor y en builds (Quest 3 incluido).
/// </summary>
[CreateAssetMenu(fileName = "PrefabDatabase", menuName = "InmobiliariaVR/Prefab Database")]
public class PrefabDatabase : ScriptableObject
{
    [System.Serializable]
    public class CategoryEntry
    {
        public string key;
        public List<GameObject> variants = new List<GameObject>();
    }

    public List<CategoryEntry> entries = new List<CategoryEntry>();

    private Dictionary<string, List<GameObject>> lookup;

    private void BuildLookup()
    {
        lookup = new Dictionary<string, List<GameObject>>();
        foreach (var e in entries)
        {
            if (!string.IsNullOrEmpty(e.key))
                lookup[e.key] = e.variants;
        }
    }

    public bool HasCategory(string key)
    {
        if (lookup == null) BuildLookup();
        return lookup.ContainsKey(key) && lookup[key].Count > 0;
    }

    public GameObject GetPrefab(string key, int variantNumber)
    {
        if (lookup == null) BuildLookup();
        if (!lookup.TryGetValue(key, out var list) || list.Count == 0)
            return null;

        int index = Mathf.Clamp(variantNumber - 1, 0, list.Count - 1);
        return list[index];
    }

    public int GetVariantCount(string key)
    {
        if (lookup == null) BuildLookup();
        return lookup.TryGetValue(key, out var list) ? list.Count : 0;
    }

    /// <summary>
    /// Devuelve cuántas categorías/subcategorías tiene la base de datos (para debugging).
    /// </summary>
    public int TotalCategories => entries.Count;
}
