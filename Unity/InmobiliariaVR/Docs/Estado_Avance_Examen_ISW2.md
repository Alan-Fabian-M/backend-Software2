# Estado de Avance y Checklist — Primer Parcial (ISW2)
**Proyecto:** InmobiliariaVR (Marketplace Inmobiliario con Realidad Virtual, IA Local y Fabricación Digital)  
**Grupo:** Grupo 1 (Tema: Realidad Virtual / Asistente Local)  
**Fecha de Entrega:** 22 de septiembre  

---

## 📊 Porcentaje Global Estimado: **~80%** *(revisado 2026-09-10, subía de ~78%)*

> Sube porque se redactó el documento formal en Word del **punto 1 (Fundamentación Teórica)** y el **punto 2 (Tres Aplicaciones Reales)** — `InmobiliariaVR_FundamentacionTeorica.docx`, con las 5 fuentes hiladas en texto corrido por pilar + síntesis, referencias en APA, y el análisis de Shapespark/IrisVR/Roomle con su relación al proyecto. Ambos puntos eran de los más atrasados (55% y 75%) y ahora están en 85%, solo faltan imágenes/capturas. Motivos previos que se mantienen: (1) se adoptó el schema v2 del Scene Graph propuesto por Alan (`prefab_id`, `room_info.bounds`, `properties.color_hex/material_type` — incluye detección de color real del mueble en la foto), y (2) `Monoambiente_Jazmin` ya tiene el contenido completo que definió el equipo para la sala (sofá, tv, mesas, sillas, lámpara y librero). Se está compilando el APK de `Monoambiente_Jazmin` para probar en el Quest 3.

El proyecto tiene un avance sobresaliente en la parte más difícil del examen (**el desarrollo de software interactivo con IA local sin conexión**). A continuación se desglosa el avance según los criterios oficiales de evaluación del docente:

---

## 1. Fundamentación Teórica y Estado del Arte
*Ponderación del punto: Alta (Revisión bibliográfica formal, fuentes de los últimos 3 años, sin IA/Wikipedia como fuente primaria).*

- [x] **Identificación del alcance del proyecto:** Marketplace + VR + IA + Impresión 3D (`Propuesta_Sistema_Inmobiliario_VR.md`).
- [x] **Búsqueda bibliográfica formal:** 5 artículos verificados (IEEE, Frontiers, ScienceDirect/Elsevier, MDPI), 2023-2025, uno por cada pilar del proyecto — ver [`Bibliografia_Formal.md`](Bibliografia_Formal.md).
- [x] **Redacción del marco teórico:** Documento formal en Word (`InmobiliariaVR_FundamentacionTeorica.docx`) con las 5 fuentes hiladas en texto corrido (4 subsecciones por pilar + síntesis) y referencias en formato APA.
- [ ] Faltan las imágenes explicativas en el documento (diagramas/capturas) — el texto ya está completo.
- **Estado:** 🟢 **85% completado**

---

## 2. Tres Aplicaciones Reales en el Mundo
*Requisito: 3 aplicaciones existentes, de gran envergadura, distintas entre sí pero sobre el mismo tema, con documentación de funcionalidades, desarrolladores, comercialización y tecnologías.*

- [x] **Selección y análisis técnico:**
  1. **Shapespark:** Recorridos 3D/VR web con cambio de acabados e iluminación horneada.
  2. **IrisVR (Prospect):** Entorno VR colaborativo para BIM, revisión arquitectónica y notas por voz.
  3. **Roomle:** Configurador modular 3D/AR/VR B2B para inmobiliarias y retail con cotizador automático.
- [x] **Documentación técnica previa:** Registrado en [`Analisis_Recomendaciones_Examen.md`](Analisis_Recomendaciones_Examen.md).
- [x] **Redactado en documento formal:** las 3 aplicaciones (funcionalidades, comercialización, tecnologías) + relación con InmobiliariaVR, en `InmobiliariaVR_FundamentacionTeorica.docx`.
- [ ] **Material para el taller/exposición:** Elaborar diapositivas y/o video demostrativo sin audio de su funcionamiento (requisito del examen). Falta además el material demostrativo (capturas/video) de la app elegida como referencia — ver `Rubrica_Examen_Preguntas.md` punto 2.
- **Estado:** 🟢 **85% completado**

