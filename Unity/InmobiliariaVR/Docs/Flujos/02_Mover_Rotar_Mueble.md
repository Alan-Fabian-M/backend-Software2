# Flujo: Mover y rotar un mueble

Ejemplo completo pedido como referencia: desde que el jugador agarra un mueble hasta que queda reposicionado.

## Precondición

La app tiene que estar en `AppState.Personalizacion` (ver [flujo de recorrido y menú](01_Recorrido_y_Menu.md)). Si no lo está, todos los pasos de manipulación se ignoran silenciosamente (los métodos chequean el estado y retornan sin hacer nada).

## Paso a paso

1. **El jugador apunta el control al mueble** (Sofá, Mesa o TV en `Sala_MVP`, cada uno con un `XRSimpleInteractable`) **y presiona el grip (botón "Select")**.
2. XRI dispara el evento `Select Entered` del `XRSimpleInteractable`, conectado en el Inspector a [`FurnitureSelectable.OnSeleccionEntra()`](../../Assets/Scripts/FurnitureSelectable.cs). Esto marca `seleccionActiva = true` y guarda el momento en que empezó (`momentoInicioSeleccion`).
3. En cada `Update()`, mientras el grip siga apretado, `FurnitureSelectable` mide cuánto tiempo pasó desde que empezó la selección.
   - Si el jugador **suelta antes** de `tiempoParaAbrir` (0.35 s por defecto), dispara el evento `Select Exited` → `OnSeleccionSale()` → `seleccionActiva = false`, y no pasa nada más (en el caso del Sofá, un toque corto en cambio cicla su color — ver [flujo de cambio de material](03_Cambio_Material.md), es un listener aparte en `selectEntered`).
   - Si lo **mantiene apretado** 0.35 s o más, `FurnitureSelectable` llama a [`FurnitureManipulatorVR.AbrirMenuPara(transform)`](../../Assets/Scripts/FurnitureManipulatorVR.cs), pasándole el `Transform` del mueble agarrado.
4. `AbrirMenuPara`:
   - Vuelve a chequear que el estado sea `Personalizacion` (por si cambió mientras se sostenía el grip).
   - Guarda el mueble como `muebleActual`.
   - Pone el nombre del mueble en el texto del menú (`nombreMueble`).
   - Posiciona el menú (`menuRoot`) 0.4 m por encima del mueble y lo activa.
5. **El jugador usa los botones de flecha del menú** (Adelante/Atrás/Izquierda/Derecha), cada uno llamando a [`Mover(direccion)`](../../Assets/Scripts/FurnitureManipulatorVR.cs) con un índice 0-3. `Mover` traslada `muebleActual` en el mundo por `pasoMovimiento` metros (0.1 m por click) y reubica el menú arriba del mueble en su nueva posición, para que siga acompañándolo.
6. **El jugador usa los botones de rotación** (izquierda/derecha), llamando a [`Rotar(direccion)`](../../Assets/Scripts/FurnitureManipulatorVR.cs) con -1 o 1, que rota el mueble `pasoRotacion` grados (15° por defecto) sobre el eje Y.
7. **El jugador cierra el menú** llamando a [`CerrarMenu()`](../../Assets/Scripts/FurnitureManipulatorVR.cs), que oculta `menuRoot` y limpia `muebleActual`. El mueble queda en la posición/rotación donde se lo dejó — no hay un paso explícito de "confirmar", el cambio ya está aplicado al `Transform` en cada click.

## Por qué un solo menú compartido

Los muebles de `Sala_MVP` son fijos en la escena (no se instancian en runtime), así que un único `FurnitureManipulatorVR` en la escena alcanza: solo cambia a qué `Transform` (`muebleActual`) apunta según qué mueble se activó. Ver el comentario al inicio de [`FurnitureManipulatorVR.cs`](../../Assets/Scripts/FurnitureManipulatorVR.cs) para la comparación con el proyecto de referencia (Room Designer), que sí instanciaba muebles dinámicamente.

## Relación con el guardado

Este flujo solo modifica el `Transform` del mueble en memoria. Para persistir la posición/rotación final entre sesiones hace falta el paso explícito de guardado — ver [flujo de guardado de diseño](05_Guardado_Diseno.md).
