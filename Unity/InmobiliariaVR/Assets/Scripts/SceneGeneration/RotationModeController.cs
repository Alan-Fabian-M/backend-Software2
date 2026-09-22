using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Modo de rotación al soltar un mueble o un "Muro divisorio" (Sección C del plan de edición
/// avanzada, 2026-09-18) -- inspirado en la rueda de edición de "7 Days to Die" que mandaste de
/// referencia:
///
///   • Simple (default): al soltarlo, se endereza (se fuerzan los ejes X/Z a 0) y el giro en Y se
///     redondea al paso de la grilla (15° para muebles, 90° para muros divisorios) -- fácil de
///     alinear, pero sin control fino.
///   • Avanzada: rotación libre en los 3 ejes, tal cual quedó al soltarlo, sin redondear ni
///     enderezar nada -- para poder inclinar/girar un mueble a mano con precisión total.
///
/// AJUSTE 2026-09-19 (pedido de Alan, punto 3: "rotación simple o avanzada por cada objeto y no
/// individual"): esto era un modo GLOBAL -- un solo botón flotante alternaba Simple/Avanzada para
/// TODA la sala a la vez. Ahora es POR OBJETO: cada mueble o muro recuerda su propio modo (guardado
/// acá en un diccionario por GameObject, sin necesidad de agregarle un campo a cada componente
/// existente), y se cambia desde el botón "🔄 Rotar" del menú de ESE objeto puntual
/// (CrudPanelController), no desde un botón flotante -- que además se sacó (ajuste punto 1:
/// "eliminar los botones que aparecen frente al usuario"), porque ya no representaba un solo
/// estado global que tuviera sentido mostrar todo el tiempo.
/// </summary>
public enum ModoRotacion
{
    Simple,
    Avanzada
}

public class RotationModeController : MonoBehaviour
{
    private static RotationModeController instance;

    private static readonly Dictionary<GameObject, ModoRotacion> modoPorObjeto = new Dictionary<GameObject, ModoRotacion>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureExists();
    }

    public static void EnsureExists()
    {
        if (instance != null) return;
        GameObject go = new GameObject("RotationModeManager");
        instance = go.AddComponent<RotationModeController>();
    }

    private void Awake()
    {
        instance = this;
    }

    /// <summary>Modo de rotación de ESTE objeto puntual (Simple por default si nunca se tocó).</summary>
    public static ModoRotacion ObtenerModo(GameObject obj)
    {
        if (obj != null && modoPorObjeto.TryGetValue(obj, out ModoRotacion modo))
            return modo;
        return ModoRotacion.Simple;
    }

    /// <summary>Alterna Simple/Avanzada para ESTE objeto puntual -- llamado desde el botón "🔄
    /// Rotar" del menú de CrudPanelController, que ya sabe sobre qué objeto se abrió.</summary>
    public static void ToggleModoPara(GameObject obj)
    {
        if (obj == null) return;

        ModoRotacion actual = ObtenerModo(obj);
        ModoRotacion nuevo = (actual == ModoRotacion.Simple) ? ModoRotacion.Avanzada : ModoRotacion.Simple;
        modoPorObjeto[obj] = nuevo;

        string mensaje = (nuevo == ModoRotacion.Simple)
            ? "Rotación de este objeto: Simple (se endereza y alinea a la grilla)"
            : "Rotación de este objeto: Avanzada (libre en los 3 ejes, sin alinear)";
        ToastNotificationUI.Show(mensaje, 2.5f);
    }
}
