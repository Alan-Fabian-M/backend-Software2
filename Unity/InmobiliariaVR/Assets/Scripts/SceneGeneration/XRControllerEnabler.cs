using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using System.Collections;

/// <summary>
/// Watchdog que garantiza que ambos controllers de Meta Quest estén visibles
/// cuando se detectan controllers físicos.
///
/// Causa raíz: XRInputModalityManager.TryGetControllerDevice selecciona el
/// XRController device con el lastUpdateTime más reciente. Cuando
/// HandInteractionProfile está habilitado junto con los perfiles de controller
/// (MetaQuestTouchPlus, OculusTouch), OpenXR crea AMBOS tipos de devices por
/// mano.  Si el device HandInteraction de la mano izquierda tiene un timestamp
/// más reciente, el manager lo elige, detecta que NO está tracked (porque el
/// usuario usa controles físicos, no manos), y oculta el controller izquierdo
/// con SetActive(false).  El derecho funciona porque su device real del
/// controller tiene un timestamp más reciente por orden de inicialización.
///
/// Este script corre una corrutina que, después de un breve delay inicial,
/// revisa periódicamente si algún controller está oculto a pesar de haber
/// un device físico real conectado, y lo fuerza a estar activo.
/// </summary>
public class XRControllerEnabler : MonoBehaviour
{
    /// <summary>Segundos a esperar antes de la primera revisión (da tiempo a que los devices se registren).</summary>
    private const float INITIAL_DELAY = 1.5f;

    /// <summary>Intervalo entre revisiones periódicas.</summary>
    private const float CHECK_INTERVAL = 0.5f;

    /// <summary>Después de esta cantidad de segundos sin correcciones, reducimos la frecuencia.</summary>
    private const float RELAX_AFTER = 10f;

    /// <summary>Intervalo relajado una vez que ya no hubo correcciones recientes.</summary>
    private const float RELAXED_INTERVAL = 2f;

    private XRInputModalityManager cachedManager;
    private float timeSinceLastCorrection;

    private void Start()
    {
        StartCoroutine(WatchdogRoutine());
    }

    private IEnumerator WatchdogRoutine()
    {
        // Esperar antes de la primera revisión para que OpenXR registre los devices
        yield return new WaitForSeconds(INITIAL_DELAY);

        while (true)
        {
            ForceEnableControllers();

            // Usar intervalo relajado si no hubo correcciones recientes
            float interval = timeSinceLastCorrection > RELAX_AFTER
                ? RELAXED_INTERVAL
                : CHECK_INTERVAL;

            yield return new WaitForSeconds(interval);
            timeSinceLastCorrection += interval;
        }
    }

    private void ForceEnableControllers()
    {
        // Cachear el manager para no buscarlo cada frame
        if (cachedManager == null)
        {
            cachedManager = FindAnyObjectByType<XRInputModalityManager>();
            if (cachedManager == null)
                return;
        }

        bool leftPhysicalDetected = false;
        bool rightPhysicalDetected = false;

        // Revisar todos los XRController devices del Input System
        var devices = InputSystem.devices;
        for (int i = 0; i < devices.Count; i++)
        {
            var device = devices[i];
            if (device is not XRController xrCtrl)
                continue;

            // Ignorar devices que NO son controllers físicos reales.
            // HandInteraction devices son creados por HandInteractionProfile y
            // causan el conflicto de detección.
            string layoutName = xrCtrl.layout;
            if (layoutName.Contains("HandInteraction") ||
                layoutName.Contains("HoloLens") ||
                layoutName.Contains("DPad") ||
                layoutName.Contains("PalmPose"))
            {
                continue;
            }

            // Verificar si el device está asignado a la mano izquierda o derecha
            var usages = device.usages;
            for (int u = 0; u < usages.Count; u++)
            {
                if (usages[u] == CommonUsages.LeftHand)
                    leftPhysicalDetected = true;
                else if (usages[u] == CommonUsages.RightHand)
                    rightPhysicalDetected = true;
            }
        }

        // Si se detectan controllers físicos, forzar que estén activos
        bool corrected = false;

        if (leftPhysicalDetected)
        {
            GameObject leftCtrl = cachedManager.leftController;
            if (leftCtrl != null && !leftCtrl.activeSelf)
            {
                leftCtrl.SetActive(true);
                Debug.Log("[XRControllerEnabler] Left Controller forzado activo — " +
                          "device físico detectado pero XRInputModalityManager lo había ocultado.");
                corrected = true;
            }
        }

        if (rightPhysicalDetected)
        {
            GameObject rightCtrl = cachedManager.rightController;
            if (rightCtrl != null && !rightCtrl.activeSelf)
            {
                rightCtrl.SetActive(true);
                Debug.Log("[XRControllerEnabler] Right Controller forzado activo — " +
                          "device físico detectado pero XRInputModalityManager lo había ocultado.");
                corrected = true;
            }
        }

        if (corrected)
        {
            timeSinceLastCorrection = 0f;
        }
    }
}
