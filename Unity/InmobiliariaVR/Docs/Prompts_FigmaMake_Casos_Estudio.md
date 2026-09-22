# Prompts para Figma Make — Mockups de los 3 Casos de Estudio

> [!INFO] Para qué es este documento
> Cubre el punto 3 de la rúbrica del examen ("Tres Casos de Estudio Locales y Prototipos"), que pide mockup en Figma de los 3 escenarios definidos en [Estado_Avance_Examen_ISW2.md](Estado_Avance_Examen_ISW2.md). Cada sección de abajo es un prompt listo para pegar en **Figma Make** (figma.com/make) y generar el prototipo de pantallas correspondiente. No reemplaza el trabajo en Unity — estos mockups son de las apps **web/móviles** que rodean al núcleo VR (marketplace, catálogo, gestión), tal como se describe en la [Propuesta_Sistema_Inmobiliario_VR.md](Propuesta_Sistema_Inmobiliario_VR.md).

---

## Antes de empezar

- Generá los 3 en **archivos separados** de Figma Make (no todo en un solo prompt) — resultan en sistemas de pantallas más limpios y fáciles de iterar.
- Si querés que los 3 casos se vean como una misma "familia visual" (recomendado para la exposición), pegá primero el bloque **"Estilo compartido"** al final de este documento junto con cada prompt.
- Cada prompt está escrito para pedir un flujo completo de pantallas conectadas (no una pantalla suelta), porque la rúbrica pide "diseño y mockup" del flujo, no solo una imagen.
- Después de generar, revisá que Figma Make haya creado los estados de "vacío", "cargando" y al menos un caso de error en las pantallas clave — si no, pedíselo como ajuste puntual.

---

## Caso 1 — Preventa de departamentos en pozo (Constructora local)

> Recorrido interactivo para cerrar ventas antes de construir. Es el caso elegido para desarrollo completo (`Sala_MVP` en Unity) — este mockup cubre el **marketplace web** que rodea a la experiencia VR: cómo el cliente llega al departamento, y qué pasa después de salir del visor.

### Prompt para Figma Make

```
Diseñá el prototipo de un marketplace inmobiliario web/responsive llamado "InmobiliariaVR",
enfocado en preventa de departamentos que todavía no están construidos. El público es gente
comprando su primer departamento en una constructora local, así que el tono debe ser confiable,
cálido y poco intimidante (no corporativo frío).

Pantallas necesarias, conectadas como un flujo real de principio a fin:

1. Home / Catálogo de proyectos: grid de tarjetas de proyectos inmobiliarios (ej. "Condominio
   Vista Verde"), cada una con imagen, ubicación, rango de precios y estado (Preventa /
   En construcción / Entrega inmediata). Filtros por ciudad, precio y cantidad de ambientes.

2. Detalle de proyecto: info general de la torre/condominio (ubicación en mapa, amenities,
   fecha estimada de entrega, galería de renders) y una grilla de unidades disponibles
   (ej. "Dpto A-302 — 85 m² — Bs 650.000 — Disponible").

3. Ficha técnica del departamento: plano 2D, metraje, precio, estado (disponible/reservado/
   vendido), lista de acabados incluidos, y un botón destacado "Recorrer en VR" / "Personalizar
   en VR" que es el paso que continúa en la app de Unity (no hace falta diseñar esa parte,
   solo el botón y un texto breve explicando que se abre en el visor).

4. Pantalla de "Resumen de tu diseño": a la que el usuario vuelve después de personalizar en
   VR (colores de pared elegidos, modelo de sofá, etc. — mostralo como una lista con swatches
   de color y miniaturas, dato simulado). Botón "Solicitar cotización".

5. Formulario de cotización/reserva: datos de contacto, resumen del departamento + personalización
   elegida, monto de reserva, y confirmación.

6. Panel simple de "Mis reservas" para el usuario logueado: estado de cada reserva
   (En revisión / Aprobada / Cotización enviada).

Estilo: moderno, confiable, con acentos cálidos (no solo azul corporativo). Mobile-first pero
que también se vea bien en desktop. Usá datos de ejemplo realistas en español (nombres de
proyectos, precios en Bs, ubicaciones de una ciudad boliviana).
```

---

## Caso 2 — Estudio de interiorismo y muebles a medida

> Catálogo inmersivo con asistente de recomendaciones. A diferencia del Caso 1 (comprar un depto nuevo), acá el usuario ya tiene una casa/depto y quiere rediseñar un ambiente con muebles a medida, con ayuda de un asistente de IA.

### Prompt para Figma Make

