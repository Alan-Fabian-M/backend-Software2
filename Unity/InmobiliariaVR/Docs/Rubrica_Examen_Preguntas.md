# Rúbrica del día del examen — preguntas y plan de respuesta

Transcripción de la hoja de evaluación que el docente usará el día del examen (foto compartida por el equipo), con el estado actual del proyecto frente a cada punto y qué falta preparar. Puntaje máximo visto en la hoja: **60**.

---

## 1. ¿El trabajo asignado está completamente terminado en un 100% de acuerdo a lo solicitado?

**Puntaje:** 0 / 20 (marcado NO en el ejemplo)

**Qué pide:** el alcance completo declarado en la propuesta — VR + IA local + app móvil con sensores + impresión 3D + conexión con marketplace/backend.

**Estado actual:** VR (80%) e IA local (90%) están avanzados. La app móvil arrancó (escena `AppMovil_AR` con AR Foundation + ARCore, colocación de muebles con la cámara, permiso y captura de micrófono — ver sección 14 de [Progreso_MVP_Unity.md](Progreso_MVP_Unity.md)), pero todavía falta:
- Probar la app móvil en un celular Android real (solo se validó que compila sin errores en el Editor).
- Transcripción de voz a texto en la app móvil (por ahora el comando se escribe en un campo de texto).
- Conexión con marketplace web + backend (guardar diseño, cotización).
- Reconocimiento de voz de la versión VR sin resolver del todo (falta confirmar que funcione sin internet).

**Para levantar el puntaje:** terminar de probar la app móvil en un dispositivo real es lo que más rápido puede mover este punto de NO a SI — es lo único 100% nuevo, el resto son ajustes sobre algo que ya funciona.

---

## 2. ¿La aplicación que eligió está 100% terminada y en producción, con detalle suficiente para demostración?

**Puntaje:** 20 / 20 (marcado SI en el ejemplo)

**Qué pide:** de las 3 aplicaciones reales analizadas (Shapespark, IrisVR/Prospect, Roomle), mostrar evidencia concreta de que existen y funcionan en producción — capturas, video, o demo en vivo — no solo la descripción escrita.

**Estado actual:** hay análisis técnico escrito en `Analisis_Recomendaciones_Examen.md`, pero falta el material demostrativo (capturas/video) de la app elegida como referencia principal.

**Para preparar:** juntar 3-5 capturas de pantalla o un video corto de demostración de la app elegida, y citar la fuente (sitio oficial, video de producto, etc.).

---

## 3. ¿El dispositivo se encuentra completo con recursos disponibles y a sí mismo, y estos dispositivos están impresos como anexo en el documento?

**Puntaje:** 0 / 20 (marcado NO en el ejemplo)

**Qué pide:** un anexo impreso en el documento con fotos y especificaciones del hardware usado (visor VR, celular con sensores para la app móvil), no solo la mención en el texto.

**Estado actual:** no existe ese anexo.

**Para preparar:** armar una página de anexo con:
- Foto y specs del visor VR usado (modelo, resolución, sensores).
- Foto y specs del celular usado para la app móvil (cámara, micrófono, sensores AR).
- Breve nota de para qué se usa cada recurso del dispositivo en la app.

---

## 4. Realizar material para ser compartido (videos, esquemas y otros), que se encuentren en su documento para acceder desde un código QR

**Puntaje:** 20 / 20 (marcado SI en el ejemplo)

**Qué pide:** preparar material audiovisual (videos demostrativos, diagramas/esquemas) alojado en un lugar accesible (Drive, YouTube no listado, etc.) y poner un código QR en el documento que lleve directo a ese material.

**Estado actual:** no existe todavía — es el ítem más nuevo, no hay nada armado.

**Para preparar:**
1. Grabar un video corto (2-4 min) mostrando el recorrido VR, personalización, IA por voz/texto y exportación STL.
2. Subir el video y los esquemas/diagramas a una carpeta de Drive compartida.
3. Generar un código QR que apunte a esa carpeta y pegarlo en el documento final.

---

## 5. ¿La aplicación ha sido desarrollada bajo una metodología de desarrollo y siguiendo estándares de codificación, los mismos que se encuentran en su anexo de la documentación?

**Puntaje:** 20 / 20 (marcado SI en el ejemplo)

**Qué pide:** un anexo formal que describa la metodología de desarrollo usada (ej. Scrum/Kanban, sprints) y los estándares de codificación aplicados (convenciones de nombres, estructura de carpetas, patrones usados).

**Estado actual:** cubierto parcialmente por [`Docs/README.md`](README.md), [`Docs/Flujos/`](Flujos/) y [`Progreso_MVP_Unity.md`](Progreso_MVP_Unity.md), pero falta empaquetarlo explícitamente como "anexo de metodología y estándares" citable en el documento final.

**Para preparar:** redactar un anexo corto que enlace/resuma esos documentos ya existentes como evidencia formal de metodología y estándares.

---

## Resumen — prioridad para levantar puntaje

| Prioridad | Ítem | Motivo |
|---|---|---|
| 1 | Punto 1 — App móvil + conexión backend | Es el gap más grande y afecta directamente si se puede marcar "100% terminado" |
| 2 | Punto 3 — Anexo de hardware impreso | No requiere programar, solo armar el documento con fotos/specs |
| 3 | Punto 4 — Video + esquemas + QR | Tampoco requiere programar, pero toma tiempo grabar/editar y armar el QR |
| 4 | Punto 2 — Evidencia de la app de referencia | Rápido de conseguir (capturas/video ya existente de la app elegida) |
| 5 | Punto 5 — Anexo de metodología | Ya está casi todo escrito, solo falta compaginarlo como anexo formal |
