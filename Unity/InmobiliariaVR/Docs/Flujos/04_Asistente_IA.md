# Flujo: Asistente de IA local (texto y voz)

Desde que el usuario pide un cambio en lenguaje natural hasta que se aplica en la escena, usando un modelo corriendo local en [Ollama](https://ollama.com/) (sin conexión a internet).

## Entrada por texto

1. El jugador escribe en el `InputField` del panel de IA y confirma → [`OllamaAIController.EnviarComandoDesdeInput()`](../../Assets/Scripts/OllamaAIController.cs) toma el texto del campo, llama a `EnviarComando(texto)` y limpia el campo.

## Entrada por voz (alternativa, solo Windows)

1. El jugador presiona el botón de micrófono → [`VoiceCommandController.ToggleEscucha()`](../../Assets/Scripts/VoiceCommandController.cs). En Windows (Editor o build standalone) esto arranca un `DictationRecognizer` de `UnityEngine.Windows.Speech`; en cualquier otra plataforma (incluido un APK de Quest) muestra "no disponible" porque esa API no existe ahí.
2. Mientras el usuario habla, `OnDictationHypothesis` va mostrando el texto parcial reconocido (feedback en vivo, sin confirmar todavía).
3. Al terminar la frase, `OnDictationResult(texto, confianza)` se dispara con el texto final: detiene la escucha y, si hay texto, llama directamente a [`OllamaAIController.EnviarComando(texto)`](../../Assets/Scripts/OllamaAIController.cs) — el mismo punto de entrada que usa el campo de texto.
4. Si el reconocedor da error o se corta por silencio prolongado (`OnDictationError` / `OnDictationComplete`), se detiene la escucha y se muestra el motivo en el texto de estado, sin llegar a llamar a Ollama.

## De ahí en adelante, es el mismo flujo (texto o voz)

5. [`EnviarComando(textoUsuario)`](../../Assets/Scripts/OllamaAIController.cs) arranca la coroutine `ConsultarOllama`, que muestra "Pensando..." y arma un prompt fijo que le explica al modelo el dominio (colores válidos de pared: blanco/beige/gris; de sofá: cuero/azul/verde) y le exige responder **solo** con un JSON del tipo `{"target":"pared"|"sofa","color":"<color>"}` (o `"ninguno"` si el pedido no aplica).
6. Se hace un `POST` a `http://localhost:11434/api/generate` (servidor Ollama local, modelo `gemma2:2b` por defecto) con ese prompt, vía `UnityWebRequest`.
7. Si la request falla (Ollama no está corriendo, por ejemplo), se muestra "Error de conexion con Ollama" y el flujo termina ahí.
8. Si responde, `LimpiarRespuesta()` le sacan los bloques ```` ```json ```` que el modelo suele agregar de más y recorta todo lo que quede antes del primer `{` y después del último `}`, para quedarse solo con el JSON.
9. Ese JSON se deserializa a `ComandoIA { target, color }`. Si no es un JSON válido, se avisa "No entendi la respuesta de la IA." y se corta ahí.
10. [`AplicarComando(comando)`](../../Assets/Scripts/OllamaAIController.cs) traduce el resultado a una llamada real sobre la escena:
    - Si `target == "pared"`: busca el índice del color dentro de `coloresPared` y llama a `SetMaterialByIndex(index)` en **cada** `MaterialChangerVR` del array `paredes` (así un solo pedido de voz puede pintar varias paredes a la vez).
    - Si `target == "sofa"`: mismo mecanismo pero contra el único `MaterialChangerVR` del sofá.
    - Si el color no está en la lista esperada, o `target` es `"ninguno"` u otro valor, se muestra un mensaje de error/aviso y no se toca la escena.
11. El resultado final (aplicado o no) se refleja en el texto de estado del panel de IA (`textoEstado`), lo mismo que usa `VoiceCommandController` para mostrar sus propios mensajes de estado.

## Puntos a tener en cuenta

- El "vocabulario" de colores válidos (`coloresPared`, `coloresSofa`) está hardcodeado en `OllamaAIController` y tiene que coincidir en orden con el array `materials` de cada `MaterialChangerVR` correspondiente — son dos listas paralelas que hay que mantener sincronizadas a mano.
- El reconocimiento de voz (paso "Entrada por voz") es exclusivo de Windows; en un build para Quest standalone habría que reemplazarlo por un motor de voz on-device para Android (el propio script lo aclara en su comentario de cabecera).
