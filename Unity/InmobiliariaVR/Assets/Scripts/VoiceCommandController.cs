using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

// Reconocimiento de voz local (Windows Speech Recognition) para dictarle pedidos
// al asistente IA sin usar teclado. La frase reconocida se manda tal cual a
// OllamaAIController.EnviarComando(), que la interpreta via el modelo local de
// Ollama (igual que si se hubiera escrito a mano en el campo de texto).
//
// DISENO: KeywordRecognizer (gramatica cerrada), no DictationRecognizer (dictado
// libre) a proposito. Al probar DictationRecognizer con microfono real (seccion
// 11ter de Progreso_MVP_Unity.md) Windows tiro "Dictation support is not enabled
// on this device", y ese mismo panel de Windows (Privacidad > Voz) controla si el
// dictado libre usa el servicio en la nube de Microsoft o el motor local segun la
// version de Windows -- lo cual podia no cumplir el requisito eliminatorio del
// examen ("IA local, sin conexion a servidor externo"). KeywordRecognizer reconoce
// unicamente las frases de la gramatica armada en ConstruirGramatica() -- siempre
// 100% on-device, sin depender de internet -- a cambio de no aceptar lenguaje
// completamente libre.
//
// LIMITACION DE PLATAFORMA: UnityEngine.Windows.Speech solo funciona en Windows
// (Editor o build standalone). No funciona en un APK de Quest standalone ni en
// Android -- para esas plataformas haria falta otro STT on-device (ej. Vosk).
public class VoiceCommandController : MonoBehaviour
{
    [Header("Referencias")]
    public OllamaAIController ollamaController;
    public Text textoEstado;

    [Header("UI (opcional)")]
    [Tooltip("Texto del boton de microfono, para mostrar si esta escuchando o no")]
    public Text textoBotonMicrofono;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private KeywordRecognizer keywordRecognizer;
#endif
    private bool escuchando;

    private static readonly string[] ColoresPared = { "blanco", "blanca", "beige", "gris" };
    private static readonly string[] ObjetivosSofa = { "sofa", "sillon" };
    private static readonly string[] ColoresSofa = { "cuero", "azul", "verde" };
    private static readonly string[] Prefijos = { "", "pone ", "quiero " };

    private void OnDestroy()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        DetenerEscucha();
#endif
    }

    // Llamar desde el boton de microfono del panel de IA.
    public void ToggleEscucha()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (escuchando) DetenerEscucha();
        else EmpezarEscucha();
#else
        SetEstado("Reconocimiento de voz no disponible en esta plataforma (solo Windows).");
#endif
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private void EmpezarEscucha()
    {
        if (keywordRecognizer != null) return;

        keywordRecognizer = new KeywordRecognizer(ConstruirGramatica(), ConfidenceLevel.Medium);
        keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
        keywordRecognizer.Start();

        escuchando = true;
        ActualizarTextoBoton();
        SetEstado("Escuchando... decí un pedido (ej. \"pared gris\", \"sofa azul\").");
    }

    private void DetenerEscucha()
    {
        if (keywordRecognizer == null) return;

        keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;
        if (keywordRecognizer.IsRunning) keywordRecognizer.Stop();
        keywordRecognizer.Dispose();
        keywordRecognizer = null;

        escuchando = false;
        ActualizarTextoBoton();
    }

    private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        string texto = args.text;
        SetEstado("Escuchado: \"" + texto + "\"");
        DetenerEscucha();
        if (ollamaController != null && !string.IsNullOrWhiteSpace(texto))
        {
            ollamaController.EnviarComando(texto);
        }
    }

    // Arma la gramatica cerrada (100% local) combinando prefijos naturales +
    // objetivo (pared/sofa/sillon) + color valido para ese objetivo. Mismos
    // colores que reconoce OllamaAIController, asi el pedido siempre es valido.
    private string[] ConstruirGramatica()
    {
        var frases = new List<string>();
        foreach (var prefijo in Prefijos)
        {
            foreach (var color in ColoresPared)
                frases.Add(prefijo + "pared " + color);

            foreach (var objetivo in ObjetivosSofa)
                foreach (var color in ColoresSofa)
                    frases.Add(prefijo + objetivo + " " + color);
        }
        return frases.ToArray();
    }
#endif

    private void ActualizarTextoBoton()
    {
        if (textoBotonMicrofono != null) textoBotonMicrofono.text = escuchando ? "Escuchando..." : "Hablar";
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[VoiceCommandController] " + mensaje);
    }
}
