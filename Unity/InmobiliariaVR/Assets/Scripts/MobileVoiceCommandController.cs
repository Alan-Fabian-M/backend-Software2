using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

// Version para celular (Android) del boton de microfono del asistente de IA.
// Usa el sensor de microfono del dispositivo (permiso en runtime + captura de
// audio real vía Microphone.Start), que es el requisito obligatorio del examen
// para la app movil complementaria.
//
// LIMITACION IMPORTANTE (documentada a proposito, mismo criterio de honestidad
// que VoiceCommandController.cs para Windows): Unity no trae un reconocedor de
// voz (speech-to-text) multiplataforma nativo para Android. Para transcribir el
// audio capturado a texto hacen falta, ademas de este script:
//   a) un plugin nativo de Android (ej. RecognizerIntent de Android vía una
//      Activity propia en Assets/Plugins/Android/ que reciba el resultado con
//      UnitySendMessage), o
//   b) un motor de STT on-device de terceros (ej. Vosk).
// Ninguno de los dos esta integrado todavia — queda documentado como siguiente
// paso en Docs/Progreso_MVP_Unity.md. Mientras tanto, el pedido en lenguaje
// natural a la IA se escribe en el InputField de OllamaAIController (misma UI
// que ya funciona en la version de escritorio), y este script deja probado que
// el permiso y la captura de audio del microfono del celular funcionan.
public class MobileVoiceCommandController : MonoBehaviour
{
    [Header("UI")]
    public Text textoEstado;
    public Text textoBotonMicrofono;

    private AudioClip clipGrabado;
    private bool escuchando;
    private const int SegundosMaximoGrabacion = 8;
    private const int FrecuenciaMuestreo = 16000;

    // Llamar desde el boton de microfono del panel de IA (celular).
    public void ToggleEscucha()
    {
        if (escuchando) DetenerEscucha();
        else EmpezarEscucha();
    }

    private void EmpezarEscucha()
    {
        if (!PedirPermisoMicrofono())
        {
            SetEstado("Sin permiso de microfono. Habilitalo en Ajustes > Apps.");
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            SetEstado("No se encontro ningun microfono en el dispositivo.");
            return;
        }

        clipGrabado = Microphone.Start(null, false, SegundosMaximoGrabacion, FrecuenciaMuestreo);
        escuchando = true;
        ActualizarTextoBoton();
        SetEstado("Escuchando... (grabacion de audio real; falta conectar el motor de transcripcion — ver comentario del script)");
    }

    private void DetenerEscucha()
    {
        if (!escuchando) return;

        Microphone.End(null);
        escuchando = false;
        ActualizarTextoBoton();
        SetEstado("Grabacion detenida. Escribi el pedido en el campo de texto mientras se integra la transcripcion automatica.");
    }

    private bool PedirPermisoMicrofono()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
        {
            Permission.RequestUserPermission(Permission.Microphone);
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
        }
        return true;
#else
        return Application.HasUserAuthorization(UserAuthorization.Microphone) ||
               Application.RequestUserAuthorization(UserAuthorization.Microphone) != null;
#endif
    }

    private void ActualizarTextoBoton()
    {
        if (textoBotonMicrofono != null) textoBotonMicrofono.text = escuchando ? "Detener" : "Hablar";
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[MobileVoiceCommandController] " + mensaje);
    }
}
