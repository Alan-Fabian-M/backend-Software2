# Flujo: Exportar la sala a un archivo STL (impresión 3D)

De la escena de Unity, tal como quedó personalizada, a un archivo `.stl` binario listo para un slicer (PrusaSlicer/OrcaSlicer) e imprimir una maqueta física.

## Paso a paso

1. **El jugador presiona el botón "Exportar a STL"** del menú → [`StlExportController.ExportarInmueble()`](../../Assets/Scripts/StlExportController.cs).
2. Si `raizAExportar` (el objeto raíz de la sala, asignado en el Inspector — por defecto toda `Sala_MVP`) no está asignado, se muestra "No hay nada asignado para exportar." y el flujo termina ahí.
3. Se genera un nombre de archivo único con fecha y hora: `InmobiliariaVR_yyyyMMdd_HHmmss.stl`, dentro de `Application.persistentDataPath/Exports` (la carpeta se resuelve en el momento del llamado, no antes, porque `Application.persistentDataPath` no se puede leer en un inicializador de campo de Unity).
4. Se llama a [`StlExporter.ExportarAArchivo(raices, ruta)`](../../Assets/Scripts/StlExporter.cs), pasándole el objeto raíz:
   1. Recorre todos los `MeshFilter` hijos de la raíz (paredes, piso, puerta, y cada mueble **en su posición/rotación/color actual**, ya que la geometría se lee directamente del `Transform` en ese momento).
   2. Por cada triángulo de cada mesh, transforma sus vértices de espacio local a espacio de mundo (`Transform.TransformPoint`) y los junta en una lista plana de triángulos.
   3. Escribe esa lista como STL binario (`EscribirStlBinario`): header de 80 bytes, cantidad de triángulos, y luego normal + 3 vértices + padding por cada triángulo.
5. Al escribir cada triángulo se hacen dos conversiones necesarias para que el archivo se vea bien en un slicer:
   - **Unidades**: metros de Unity → milímetros de impresión, multiplicando por `MilimetrosPorMetro` (20 por defecto = escala 1:50, para que una sala de ~4 m imprima una maqueta de ~8 cm).
   - **Ejes y lateralidad**: Unity es zurdo con Y arriba; STL asume diestro con Z arriba. Se remapean las coordenadas `(x,y,z) → (x,z,y)` y se invierte el orden de dos de los tres vértices del triángulo, para que las normales sigan apuntando hacia afuera (si no se invirtiera el orden, las piezas se verían "espejadas" con las normales para adentro).
6. Al terminar, `ExportarAArchivo` devuelve la cantidad de triángulos exportados, y `StlExportController` muestra en el texto de estado algo como `Exportado: InmobiliariaVR_20260909_153000.stl (48213 triangulos)`.
7. Si algo falla en el camino (permiso de escritura, ruta inválida, etc.), se captura la excepción, se muestra "Error al exportar: ..." en el texto de estado y se loguea el detalle completo en la consola de Unity.

## Por qué no hace falta "engrosar" las paredes

A diferencia de un escaneo de un cuarto real (donde las paredes detectadas no tienen espesor y hay que generarles volumen antes de poder imprimirlas — ver `investigacion_VR_impresion3D.md` en la carpeta padre, que menciona la librería `g3sharp` para ese caso), en `Sala_MVP` las paredes y el piso ya son cubos con volumen real y los muebles son modelos 3D sólidos. Por eso `StlExporter` puede exportar las mallas tal cual, sin ningún paso adicional de generación de espesor.

## Relación con otros flujos

Esta exportación lee la escena **en el momento en que se ejecuta**, incluyendo cualquier cambio hecho por los flujos de [mover mueble](02_Mover_Rotar_Mueble.md) y [cambio de material](03_Cambio_Material.md) (los materiales no afectan la geometría exportada, pero las posiciones sí). No depende del [guardado de diseño en JSON](05_Guardado_Diseno.md): son dos salidas independientes de la misma personalización en memoria.
