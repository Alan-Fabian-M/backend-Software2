# Anexo: Evidencia de la aplicación real de referencia

Responde al punto 2 de la rúbrica del examen (ver [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md)): "¿La aplicación que eligió está 100% terminada y en producción, con detalle suficiente para demostración?"

Las 3 aplicaciones ya estaban identificadas en [`Analisis_Recomendaciones_Examen.md`](Analisis_Recomendaciones_Examen.md). Lo que faltaba era **evidencia verificable de que existen y funcionan en producción hoy**, no solo la descripción escrita. Se visitaron los 3 sitios oficiales (9 de septiembre de 2026) para confirmarlo.

---

## Recomendación: usar Shapespark como demo en vivo el día del examen

De las 3, es la única que se puede **mostrar funcionando en tiempo real frente al profesor** sin instalar nada: corre en el navegador (WebGL/HTML5), tiene recorridos de ejemplo reales de clientes embebidos en la propia home, y no depende de hardware VR para la demo básica.

**Cómo demostrarla en el examen:**
1. Abrir [shapespark.com](https://shapespark.com/) en el navegador.
2. Bajar hasta la sección "See how customers use Shapespark" — son recorridos reales subidos por estudios de arquitectura clientes (Motion Wave, makebelieve graphics, TIGERX, etc.), cada uno con su propio link a un walkthrough interactivo real.
3. Abrir uno y caminar por él en vivo (mismo tipo de interacción — recorrido 3D interactivo — que `Sala_MVP`), para comparar contra la demo propia.

---

## Evidencia de producción por aplicación

### 1. Shapespark — ✅ SaaS activo, con clientes reales verificables

- **Sitio:** [shapespark.com](https://shapespark.com/)
- **Modelo comercial confirmado:** planes de suscripción activos y con precio público — Starter (USD 29/mes), Standard (USD 49/mes), Plus (USD 99/mes), Premium (USD 249/mes) — ver [shapespark.com/pricing](https://shapespark.com/pricing).
- **Clientes reales citados con nombre y cargo** (no genéricos): *"Shapespark finally allows what you want from the big game engines"* — Tim Bonnke, Owner, build | Architektur-Visualisierung.
- **Prueba de "en producción":** la propia home embebe recorridos reales subidos por 12 estudios distintos (Motion Wave, makebelieve graphics, My3ideas, Aura 3D Studio, Lightform 3D, TIGERX, Little Moon, Movimento Club, Kazooie, entre otros).

### 2. Roomle — ✅ Plataforma activa, con marcas reconocibles

- **Sitio:** [roomle.com](https://roomle.com/)
- **Modelo comercial confirmado:** producto B2B llamado "Rubens Platform" (3D Viewer, Product Configurator, Room Designer, AR), con casos de éxito nombrados (ej. "Shelved Modular Storage System").
- **Frase textual del sitio:** *"Famous manufacturers and retailers around the world use Rubens to sell their products intuitively."*

### 3. IrisVR (Prospect) — ⚠️ Verificar antes de citarla como "100% en producción"

- **Sitio:** [irisvr.com](https://irisvr.com/)
- **Hallazgo importante (no estaba en el análisis original):** la propia home de IrisVR muestra un aviso: *"New! From the team who brought you Prospect by IrisVR: Autodesk Workshop XR..."* — esto sugiere que **Prospect fue reemplazado/sucedido por un nuevo producto de Autodesk** (Workshop XR), tras la adquisición de IrisVR por Autodesk. Prospect sigue listado en el sitio (con clientes como Corgan, Mortenson, Black & Veatch), pero **puede no ser ya el producto activo principal**.
- **Recomendación:** si el profesor pregunta puntualmente por esta, aclarar este matiz en vez de presentarla como "100% terminada y en producción" sin salvedad — es más sólido mostrar que el equipo investigó a fondo y encontró este detalle, que forzar una afirmación que un vistazo rápido a la web contradice.

---

## Qué falta para cerrar este punto

- [ ] Sacar 2-3 capturas propias del recorrido de Shapespark elegido para mostrar (no se guardaron capturas de los sitios en este repo por derechos de autor — hay que entrar y capturar en el momento, o durante la preparación previa).
- [ ] Decidir si mantener las 3 apps como "las 3 aplicaciones reales" del punto 2 del examen general, o reemplazar IrisVR por otra si el profesor exige que las 3 estén indiscutiblemente "en producción" tal cual (alternativas del mismo rubro: **Enscape**, **Twinmotion**, **YouVR**).
- [ ] Si se decide profundizar la demo en vivo, practicar antes el recorrido elegido de Shapespark (conexión a internet necesaria — a diferencia de `Sala_MVP`, que corre sin internet salvo el paso de IA).
