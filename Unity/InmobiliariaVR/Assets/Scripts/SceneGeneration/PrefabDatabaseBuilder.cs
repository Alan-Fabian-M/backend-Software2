#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
/// EDITOR SCRIPT - Escanea uno o más "asset packs" de muebles y construye el PrefabDatabase.
/// Ejecutar: Menu > InmobiliariaVR > Build Prefab Database
///
/// Agrupa los prefabs en dos niveles:
/// 1. Por carpeta de primer nivel dentro de cada pack (ej: "Sofas" -> los 50 Sofa01..Sofa50,
///    o "Bathroom" -> BathTub_Base_01, Toilet_Apt_01, etc.)
/// 2. Por prefijo de nombre dentro de cada carpeta (ej: "Kitchen/Refrigerator" -> Refrigerator01..07)
///
/// Esto funciona sin importar el naming exacto de cada asset pack: agrupa automáticamente
/// todo lo que comparte el mismo texto antes del primer dígito.
///
/// SEXTA RONDA (2026-09-19, pedido de Alan): agregado un segundo asset pack -- "Brick Project
/// Studio/Apartment Kit" -- además del "Furniture Mega Pack" original. Antes SOURCE_FOLDER era
/// una única carpeta; ahora SOURCE_FOLDERS es una lista, y el escaneo ACUMULA sobre el mismo
/// diccionario en vez de reiniciarlo en cada pack -- así, si dos packs distintos tienen una
/// carpeta de primer nivel con el mismo nombre (ej: los dos traen una carpeta "Bathroom" o
/// "Kitchen"), sus prefabs terminan MEZCLADOS bajo la misma categoría/pestaña del catálogo en
/// vez de que el segundo pack pise/reemplace al primero -- esto es justamente lo que evita el
/// "conflicto" entre packs que pedías: ningún mueble existente desaparece, y los nuevos se suman
/// a la pestaña que corresponda (o crean una pestaña nueva si el nombre de carpeta no existía
/// antes). Si el mismo prefab exacto apareciera dos veces (no debería pasar entre dos packs
/// distintos, pero por las dudas), se lo ignora la segunda vez.
///
/// Del pack "Apartment Kit" solo se toman las carpetas "_Prefabs/Furniture" (muebles en sí:
/// baño, dormitorio, cocina, living, misceláneos, mueble de vinos) y "_Prefabs/Props" (objetos
/// chicos de decoración: iluminación, electrónica, arte, elementos de cocina/baño, esculturas) --
/// exactamente el mismo trato que ya tienen los muebles del Furniture Mega Pack (selectable/
/// movable/colorable/deletable = true por defecto, ver PrefabMapper.GetMetadata, caso "default").
/// A propósito NO se incluyó "_Prefabs/Apt Build Kit" ni "_Prefabs/Structures" (paredes,
/// exteriores, techos, pisos/cielorrasos, escaleras, marcos de puertas/ventanas, molduras,
/// columnas): esas son piezas ESTRUCTURALES para armar el edificio en sí, no objetos sueltos
/// para amueblar una habitación ya armada -- este proyecto ya tiene su propio sistema de
/// paredes/muros (MuroEditable, WallDividerToolButtonController), así que agregarlas al
/// catálogo de muebles no encajaría con cómo se arma una sala acá. Si en realidad querés poder
/// agregar alguna pieza puntual de esas (por ejemplo, algún marco de puerta como objeto de
/// decoración), decime cuál y la agrego aparte.
///
/// Vuelve a ejecutar este menú cada vez que agregues o cambies prefabs en cualquiera de estos
/// packs.
/// </summary>
public static class PrefabDatabaseBuilder
{
    // Cada carpeta acá es un "asset pack" independiente que se escanea y se combina en el mismo
    // PrefabDatabase. Agregá una línea más acá si en el futuro sumás otro pack de muebles.
    private static readonly string[] SOURCE_FOLDERS = new string[]
    {
        "Assets/Furniture Mega Pack/Prefabs",
        "Assets/Brick Project Studio/Apartment Kit/_Prefabs/Furniture",
        "Assets/Brick Project Studio/Apartment Kit/_Prefabs/Props",
    };

    private const string OUTPUT_PATH = "Assets/Resources/PrefabDatabase.asset";

