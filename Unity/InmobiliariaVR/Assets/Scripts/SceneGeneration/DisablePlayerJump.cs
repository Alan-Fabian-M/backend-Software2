using UnityEngine;

/// <summary>
/// CAUSA ENCONTRADA de "el botón A hace saltar al personaje Y abre el modal": el XR Origin (XR
/// Rig) que trae de fábrica el XR Interaction Toolkit (Starter Assets, el rig base sobre el que
/// se armó "XR Origin Hands (XR Rig)" de este proyecto) incluye un sistema de locomoción con
/// salto ("Jump", componente JumpProvider), y en el asset compartido "XRI Default Input
/// Actions.inputactions" esa acción "Jump" está atada, de fábrica, exactamente al mismo botón
/// físico que usamos para abrir el modal de presets: <XRController>{RightHand}/{PrimaryButton}
/// (el botón A del control derecho).
///
/// O sea: NO es un bug de este proyecto ni de PresetLoaderController -- son dos sistemas
/// completamente independientes escuchando el mismo botón físico a la vez. Uno (el de Unity)
/// hace saltar al jugador con física real (CharacterController + gravedad, salto de hasta 1.25m
/// de alto), y el otro (el nuestro) abre/cierra el modal. Los dos se disparan juntos cada vez
/// que se aprieta A.
///
/// Como esta app es un visor de ambientes/muebles (no un juego de plataformas), no hace falta
/// que el usuario salte -- así que se desactiva directamente ese proveedor de salto.
///
/// Se busca el componente por el NOMBRE de su clase ("JumpProvider") en vez de referenciar el
/// tipo directamente en código, porque el namespace exacto cambió entre versiones del XR
/// Interaction Toolkit (está reorganizado en subnamespaces de Locomotion en la versión 3.x) y
/// así el fix funciona igual sin depender de ese detalle interno ni de agregar una referencia de
/// ensamblado nueva.
///
/// [RuntimeInitializeOnLoadMethod] hace que esto corra solo, apenas carga cada escena, SIN
/// necesitar agregar este script a mano a ningún GameObject desde el Editor de Unity (algo que
/// no se puede hacer desde esta sesión remota, sin acceso al Editor). Se aplica en todas las
/// escenas de la app (Sala_MVP, Departamento_B, Dormitorio, etc.), porque todas comparten el
/// mismo problema si usan el mismo XR Origin.
/// </summary>
public static class DisablePlayerJump
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void DisableJumpProvider()
    {
        MonoBehaviour[] todosLosComponentes = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);

        int desactivados = 0;

        foreach (MonoBehaviour componente in todosLosComponentes)
        {
            if (componente == null) continue;

            if (componente.GetType().Name == "JumpProvider")
            {
                componente.enabled = false;
                desactivados++;
            }
        }

        if (desactivados > 0)
        {
            Debug.Log($"[DisablePlayerJump] Se desactivó el salto del jugador " +
                $"({desactivados} JumpProvider encontrado/s) -- el botón A queda solo para " +
                $"abrir el modal de presets, sin hacer saltar al personaje.");
        }
    }
}