---

## 3. Tres Casos de Estudio Locales y Prototipos
*Requisito: 3 escenarios en el medio local con planeación, requerimientos, diseño y mockup en Figma. Uno de ellos debe llegar a implementación completa.*

- [x] **Definición de los 3 escenarios:**
  1. **Preventa de departamentos en pozo (Constructora local):** Recorrido interactivo para cerrar ventas antes de construir.
  2. **Estudio de interiorismo y muebles a medida:** Catálogo inmersivo con asistente de recomendaciones.
  3. **Remodelación y restauración de viviendas:** Visualización previa de cambios en pisos/muros y maqueta 3D física.
- [ ] **Diseño en Figma (Mockups):** Prototipar el flujo de pantallas y experiencia de usuario de los 3 casos.
- [x] **Selección del caso para desarrollo completo:** Caso 1 (Preventa Inmobiliaria con personalización y cotización).
- **Estado:** 🟡 **35% completado**

---

## 4. Implementación Técnica del Caso Elegido (Software)
*Requisitos técnicos obligatorios según las bases del examen:*

### A. Prototipo VR en Unity
- [x] Entorno base interactivo en Unity 6 con `XR Interaction Toolkit` y `OpenXR` (`Sala_MVP.unity`).
- [x] Locomoción, teletransporte y raycast para selección de objetos.
- [x] Sistema de cambio de materiales (`MaterialChangerVR.cs`) para paredes (blanco, beige, gris) y sofá (cuero, azul, verde).
- [x] Panel de interfaz en World Space interactivo con puntero láser (`MenuPersonalizacion`).
- [x] **Probado con hardware real:** Alan corrió `Sala_MVP` en un Meta Quest 3 (Quest Link) — controles, cambio de color y menú de mover muebles confirmados con el visor puesto.
- [x] **Bugs de layout corregidos tras usar la app de verdad:** panel de IA mostrando el reverso (texto espejado), botón de menú flotando cerca de la puerta (se confundía con un agarrador), TV mal ubicada — corregido en `Sala_MVP` y `Departamento_B`.
- [x] **Bonus — segunda escena de ejemplo:** `Dormitorio.unity` (cama, mesa de noche, ropero con texturas PBR del pack de `recursos`) reusando toda la lógica existente sin cambios — valida que la arquitectura escala a otros tipos de ambiente para el marketplace.
- [x] **Modelo 3D definitivo de Blender integrado:** `Monoambiente_Jazmin.unity` — departamento real modelado por Jazmín (194 mallas: dormitorio, cocina, living, baño, oficina, entrada, pasillo), materiales ya en shader URP/Lit sin necesitar conversión, colliders generados, caminable y confirmado en Play Mode con todo el sistema de menús/IA existente reutilizado sin cambios.
  > [!INFO] Alcance de esta primera integración
  > Confirmar con Jazmín si el modelo es 100% propio o si usó una base de Sketchfab (los nombres internos del archivo lo sugieren) para citarlo correctamente si aplica.
- [x] **Personalización interactiva en `Monoambiente_Jazmin`:** el usuario toca (Select/Grip) la cama, el ropero, la mesa y las sillas del comedor para ciclar entre el modelo original de Jazmín y 2 alternativas cada uno (`CamaSwapperVR.cs` / `MuebleSwapperVR.cs`, pack de `recursos`) — mismo patrón táctil que `MaterialChangerVR`. Probado de punta a punta (ciclo completo original→opción1→opción2→original) para los 4 muebles.
- [ ] Falta hornear la iluminación de `Monoambiente_Jazmin` (hoy usa luz en tiempo real sin optimizar) y wirear `MaterialChangerVR` a paredes específicas.
- **Estado VR:** 🟢 **98% completado**

