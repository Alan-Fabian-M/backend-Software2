using UnityEngine;
using System.Collections;
using System.IO;
using UnityEngine.Networking;

/// <summary>
/// Cliente que transfiere STLs desde Quest a PC por WiFi.
/// Envía STL al servidor (stl_receiver_service.py) que lo guarda y abre en Bambu Studio.
///
/// Diagnóstico integrado (2026-09-19): antes de subir el STL hace un "ping" GET al servidor para
/// distinguir claramente los tipos de falla, y muestra en pantalla el motivo exacto
/// (no conecta / permiso / servidor rechazó), no un error genérico.
/// </summary>
public class STLTransferClient : MonoBehaviour
{
    // URL del servidor - IP real de la PC de Alan.
    private string pcServerURL = "http://192.168.13.55:5000/upload-stl";

    public void ExportAndSendSTL(GameObject objeto)
    {
        if (objeto == null)
        {
            ToastNotificationUI.Show("❌ Selecciona un objeto primero", 2f);
            return;
        }
        StartCoroutine(SendSTLToPC(objeto));
    }

    /// <summary>
    /// Exporta y envía la maqueta completa de la sala (piso + muros + muebles) a la PC vía WiFi.
    /// </summary>
    public void ExportAndSendRoomSTL(System.Collections.Generic.List<GameObject> roomObjects, string roomName = "Maqueta_Sala", Bounds? boundingBoxLimit = null)
    {
        if (roomObjects == null || roomObjects.Count == 0)
        {
            ToastNotificationUI.Show("❌ No hay objetos en la sala para exportar", 2f);
            return;
        }
        StartCoroutine(SendRoomSTLToPC(roomObjects, roomName, boundingBoxLimit));
    }

    /// <summary>Devuelve la URL base (health) a partir de la de upload.</summary>
    private string BaseURL()
    {
        return pcServerURL.Replace("/upload-stl", "/");
    }

