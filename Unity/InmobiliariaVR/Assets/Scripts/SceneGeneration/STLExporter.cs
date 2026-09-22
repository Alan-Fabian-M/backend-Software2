using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Exportador de mallas de Unity a formato STL (binario o ASCII) para impresión 3D.
///
/// Esta clase FALTABA en el proyecto: tanto STLTransferClient.cs (envío WiFi a la PC) como el
/// antiguo flujo de exportación la llamaban, pero nunca existía en disco -- por eso el proyecto
/// no podía compilar apenas algo la referenciara (CS0103). Este archivo la implementa.
///
/// Detalles clave de la conversión (por qué el STL sale bien orientado y a la escala correcta):
///
/// 1. ESCALA 1:4 -- 1 metro en VR = 250 mm impresos. Unity trabaja en metros; los slicers
///    (Bambu Studio) interpretan las coordenadas del STL como MILÍMETROS. Por eso cada
///    coordenada se multiplica por SCALE_UNITY_TO_MM (250): un mueble de 1 m en la escena sale
///    como 250 mm de STL, que es exactamente la maqueta a escala 1:4.
///
/// 2. SISTEMA DE COORDENADAS -- Unity es Y-arriba y "left-handed"; el mundo de impresión/STL es
///    Z-arriba y "right-handed". Se intercambian los ejes Y y Z (Unity.Y -> STL.Z, Unity.Z ->
///    STL.Y) para que el objeto salga "parado" en la cama de impresión y no acostado. Ese
///    intercambio invierte el "handedness", así que además se invierte el orden de los vértices
///    de cada triángulo (winding) para que las normales sigan apuntando hacia AFUERA -- si no,
///    el slicer vería el modelo "del revés" y generaría soportes y relleno mal.
///
/// 3. CULTURA INVARIANTE -- el STL ASCII escribe los números con PUNTO decimal siempre
///    (CultureInfo.InvariantCulture). Sin esto, en una PC con locale español (coma decimal) los
///    vértices saldrían como "12,5" en vez de "12.5" y Bambu Studio no podría leer el archivo.
///
/// 4. MALLAS LEGIBLES -- mesh.vertices sólo funciona si la malla tiene Read/Write habilitado en
///    su importador. Las mallas que no lo tengan se omiten con un warning (no rompen la
///    exportación del resto).
/// </summary>
public static class STLExporter
{
    /// <summary>1 metro Unity (VR) = 250 mm físicos impresos. Escala 1:4.</summary>
    public const float SCALE_UNITY_TO_MM = 250f;

    // Un triángulo ya convertido a coordenadas STL (mm, Z-arriba) y con su normal calculada.
    private struct Triangulo
    {
        public Vector3 a, b, c, normal;
    }

    // -----------------------------------------------------------------
    // API pública -- dos sobrecargas: a archivo (ruta) y a Stream (memoria).
    // -----------------------------------------------------------------

