using UnityEngine;

public class MaterialChangerVR : MonoBehaviour
{
    [Tooltip("Arrastra aqui los materiales que quieres poder intercambiar (ej. pisos, paredes)")]
    public Material[] materials;

    private int currentMaterialIndex = 0;
    private Renderer targetRenderer;

    void Start()
    {
        // GetComponentInChildren (no GetComponent) porque los modelos importados suelen
        // traer el mesh en un hijo anidado del prefab, no en el mismo GameObject.
        targetRenderer = GetComponentInChildren<Renderer>();
        if (targetRenderer == null)
        {
            Debug.LogWarning("No se encontro un Renderer en " + gameObject.name + " ni en sus hijos. MaterialChangerVR necesita uno.");
        }
    }

    // Solo se permite personalizar mientras el StateManager esta en modo Personalizacion,
    // para evitar cambios accidentales durante el recorrido normal.
    private bool PuedeCambiarMaterial()
    {
        return StateManager.Instance.CurrentState == AppState.Personalizacion;
    }

    // Esta funcion se llamara cuando el usuario de VR interactue con el objeto
    public void ChangeToNextMaterial()
    {
        if (!PuedeCambiarMaterial() || materials.Length == 0 || targetRenderer == null) return;

        currentMaterialIndex++;
        if (currentMaterialIndex >= materials.Length)
        {
            currentMaterialIndex = 0; // Volver al inicio
        }

        Material nuevoMaterial = materials[currentMaterialIndex];
        targetRenderer.material = nuevoMaterial;

        // BUG ENCONTRADO (quinta ronda, 2026-09-19 -- "el cambio de color de los muebles no se
        // aplica" / "no quedan con el color que quiero"): si este objeto está sostenido en este
        // momento, tiene puesta la "piel" verde temporal de TemporaryGhostVisual (activada al
        // agarrarlo), que cachea el material de ANTES de agarrarlo para devolverlo al soltar --
        // sin avisarle acá que el material real cambió, Desactivar() restauraba el color VIEJO
        // (de antes de elegir el nuevo) al soltar el mueble, pisando el cambio que acabás de
        // hacer. Ver el comentario completo en TemporaryGhostVisual.ActualizarMaterialSiEstaActivo.
        // No hace nada si el objeto no tiene la piel activa en este momento (caso normal).
        TemporaryGhostVisual.ActualizarMaterialSiEstaActivo(gameObject, targetRenderer, nuevoMaterial);
    }

    // Permite fijar un material puntual (ej. desde un boton de menu con color especifico)
    public void SetMaterialByIndex(int index)
    {
        if (!PuedeCambiarMaterial() || targetRenderer == null || index < 0 || index >= materials.Length) return;

        currentMaterialIndex = index;
        Material nuevoMaterial = materials[currentMaterialIndex];
        targetRenderer.material = nuevoMaterial;

        // Mismo fix que ChangeToNextMaterial() -- ver el comentario ahí arriba.
        TemporaryGhostVisual.ActualizarMaterialSiEstaActivo(gameObject, targetRenderer, nuevoMaterial);
    }
}
