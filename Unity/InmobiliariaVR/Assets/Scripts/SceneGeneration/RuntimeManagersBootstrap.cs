using UnityEngine;

/// <summary>
/// CAUSA ENCONTRADA de "el botón Y (y tampoco B, ni el catálogo apuntando con el rayo) no hacen
/// nada": no era un problema de qué botón físico usa cada mano, ni de los perfiles de OpenXR --
/// el componente FurnitureCatalogController (y también SceneSaveLoadController) NUNCA se llegó a
/// agregar a la escena real.
///
/// Esos dos componentes solo se crean si alguien corre, DESDE DENTRO DEL EDITOR DE UNITY, el
/// menú "InmobiliariaVR > Setup Scene Generation" (ver SETUP_SceneGeneration.cs) y después
/// guarda la escena con Ctrl+S -- ese paso se agregó recién cuando se creó el catálogo, varias
/// rondas de esta sesión atrás. Como no hubo forma de abrir el Editor de Unity en ningún momento
/// para correrlo, el GameObject "FurnitureCatalogManager" nunca terminó guardado en
/// Sala_MVP.unity, así que el componente simplemente no existía en el build que probaste -- ni
/// el botón Y, ni el B, ni el botón flotante "🛋 Catálogo" con el rayo podían hacer nada, porque
/// no había nadie escuchando esos eventos.
///
/// (El botón X sí funcionaba desde el principio porque FurnitureColorButtonController se crea
/// solo en tiempo de ejecución, vía su propio EnsureExists(), sin depender de ningún paso manual
/// del Editor.)
///
/// Este script hace lo mismo que "Setup Scene Generation", pero automáticamente, apenas arranca
/// cualquier escena de la app (con [RuntimeInitializeOnLoadMethod]) -- así el catálogo y el
/// guardado de escenas van a estar siempre disponibles en cualquier build futuro, sin depender
/// de acordarse de correr nada a mano desde el Editor. Cada controlador ya se protege solo
/// contra duplicados (con su propio FindAnyObjectByType antes de crearse), así que esto es
/// seguro de dejar puesto aunque en algún momento SÍ se corra el Setup manual también.
/// </summary>
public static class RuntimeManagersBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureManagersExist()
    {
        if (Object.FindAnyObjectByType<SceneGenerator>() == null)
            new GameObject("SceneGeneratorManager").AddComponent<SceneGenerator>();

        if (Object.FindAnyObjectByType<PresetLoaderController>() == null)
            new GameObject("PresetLoaderUI").AddComponent<PresetLoaderController>();

        if (Object.FindAnyObjectByType<ToastNotificationUI>() == null)
            new GameObject("ToastNotificationManager").AddComponent<ToastNotificationUI>();

        if (Object.FindAnyObjectByType<FurnitureCatalogController>() == null)
            new GameObject("FurnitureCatalogManager").AddComponent<FurnitureCatalogController>();

        if (Object.FindAnyObjectByType<SceneSaveLoadController>() == null)
            new GameObject("SceneSaveLoadManager").AddComponent<SceneSaveLoadController>();

        // El gatito ayudante, PerspectiveController y TrashCanController (puntos 4 y 5 del plan
        // original) se eliminaron a pedido de Alan (2026-09-19, ajuste punto 1) -- sus .cs
        // quedaron vacíos (ver el comentario en cada uno) y ya no se registran acá.

        // Sección C del plan de edición avanzada (2026-09-18): botón flotante de modo de
        // rotación (Simple/Avanzada) al soltar muebles y muros divisorios. Ahora también
        // accesible desde el menú "🔄 Rotar" de CrudPanelController.
        if (Object.FindAnyObjectByType<RotationModeController>() == null)
            new GameObject("RotationModeManager").AddComponent<RotationModeController>();

        // Punto 2 del plan de edición avanzada (2026-09-18, ajustado 2026-09-19): modo de edición
        // universal (click con Y sobre cualquier mueble o pared) -- activación, panel CRUD, y las
        // modalidades de "Actualizar" (incluyendo el nuevo gizmo de redimensionar por arrastre).
        if (Object.FindAnyObjectByType<UniversalEditModeController>() == null)
            new GameObject("UniversalEditModeManager").AddComponent<UniversalEditModeController>();

        if (Object.FindAnyObjectByType<CrudPanelController>() == null)
            new GameObject("CrudPanelManager").AddComponent<CrudPanelController>();

        if (Object.FindAnyObjectByType<UpdateModesController>() == null)
            new GameObject("UpdateModesManager").AddComponent<UpdateModesController>();

        if (Object.FindAnyObjectByType<ResizeHandlesController>() == null)
            new GameObject("ResizeHandlesManager").AddComponent<ResizeHandlesController>();

        // Quinta ronda (2026-09-19, pedido de Alan: salto/vuelo estilo Minecraft con el botón X
        // del control izquierdo, que hasta ahora no hacía nada mientras no sostenías un mueble).
        if (Object.FindAnyObjectByType<PlayerFlightController>() == null)
            new GameObject("PlayerFlightManager").AddComponent<PlayerFlightController>();

        // Sexta ronda (2026-09-19, pedido de Alan: modal futurista con la lista de botones y qué
        // hace cada uno). Se abre desde el nuevo botón "🎮 Controles" del menú de monoambientes
        // (ver PresetLoaderController).
        if (Object.FindAnyObjectByType<ControlsHelpPanelController>() == null)
            new GameObject("ControlsHelpPanelManager").AddComponent<ControlsHelpPanelController>();

        // Watchdog: fuerza ambos controllers de Meta Quest a estar activos cuando se
        // detectan controllers físicos. Soluciona un bug donde XRInputModalityManager
        // oculta el mando izquierdo al confundir un device de HandInteraction con el
        // controller real (por orden de lastUpdateTime de los devices del Input System).
        if (Object.FindAnyObjectByType<XRControllerEnabler>() == null)
            new GameObject("XRControllerEnabler").AddComponent<XRControllerEnabler>();
    }
}
