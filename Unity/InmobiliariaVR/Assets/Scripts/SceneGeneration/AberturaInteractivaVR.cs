using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Controlador universal de aberturas interactivo en Meta Quest 3 VR.
/// Soporta:
///   1. Puerta simple (hoja única que gira en su bisagra a 90° o usa Animator).
///   2. Puerta doble (dos hojas que abren simultáneamente en sentidos opuestos: -90° y +90°).
///   3. Ventana de pared (hoja corrediza o batiente que abre/cierra).
///
/// Modos de activación:
///   - Gatillo (Trigger / Activate) del control VR apuntando a la abertura.
///   - Botón "Abrir / Cerrar" en el panel CRUD de edición.
///   - Método público Toggle() / Abrir() / Cerrar().
/// </summary>
public class AberturaInteractivaVR : MonoBehaviour
{
    public enum TipoAbertura
    {
        PuertaSimple,
        PuertaDoble,
        Ventana
    }

    [Header("Configuración General")]
    public TipoAbertura tipo = TipoAbertura.PuertaSimple;
    public bool estaAbierta = false;
    public bool EstaAbierta => estaAbierta;
    public float duracionAnimacion = 0.4f;

    [Header("Puertas (Rotación de Hojas)")]
    public Transform hojaIzquierda;
    public Transform hojaDerecha; // Solo para PuertaDoble
    public float anguloApertura = 90f;

    [Header("Ventanas (Deslizante o Batiente)")]
    public bool ventanaEsDeslizante = true;
    public Vector3 desplazamientoVentana = new Vector3(0f, 0f, -0.6f);

    [Header("Animator (Opcional)")]
    public Animator animator;
    public string clipAbrir = "Opening";
    public string clipCerrar = "Closing";

    private Quaternion rotacionInicialIzq;
    private Quaternion rotacionInicialDer;
    private Vector3 posicionInicialVentana;
    private Coroutine corrutinaAnimacion;
    private bool inicializado = false;

    private void Awake()
    {
        Inicializar();
    }

    private void Start()
    {
        ConectarInteractable();
    }