### B. Inteligencia Artificial Local (Sin Servidor Externo)
- [x] Cumplimiento estricto del requisito: Todo corre en `localhost:11434` vía Ollama sin conexión a internet.
- [x] Script de conexión C# (`OllamaAIController.cs`) consumiendo el modelo ligero `gemma2:2b`.
- [x] Inferencia estructurada en formato JSON (`{"target":"pared|sofa","color":"..."}`).
- [x] Integración de reconocimiento de voz manos libres (`VoiceCommandController.cs`) con `KeywordRecognizer` — gramática cerrada, 100% on-device siempre, sin depender del servicio en la nube de Windows (a diferencia del `DictationRecognizer` original).
- [ ] Falta confirmar con micrófono real que `KeywordRecognizer` reconoce las frases esperadas (el bug de permisos que afectaba a `DictationRecognizer` no debería aplicar, pero sin confirmar aún).
- [x] **Bonus — generación de layout de sala por IA:** `RoomLayoutAIController.cs` (nueva pestaña "Diseñar" en `Sala_MVP`) interpreta una descripción en lenguaje natural de toda la sala (ej. "sofá pared oeste, mesa en el centro, tv pared norte") y reposiciona los muebles automáticamente — probado de punta a punta, responde en ~45-50s.
  > [!INFO] Decisión de diseño: se descartó leer un croquis como **imagen** con un modelo de visión local (se probó `moondream`) porque en esta máquina (sin GPU dedicada) el procesamiento de una sola imagen tardó más de 3 minutos sin terminar — inviable para un demo en vivo. Se pivoteó a que el usuario describa la sala con palabras, reusando el mismo Ollama de texto que ya responde rápido.
- [x] **Bonus 2 — Spatial Scene Compiler:** el equipo propuso una arquitectura más ambiciosa (leer una foto de un croquis con IA especializada en vez de texto). Se validó y prototipó: `SpatialSceneCompiler/` (servidor Python local, sin internet) con **YOLOv8n** (detecta muebles, 0.24s/imagen — mucho más rápido que un modelo de visión-lenguaje pesado), **OpenCV** (contorno de la sala) y **Tesseract OCR** (lee dimensiones escritas), genera un Scene Graph en JSON que `CroquisSceneCompilerController.cs` (nueva pestaña "Croquis" en `Sala_MVP`) consume para reposicionar los muebles. Probado de punta a punta con una foto real (detectó una cama correctamente).
  > [!WARNING] Limitación conocida, ya documentada en el código: no existe un modelo YOLO-OBB público con clases de muebles, así que la rotación de cada mueble siempre viene en 0° — se ajusta a mano en VR con el menú de mover/rotar ya existente. Falta además probar con un croquis real dibujado a mano por el equipo (la letra manuscrita es el punto más incierto del OCR).
- **Estado IA Local:** 🟢 **99% completado**

### C. Aplicación Móvil Pertinente con Sensores (🚨 OBLIGATORIO)
- [x] Crear la app móvil complementaria en Unity — escena `Assets/Scenes/AppMovil_AR.unity` (AR Companion App, misma proyecto/repo), ver sección 14 de [`Progreso_MVP_Unity.md`](Progreso_MVP_Unity.md).
- [x] Uso del sensor de Cámara: detección de superficies AR (AR Foundation + ARCore) y colocación de muebles con un tap (`ArFurniturePlacer.cs`).
- [x] Uso del sensor de Micrófono: permiso en runtime + captura de audio real (`MobileVoiceCommandController.cs`).
- [x] **Build de Android compilado y sin errores** (`InmobiliariaVR_AppMovilAR.apk`, ~959 MB, 0 errores).
- [x] **Asistente IA validado dentro de la escena** vía XR Simulation en el Editor (sin necesitar el celular): "pone la pared gris" → `Listo: pared -> gris`, "pone el sillón azul" → `Listo: sofa -> azul`, ambos confirmados en pantalla.
- [ ] Transcripción de voz a texto (STT) todavía sin resolver — mismo tipo de limitación ya documentada para Windows (sección 11ter de `Progreso_MVP_Unity.md`). Mientras tanto, el comando se escribe en el campo de texto (ya funcional, mismo backend Ollama).
- [ ] **Probar en un celular Android real con ARCore certificado** — se intentó con Redmi 15, Galaxy A12 y Nubia Neo 3 5G, ninguno está en la lista oficial de dispositivos soportados por ARCore. Falta conseguir un equipo certificado (ver sección 14 de `Progreso_MVP_Unity.md`).
- **Estado App Móvil:** 🟢 **80% completado** *(build compilado sin errores, lógica de colocación AR y asistente IA validados vía simulador; falta transcripción de voz y la prueba final en un celular con ARCore certificado)*

