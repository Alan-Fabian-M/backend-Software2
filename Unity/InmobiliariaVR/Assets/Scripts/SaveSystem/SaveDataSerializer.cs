using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

// Guarda y carga el diseno personalizado del cliente como JSON local.
// Portado de SaveDataSerializer de Room Designer (TeamFWS); mismo enfoque
// generico de Application.persistentDataPath, sin dependencias de Meta XR.
public static class SaveDataSerializer
{
    private static readonly string SaveDirectory = Path.Combine(Application.persistentDataPath, "DesignLayouts");

    static SaveDataSerializer()
    {
        Directory.CreateDirectory(SaveDirectory);
    }

    public static async Task SaveLayout(DesignLayoutData layout, string filename)
    {
        try
        {
            string json = JsonUtility.ToJson(layout, true);
            string path = Path.Combine(SaveDirectory, filename + ".json");
            await File.WriteAllTextAsync(path, json);
            Debug.Log("Diseno guardado en " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Error al guardar el diseno: " + e.Message);
            throw;
        }
    }

    public static async Task<DesignLayoutData> LoadLayout(string filename)
    {
        try
        {
            string path = Path.Combine(SaveDirectory, filename + ".json");
            if (!File.Exists(path))
            {
                Debug.LogWarning("No se encontro un archivo de diseno en " + path);
                return null;
            }

            string json = await File.ReadAllTextAsync(path);
            return JsonUtility.FromJson<DesignLayoutData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("Error al cargar el diseno: " + e.Message);
            throw;
        }
    }

    public static string[] GetSavedLayouts()
    {
        return Directory.GetFiles(SaveDirectory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .ToArray();
    }
}
