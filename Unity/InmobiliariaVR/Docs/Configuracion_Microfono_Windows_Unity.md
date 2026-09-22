# Guía de Configuración: Permisos de Micrófono y Voz en Windows para Unity

Esta guía detalla los pasos exactos para configurar los permisos de audio en Windows para que Unity y el script [`VoiceCommandController.cs`](../Assets/Scripts/VoiceCommandController.cs) puedan capturar tu voz sin errores usando la API nativa `UnityEngine.Windows.Speech.DictationRecognizer`.

---

## 1. Habilitar Acceso al Micrófono para Aplicaciones de Escritorio (Crítico)

Windows diferencia entre aplicaciones de la Microsoft Store y aplicaciones clásicas de escritorio (`.exe` como Unity Editor o tu juego compilado).

1. Abre **Configuración** presionando las teclas `Windows + I`.
2. Dirígete a **Privacidad y seguridad** (en Windows 10: **Privacidad**) $\rightarrow$ **Micrófono**.
3. Verifica los siguientes interruptores:
   - **Acceso al micrófono:** Debe estar en **Activado**.
   - **Permitir que las aplicaciones accedan al micrófono:** Debe estar en **Activado**.
4. ⚠️ **Paso fundamental:** Baja en esa misma pantalla hasta encontrar:
   - **"Permitir que las aplicaciones de escritorio accedan al micrófono"** (*Let desktop apps access your microphone*).
   - **Debe estar en ACTIVADO.**
   - *(En la lista debajo de este interruptor verás si `Unity.exe` intentó acceder recientemente).*

---

## 2. Activar el Servicio de Reconocimiento de Voz de Windows

`DictationRecognizer` no procesa el audio por su cuenta; se comunica con los servicios de reconocimiento de voz del sistema operativo Windows.

1. En la ventana de **Configuración**, ve a **Privacidad y seguridad** $\rightarrow$ **Voz** (*Speech*).
2. Asegúrate de activar:
   - **Reconocimiento de voz en línea** (*Online speech recognition*).
   - Si esta opción está apagada por directivas de privacidad, Windows bloqueará la inicialización de `DictationRecognizer` en Unity con un error `DictationError`.

---

## 3. Verificar Idioma del Motor de Voz

Para que el reconocedor entienda español correctamente:

1. Ve a **Configuración** $\rightarrow$ **Hora e idioma** $\rightarrow$ **Voz**.
2. En la sección **Idioma de voz** (*Speech language*), comprueba cuál es el idioma seleccionado (ej. *Español (España)*, *Español (México)*, etc.).
3. Si vas a dictarle comandos en español a Ollama, el idioma de voz de Windows debe ser español. Si no lo tienes instalado, puedes agregarlo en **Paquetes de voz**.

---

## 4. Comprobar Dispositivo de Entrada de Audio

Tu laptop cuenta con el controlador **Realtek High Definition Audio** en estado óptimo. Para verificar que esté recibiendo señal:

1. Ve a **Configuración** $\rightarrow$ **Sistema** $\rightarrow$ **Sonido**.
2. En la sección **Entrada** (*Input*), selecciona tu micrófono (*Micrófono Realtek High Definition Audio*).
3. Habla normalmente y comprueba que la barra de **Volumen de entrada** se mueva hacia la derecha.

---

## 5. Prueba de Funcionamiento en Unity

1. Abre el proyecto en **Unity Editor**.
2. Carga la escena `Assets/Scenes/Sala_MVP.unity`.
3. Dale al botón **Play** ▶️.
4. En el panel de Asistente IA, activa el botón del micrófono (llama a `ToggleEscucha()` de `VoiceCommandController`).
5. En el panel de estado deberías ver:
   ```text
   Escuchando... decí tu pedido.
   ```
6. Habla al micrófono (ejemplo: *"Pinta las paredes de color beige"*).
7. Verás la transcripción en vivo:
   ```text
   (escuchando) pinta las paredes...
   Escuchado: "Pinta las paredes de color beige"
   ```
8. Automáticamente, ese texto se enviará a [`OllamaAIController.cs`](../Assets/Scripts/OllamaAIController.cs) para cambiar los materiales en tiempo real.

---

## 6. Solución de Problemas Comunes

| Síntoma / Error en Consola | Causa Probable | Solución |
|---|---|---|
| `DictationError: Dictation was cancelled` | Privacidad de voz desactivada en Windows | Activar "Reconocimiento de voz en línea" en Privacidad. |
| No reconoce palabras / queda en silencio | Micrófono de entrada incorrecto o muteado | Revisar en Configuración > Sonido que el micrófono predeterminado no esté en silencio. |
| Reconoce texto en inglés o palabras sin sentido | El idioma de voz de Windows no coincide con tu idioma | Instalar y seleccionar el paquete de voz en Español en Hora e idioma > Voz. |
