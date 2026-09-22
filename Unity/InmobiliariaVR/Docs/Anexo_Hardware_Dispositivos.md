# Anexo: Dispositivos utilizados

Responde al punto 3 de la rúbrica del examen (ver [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md)): "¿El dispositivo se encuentra completo con recursos disponibles, y estos dispositivos están impresos como anexo en el documento?"

> ⚠️ **Pendiente antes de imprimir:** este documento tiene dos espacios marcados `[FOTO PENDIENTE]` — hay que sacar una foto real del visor y (cuando esté decidido) del celular, guardarlas en la carpeta [`Anexo_Hardware_Fotos/`](Anexo_Hardware_Fotos/) de esta misma carpeta `Docs/`, y reemplazar el marcador por la imagen (`![Meta Quest 3](Anexo_Hardware_Fotos/quest3.jpg)`). No se generó ninguna imagen automáticamente — el examen pide el dispositivo real usado por el equipo, no una foto de stock genérica.

---

## 1. Visor de Realidad Virtual — Meta Quest 3

**[FOTO PENDIENTE — foto real del visor del equipo, guardar en `Anexo_Hardware_Fotos/quest3.jpg`]**

| Especificación | Detalle |
|---|---|
| Fabricante / modelo | Meta Quest 3 |
| Tipo | Visor VR standalone (autónomo), también usable como PC VR vía **Quest Link** (cable o Air Link) |
| Panel | LCD, 2064 × 2208 px por ojo |
| Tasa de refresco | 90 Hz / 120 Hz (experimental) |
| Procesador | Qualcomm Snapdragon XR2 Gen 2 |
| Sensores relevantes para este proyecto | Cámaras de seguimiento (6DOF de cabeza y manos), sensores de profundidad (passthrough color), controles táctiles con sensores de presión (Grip/Trigger) |
| Recursos usados por la app | Seguimiento de manos/controles para locomoción, teletransporte y selección de objetos (rayo/mano); botones **Grip** (`Select`) y **Trigger** (`Activate`) para toda la interacción documentada en [`Docs/Flujos/`](Flujos/) |
| Modo de conexión usado en las pruebas | *(completar: standalone con el build APK, o PC VR vía Quest Link/Air Link a la máquina con Unity)* |

**Uso en el proyecto:** es el dispositivo objetivo para el recorrido VR completo — locomoción, personalización de muebles, pintura de superficies y menú de exportación (ver [`Docs/README.md`](README.md) para el detalle de arquitectura). Durante el desarrollo, buena parte de las pruebas se hicieron también con el **XR Device Simulator** de Unity (mouse/teclado, sin visor físico) — ver sección 10 de [`Progreso_MVP_Unity.md`](Progreso_MVP_Unity.md) — y quedó pendiente en el checklist de avance "probar con visor real de punta a punta" para todos los flujos.

---

## 2. Celular — pendiente de definir

**[FOTO PENDIENTE — se completa cuando el equipo decida el dispositivo]**

La app móvil complementaria (requisito obligatorio del examen, sensores de cámara/micrófono) todavía no se desarrolló — ver punto 1 de [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md) y el ítem "App móvil" en [`Estado_Avance_Examen_ISW2.md`](Estado_Avance_Examen_ISW2.md). Una vez elegido el celular de prueba, completar esta tabla:

| Especificación | Detalle |
|---|---|
| Fabricante / modelo | *(pendiente)* |
| Sistema operativo | *(pendiente — Android/iOS, versión)* |
| Sensores relevantes | *(pendiente — cámara para AR, micrófono para comandos de voz a la IA local)* |
| Recursos usados por la app | *(pendiente — detección de superficies AR, captura de audio)* |

---

## 3. Servidor de IA local (equipo de escritorio/notebook)

Aunque no es un "dispositivo" en el sentido de sensores, el examen exige que la IA corra local sin conexión a internet — vale la pena dejar constancia del equipo que corre Ollama.

**[FOTO PENDIENTE — opcional, foto de la notebook/PC usada como servidor, si el docente lo pide]**

| Especificación | Detalle |
|---|---|
| Rol | Corre `ollama serve` con el modelo `gemma2:2b` en `localhost:11434`, consumido por [`OllamaAIController.cs`](../Assets/Scripts/OllamaAIController.cs) |
| Requisito que cumple | IA local sin conexión a servidor externo (obligatorio según las bases del examen) |
| Equipo usado | *(completar: marca/modelo, RAM, si tiene GPU dedicada — Ollama corre más rápido con GPU pero no es obligatorio para `gemma2:2b`)* |

---

## Cómo completar este anexo antes de imprimir

1. Sacar una foto real del Meta Quest 3 del equipo (con o sin los controles al lado) y guardarla como `Docs/Anexo_Hardware_Fotos/quest3.jpg`.
2. Completar el modo de conexión usado (standalone vs. Quest Link) en la tabla de la sección 1.
3. Cuando se decida el celular para la app móvil, completar la sección 2 y sacarle una foto.
4. Reemplazar los marcadores `[FOTO PENDIENTE ...]` por `![descripción](Anexo_Hardware_Fotos/archivo.jpg)` una vez que las fotos estén en la carpeta.
5. Exportar/imprimir este archivo (o pegarlo como anexo dentro del documento principal del examen).
