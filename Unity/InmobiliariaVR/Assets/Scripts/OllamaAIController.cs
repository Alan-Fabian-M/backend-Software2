using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Conecta Unity con un servidor Ollama local (sin internet) para interpretar
// comandos de personalizacion en lenguaje natural ("pone la pared gris")
// y traducirlos a llamadas sobre MaterialChangerVR.
public class OllamaAIController : MonoBehaviour
{
    [Header("Conexion a Ollama (servidor local, sin internet)")]
    public string ollamaUrl = "http://localhost:11434/api/generate";
    public string modelo = "gemma2:2b";

    [Header("Objetos controlables")]
    public MaterialChangerVR[] paredes;
    public MaterialChangerVR sofa;

    [Header("UI (opcional)")]
    public InputField campoComando;
    public Text textoEstado;

    private readonly string[] coloresPared = { "blanco", "beige", "gris" };
    private readonly string[] coloresSofa = { "cuero", "azul", "verde" };

    public void EnviarComandoDesdeInput()
    {
        if (campoComando == null || string.IsNullOrWhiteSpace(campoComando.text)) return;
        EnviarComando(campoComando.text);
        campoComando.text = "";
    }

    public void EnviarComando(string textoUsuario)
    {
        StartCoroutine(ConsultarOllama(textoUsuario));
    }

    private IEnumerator ConsultarOllama(string textoUsuario)
    {
        SetEstado("Pensando...");

        string prompt =
            "Sos un asistente de un sistema inmobiliario en VR. " +
            "El usuario puede pedir cambiar el color de una pared o del sofa. " +
            "Colores validos para pared: blanco, beige, gris. " +
            "Colores validos para sofa: cuero, azul, verde. " +
            "Responde SOLO con un JSON compacto, sin texto adicional ni bloques de codigo, con este formato exacto: " +
            "{\"target\":\"pared\"|\"sofa\",\"color\":\"<uno de los colores validos>\"}. " +
            "Si el pedido no es claro o no aplica, responde {\"target\":\"ninguno\",\"color\":\"ninguno\"}. " +
            "Pedido del usuario: \"" + textoUsuario + "\"";

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

            ComandoIA comando;
            try
            {
                comando = JsonUtility.FromJson<ComandoIA>(limpio);
            }
            catch (Exception)
            {
                SetEstado("No entendi la respuesta de la IA.");
                yield break;
            }

            AplicarComando(comando);
        }
    }

    // El modelo a veces envuelve el JSON en ```json ... ``` o agrega texto alrededor.
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

    private void AplicarComando(ComandoIA comando)
    {
        if (comando == null || comando.target == "ninguno")
        {
            SetEstado("No pude interpretar ese pedido.");
            return;
        }

        if (comando.target == "pared")
        {
            int index = Array.IndexOf(coloresPared, comando.color);
            if (index < 0) { SetEstado("Color de pared no reconocido: " + comando.color); return; }
            foreach (var p in paredes)
                if (p != null) p.SetMaterialByIndex(index);
            SetEstado("Listo: pared -> " + comando.color);
        }
        else if (comando.target == "sofa")
        {
            int index = Array.IndexOf(coloresSofa, comando.color);
            if (index < 0) { SetEstado("Color de sofa no reconocido: " + comando.color); return; }
            if (sofa != null) sofa.SetMaterialByIndex(index);
            SetEstado("Listo: sofa -> " + comando.color);
        }
        else
        {
            SetEstado("Pedido no aplicable: " + comando.target);
        }
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[OllamaAIController] " + mensaje);
    }
}

[Serializable]
public class OllamaGenerateRequest
{
    public string model;
    public string prompt;
    public bool stream;
}

[Serializable]
public class OllamaGenerateResponse
{
    public string model;
    public string response;
    public bool done;
}

[Serializable]
public class ComandoIA
{
    public string target;
    public string color;
}
