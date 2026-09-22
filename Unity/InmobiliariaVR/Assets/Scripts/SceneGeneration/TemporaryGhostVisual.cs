using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "Piel" verde temporal para dar mejor referencia visual mientras se posiciona o redimensiona un
/// mueble o un "Muro divisorio" (ajuste pedido por Alan el 2026-09-19, punto 3): mientras el
/// objeto se está sosteniendo/arrastrando (SceneGenerator.StartFootprintTracking), flotando en
/// "Edición flotante" (UpdateModesController), o redimensionando con arrastre de esferitas
/// (ResizeHandlesController), todos sus Renderers quedan cubiertos con un material verde opaco.
///
/// Al terminar (Desactivar), cada Renderer recupera EXACTAMENTE el material que tenía puesto
/// antes de activarse -- el color con el que el objeto fue creado (el de fábrica del catálogo) o
/// el que le hayas puesto vos después (con el botón X de color, o el panel de pintar muros), tal
/// cual pediste.
///
/// Simplificación consciente: en vez de un wireframe real (líneas dibujando solo los bordes, que
/// necesitaría un shader propio con soporte de geometría/bordes, imposible de verificar sin poder
/// abrir el Editor), se usa un tinte verde sólido y opaco sobre todo el objeto -- mismo shader
/// Unlit + un color que ya usan con éxito la huella del piso y el pintado de muros en este mismo
/// proyecto, así que es una técnica probada y segura de que funcione a la primera en el Quest.
/// </summary>
public static class TemporaryGhostVisual
{
    private class Cache
    {
        public Renderer[] renderers;
        public Material[] originales;
    }

    private static readonly Dictionary<GameObject, Cache> activos = new Dictionary<GameObject, Cache>();
    private static Material materialVerdeCompartido;

    private static Material ObtenerMaterialVerde()
    {
        if (materialVerdeCompartido == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            materialVerdeCompartido = new Material(shader) { color = new Color(0.25f, 0.95f, 0.35f) };
        }
        return materialVerdeCompartido;
    }

    /// <summary>
    /// Cubre todos los Renderer del objeto (y sus hijos) con el material verde compartido,
    /// cacheando antes el material real de cada uno para poder devolvérselo después. Si el objeto
    /// ya tenía la piel activa, no hace nada (evita pisar el material original cacheado si por
    /// algún motivo se llama dos veces seguidas sin pasar por Desactivar en el medio).
    /// </summary>
    public static void Activar(GameObject obj)
    {
        if (obj == null || activos.ContainsKey(obj)) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Material verde = ObtenerMaterialVerde();
        Material[] originales = new Material[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            originales[i] = renderers[i].sharedMaterial;
            renderers[i].sharedMaterial = verde;
        }

        activos[obj] = new Cache { renderers = renderers, originales = originales };
    }

    /// <summary>
    /// Devuelve a cada Renderer el material exacto que tenía antes de Activar. Seguro de llamar
    /// aunque el objeto no tuviera la piel puesta (no hace nada en ese caso) -- así se puede
    /// llamar "por las dudas" desde varios lugares (por ejemplo FinalizePlacement) sin tener que
    /// llevar la cuenta de si hacía falta o no.
    /// </summary>
    public static void Desactivar(GameObject obj)
    {
        if (obj == null) return;
        if (!activos.TryGetValue(obj, out Cache cache)) return;

        for (int i = 0; i < cache.renderers.Length; i++)
        {
            if (cache.renderers[i] != null && cache.originales[i] != null)
                cache.renderers[i].sharedMaterial = cache.originales[i];
        }

        activos.Remove(obj);
    }

    /// <summary>
    /// BUG ENCONTRADO Y CORREGIDO (quinta ronda, 2026-09-19 -- reportado por Alan: "el cambio de
    /// color de los muebles no se aplica" / "no quedan con el color que quiero"): la causa real
    /// era ACÁ, no en MaterialChangerVR ni en StateManager (el fix de la ronda anterior sobre
    /// AppState.Personalizacion era defensivo/inofensivo, pero no atacaba el problema real).
    ///
    /// Secuencia del bug: 1) agarrás el mueble -- SceneGenerator.StartFootprintTracking llama
    /// Activar(obj), que cachea el material de ANTES de agarrarlo (el que tenía en ese momento) y
    /// lo pinta de verde. 2) mientras lo sostenés, apretás X -- MaterialChangerVR.ChangeToNextMaterial()
    /// cambia el material del Renderer al color elegido (se ve bien, reemplaza el verde). 3) soltás
    /// el mueble -- StopFootprintTracking llama Desactivar(obj), que restaura el material CACHEADO
    /// en el paso 1 -- el de ANTES de elegir el color nuevo -- pisando por completo el cambio que
    /// acabás de hacer. Resultado: el color se ve bien un instante mientras sostenés el mueble, pero
    /// se revierte solo al soltarlo.
    ///
    /// Fix: cada vez que MaterialChangerVR efectivamente cambia el material de un renderer, avisa
    /// acá con este método -- si ese objeto tiene la piel verde activa en este momento, se
    /// actualiza el material "real" cacheado para ese renderer puntual, así Desactivar() restaura
    /// el color RECIÉN ELEGIDO en vez del viejo. No hace nada si el objeto no tiene la piel activa
    /// (caso normal: cambiar de color sin tenerlo agarrado, si llegara a pasar).
    /// </summary>
    public static void ActualizarMaterialSiEstaActivo(GameObject obj, Renderer renderer, Material nuevoMaterial)
    {
        if (obj == null || renderer == null) return;
        if (!activos.TryGetValue(obj, out Cache cache)) return;

        for (int i = 0; i < cache.renderers.Length; i++)
        {
            if (cache.renderers[i] == renderer)
            {
                cache.originales[i] = nuevoMaterial;
                return;
            }
        }
    }
}
