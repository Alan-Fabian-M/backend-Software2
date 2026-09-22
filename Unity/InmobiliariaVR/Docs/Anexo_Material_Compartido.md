# Anexo: Material compartido (videos, esquemas) accesible por código QR

Responde al punto 4 de la rúbrica del examen (ver [`Rubrica_Examen_Preguntas.md`](Rubrica_Examen_Preguntas.md)): material para compartir — videos, esquemas y otros — accesible desde un código QR en el documento.

> ⚠️ **Qué está listo y qué falta.** Los esquemas ya están generados (sección 1). El video **no se puede grabar automáticamente** — necesita a alguien del equipo jugando la escena en Play Mode o en el visor mientras se graba pantalla. El QR final **no se puede generar todavía** porque necesita apuntar a un link real de Drive con el material ya subido — instrucciones al final (sección 3).

---

## 1. Esquemas (listos)

Generados a partir del código real del proyecto (no son mockups genéricos — cada caja/flecha corresponde a un método o clase que existe en `Assets/Scripts/`):

| Esquema | Archivo | Qué muestra |
|---|---|---|
| Máquina de estados | [`Esquemas/diagrama_estados.svg`](Esquemas/diagrama_estados.svg) | Los `AppState` (Recorrido/Personalización/Pintura/Menú) y qué botón dispara cada transición |
| Arquitectura de scripts | [`Esquemas/diagrama_arquitectura.svg`](Esquemas/diagrama_arquitectura.svg) | Los 10 scripts principales agrupados por responsabilidad y su relación con `StateManager` |
| Flujo del asistente de IA | [`Esquemas/diagrama_flujo_ia.svg`](Esquemas/diagrama_flujo_ia.svg) | Del pedido en lenguaje natural del usuario hasta el cambio de color aplicado en la escena, pasando por Ollama |

Estos tres `.svg` se pueden abrir en cualquier navegador o insertar directo en el documento final (Word/PDF acepta SVG o se puede exportar cada uno a PNG si hace falta).

---

## 2. Video demostrativo (guion listo, falta grabar)

Guion pensado para un video corto (**3-4 minutos**), en orden, cada bloque mapeado a un flujo ya documentado en [`Docs/Flujos/`](Flujos/) para que quien grabe sepa exactamente qué mostrar y por qué:

| # | Duración aprox. | Qué mostrar | Flujo de referencia |
|---|---|---|---|
| 1 | 20 s | Intro: sala `Sala_MVP`, jugador caminando/teletransportándose (modo Recorrido) | [01_Recorrido_y_Menu.md](Flujos/01_Recorrido_y_Menu.md) |
| 2 | 25 s | Abrir el menú principal con `Boton_Menu`, mostrar que se reposiciona frente al jugador, cambiar de pestaña (Recorrido → Personalizar → Pintar) | [01_Recorrido_y_Menu.md](Flujos/01_Recorrido_y_Menu.md) |
| 3 | 40 s | En modo Personalización: agarrar el Sofá con un toque corto (cicla color), después sostener el grip sobre la Mesa para abrir el menú de mover/rotar y usar los botones de flecha y rotación | [02_Mover_Rotar_Mueble.md](Flujos/02_Mover_Rotar_Mueble.md), [03_Cambio_Material.md](Flujos/03_Cambio_Material.md) |
| 4 | 30 s | Pestaña Pintar: elegir un color de la paleta, apuntar con el rayo a una pared y pintarla | [03_Cambio_Material.md](Flujos/03_Cambio_Material.md) |
| 5 | 45 s | Panel de IA: escribir o dictar por voz un pedido tipo *"poné la pared gris"* y mostrar el resultado en tiempo real, mencionando que Ollama corre en `localhost` sin internet | [04_Asistente_IA.md](Flujos/04_Asistente_IA.md) |
| 6 | 20 s | Pestaña Exportar: botón "Exportar a STL", mostrar el mensaje de estado con la cantidad de triángulos | [06_Exportacion_STL.md](Flujos/06_Exportacion_STL.md) |
| 7 | 15 s | Cierre: mostrar el archivo `.stl` generado en la carpeta de exportación (opcional: abierto en un visor STL/slicer) | [06_Exportacion_STL.md](Flujos/06_Exportacion_STL.md) |

**Cómo grabarlo:** grabación de pantalla del Editor de Unity en Play Mode (OBS Studio o el grabador de Windows `Win+Alt+R`) mientras alguien controla la escena con el XR Device Simulator (teclado/mouse) o con el visor conectado. No hace falta narración —el guion de arriba ya sirve como lista de chequeo de qué capturar en orden.

---

## 3. Subir el material y generar el código QR (pasos finales, manuales)

1. **Crear una carpeta en Google Drive** (o similar) llamada por ejemplo `InmobiliariaVR - Material Examen`, con permisos "Cualquiera con el link puede ver".
2. Subir ahí:
   - El video grabado (sección 2).
   - Los tres `.svg` de esquemas (sección 1) — o exportados a PNG/PDF si se prefiere.
   - Opcional: capturas sueltas adicionales.
3. Copiar el link de la carpeta compartida.
4. Generar el QR a partir de ese link con cualquier generador gratuito (ej. [qr-code-generator.com](https://www.qr-code-generator.com/) o `qrencode` si alguien tiene Python/CLI) y descargarlo como imagen.
5. Pegar esa imagen de QR en el documento final del examen, junto a este anexo.

> 💡 Si me pasás el link de la carpeta de Drive una vez que esté armada, puedo generar el código QR correspondiente y dejarlo guardado en `Docs/Esquemas/` listo para insertar en el documento — no lo genero ahora porque un QR apuntando a un link inexistente no sirve de nada el día del examen.
