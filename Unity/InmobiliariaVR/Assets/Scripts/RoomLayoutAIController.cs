using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Genera la disposicion de los muebles de la sala a partir de una descripcion en
// lenguaje natural (texto o voz), usando el mismo Ollama LOCAL (modelo de texto,
// sin vision) que ya usa OllamaAIController para los comandos de color.
//
// DISENO: se descarto leer un croquis como IMAGEN con un modelo de vision local
// (se probo con "moondream") porque en esta maquina (sin GPU dedicada, solo
// graficos integrados) el procesamiento de una sola imagen tardo mas de 3
// minutos sin terminar -- inviable para un demo en vivo. En cambio, el usuario
// describe la sala con palabras (ej. "sofa contra la pared oeste, mesa en el
// centro, tv en la pared norte") y se reusa el modelo de texto (gemma2:2b) que
// ya responde en pocos segundos. Cumple el mismo flujo pedido (IA -> JSON ->
// Unity arma la sala), solo que la entrada es texto en vez de una imagen.
public class RoomLayoutAIController : MonoBehaviour
{
    [Header("Conexion a Ollama (servidor local, sin internet)")]
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string modelo = "gemma2:2b";

    [Header("Muebles a posicionar (mismos GameObjects que ya mueve FurnitureManipulatorVR)")]
    public Transform sofa;
    public Transform mesa;
    public Transform tv;

    [Header("UI (opcional)")]
    public InputField campoDescripcion;
    public Text textoEstado;

    // Posiciones canonicas dentro de la sala de 4x4m (paredes en X=+-2, Z=+-2),
    // separadas de las paredes lo suficiente para no atravesarlas.
    private static readonly Vector3 PosParedNorte = new Vector3(0f, 0f, 1.5f);
    private static readonly Vector3 PosParedSur = new Vector3(0.4f, 0f, -1.5f); // corrido para no tapar la puerta
    private static readonly Vector3 PosParedEste = new Vector3(1.5f, 0f, 0f);
    private static readonly Vector3 PosParedOeste = new Vector3(-1.5f, 0f, 0f);
    private static readonly Vector3 PosCentro = new Vector3(0f, 0f, 0f);

    public void GenerarDesdeInput()
    {
        if (campoDescripcion == null || string.IsNullOrWhiteSpace(campoDescripcion.text)) return;
        GenerarSalaDesdeDescripcion(campoDescripcion.text);
    }

    public void GenerarSalaDesdeDescripcion(string descripcionUsuario)
    {
        StartCoroutine(ConsultarOllama(descripcionUsuario));
    }

    private IEnumerator ConsultarOllama(string descripcionUsuario)
    {
        SetEstado("Generando la sala...");

        string prompt =
            "Sos un asistente que interpreta la descripcion en lenguaje natural de la disposicion " +
            "de los muebles de un living (sofa, mesa, tv) y la traduce a un layout. " +
            "Posiciones validas: pared_norte, pared_sur, pared_este, pared_oeste, centro. " +
            "Respondé SOLO con un JSON compacto, sin texto adicional ni bloques de codigo, con este " +
            "formato exacto: {\"muebles\":[{\"tipo\":\"sofa\",\"posicion\":\"<una posicion valida>\"}," +
            "{\"tipo\":\"mesa\",\"posicion\":\"<una posicion valida>\"},{\"tipo\":\"tv\",\"posicion\":\"<una posicion valida>\"}]}. " +
            "Si el usuario no menciona algun mueble, elegi una posicion razonable para el (ej. mesa en el centro). " +
            "Descripcion del usuario: \"" + descripcionUsuario + "\"";

        var reqBody = new OllamaGenerateRequest { model = modelo, prompt = prompt, stream = false };
        string json = JsonUtility.ToJson(reqBody);

        using (var www = new UnityWebRequest(ollamaUrl, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                SetEstado("Error de conexion con Ollama: " + www.error);
                yield break;
            }

            OllamaGenerateResponse resp = JsonUtility.FromJson<OllamaGenerateResponse>(www.downloadHandler.text);
            string limpio = LimpiarRespuesta(resp.response);

            LayoutIA layout;
            try
            {
                layout = JsonUtility.FromJson<LayoutIA>(limpio);
            }
            catch (Exception)
            {
                SetEstado("No entendi la respuesta de la IA.");
                yield break;
            }

            AplicarLayout(layout);
        }
    }

    private string LimpiarRespuesta(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return texto;
        texto = texto.Replace("```json", "").Replace("```", "").Trim();

        int inicio = texto.IndexOf('{');
        int fin = texto.LastIndexOf('}');
        if (inicio >= 0 && fin > inicio)
            texto = texto.Substring(inicio, fin - inicio + 1);

        return texto.Trim();
    }

    private void AplicarLayout(LayoutIA layout)
    {
        if (layout == null || layout.muebles == null || layout.muebles.Length == 0)
        {
            SetEstado("No pude interpretar esa disposicion.");
            return;
        }

        int aplicados = 0;
        foreach (var item in layout.muebles)
        {
            Transform objetivo = null;
            float rotacionY = 0f;
            switch (item.tipo)
            {
                case "sofa": objetivo = sofa; break;
                case "mesa": objetivo = mesa; break;
                case "tv": objetivo = tv; break;
            }
            if (objetivo == null) continue;

            Vector3 pos;
            switch (item.posicion)
            {
                case "pared_norte": pos = PosParedNorte; rotacionY = 180f; break;
                case "pared_sur": pos = PosParedSur; rotacionY = 0f; break;
                case "pared_este": pos = PosParedEste; rotacionY = 90f; break;
                case "pared_oeste": pos = PosParedOeste; rotacionY = 270f; break;
                default: pos = PosCentro; rotacionY = objetivo.eulerAngles.y; break;
            }

            objetivo.position = new Vector3(pos.x, objetivo.position.y, pos.z);
            objetivo.rotation = Quaternion.Euler(0f, rotacionY, 0f);
            aplicados++;
        }

        SetEstado(aplicados > 0 ? "Sala generada: " + aplicados + " mueble(s) ubicados." : "No pude ubicar ningun mueble.");
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[RoomLayoutAIController] " + mensaje);
    }
}

[Serializable]
public class LayoutIA
{
    public MuebleLayout[] muebles;
}

[Serializable]
public class MuebleLayout
{
    public string tipo;
    public string posicion;
}