    [MenuItem("InmobiliariaVR/Build Prefab Database")]
    public static void BuildDatabase()
    {
        List<string> sourcesValidas = SOURCE_FOLDERS.Where(AssetDatabase.IsValidFolder).ToList();
        List<string> sourcesFaltantes = SOURCE_FOLDERS.Except(sourcesValidas).ToList();

        foreach (string faltante in sourcesFaltantes)
            Debug.LogWarning($"PrefabDatabaseBuilder: no se encontró la carpeta '{faltante}' -- se la salteó. " +
                              $"Si ese asset pack sí está en el proyecto pero en otra ruta, ajustá SOURCE_FOLDERS en PrefabDatabaseBuilder.cs.");

        if (sourcesValidas.Count == 0)
        {
            Debug.LogError("PrefabDatabaseBuilder: no se encontró NINGUNA de las carpetas configuradas en SOURCE_FOLDERS. Revisá las rutas.");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");

        PrefabDatabase database = AssetDatabase.LoadAssetAtPath<PrefabDatabase>(OUTPUT_PATH);
        if (database == null)
        {
            database = ScriptableObject.CreateInstance<PrefabDatabase>();
            AssetDatabase.CreateAsset(database, OUTPUT_PATH);
        }

        database.entries.Clear();

        // key -> lista de (nombreArchivo, prefab) -- se ACUMULA a través de todos los packs (ver
        // el comentario de clase) en vez de reiniciarse en cada uno, para que dos packs con una
        // carpeta del mismo nombre terminen combinados en la misma categoría/pestaña.
        var byKey = new Dictionary<string, List<(string name, GameObject go)>>();
        int totalPrefabs = 0;

        // Para el resumen final en consola: qué pack aportó qué categorías de primer nivel.
        var categoriasPorPack = new Dictionary<string, List<string>>();

        foreach (string sourceFolder in sourcesValidas)
        {
            var categoriasDeEstePack = new List<string>();
            categoriasPorPack[sourceFolder] = categoriasDeEstePack;

            string[] categoryFolders = AssetDatabase.GetSubFolders(sourceFolder);

            foreach (string categoryFolder in categoryFolders)
            {
                string categoryName = Path.GetFileName(categoryFolder);
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { categoryFolder });

                if (!byKey.TryGetValue(categoryName, out var folderList))
                {
                    folderList = new List<(string, GameObject)>();
                    byKey[categoryName] = folderList;
                }

                if (guids.Length > 0)
                    categoriasDeEstePack.Add(categoryName);

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;

                    // Por si el mismo prefab ya se agregó (no debería pasar entre dos packs
                    // distintos, pero evita duplicarlo en el catálogo si pasara).
                    if (folderList.Any(x => x.go == prefab)) continue;

                    string fileName = Path.GetFileNameWithoutExtension(path);
                    folderList.Add((fileName, prefab));
                    totalPrefabs++;

                    // Sub-categoría por prefijo (texto antes del primer dígito)
                    string prefix = GetPrefix(fileName);
                    string subKey = $"{categoryName}/{prefix}";
                    if (!byKey.TryGetValue(subKey, out var subList))
                    {
                        subList = new List<(string, GameObject)>();
                        byKey[subKey] = subList;
                    }
                    if (!subList.Any(x => x.go == prefab))
                        subList.Add((fileName, prefab));
                }
            }
        }

        foreach (var kvp in byKey)
        {
            var sorted = kvp.Value.OrderBy(x => x.name).Select(x => x.go).ToList();
            database.entries.Add(new PrefabDatabase.CategoryEntry
            {
                key = kvp.Key,
                variants = sorted
            });
        }

        EditorUtility.SetDirty(database);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"=== PrefabDatabase construido: {totalPrefabs} prefabs, {byKey.Count} categorías/subcategorías, " +
                   $"{sourcesValidas.Count} pack(s) escaneado(s) ===");
        Debug.Log($"Guardado en: {OUTPUT_PATH}");

        foreach (var kvp in categoriasPorPack)
            Debug.Log($"  📦 {kvp.Key}: {string.Join(", ", kvp.Value)}");
    }

    /// <summary>
    /// Extrae el prefijo alfabético de un nombre de archivo (todo antes del primer dígito).
    /// "Sofa01" -> "Sofa", "CabinetACorner02" -> "CabinetACorner", "CabinetA_Sink" -> "CabinetA_Sink" (sin dígitos)
    /// </summary>
    private static string GetPrefix(string fileName)
    {
        Match match = Regex.Match(fileName, @"^(.*?)(\d+)");
        if (match.Success)
            return match.Groups[1].Value;
        return fileName; // No tiene dígitos, es su propia categoría única
    }
}
#endif
