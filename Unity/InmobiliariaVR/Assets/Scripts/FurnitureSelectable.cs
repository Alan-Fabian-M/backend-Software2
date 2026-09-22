using UnityEngine;

// Abre el menu compartido de mover/rotar (FurnitureManipulatorVR) con un solo
// boton: mantener presionado "Select" (grip) sobre el mueble.
//
// Se probo primero con "Activate" (trigger) separado de "Select" (grip) para no
// pisar el ciclo de color del Sofa (que ya usa Select), pero probando a mano con
// el XR Device Simulator se descubrio que en XRI "Activate" solo se dispara si el
// objeto YA esta seleccionado en simultaneo (hay que sostener grip Y apretar
// trigger a la vez) — un gesto de dos botones dificil de ejecutar y de probar.
// Ahora se usa un solo boton (Select) con umbral de tiempo: un toque corto en el
// Sofa sigue ciclando el color (via el listener de MaterialChangerVR, que ya
// estaba en selectEntered y no se toco), y mantenerlo presionado un instante mas
// abre el menu de mover/rotar. En Mesa/TV (sin MaterialChangerVR) un toque corto
// no hace nada y sostenerlo abre el menu igual, para que el gesto sea consistente
// en los tres muebles.
public class FurnitureSelectable : MonoBehaviour
{
    [Tooltip("Segundos que hay que mantener presionado Select antes de abrir el menu")]
    public float tiempoParaAbrir = 0.35f;

    public FurnitureManipulatorVR manipulador;

    private bool seleccionActiva;
    private bool menuAbiertoEnEstaSeleccion;
    private float momentoInicioSeleccion;

    // Llamar desde el evento "Select Entered" del XRSimpleInteractable del mueble.
    public void OnSeleccionEntra()
    {
        seleccionActiva = true;
        menuAbiertoEnEstaSeleccion = false;
        momentoInicioSeleccion = Time.time;
    }

    // Llamar desde el evento "Select Exited" del XRSimpleInteractable del mueble.
    public void OnSeleccionSale()
    {
        seleccionActiva = false;
    }

    private void Update()
    {
        if (!seleccionActiva || menuAbiertoEnEstaSeleccion) return;

        if (Time.time - momentoInicioSeleccion >= tiempoParaAbrir)
        {
            menuAbiertoEnEstaSeleccion = true;
            if (manipulador != null) manipulador.AbrirMenuPara(transform);
        }
    }
}
