# Flujo: Guardar y cargar un diseño personalizado

Cómo se persiste (y recupera) la disposición de muebles y los acabados elegidos por el cliente, como JSON local en disco.

## Modelo de datos

[`DesignLayoutData`](../../Assets/Scripts/SaveSystem/DesignLayoutData.cs) es la estructura serializable que representa un diseño completo:

- `furniture: List<FurnitureData>` — por cada mueble: `modelId` (identificador del modelo), `position`, `rotation` y `scale`.
- `surfaces: List<SurfaceData>` — por cada superficie: `surfaceTag` (`"Wall"`, `"Floor"`, `"Ceiling"`, etc.) y `materialIndex` (el mismo índice que usa `MaterialChangerVR.materials`).

Esta estructura es intencionalmente simple: no guarda referencias a `GameObject` ni componentes de Unity, solo los datos necesarios para reconstruir el estado.

## Guardar

1. Algo del proyecto (por ejemplo, un botón "Guardar diseño" del menú — el llamado real no está en un script propio, se armaría a mano con los datos actuales de la escena) construye un `DesignLayoutData`: recorre los muebles de la escena para llenar `furniture`, y las superficies para llenar `surfaces`.
2. Se llama a [`SaveDataSerializer.SaveLayout(layout, filename)`](../../Assets/Scripts/SaveSystem/SaveDataSerializer.cs), que:
   - Convierte el objeto a JSON con `JsonUtility.ToJson(layout, true)`.
   - Lo escribe de forma asíncrona (`File.WriteAllTextAsync`) en `Application.persistentDataPath/DesignLayouts/<filename>.json`.
   - Si falla (permiso, disco lleno, etc.), loguea el error y relanza la excepción para que quien llamó pueda mostrar un mensaje al usuario.

`SaveDirectory` se crea (si no existe) en el constructor estático de la clase, así que la carpeta `DesignLayouts` siempre está lista antes del primer guardado.

## Cargar

1. Se llama a [`SaveDataSerializer.LoadLayout(filename)`](../../Assets/Scripts/SaveSystem/SaveDataSerializer.cs).
2. Si el archivo `<filename>.json` no existe en `DesignLayouts`, devuelve `null` (con un warning en consola) en vez de fallar.
3. Si existe, lee el texto de forma asíncrona y lo deserializa con `JsonUtility.FromJson<DesignLayoutData>`.
4. Quien llama a `LoadLayout` es responsable de tomar ese `DesignLayoutData` y aplicarlo de vuelta a la escena: reposicionar cada mueble según `FurnitureData` y volver a asignar los materiales de cada superficie según `SurfaceData.materialIndex` (típicamente vía `MaterialChangerVR.SetMaterialByIndex`).

## Listar diseños guardados

[`SaveDataSerializer.GetSavedLayouts()`](../../Assets/Scripts/SaveSystem/SaveDataSerializer.cs) devuelve los nombres (sin extensión) de todos los `.json` en `DesignLayouts`, útil para armar una lista de "diseños guardados" en un menú de carga.

## Relación con otros flujos

- Los cambios de posición/rotación del [flujo de mover mueble](02_Mover_Rotar_Mueble.md) y de material del [flujo de cambio de material](03_Cambio_Material.md) modifican la escena en memoria, pero **no se guardan solos**: hace falta este flujo para persistirlos.
- El archivo `.stl` del [flujo de exportación](06_Exportacion_STL.md) es una salida distinta (geometría para imprimir), no reemplaza a este guardado de datos de diseño.
