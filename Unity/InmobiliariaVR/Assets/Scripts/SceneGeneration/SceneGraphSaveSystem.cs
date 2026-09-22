using System;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Guarda y carga escenas generadas (Scene Graph JSON) como archivos locales dentro del
/// propio Quest, en Application.persistentDataPath -- no hace falta ningun servidor ni
/// base de datos externa para esto. Cada escena guardada es un .json con el mismo schema
/// que ya usa todo el pipeline (SceneGenerator.GenerateSceneAsync), asi que cargar una
/// escena guardada usa exactamente el mismo camino que cargar un preset o un croquis.
///
/// Reemplaza (para este flujo) al par DesignLayoutData/SaveDataSerializer, que quedo
/// pensado para el schema viejo (modelId generico + indice de material) y no para el
/// schema nuevo de scene_elements. Ese sistema viejo se deja intacto por si se usa en
/// otro lado, pero el guardado de escenas generadas usa este nuevo sistema.
/// </summary>
public static class SceneGraphSaveSystem
{
    private const string CARPETA = "Escenas";
    private const string EXTENSION = ".json";

    private static string DirectorioGuardado
    {
        get
        {
            string dir = Path.Combine(Application.persistentDataPath, CARPETA);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>
    /// Convierte un nombre elegido por el usuario en un nombre de archivo seguro
    /// (sin caracteres invalidos para el sistema de archivos).
    /// </summary>
    private static string SanearNombre(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            nombre = "Escena";

        foreach (char c in Path.GetInvalidFileNameChars())
            nombre = nombre.Replace(c, '_');

        return nombre.Trim();
    }

    private static string RutaParaNombre(string nombre) =>
        Path.Combine(DirectorioGuardado, SanearNombre(nombre) + EXTENSION);

    /// <summary>
    /// Guarda el JSON de una escena con el nombre dado. Si ya existe una escena con ese
    /// nombre, la sobreescribe.
    /// </summary>
    public static void GuardarEscena(string nombre, string json)
    {
        try
        {
            File.WriteAllText(RutaParaNombre(nombre), json);
            Debug.Log($"[SceneGraphSaveSystem] Escena guardada: {nombre}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneGraphSaveSystem] Error al guardar la escena '{nombre}': {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Genera un nombre por defecto basado en la fecha/hora actual, para guardar sin
    /// tener que pedirle un nombre al usuario (evita depender de un teclado en VR).
    /// </summary>
    public static string GenerarNombrePorDefecto() =>
        "Escena_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

    public static string CargarEscena(string nombre)
    {
        string ruta = RutaParaNombre(nombre);
        if (!File.Exists(ruta))
        {
            Debug.LogWarning($"[SceneGraphSaveSystem] No se encontro la escena '{nombre}' en {ruta}");
            return null;
        }

        try
        {
            return File.ReadAllText(ruta);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SceneGraphSaveSystem] Error al cargar la escena '{nombre}': {e.Message}");
            throw;
        }
    }

    /// <summary>
    /// Lista los nombres de las escenas guardadas (sin la extension .json), mas recientes primero.
    /// </summary>
    public static string[] ListarEscenas()
    {
        if (!Directory.Exists(DirectorioGuardado)) return Array.Empty<string>();

        return Directory.GetFiles(DirectorioGuardado, "*" + EXTENSION)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
    }

    public static bool ExisteEscena(string nombre) => File.Exists(RutaParaNombre(nombre));

    public static void BorrarEscena(string nombre)
    {
        string ruta = RutaParaNombre(nombre);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
            Debug.Log($"[SceneGraphSaveSystem] Escena borrada: {nombre}");
        }
    }
}
