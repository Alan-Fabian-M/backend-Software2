using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

// Exporta mallas de Unity a un archivo STL binario, para mandar a imprimir una
// maqueta 3D del inmueble (o de la disposicion de muebles elegida por el cliente).
//
// Nuestros objetos (paredes/piso como cubos, muebles importados) ya son solidos
// cerrados (tienen volumen real), a diferencia de un escaneo de un cuarto real
// (paredes sin espesor). Por eso no hace falta el paso de "darle espesor a las
// paredes" que se investigo para el caso general (ver Docs/investigacion_VR_impresion3D.md,
// que menciona g3sharp para eso) — alcanza con exportar las mallas tal cual.
//
// Conversion de ejes: Unity es zurdo (Left-Handed) con Y arriba; STL/impresion 3D
// asume diestro (Right-Handed) con Z arriba. Se remapea (x,y,z) -> (x,z,y) y se
// invierte el orden de los vertices de cada triangulo para compensar el cambio
// de lateralidad (si no, las piezas salen "espejadas" y con las normales para adentro).
public static class StlExporter
{
    // mm de impresion por cada metro de la escena. 20 = escala 1:50 (una sala de
    // 4m de largo imprime ~8cm), razonable para una maqueta de escritorio.
    public static float MilimetrosPorMetro = 20f;

    public static int ExportarAArchivo(IEnumerable<GameObject> raices, string rutaArchivo)
    {
        List<(Vector3 a, Vector3 b, Vector3 c)> triangulos = new List<(Vector3, Vector3, Vector3)>();

        foreach (GameObject raiz in raices)
        {
            if (raiz == null || SceneGenerator.EsElementoEntornoVirtual(raiz)) continue;
            foreach (MeshFilter mf in raiz.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || SceneGenerator.EsElementoEntornoVirtual(mf.gameObject)) continue;
                RecolectarTriangulos(mf, triangulos);
            }
        }

        EscribirStlBinario(rutaArchivo, triangulos);
        return triangulos.Count;
    }

    private static void RecolectarTriangulos(MeshFilter mf, List<(Vector3, Vector3, Vector3)> destino)
    {
        Mesh mesh = mf.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        Transform t = mf.transform;

        for (int sub = 0; sub < mesh.subMeshCount; sub++)
        {
            int[] indices = mesh.GetTriangles(sub);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 p0 = t.TransformPoint(vertices[indices[i]]);
                Vector3 p1 = t.TransformPoint(vertices[indices[i + 1]]);
                Vector3 p2 = t.TransformPoint(vertices[indices[i + 2]]);
                destino.Add((p0, p1, p2));
            }
        }
    }

    private static void EscribirStlBinario(string rutaArchivo, List<(Vector3 a, Vector3 b, Vector3 c)> triangulos)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(rutaArchivo));

        using (FileStream fs = new FileStream(rutaArchivo, FileMode.Create, FileAccess.Write))
        using (BinaryWriter w = new BinaryWriter(fs, Encoding.ASCII))
        {
            byte[] header = new byte[80];
            byte[] texto = Encoding.ASCII.GetBytes("InmobiliariaVR STL export");
            System.Array.Copy(texto, header, Mathf.Min(texto.Length, 80));
            w.Write(header);
            w.Write((uint)triangulos.Count);

            float escala = MilimetrosPorMetro;

            foreach (var tri in triangulos)
            {
                // Metros -> milimetros de impresion, y remapeo de ejes (Y de Unity pasa a ser Z).
                Vector3 q0 = new Vector3(tri.a.x, tri.a.z, tri.a.y) * escala;
                Vector3 q1 = new Vector3(tri.b.x, tri.b.z, tri.b.y) * escala;
                Vector3 q2 = new Vector3(tri.c.x, tri.c.z, tri.c.y) * escala;

                // Se invierte el orden (q0, q2, q1) para compensar el cambio de lateralidad
                // del remapeo de ejes y mantener las normales apuntando hacia afuera.
                Vector3 normal = Vector3.Cross(q2 - q0, q1 - q0).normalized;

                w.Write(normal.x); w.Write(normal.y); w.Write(normal.z);
                w.Write(q0.x); w.Write(q0.y); w.Write(q0.z);
                w.Write(q2.x); w.Write(q2.y); w.Write(q2.z);
                w.Write(q1.x); w.Write(q1.y); w.Write(q1.z);
                w.Write((ushort)0);
            }
        }
    }
}
