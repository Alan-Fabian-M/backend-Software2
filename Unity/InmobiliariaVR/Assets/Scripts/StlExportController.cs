using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// Conecta el boton "Exportar a STL" del menu con StlExporter. Exporta toda la
// sala (paredes, piso, puerta, muebles en su posicion/color actuales) a un
// archivo .stl listo para mandar a un slicer (PrusaSlicer/OrcaSlicer) e imprimir
// una maqueta, como plantea la propuesta.
public class StlExportController : MonoBehaviour
{
    [Tooltip("Objeto raiz a exportar (por defecto, toda la Sala)")]
    public GameObject raizAExportar;

    public Text textoEstado;

    // Application.persistentDataPath no se puede leer en un inicializador de campo
    // (Unity lo tira como error) -- se resuelve recien cuando se llama esta funcion.
    private static string ObtenerCarpetaExportacionInterna()
    {
        return Path.Combine(Application.persistentDataPath, "Exports");
    }

    // Llamar desde el boton "Exportar a STL".
    public void ExportarInmueble()
    {
        try
        {
            if (raizAExportar == null)
            {
                SetEstado("No hay nada asignado para exportar.");
                return;
            }

            string nombreArchivo = "InmobiliariaVR_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".stl";
            string ruta = Path.Combine(ObtenerCarpetaExportacionInterna(), nombreArchivo);

            int triangulos = StlExporter.ExportarAArchivo(new[] { raizAExportar }, ruta);

            SetEstado("Exportado: " + nombreArchivo + " (" + triangulos + " triangulos)");
            Debug.Log("[StlExportController] STL guardado en " + ruta);
        }
        catch (Exception e)
        {
            SetEstado("Error al exportar: " + e.Message);
            Debug.LogError("[StlExportController] " + e);
        }
    }

    public static string ObtenerCarpetaExportacion()
    {
        return ObtenerCarpetaExportacionInterna();
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
    }
}