    private IEnumerator SendRoomSTLToPC(System.Collections.Generic.List<GameObject> roomObjects, string roomName, Bounds? boundingBoxLimit = null)
    {
        ToastNotificationUI.Show($"🏛️ Exportando maqueta ({roomObjects.Count} objetos)...", 2.5f);

        // 1. Exportar maqueta a memoria (auto-fit a cama de 190 mm)
        var memoryStream = new System.IO.MemoryStream();
        bool exportOk = STLExporter.ExportRoomToSTL(roomObjects, memoryStream, binary: true, maxDimensionMm: 190f, solidName: roomName, boundingBoxLimit: boundingBoxLimit);
        if (!exportOk)
        {
            memoryStream.Dispose();
            ToastNotificationUI.Show("❌ No se pudo exportar la maqueta STL", 4f);
            yield break;
        }

        byte[] stlData = memoryStream.ToArray();
        memoryStream.Dispose();
        if (stlData.Length == 0)
        {
            ToastNotificationUI.Show("❌ STL de maqueta vacío (0 triángulos)", 3f);
            yield break;
        }

        string cleanName = roomName.Replace(" ", "_");
        float kb = stlData.Length / 1024f;
        Debug.Log($"[STL] Maqueta generada: {kb:F1}KB de '{cleanName}'. Server: {pcServerURL}");

        // 2. PRE-FLIGHT: ping GET al servidor para aislar problemas de conexión
        ToastNotificationUI.Show($"🔍 Probando conexión con la PC...\n{BaseURL()}", 2f);
        yield return null;

        using (UnityWebRequest ping = UnityWebRequest.Get(BaseURL()))
        {
            ping.timeout = 8;
            yield return ping.SendWebRequest();

            if (ping.result != UnityWebRequest.Result.Success)
            {
                string diag = Diagnostico(ping);
                Debug.LogError($"[STL] PRE-FLIGHT FALLÓ: {diag}");
                ToastNotificationUI.Show($"❌ No llego a la PC.\n{diag}\n{BaseURL()}", 7f);
                yield break;
            }
            Debug.Log($"[STL] Pre-flight OK: {ping.downloadHandler.text}");
        }

        // 3. POST con el STL de la maqueta
        ToastNotificationUI.Show($"🌐 Enviando maqueta ({kb:F1}KB) a la PC...", 3f);
        yield return null;

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", stlData, $"{cleanName}.stl", "application/octet-stream");
        form.AddField("object_name", cleanName);

        using (UnityWebRequest req = UnityWebRequest.Post(pcServerURL, form))
        {
            req.timeout = 90;
            Debug.Log($"[STL] POST maqueta -> {pcServerURL}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[STL] OK: {req.downloadHandler.text}");
                ToastNotificationUI.Show("✅ Maqueta enviada a la PC.\nAbriendo Bambu Studio...", 4.5f);
            }
            else
            {
                string diag = Diagnostico(req);
                string cuerpo = req.downloadHandler != null ? req.downloadHandler.text : "";
                Debug.LogError($"[STL] POST maqueta FALLÓ: {diag} | body: {cuerpo}");
                ToastNotificationUI.Show($"❌ Falló el envío de la maqueta.\n{diag}", 7f);
            }
        }
    }

    private IEnumerator SendSTLToPC(GameObject objeto)
    {
        ToastNotificationUI.Show("📤 Exportando a STL...", 2f);

        // 1. Exportar STL a memoria
        var memoryStream = new System.IO.MemoryStream();
        bool exportOk = STLExporter.ExportToSTL(objeto, memoryStream, binary: true);
        if (!exportOk)
        {
            memoryStream.Dispose();
            ToastNotificationUI.Show("❌ No se pudo exportar el STL\n(¿malla sin Read/Write?)", 4f);
            yield break;
        }

        byte[] stlData = memoryStream.ToArray();
        memoryStream.Dispose();
        if (stlData.Length == 0)
        {
            ToastNotificationUI.Show("❌ STL vacío (0 triángulos)", 3f);
            yield break;
        }

        string objectName = objeto.name.Replace(" ", "_");
        float kb = stlData.Length / 1024f;
        Debug.Log($"[STL] Generado {kb:F1}KB de '{objectName}'. Server: {pcServerURL}");

        // 2. PRE-FLIGHT: ping GET al servidor para aislar problemas de conexión
        ToastNotificationUI.Show($"🔍 Probando conexión con la PC...\n{BaseURL()}", 2f);
        yield return null;

        using (UnityWebRequest ping = UnityWebRequest.Get(BaseURL()))
        {
            ping.timeout = 8;
            yield return ping.SendWebRequest();

            if (ping.result != UnityWebRequest.Result.Success)
            {
                string diag = Diagnostico(ping);
                Debug.LogError($"[STL] PRE-FLIGHT FALLÓ: {diag}");
                ToastNotificationUI.Show($"❌ No llego a la PC.\n{diag}\n{BaseURL()}", 7f);
                yield break;
            }
            Debug.Log($"[STL] Pre-flight OK: {ping.downloadHandler.text}");
        }

        // 3. POST con el STL
        ToastNotificationUI.Show($"🌐 Enviando {kb:F1}KB a la PC...", 3f);
        yield return null;

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", stlData, $"{objectName}.stl", "application/octet-stream");
        form.AddField("object_name", objectName);

        using (UnityWebRequest req = UnityWebRequest.Post(pcServerURL, form))
        {
            req.timeout = 60;
            Debug.Log($"[STL] POST -> {pcServerURL}");
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                Debug.Log($"[STL] OK: {req.downloadHandler.text}");
                ToastNotificationUI.Show("✅ Enviado a la PC.\nAbriendo Bambu Studio...", 4f);
            }
            else
            {
                string diag = Diagnostico(req);
                string cuerpo = req.downloadHandler != null ? req.downloadHandler.text : "";
                Debug.LogError($"[STL] POST FALLÓ: {diag} | body: {cuerpo}");
                ToastNotificationUI.Show($"❌ Falló el envío.\n{diag}", 7f);
            }
        }
    }

    /// <summary>Traduce el resultado de UnityWebRequest a un mensaje claro con el motivo.</summary>
    private string Diagnostico(UnityWebRequest r)
    {
        string tipo;
        switch (r.result)
        {
            case UnityWebRequest.Result.ConnectionError:
                tipo = "No conecta (red / cleartext / sin permiso INTERNET)";
                break;
            case UnityWebRequest.Result.ProtocolError:
                tipo = "El servidor respondió con error";
                break;
            case UnityWebRequest.Result.DataProcessingError:
                tipo = "Error procesando la respuesta";
                break;
            default:
                tipo = r.result.ToString();
                break;
        }
        return $"{tipo}\ncode={r.responseCode} · {r.error}";
    }

    public void SetServerURL(string url)
    {
        pcServerURL = url;
        Debug.Log($"[STL] URL actualizada: {pcServerURL}");
    }

    public void TestConnection()
    {
        StartCoroutine(Ping());
    }

    private IEnumerator Ping()
    {
        ToastNotificationUI.Show($"🔍 Probando {BaseURL()}...", 2f);
        using (UnityWebRequest r = UnityWebRequest.Get(BaseURL()))
        {
            r.timeout = 8;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
                ToastNotificationUI.Show("✅ Servidor alcanzable", 3f);
            else
                ToastNotificationUI.Show($"❌ {Diagnostico(r)}", 6f);
        }
    }
}