### D. Exportación e Impresión 3D (Módulo Complementario)
- [x] Investigación de flujo de exportación STL (`investigacion_VR_impresion3D.md`).
- [x] Script de exportación implementado y probado — `StlExporter.cs` + `StlExportController.cs` (STL binario, sin necesitar `pb_Stl`/`g3sharp`), ver sección 12bis de [`Progreso_MVP_Unity.md`](Progreso_MVP_Unity.md).
- [ ] Abrir el archivo generado en un slicer real (PrusaSlicer/OrcaSlicer) para la validación final — solo se validó a nivel binario hasta ahora.
- **Estado Impresión 3D:** 🟢 **80% completado** *(corregido — el checklist anterior no reflejaba que esto ya estaba implementado y probado)*

---

## 5. Entregables de Exposición y Artículos Individuales
- [ ] **Artículo individual inédito:** Cada uno de los 5 integrantes debe elaborar su propio artículo técnico sobre su especialidad en el tema.
- [ ] **Diapositivas de la presentación:** Preparar la defensa de 40 a 45 minutos.
- [ ] **Material de apoyo (2 recursos mínimos):** Repositorios GitHub de soporte, videos demostrativos y carpeta descargable en Drive.
- **Estado:** 🔴 **10% completado**

---

## 📌 Próximos Pasos Recomendados (Orden de Prioridad)

- [x] ~~Probar `Sala_MVP` en un Meta Quest 3 real~~ — hecho 2026-09-10 (Alan, vía Quest Link).
- [x] ~~Resolver el riesgo de que el reconocimiento de voz dependiera de internet~~ — hecho 2026-09-10 (`KeywordRecognizer`, ver sección 4B).
- [x] ~~Compilar y validar `AppMovil_AR` para Android~~ — hecho 2026-09-10, build sin errores + lógica confirmada vía XR Simulation (ver sección 4C).
- [x] ~~Prompts para Figma Make de los 3 casos de estudio~~ — hecho 2026-09-10, ver [Prompts_FigmaMake_Casos_Estudio.md](Prompts_FigmaMake_Casos_Estudio.md).
- [x] ~~Generar layout de sala por IA a partir de una descripción~~ — hecho 2026-09-10, `RoomLayoutAIController.cs` + pestaña "Diseñar" (ver sección 4B).
- [x] ~~Prototipar y validar la arquitectura Spatial Scene Compiler (YOLO+OpenCV+OCR)~~ — hecho 2026-09-10, ver sección 4B.
- [x] ~~Integrar el modelo 3D definitivo de Blender~~ — hecho 2026-09-10, `Monoambiente_Jazmin.unity` (ver sección 4A).
- [x] ~~Wirear interacciones (cambiar modelos de muebles) en `Monoambiente_Jazmin`~~ — hecho 2026-09-10, cama/ropero/mesa/sillas (ver sección 4A).

1. **Probar el Spatial Scene Compiler con un croquis real dibujado a mano por el equipo** (no una foto de stock) — es el único paso que falta para validar el OCR de verdad.
2. **Conseguir un celular Android con ARCore certificado para la prueba final en hardware real** — Redmi 15, Galaxy A12 y Nubia Neo 3 5G descartados (no certificados). Pedir prestado uno de la lista recomendada (cualquier Redmi Note, Galaxy A5x/A7x/S, Pixel, Moto G reciente) a alguien fuera del equipo si hace falta.
3. **Confirmar con Jazmín el origen del modelo `monoambiente.fbx`** (los nombres internos sugieren una base de Sketchfab) — si es un asset con licencia CC, agregar la atribución correspondiente.
4. **Hornear la iluminación de `Monoambiente_Jazmin`** — hoy usa luz en tiempo real sin optimizar.
5. **Confirmar `KeywordRecognizer` con micrófono real** — probar el botón "Hablar" en el Editor diciendo frases como "pared gris" o "quiero sofa azul".
6. **Generar los mockups en Figma Make** con los prompts ya preparados, y asignar a los integrantes correspondientes para iterar.
7. **Artículos, diapositivas y material de apoyo (punto 5, el más atrasado — 10%):** confirmar avance con Santi.
5. **Importar modelo 3D de Blender:** Reemplazar las primitivas por el modelo `.fbx` de la sala.
