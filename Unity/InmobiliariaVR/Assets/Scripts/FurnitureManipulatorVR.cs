using UnityEngine;
using UnityEngine.UI;

// Menu compartido para mover y rotar el mueble seleccionado, solo mientras el
// StateManager esta en AppState.Personalizacion (mismo criterio que MaterialChangerVR).
// Adaptado de FurnitureManipulator de Room Designer (TeamFWS): alli cada mueble
// spawneado en runtime tenia su propia instancia del menu (con OVRSpatialAnchor);
// como los muebles de Sala_MVP son fijos y no se instancian dinamicamente, alcanza
// con un unico menu en la escena que cambia de referencia segun que mueble se active.
// Se uso rotacion por botones (+-15 grados) en vez del slider original: en VR,
// apuntar con un rayo y arrastrar un slider es mucho menos confiable que un click.
public class FurnitureManipulatorVR : MonoBehaviour
{
    [Header("UI del menu")]
    public GameObject menuRoot;
    public Text nombreMueble;

    [Header("Movimiento y rotacion")]
    [Tooltip("Distancia (metros) que se mueve el mueble por cada click de flecha")]
    public float pasoMovimiento = 0.1f;
    [Tooltip("Grados que rota el mueble por cada click de rotacion")]
    public float pasoRotacion = 15f;

    private Transform muebleActual;

    private void Start()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    private bool PuedeManipular()
    {
        return StateManager.Instance.CurrentState == AppState.Personalizacion;
    }

    // Llamar desde FurnitureSelectable cuando se activa (boton "Activate") un mueble.
    public void AbrirMenuPara(Transform mueble)
    {
        if (!PuedeManipular() || mueble == null || menuRoot == null) return;

        muebleActual = mueble;

        if (nombreMueble != null) nombreMueble.text = mueble.name;

        menuRoot.transform.position = mueble.position + Vector3.up * 0.4f;
        menuRoot.SetActive(true);
    }

    public void CerrarMenu()
    {
        if (menuRoot != null) menuRoot.SetActive(false);
        muebleActual = null;
    }

    // Llamar desde los botones de flecha del menu. 0=Adelante,1=Atras,2=Izquierda,3=Derecha
    public void Mover(int direccion)
    {
        if (!PuedeManipular() || muebleActual == null) return;

        Vector3 direccionMundo;
        switch (direccion)
        {
            case 0: direccionMundo = Vector3.forward; break;
            case 1: direccionMundo = Vector3.back; break;
            case 2: direccionMundo = Vector3.left; break;
            default: direccionMundo = Vector3.right; break;
        }

        muebleActual.Translate(direccionMundo * pasoMovimiento, Space.World);
        menuRoot.transform.position = muebleActual.position + Vector3.up * 0.4f;
    }

    // Llamar desde los botones de rotacion. -1 = izquierda, 1 = derecha
    public void Rotar(int direccion)
    {
        if (!PuedeManipular() || muebleActual == null) return;
        muebleActual.Rotate(Vector3.up, direccion * pasoRotacion, Space.World);
    }
}