```
Diseñá el prototipo de una app web/responsive para un estudio de interiorismo llamado
"InmobiliariaVR Interiores", donde el cliente arma el diseño de un ambiente de su casa
(living, dormitorio, cocina) eligiendo muebles a medida, con ayuda de un asistente de IA
que sugiere combinaciones. Tono cálido, inspirador, tipo catálogo de diseño de interiores
premium pero accesible.

Pantallas necesarias, conectadas como un flujo real de principio a fin:

1. Home: selector de tipo de ambiente a diseñar (Living / Dormitorio / Cocina / Comedor),
   cada uno con una foto inspiracional y una breve descripción.

2. Catálogo de muebles por categoría (Sofás / Mesas / Sillas / Almacenamiento): grid de
   tarjetas con foto del mueble, nombre, rango de precio, y tags de estilo (Moderno,
   Clásico, Minimalista, Rústico). Filtros por categoría, estilo y precio.

3. Panel del "Asistente de diseño IA": un chat lateral o modal donde el usuario escribe
   pedidos en lenguaje natural (ej. "¿qué color de pared combina con una mesa de madera
   oscura?" o "necesito un sofá para un living chico") y recibe sugerencias con miniaturas
   de productos recomendados. Mostrá 2-3 mensajes de ejemplo ya completados en la conversación.

4. Vista de "Mi diseño": resumen visual del ambiente armado — lista de los muebles elegidos
   con miniatura, color/acabado elegido para cada uno, y precio total. Botón "Ver en 3D/VR"
   (solo el botón, sin diseñar esa parte) y botón "Solicitar presupuesto de fabricación
   a medida".

5. Formulario de solicitud de fabricación a medida: medidas del espacio, materiales
   preferidos, plazo deseado, datos de contacto.

6. Pantalla de seguimiento de pedido: línea de tiempo simple (Presupuestado → Confirmado →
   En fabricación → Listo para entrega).

Estilo: cálido, inspirador, con fotografía de producto grande y protagonista. Tipografía
elegante pero legible. Usá datos de ejemplo realistas en español.
```

---

## Caso 3 — Remodelación y restauración de viviendas

> Visualización previa de cambios en pisos/muros y maqueta 3D física. El usuario ya vive en la casa y quiere ver cómo quedaría antes de decidirse a remodelar — con salida a impresión 3D de una maqueta física, distinto foco que los otros dos casos.

### Prompt para Figma Make

```
Diseñá el prototipo de una app web/responsive para un servicio de remodelación de viviendas
llamado "InmobiliariaVR Remodela", donde el cliente sube fotos o el plano de un ambiente
actual, visualiza propuestas de cambio (pisos, muros, acabados) y puede pedir una maqueta
3D impresa de la propuesta antes de decidirse a remodelar de verdad. Tono profesional,
técnico pero cercano, orientado a dar confianza antes de una obra.

Pantallas necesarias, conectadas como un flujo real de principio a fin:

1. Home: propuesta de valor clara ("Visualizá tu remodelación antes de romper una sola
   pared") con botón "Empezar mi proyecto" y ejemplos de antes/después en miniatura.

2. Flujo de carga del ambiente actual: subida de fotos del ambiente o plano existente,
   con un formulario simple (tipo de ambiente, m², qué se quiere remodelar: piso / paredes
   / ambos).

3. Comparador "Antes / Después": vista con slider o tabs que compara una foto del ambiente
   actual contra la propuesta de remodelación generada (usá imágenes de ejemplo tipo
   antes/después de renovación de interiores). Selector lateral de acabados a probar
   (tipo de piso: porcelanato/madera/cerámico — color de muro).

4. Vista de "Propuesta guardada": resumen de los cambios elegidos con muestras de material,
   costo estimado de la obra, y un botón "Pedir maqueta 3D impresa de esta propuesta".

5. Formulario de pedido de maqueta 3D: escala deseada (ej. 1:50), qué parte del ambiente
   incluir, dirección de entrega o retiro, costo estimado de impresión.

6. Pantalla de seguimiento: línea de tiempo simple (Propuesta aprobada → Modelo preparado →
   Imprimiendo → Lista para entrega), con una foto de ejemplo de una maqueta impresa.

Estilo: profesional, confiable, con el comparador antes/después como elemento visual
protagonista de todo el flujo. Colores neutros con un acento de color para las zonas
"después". Usá datos de ejemplo realistas en español.
```

---

## Estilo compartido (opcional, pegar antes de cada prompt de arriba)

> Usalo si querés que los 3 casos compartan la misma identidad visual para la exposición del examen — pegalo como primer párrafo de cada prompt, antes del texto específico de cada caso.

```
Usá esta identidad visual consistente en todas las pantallas: paleta con un color primario
azul-verde confiable (tipo "InmobiliariaVR"), tipografía sans-serif moderna y legible,
esquinas redondeadas suaves en tarjetas y botones, mucho espacio en blanco, iconografía
lineal simple. Navegación superior o inferior simple y consistente entre pantallas del
mismo flujo.
```

---

## Después de generar en Figma Make

- [ ] Exportar o linkear los 3 prototipos en el material de apoyo del examen (punto 5 de la rúbrica).
- [ ] Revisar que el flujo de cada caso se pueda "clickear" de punta a punta en modo presentación de Figma, para la defensa de 40-45 min.
- [ ] Asignar cada caso a un integrante distinto del equipo para iterar sobre el mockup generado (ajustar textos, copys, casos de error).