    /// <summary>
    /// Exporta el GameObject (y todos sus hijos con MeshFilter/SkinnedMeshRenderer) a un archivo STL.
    /// </summary>
    /// <returns>true si se escribió al menos un triángulo correctamente.</returns>
    public static bool ExportToSTL(GameObject root, string filePath, bool binary = true)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[STLExporter] Ruta de archivo vacía.");
            return false;
        }

        try
        {
            // Asegurar que la carpeta destino existe.
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                return ExportToSTL(root, fs, binary);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[STLExporter] Error escribiendo archivo '{filePath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Exporta el GameObject a un Stream (por ejemplo un MemoryStream para enviarlo por red).
    /// NO cierra el stream -- el llamador es dueño de él (así STLTransferClient puede leer el
    /// MemoryStream después de exportar).
    /// </summary>
    /// <returns>true si se escribió al menos un triángulo correctamente.</returns>
    public static bool ExportToSTL(GameObject root, Stream stream, bool binary = true)
    {
        if (root == null)
        {
            Debug.LogError("[STLExporter] GameObject raíz nulo.");
            return false;
        }
        if (stream == null)
        {
            Debug.LogError("[STLExporter] Stream nulo.");
            return false;
        }

        List<Triangulo> triangulos = RecolectarTriangulos(root);
        if (triangulos.Count == 0)
        {
            Debug.LogError($"[STLExporter] '{root.name}' no produjo triángulos. " +
                           "¿El objeto tiene MeshFilter con malla legible (Read/Write ON)?");
            return false;
        }

        try
        {
            if (binary)
                EscribirBinario(stream, triangulos);
            else
                EscribirASCII(stream, triangulos, root.name);

            Debug.Log($"[STLExporter] '{root.name}' exportado: {triangulos.Count} triángulos " +
                      $"({(binary ? "binario" : "ASCII")}).");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[STLExporter] Error serializando STL: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Exporta una colección de GameObjects (ej: piso + muros + muebles dentro de su perímetro)
    /// a un archivo STL único. Centra la maqueta en (0,0) horizontalmente, apoya la base en Z=0,
    /// y ajusta la escala automáticamente para que quepa en la cama de impresión (máx 180-200 mm).
    /// Descarta automáticamente cualquier geometría del entorno VR o fuera del perímetro del piso.
    /// </summary>
    public static bool ExportRoomToSTL(IEnumerable<GameObject> objects, string filePath, bool binary = true, float maxDimensionMm = 190f, string solidName = "Maqueta_Sala", Bounds? boundingBoxLimit = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            Debug.LogError("[STLExporter] Ruta de archivo vacía.");
            return false;
        }

        try
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                return ExportRoomToSTL(objects, fs, binary, maxDimensionMm, solidName, boundingBoxLimit);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[STLExporter] Error escribiendo archivo de maqueta '{filePath}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sobrecarga para exportar la maqueta a un Stream (MemoryStream para envío por WiFi a la PC).
    /// </summary>
    public static bool ExportRoomToSTL(IEnumerable<GameObject> objects, Stream stream, bool binary = true, float maxDimensionMm = 190f, string solidName = "Maqueta_Sala", Bounds? boundingBoxLimit = null)
    {
        if (objects == null)
        {
            Debug.LogError("[STLExporter] Lista de objetos de la sala nula.");
            return false;
        }
        if (stream == null)
        {
            Debug.LogError("[STLExporter] Stream nulo.");
            return false;
        }

        List<Triangulo> triangulos = RecolectarTriangulosMaqueta(objects, maxDimensionMm, boundingBoxLimit);
        if (triangulos.Count == 0)
        {
            Debug.LogError("[STLExporter] La maqueta no produjo triángulos.");
            return false;
        }

        try
        {
            if (binary)
                EscribirBinario(stream, triangulos);
            else
                EscribirASCII(stream, triangulos, solidName);

            Debug.Log($"[STLExporter] Maqueta '{solidName}' exportada: {triangulos.Count} triángulos " +
                      $"({(binary ? "binario" : "ASCII")}).");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[STLExporter] Error serializando STL de maqueta: {ex.Message}");
            return false;
        }
    }

    private struct RawTri
    {
        public Vector3 a, b, c;
    }

    private static List<Triangulo> RecolectarTriangulosMaqueta(IEnumerable<GameObject> objects, float maxDimensionMm, Bounds? boundingBoxLimit = null)
    {
        var rawTris = new List<RawTri>();
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;

        HashSet<MeshFilter> processedMeshFilters = new HashSet<MeshFilter>();
        HashSet<SkinnedMeshRenderer> processedSMRs = new HashSet<SkinnedMeshRenderer>();
        int totalObjetos = 0;

        // Determinar límites de recorte espacial del departamento (para excluir cualquier geometría del entorno VR)
        Bounds limitBounds = default;
        bool hasLimit = false;

        if (boundingBoxLimit.HasValue && boundingBoxLimit.Value.size.sqrMagnitude > 0.001f)
        {
            limitBounds = boundingBoxLimit.Value;
            hasLimit = true;
        }
        else if (objects != null)
        {
            foreach (var obj in objects)
            {
                if (obj == null || SceneGenerator.EsElementoEntornoVirtual(obj)) continue;
                string n = obj.name.ToLower();
                if (obj.CompareTag("Floor") || n.Contains("floor") || n.Contains("piso"))
                {
                    Bounds b = SceneGenerator.CalcularBoundsPisoCompleto(obj);
                    if (!hasLimit)
                    {
                        limitBounds = b;
                        hasLimit = true;
                    }
                    else
                    {
                        limitBounds.Encapsulate(b);
                    }
                }
            }
        }

        float clipMinX = float.MinValue, clipMaxX = float.MaxValue;
        float clipMinZ = float.MinValue, clipMaxZ = float.MaxValue;
        float clipMinY = float.MinValue, clipMaxY = float.MaxValue;

        if (hasLimit)
        {
            // Margen de 0.35m horizontal para muros exteriores y molduras
            clipMinX = limitBounds.min.x - 0.35f;
            clipMaxX = limitBounds.max.x + 0.35f;
            clipMinZ = limitBounds.min.z - 0.35f;
            clipMaxZ = limitBounds.max.z + 0.35f;
            // Base del piso hasta altura de 4.5m
            clipMinY = limitBounds.min.y - 0.20f;
            clipMaxY = limitBounds.max.y + 4.50f;
        }

        foreach (var obj in objects)
        {
            if (obj == null || !obj.activeInHierarchy) continue;
            // Descartar inmediatamente objetos pertenecientes al entorno virtual VR
            if (SceneGenerator.EsElementoEntornoVirtual(obj)) continue;
            totalObjetos++;

            // Recorrer todos los MeshFilter hijos
            foreach (var mf in obj.GetComponentsInChildren<MeshFilter>(false))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (SceneGenerator.EsElementoEntornoVirtual(mf.gameObject)) continue;
                if (!processedMeshFilters.Add(mf)) continue;

                Mesh mesh = mf.sharedMesh;
                Transform t = mf.transform;
                Vector3[] verts;
                int[] tris;
                try
                {
                    verts = mesh.vertices;
                    tris = mesh.triangles;
                }
                catch
                {
                    continue;
                }
                if (verts == null || tris == null || verts.Length == 0 || tris.Length == 0) continue;

                Vector3[] worldVerts = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                {
                    worldVerts[i] = t.TransformPoint(verts[i]);
                }

                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 vA = worldVerts[tris[i]];
                    Vector3 vB = worldVerts[tris[i + 1]];
                    Vector3 vC = worldVerts[tris[i + 2]];

                    if (hasLimit)
                    {
                        Vector3 triCenter = (vA + vB + vC) / 3f;
                        if (triCenter.x < clipMinX || triCenter.x > clipMaxX ||
                            triCenter.z < clipMinZ || triCenter.z > clipMaxZ ||
                            triCenter.y < clipMinY || triCenter.y > clipMaxY)
                        {
                            continue;
                        }

                        if (vA.x < clipMinX - 1.2f || vA.x > clipMaxX + 1.2f || vA.z < clipMinZ - 1.2f || vA.z > clipMaxZ + 1.2f ||
                            vB.x < clipMinX - 1.2f || vB.x > clipMaxX + 1.2f || vB.z < clipMinZ - 1.2f || vB.z > clipMaxZ + 1.2f ||
                            vC.x < clipMinX - 1.2f || vC.x > clipMaxX + 1.2f || vC.z < clipMinZ - 1.2f || vC.z > clipMaxZ + 1.2f)
                        {
                            continue;
                        }
                    }

                    rawTris.Add(new RawTri { a = vA, b = vB, c = vC });

                    if (vA.x < minX) minX = vA.x; if (vA.x > maxX) maxX = vA.x;
                    if (vA.y < minY) minY = vA.y; if (vA.y > maxY) maxY = vA.y;
                    if (vA.z < minZ) minZ = vA.z; if (vA.z > maxZ) maxZ = vA.z;

                    if (vB.x < minX) minX = vB.x; if (vB.x > maxX) maxX = vB.x;
                    if (vB.y < minY) minY = vB.y; if (vB.y > maxY) maxY = vB.y;
                    if (vB.z < minZ) minZ = vB.z; if (vB.z > maxZ) maxZ = vB.z;

                    if (vC.x < minX) minX = vC.x; if (vC.x > maxX) maxX = vC.x;
                    if (vC.y < minY) minY = vC.y; if (vC.y > maxY) maxY = vC.y;
                    if (vC.z < minZ) minZ = vC.z; if (vC.z > maxZ) maxZ = vC.z;
                }
            }

            // Mallas skinned si las hubiera
            foreach (var smr in obj.GetComponentsInChildren<SkinnedMeshRenderer>(false))
            {
                if (smr == null || smr.sharedMesh == null) continue;
                if (SceneGenerator.EsElementoEntornoVirtual(smr.gameObject)) continue;
                if (!processedSMRs.Add(smr)) continue;

                Mesh baked = new Mesh();
                try
                {
                    smr.BakeMesh(baked);
                    Vector3[] verts = baked.vertices;
                    int[] tris = baked.triangles;
                    if (verts != null && tris != null && verts.Length > 0 && tris.Length > 0)
                    {
                        Transform t = smr.transform;
                        Vector3[] worldVerts = new Vector3[verts.Length];
                        for (int i = 0; i < verts.Length; i++)
                        {
                            worldVerts[i] = t.TransformPoint(verts[i]);
                        }

                        for (int i = 0; i + 2 < tris.Length; i += 3)
                        {
                            Vector3 vA = worldVerts[tris[i]];
                            Vector3 vB = worldVerts[tris[i + 1]];
                            Vector3 vC = worldVerts[tris[i + 2]];

                            if (hasLimit)
                            {
                                Vector3 triCenter = (vA + vB + vC) / 3f;
                                if (triCenter.x < clipMinX || triCenter.x > clipMaxX ||
                                    triCenter.z < clipMinZ || triCenter.z > clipMaxZ ||
                                    triCenter.y < clipMinY || triCenter.y > clipMaxY)
                                {
                                    continue;
                                }

                                if (vA.x < clipMinX - 1.2f || vA.x > clipMaxX + 1.2f || vA.z < clipMinZ - 1.2f || vA.z > clipMaxZ + 1.2f ||
                                    vB.x < clipMinX - 1.2f || vB.x > clipMaxX + 1.2f || vB.z < clipMinZ - 1.2f || vB.z > clipMaxZ + 1.2f ||
                                    vC.x < clipMinX - 1.2f || vC.x > clipMaxX + 1.2f || vC.z < clipMinZ - 1.2f || vC.z > clipMaxZ + 1.2f)
                                {
                                    continue;
                                }
                            }

                            rawTris.Add(new RawTri { a = vA, b = vB, c = vC });

                            if (vA.x < minX) minX = vA.x; if (vA.x > maxX) maxX = vA.x;
                            if (vA.y < minY) minY = vA.y; if (vA.y > maxY) maxY = vA.y;
                            if (vA.z < minZ) minZ = vA.z; if (vA.z > maxZ) maxZ = vA.z;

                            if (vB.x < minX) minX = vB.x; if (vB.x > maxX) maxX = vB.x;
                            if (vB.y < minY) minY = vB.y; if (vB.y > maxY) maxY = vB.y;
                            if (vB.z < minZ) minZ = vB.z; if (vB.z > maxZ) maxZ = vB.z;

                            if (vC.x < minX) minX = vC.x; if (vC.x > maxX) maxX = vC.x;
                            if (vC.y < minY) minY = vC.y; if (vC.y > maxY) maxY = vC.y;
                            if (vC.z < minZ) minZ = vC.z; if (vC.z > maxZ) maxZ = vC.z;
                        }
                    }
                }
                catch { }
                finally
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(baked);
                    else UnityEngine.Object.DestroyImmediate(baked);
                }
            }
        }

        if (rawTris.Count == 0) return new List<Triangulo>();

        // Dimensiones reales de la sala en metros
        float sizeX = Mathf.Max(0.01f, maxX - minX);
        float sizeZ = Mathf.Max(0.01f, maxZ - minZ);
        float maxHorizontal = Mathf.Max(sizeX, sizeZ);

        // Factor de escala: que la dimensión mayor de la maqueta sea maxDimensionMm (ej: 190 mm)
        float scale = (maxDimensionMm > 0 && maxHorizontal > 0.01f)
            ? (maxDimensionMm / maxHorizontal)
            : SCALE_UNITY_TO_MM;

        // Centrado horizontal en el origen (0, 0)
        float centerX = (minX + maxX) * 0.5f;
        float centerZ = (minZ + maxZ) * 0.5f;

        // Nivel base: el punto más bajo en Y (base del piso) queda en Z = 0 en el STL
        float groundY = minY;

        var resultado = new List<Triangulo>(rawTris.Count);

        for (int i = 0; i < rawTris.Count; i++)
        {
            RawTri r = rawTris[i];

            // Conversión a STL (Z-arriba):
            // Unity X -> STL X (centrado)
            // Unity Z -> STL Y (centrado)
            // Unity Y -> STL Z (apoyado en la cama de impresión desde 0 hacia arriba)
            Vector3 A = new Vector3((r.a.x - centerX) * scale, (r.a.z - centerZ) * scale, (r.a.y - groundY) * scale);
            Vector3 B = new Vector3((r.b.x - centerX) * scale, (r.b.z - centerZ) * scale, (r.b.y - groundY) * scale);
            Vector3 C = new Vector3((r.c.x - centerX) * scale, (r.c.z - centerZ) * scale, (r.c.y - groundY) * scale);

            // Invertir winding (A, C, B) por el cambio de handedness
            Vector3 normal = Vector3.Cross(C - A, B - A).normalized;

            resultado.Add(new Triangulo { a = A, b = C, c = B, normal = normal });
        }

        Debug.Log($"[STLExporter] Maqueta generada: {resultado.Count} triángulos de {totalObjetos} objetos. " +
                  $"Dimensiones reales: {sizeX:F2}x{sizeZ:F2}m -> Escala: {scale:F1} mm/m -> Maqueta final: {sizeX * scale:F0}x{sizeZ * scale:F0} mm.");

        return resultado;
    }

    // -----------------------------------------------------------------
    // Recolección y conversión de la geometría
    // -----------------------------------------------------------------

    private static List<Triangulo> RecolectarTriangulos(GameObject root)
    {
        var resultado = new List<Triangulo>();

        // 1) Mallas estáticas normales (la gran mayoría de muebles/paredes).
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(false))
        {
            if (mf.sharedMesh == null) continue;
            AgregarMalla(resultado, mf.sharedMesh, mf.transform);
        }

        // 2) Mallas con skinning (algunos packs de muebles las usan). Se "hornea" la malla en su
        //    pose actual y se trata igual que una malla estática.
        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(false))
        {
            if (smr.sharedMesh == null) continue;
            Mesh horneada = new Mesh();
            try
            {
                smr.BakeMesh(horneada);              // aplica skinning/blendshapes en espacio local
                AgregarMalla(resultado, horneada, smr.transform);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[STLExporter] No se pudo hornear SkinnedMesh '{smr.name}': {ex.Message}");
            }
            finally
            {
                // Limpiar la malla temporal para no filtrar memoria.
                if (Application.isPlaying) UnityEngine.Object.Destroy(horneada);
                else UnityEngine.Object.DestroyImmediate(horneada);
            }
        }

        return resultado;
    }

    private static void AgregarMalla(List<Triangulo> destino, Mesh mesh, Transform t)
    {
        Vector3[] verts;
        int[] tris;
        try
        {
            verts = mesh.vertices;   // requiere Read/Write habilitado en el importador de la malla
            tris = mesh.triangles;
        }
        catch (Exception)
        {
            Debug.LogWarning($"[STLExporter] Malla '{mesh.name}' no es legible (Read/Write OFF). Se omite.");
            return;
        }

        if (verts == null || tris == null || verts.Length == 0 || tris.Length == 0)
            return;

        // Pre-transformar todos los vértices: local -> mundo -> coordenadas STL (mm, Z-arriba).
        var stlVerts = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 mundo = t.TransformPoint(verts[i]);   // aplica posición, rotación y escala
            stlVerts[i] = UnityAMm(mundo);
        }

        // Cada triángulo: se invierte el winding (A, C, B) por el cambio de handedness, y la
        // normal se calcula desde el orden final para que quede consistente y apunte hacia afuera.
        for (int i = 0; i + 2 < tris.Length; i += 3)
        {
            Vector3 A = stlVerts[tris[i]];
            Vector3 B = stlVerts[tris[i + 1]];
            Vector3 C = stlVerts[tris[i + 2]];

            Vector3 normal = Vector3.Cross(C - A, B - A).normalized;

            destino.Add(new Triangulo { a = A, b = C, c = B, normal = normal });
        }
    }

    /// <summary>
    /// Convierte un punto de Unity (metros, Y-arriba, left-handed) a coordenadas STL
    /// (milímetros, Z-arriba). Intercambia Y y Z, y aplica la escala 1:4.
    /// </summary>
    private static Vector3 UnityAMm(Vector3 v)
    {
        return new Vector3(
            v.x * SCALE_UNITY_TO_MM,
            v.z * SCALE_UNITY_TO_MM,   // Unity Z -> STL Y
            v.y * SCALE_UNITY_TO_MM    // Unity Y -> STL Z
        );
    }

    // -----------------------------------------------------------------
    // Escritura del formato STL
    // -----------------------------------------------------------------

    private static void EscribirBinario(Stream stream, List<Triangulo> tris)
    {
        // Formato STL binario:
        //   80 bytes  -> header libre
        //   4 bytes   -> uint32 número de triángulos
        //   por cada triángulo (50 bytes): 12 floats (normal + 3 vértices) + uint16 atributo
        // BinaryWriter en .NET siempre escribe little-endian, que es lo que pide el STL.
        using (var bw = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true))
        {
            byte[] header = new byte[80];
            byte[] titulo = Encoding.ASCII.GetBytes("InmobiliariaVR - STL binario");
            Array.Copy(titulo, header, Math.Min(titulo.Length, header.Length));
            bw.Write(header);

            bw.Write((uint)tris.Count);

            foreach (var t in tris)
            {
                EscribirVector(bw, t.normal);
                EscribirVector(bw, t.a);
                EscribirVector(bw, t.b);
                EscribirVector(bw, t.c);
                bw.Write((ushort)0);   // "attribute byte count", normalmente 0
            }

            bw.Flush();
        }
    }

    private static void EscribirVector(BinaryWriter bw, Vector3 v)
    {
        bw.Write(v.x);
        bw.Write(v.y);
        bw.Write(v.z);
    }

    private static void EscribirASCII(Stream stream, List<Triangulo> tris, string nombre)
    {
        CultureInfo ci = CultureInfo.InvariantCulture;   // punto decimal SIEMPRE
        string solid = SanitizarNombre(nombre);

        var sb = new StringBuilder();
        sb.Append("solid ").Append(solid).Append('\n');

        foreach (var t in tris)
        {
            sb.Append("  facet normal ")
              .Append(t.normal.x.ToString(ci)).Append(' ')
              .Append(t.normal.y.ToString(ci)).Append(' ')
              .Append(t.normal.z.ToString(ci)).Append('\n');
            sb.Append("    outer loop\n");
            sb.Append(VerticeASCII(t.a, ci));
            sb.Append(VerticeASCII(t.b, ci));
            sb.Append(VerticeASCII(t.c, ci));
            sb.Append("    endloop\n");
            sb.Append("  endfacet\n");
        }

        sb.Append("endsolid ").Append(solid).Append('\n');

        byte[] bytes = Encoding.ASCII.GetBytes(sb.ToString());
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static string VerticeASCII(Vector3 v, CultureInfo ci)
    {
        return "      vertex " +
               v.x.ToString(ci) + " " +
               v.y.ToString(ci) + " " +
               v.z.ToString(ci) + "\n";
    }

    private static string SanitizarNombre(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return "objeto";
        return nombre.Replace(" ", "_").Replace("\n", "").Replace("\r", "");
    }
}