    public void Inicializar()
    {
        if (inicializado) return;

        if (tipo == TipoAbertura.Ventana)
        {
            if (clipAbrir == "Opening") clipAbrir = "Openingwindow";
            if (clipCerrar == "Closing") clipCerrar = "Closingwindow";
        }

        // Auto-detectar hojas si no están asignadas
        if (hojaIzquierda == null)
        {
            AutoDetectarPartes();
        }

        if (hojaIzquierda != null)
        {
            rotacionInicialIzq = hojaIzquierda.localRotation;
            posicionInicialVentana = hojaIzquierda.localPosition;
        }

        if (hojaDerecha != null)
        {
            rotacionInicialDer = hojaDerecha.localRotation;
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        inicializado = true;
    }

    private void AutoDetectarPartes()
    {
        if (tipo == TipoAbertura.PuertaSimple)
        {
            // Buscar sub-objeto "Door", "Int_Door_01", etc.
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                string n = t.name.ToLower();
                if (n.Contains("door") && !n.Contains("frame") && !n.Contains("handle") && !n.Contains("knob"))
                {
                    hojaIzquierda = t;
                    break;
                }
            }
        }
        else if (tipo == TipoAbertura.PuertaDoble)
        {
            Transform[] todos = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in todos)
            {
                if (t == transform) continue;
                string n = t.name.ToLower();
                if ((n.Contains("door_l") || n.Contains("left") || n.Contains("izq") || n.Contains("hoja1") || n.Contains("hoja_1")) && hojaIzquierda == null)
                {
                    hojaIzquierda = t;
                }
                else if ((n.Contains("door_r") || n.Contains("right") || n.Contains("der") || n.Contains("hoja2") || n.Contains("hoja_2")) && hojaDerecha == null)
                {
                    hojaDerecha = t;
                }
            }

            // Fallback si no tienen nombres L/R explícitos: buscar dos objetos con 'door'
            if (hojaIzquierda == null || hojaDerecha == null)
            {
                foreach (Transform t in todos)
                {
                    if (t == transform) continue;
                    string n = t.name.ToLower();
                    if (n.Contains("door") && !n.Contains("frame") && !n.Contains("handle") && !n.Contains("knob"))
                    {
                        if (hojaIzquierda == null) hojaIzquierda = t;
                        else if (hojaDerecha == null && t != hojaIzquierda) hojaDerecha = t;
                    }
                }
            }
        }
        else if (tipo == TipoAbertura.Ventana)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t == transform) continue;
                string n = t.name.ToLower();
                if (n.Contains("window_01") || n.Contains("window_left") || (n.Contains("window") && !n.Contains("frame") && !n.Contains("base")))
                {
                    hojaIzquierda = t;
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Vincula la abertura a eventos de XR Toolkit para que al pulsar el gatillo (Activate)
    /// mientras se apunta con el rayo, la puerta o ventana se abra/cierre.
    /// </summary>
    private void ConectarInteractable()
    {
        XRGrabInteractable grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.activated.AddListener(OnGrabActivated);
        }

        XRSimpleInteractable simple = GetComponent<XRSimpleInteractable>();
        if (simple != null)
        {
            simple.selectEntered.AddListener(OnSimpleSelect);
        }
    }

    private void OnGrabActivated(ActivateEventArgs args)
    {
        Toggle();
    }

    private void OnSimpleSelect(SelectEnterEventArgs args)
    {
        Toggle();
    }

    /// <summary>Alterna entre abierto y cerrado.</summary>
    public void Toggle()
    {
        if (estaAbierta) Cerrar();
        else Abrir();
    }

    /// <summary>Abre la puerta o ventana.</summary>
    public void Abrir()
    {
        Inicializar();
        if (estaAbierta) return;
        estaAbierta = true;

        if (corrutinaAnimacion != null)
            StopCoroutine(corrutinaAnimacion);

        corrutinaAnimacion = StartCoroutine(Animar(true));
        MostrarToastEstado();
    }

    /// <summary>Cierra la puerta o ventana.</summary>
    public void Cerrar()
    {
        Inicializar();
        if (!estaAbierta) return;
        estaAbierta = false;

        if (corrutinaAnimacion != null)
            StopCoroutine(corrutinaAnimacion);

        corrutinaAnimacion = StartCoroutine(Animar(false));
        MostrarToastEstado();
    }

    private void MostrarToastEstado()
    {
        string nombreAbertura = tipo == TipoAbertura.PuertaDoble ? "Puerta Doble" :
                                tipo == TipoAbertura.PuertaSimple ? "Puerta" : "Ventana";
        string estado = estaAbierta ? "abierta" : "cerrada";
        string icono = tipo == TipoAbertura.Ventana ? "🪟" : "🚪";
        ToastNotificationUI.Show($"{icono} {nombreAbertura} {estado}", 1.5f);
    }

    private IEnumerator Animar(bool abrir)
    {
        // Si hay Animator con controller válido y clips de apertura/cierre, disparar animación
        if (animator != null && animator.runtimeAnimatorController != null && !string.IsNullOrEmpty(clipAbrir) && !string.IsNullOrEmpty(clipCerrar))
        {
            string clip = abrir ? clipAbrir : clipCerrar;
            int hash = Animator.StringToHash(clip);
            if (animator.HasState(0, hash))
            {
                animator.Play(clip);
                yield return new WaitForSeconds(duracionAnimacion);
                yield break;
            }
        }

        // Interpolación procedural suave
        float t = 0f;
        float dur = Mathf.Max(0.05f, duracionAnimacion);

        Quaternion rotOrigIzq = hojaIzquierda != null ? hojaIzquierda.localRotation : Quaternion.identity;
        Quaternion rotOrigDer = hojaDerecha != null ? hojaDerecha.localRotation : Quaternion.identity;
        Vector3 posOrigVentana = hojaIzquierda != null ? hojaIzquierda.localPosition : Vector3.zero;

        // Metas de destino
        Quaternion rotDestIzq = rotacionInicialIzq;
        Quaternion rotDestDer = rotacionInicialDer;
        Vector3 posDestVentana = posicionInicialVentana;

        if (abrir)
        {
            if (tipo == TipoAbertura.PuertaSimple)
            {
                rotDestIzq = rotacionInicialIzq * Quaternion.Euler(0f, anguloApertura, 0f);
            }
            else if (tipo == TipoAbertura.PuertaDoble)
            {
                rotDestIzq = rotacionInicialIzq * Quaternion.Euler(0f, -anguloApertura, 0f);
                rotDestDer = rotacionInicialDer * Quaternion.Euler(0f, anguloApertura, 0f);
            }
            else if (tipo == TipoAbertura.Ventana)
            {
                if (ventanaEsDeslizante)
                {
                    posDestVentana = posicionInicialVentana + desplazamientoVentana;
                }
                else
                {
                    rotDestIzq = rotacionInicialIzq * Quaternion.Euler(0f, anguloApertura, 0f);
                }
            }
        }

        while (t < dur)
        {
            t += Time.deltaTime;
            float factor = Mathf.SmoothStep(0f, 1f, t / dur);

            if (hojaIzquierda != null)
            {
                if (tipo == TipoAbertura.Ventana && ventanaEsDeslizante)
                    hojaIzquierda.localPosition = Vector3.Lerp(posOrigVentana, posDestVentana, factor);
                else
                    hojaIzquierda.localRotation = Quaternion.Slerp(rotOrigIzq, rotDestIzq, factor);
            }

            if (hojaDerecha != null && tipo == TipoAbertura.PuertaDoble)
            {
                hojaDerecha.localRotation = Quaternion.Slerp(rotOrigDer, rotDestDer, factor);
            }

            yield return null;
        }

        // Fijar valores finales exactos
        if (hojaIzquierda != null)
        {
            if (tipo == TipoAbertura.Ventana && ventanaEsDeslizante)
                hojaIzquierda.localPosition = posDestVentana;
            else
                hojaIzquierda.localRotation = rotDestIzq;
        }

        if (hojaDerecha != null && tipo == TipoAbertura.PuertaDoble)
        {
            hojaDerecha.localRotation = rotDestDer;
        }
    }
}
