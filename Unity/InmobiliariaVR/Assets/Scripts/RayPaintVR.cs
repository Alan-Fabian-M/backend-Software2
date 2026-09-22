using UnityEngine;

// Permite "pintar" paredes/pisos/techo con un rayo desde el controlador VR,
// aplicando un material de una paleta (misma logica de indices que
// MaterialChangerVR, para que los mismos botones de menu sirvan para ambos).
// Adaptado de RayPaint.cs de Room Designer (TeamFWS): el original ya usaba
// UnityEngine.XR (InputDevices/CommonUsages), que es API estandar de Unity
// compatible con XRI/OpenXR, no especifica de Meta. Se quito la dependencia
// del FlexibleColorPicker (asset propio de ese proyecto) y en su lugar se usa
// una paleta de materiales asignada en el Inspector.
// Solo pinta mientras el StateManager esta en AppState.Pintura.
public class RayPaintVR : MonoBehaviour
{
    [Tooltip("Transform de origen del rayo (ej. el Ray Interactor de la mano derecha)")]
    public Transform rayOrigin;

    [Tooltip("Materiales disponibles para pintar (mismo orden que uses en el menu)")]
    public Material[] paintPalette;

    [Tooltip("Tags de superficies que se pueden pintar")]
    public string[] paintableTags = { "Wall", "Floor", "Ceiling" };

    [Tooltip("Distancia maxima del rayo")]
    public float maxDistance = 10f;

    private Material materialToApply;
    private LineRenderer lineRenderer;
    private bool isEnabled;

    private void Start()
    {
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = 2;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.enabled = false;

        if (paintPalette != null && paintPalette.Length > 0)
        {
            materialToApply = paintPalette[0];
        }
    }

    private void OnEnable()
    {
        StateManager.Instance.StateChanged += OnStateChanged;
        isEnabled = StateManager.Instance.CurrentState == AppState.Pintura;
    }

    private void OnDisable()
    {
        StateManager.Instance.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(AppState newState)
    {
        isEnabled = newState == AppState.Pintura;
        if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    private void Update()
    {
        if (!isEnabled || rayOrigin == null) return;
        UpdateRayVisual();
    }

    // Llamar desde un boton de menu de colores/acabados (mismo patron que MaterialChangerVR.SetMaterialByIndex)
    public void SetMaterialByIndex(int index)
    {
        if (paintPalette == null || index < 0 || index >= paintPalette.Length) return;
        materialToApply = paintPalette[index];
    }

    // Llamar desde el evento "Select Entered" del XR Ray Interactor cuando el StateManager este en Pintura
    public void TryPaint()
    {
        if (!isEnabled || rayOrigin == null || materialToApply == null) return;

        if (Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, maxDistance))
        {
            foreach (string paintableTag in paintableTags)
            {
                if (hit.collider.CompareTag(paintableTag))
                {
                    Renderer targetRenderer = hit.collider.GetComponent<Renderer>();
                    if (targetRenderer != null)
                    {
                        targetRenderer.material = materialToApply;
                    }
                    break;
                }
            }
        }
    }

    private void UpdateRayVisual()
    {
        lineRenderer.enabled = true;

        Vector3 endPoint = Physics.Raycast(rayOrigin.position, rayOrigin.forward, out RaycastHit hit, maxDistance)
            ? hit.point
            : rayOrigin.position + rayOrigin.forward * maxDistance;

        lineRenderer.SetPosition(0, rayOrigin.position);
        lineRenderer.SetPosition(1, endPoint);
    }
}
