using UnityEngine;
using UnityEngine.UI;
using System.IO;

/// <summary>
/// Panel UI para exportación 3D de objetos.
/// Proporciona opciones para exportar a STL, 3MF y GCODE.
/// Fase 5: Exportación 3D para impresoras (2026-09-19)
/// </summary>
public class ExportPanelUI : MonoBehaviour
{
    private GameObject objetoAExportar;
    private string rutaExportacion = "";

    /// <summary>
    /// Abre panel de exportación para un objeto específico.
    /// </summary>
    public void AbrirPanelExportacion(GameObject objeto)
    {
        objetoAExportar = objeto;

        // Determinar ruta de exportación (Documentos del usuario). Nota: el enum
        // Environment.SpecialFolder de .NET NO tiene un miembro "Documents" -- el nombre real es
        // "MyDocuments" (esto rompía la compilación, CS0117).
        rutaExportacion = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        rutaExportacion = Path.Combine(rutaExportacion, "InmobiliariaVR_Exports");

        // Crear carpeta si no existe
        if (!Directory.Exists(rutaExportacion))
            Directory.CreateDirectory(rutaExportacion);

        Debug.Log($"📁 Carpeta de exportación: {rutaExportacion}");

        // Mostrar opciones
        MostrarOpcionesExportacion();
    }

    /// <summary>
    /// Muestra las opciones de exportación disponibles.
    /// </summary>
    private void MostrarOpcionesExportacion()
    {
        if (objetoAExportar == null)
        {
            Debug.LogError("No hay objeto para exportar");
            return;
        }

        string nombreObjeto = objetoAExportar.name.Replace(" ", "_");

        Debug.Log("=== OPCIONES DE EXPORTACIÓN ===");
        Debug.Log($"Objeto: {nombreObjeto}");
        Debug.Log($"Destino: {rutaExportacion}");
        Debug.Log("");
        Debug.Log("Selecciona formato:");
        Debug.Log("  1️⃣  STL ASCII  - Para impresoras 3D (recomendado)");
        Debug.Log("  2️⃣  STL Binary - Comprimido (más pequeño)");
        Debug.Log("  3️⃣  3MF        - Moderno con propiedades (recomendado)");
        Debug.Log("  4️⃣  GCODE      - Para máquinas CNC (requiere slicer profesional)");
    }

    /// <summary>
    /// Exporta a STL ASCII.
    /// </summary>
    public void ExportarSTL_ASCII()
    {
        if (objetoAExportar == null) return;

        string nombreArchivo = $"{objetoAExportar.name.Replace(" ", "_")}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.stl";
        string rutaCompleta = Path.Combine(rutaExportacion, nombreArchivo);

        STLExporter.ExportToSTL(objetoAExportar, rutaCompleta, binary: false);
        ToastNotificationUI.Show($"✅ Exportado a STL: {nombreArchivo}", 3f);
    }

    /// <summary>
    /// Exporta a STL binario.
    /// </summary>
    public void ExportarSTL_Binary()
    {
        if (objetoAExportar == null) return;

        string nombreArchivo = $"{objetoAExportar.name.Replace(" ", "_")}_BINARY_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.stl";
        string rutaCompleta = Path.Combine(rutaExportacion, nombreArchivo);

        STLExporter.ExportToSTL(objetoAExportar, rutaCompleta, binary: true);
        ToastNotificationUI.Show($"✅ Exportado a STL (Binary): {nombreArchivo}", 3f);
    }

    /// <summary>
    /// Exporta la maqueta completa de la sala (piso + muros + muebles dentro de los límites del piso).
    /// </summary>
    public void ExportarMaquetaCompleta()
    {
        if (objetoAExportar == null) return;

        var sceneGen = Object.FindAnyObjectByType<SceneGenerator>();
        var roomObjects = sceneGen != null ? sceneGen.GetObjectsInsideFloor(objetoAExportar) : new System.Collections.Generic.List<GameObject> { objetoAExportar };
        Bounds limitePiso = SceneGenerator.CalcularBoundsPisoCompleto(objetoAExportar);

        string nombreArchivo = $"Maqueta_Sala_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.stl";
        string rutaCompleta = Path.Combine(rutaExportacion, nombreArchivo);

        bool ok = STLExporter.ExportRoomToSTL(roomObjects, rutaCompleta, binary: true, maxDimensionMm: 190f, solidName: "Maqueta_Sala", boundingBoxLimit: limitePiso);
        if (ok)
            ToastNotificationUI.Show($"✅ Maqueta exportada ({roomObjects.Count} objetos): {nombreArchivo}", 3.5f);
        else
            ToastNotificationUI.Show("❌ Error exportando maqueta", 3f);
    }

    /// <summary>
    /// Exportar a 3MF: NO implementado a propósito. La decisión documentada del proyecto (ver
    /// análisis de exportación 3D) fue exportar SOLO STL -- Bambu Studio no necesita 3MF de
    /// entrada y generarlo acá sería trabajo redundante sin ningún beneficio real. Los tipos
    /// "Model3MFExporter"/"GCodeExporter" que este botón llamaba antes nunca llegaron a existir
    /// en el proyecto, lo que rompía la compilación entera (CS0103/CS0246). Se deja el botón acá
    /// (por si la UI ya lo tiene cableado) mostrando un aviso en vez de fallar en el build.
    /// </summary>
    public void Exportar3MF()
    {
        ToastNotificationUI.Show("ℹ️ 3MF no está implementado: usa STL y ábrelo en Bambu Studio.", 3f);
    }

    /// <summary>
    /// Exportar a GCODE directo desde la app: NO implementado a propósito. Una impresora Bambu
    /// Lab necesita G-code generado por un slicer real (Bambu Studio), que decide soportes,
    /// velocidades y temperaturas según el modelo de impresora -- reimplementar un slicer acá
    /// está fuera de alcance (ver BambuLabMQTT.cs y el documento
    /// "estado-final-bambu-lab-directo" sobre la limitación de slicing y el flujo aceptado con
    /// CrudPanelController.EnviarABambuLab, que sí envía el .gcode.3mf ya sliceado por WiFi).
    /// </summary>
    public void ExportarGCode()
    {
        ToastNotificationUI.Show("ℹ️ El G-code se genera sliceando el STL en Bambu Studio, no desde la app.", 4f);
    }

    /// <summary>
    /// Abre la carpeta de exportación en el explorador.
    /// </summary>
    public void AbrirCarpetaExportacion()
    {
        if (!Directory.Exists(rutaExportacion))
        {
            Debug.LogError("La carpeta no existe");
            return;
        }

        #if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
            System.Diagnostics.Process.Start("explorer.exe", rutaExportacion);
        #elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
            System.Diagnostics.Process.Start("open", rutaExportacion);
        #elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            System.Diagnostics.Process.Start("xdg-open", rutaExportacion);
        #endif

        Debug.Log($"📂 Abriendo: {rutaExportacion}");
    }

    /// <summary>
    /// Retorna la ruta de exportación actual.
    /// </summary>
    public string GetRutaExportacion()
    {
        return rutaExportacion;
    }
}
