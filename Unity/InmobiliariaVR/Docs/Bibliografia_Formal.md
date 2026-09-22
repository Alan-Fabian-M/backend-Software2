# Bibliografía formal — Fundamentación teórica (Punto 1 del examen)

Responde al punto 1 de la rúbrica del examen (ver [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md)): revisión bibliográfica formal, fuentes de los últimos 3 años (2023-2026), sin IA/Wikipedia como fuente primaria.

Las 5 fuentes de abajo se verificaron una por una visitando la página oficial de la revista/editorial (IEEE Xplore, Frontiers, ScienceDirect/Elsevier, MDPI) el 9 de septiembre de 2026 — no se tomó ningún dato de un resumen generado por IA ni de Wikipedia. Cubren los 4 pilares del proyecto: VR inmobiliaria, personalización con IA+VR, IA local/offline, e impresión 3D.

---

## 1. Realidad Virtual aplicada a inmobiliaria (pilar: recorrido VR)

> Prema, K., Chhetri, A., Singh, S. P., & Rao, D. Y. (2024). *Virtual Reality for Real Estate: Revolutionizing Property Visualization and Enhancing Client Engagement*. 2024 IEEE 4th International Conference on ICT in Business Industry & Government (ICTBIG). IEEE. https://ieeexplore.ieee.org/document/10911827

**Relevancia:** analiza directamente el caso de uso central del proyecto — usar VR para que el cliente visualice una propiedad y mejorar su compromiso/decisión de compra antes de que el inmueble exista físicamente. Sirve para justificar la Etapa 1-2 de la propuesta (recorrido VR de preventa).

---

## 2. Adopción de AR/VR en la industria AEC (pilar: contexto/estado del arte del sector)

> Fathi, S., Sabeti, S., Shoghli, O., Heydarian, A., & Balali, V. (2025). *Adoption of virtual and augmented reality in the architecture, engineering, construction, and facilities management (AEC-FM): mixed method analysis of trends, gaps, and solutions*. Frontiers in Built Environment, 11. https://doi.org/10.3389/fbuil.2025.1580639

**Relevancia:** estudio mixto (encuestas a +200 profesionales en 2018/2020/2023 + entrevistas cualitativas) sobre qué tan adoptada está la VR/AR en construcción e inmobiliaria, qué limitaciones tuvo y qué brechas quedan — sirve para el "estado del arte" y para justificar por qué un proyecto como este todavía tiene valor (la adopción real sigue por debajo del entusiasmo inicial de 2018).

---

## 3. IA + VR para personalización de interiores (pilar: personalización/menú de materiales)

> Wu, S., & Han, S. (2023). *System Evaluation of Artificial Intelligence and Virtual Reality Technology in the Interactive Design of Interior Decoration*. Applied Sciences, 13(10), 6272. https://doi.org/10.3390/app13106272

**Relevancia:** estudia exactamente el mismo cruce que hace `InmobiliariaVR` — usar IA junto con VR para que el cliente personalice la decoración interior en tiempo real, acortando la comunicación entre diseñador y cliente. Es la fuente más directamente comparable al módulo de `MaterialChangerVR`/`OllamaAIController` del proyecto.

---

## 4. IA local / offline (pilar: asistente de IA sin conexión a internet — Ollama)

> Tyndall, E., Wagner, T., Gayheart, C., Some, A., et al. (2025). *Feasibility Evaluation of Secure Offline Large Language Models with Retrieval-Augmented Generation for CPU-Only Inference*. Information, 16(9), 744. https://doi.org/10.3390/info16090744

**Relevancia:** evalúa la viabilidad real de correr modelos de lenguaje localmente (sin GPU, sin conexión a internet) en hardware de consumo — exactamente la arquitectura que usa `OllamaAIController.cs` (`localhost:11434`, `gemma2:2b`, sin servidor externo). Es la fuente que mejor respalda técnicamente por qué el enfoque "IA local" del proyecto es viable y no solo una simplificación por falta de tiempo.

---

## 5. De BIM a impresión 3D (pilar: exportación STL / maqueta física)

> García-Alvarado, R., Soza, P., Moroni, G., Pedreros, F., Avendaño, M., Banda, P., & Berríos, C. (2024). *From BIM model to 3D construction printing: A framework proposal*. Frontiers of Architectural Research, 13(4), 912–927. https://doi.org/10.1016/j.foar.2024.03.002 (ScienceDirect/Elsevier)

**Relevancia:** propone un flujo de trabajo formal para llevar un modelo digital de un edificio a una impresora 3D — el mismo problema que resuelve `StlExporter.cs`/`StlExportController.cs`, aunque a otra escala (impresión de construcción vs. maqueta de escritorio). Sirve para enmarcar teóricamente por qué el paso "modelo 3D → STL → impresión" tiene respaldo académico como flujo de trabajo en AEC.

---

## Cómo usar esto en el marco teórico

Estas 5 fichas ya tienen cita en formato APA + un párrafo de relevancia — lo que falta para cerrar el punto 1 del examen es la **redacción narrativa del marco teórico** que las hile en un texto corrido (no solo la lista), con imágenes explicativas. Eso es trabajo de escritura académica que le corresponde revisar a alguien del equipo, pero el trabajo de búsqueda y verificación de fuentes reales ya está hecho.

**Checklist actualizado** (ver también [`Estado_Avance_Examen_ISW2.md`](Estado_Avance_Examen_ISW2.md)):
- [x] Identificación del alcance del proyecto.
- [x] Búsqueda bibliográfica formal (5 fuentes verificadas, 2023-2025, IEEE/Frontiers/ScienceDirect/MDPI).
- [ ] Redacción del marco teórico como documento formal corrido, con imágenes.
