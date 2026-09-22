using UnityEngine;

// Abre/cierra el menu principal posicionandolo siempre frente al jugador, en vez
// de dejarlo fijo en un punto de la sala. Adaptado de ToggleMenu.cs de Room
// Designer (TeamFWS): el original escuchaba el boton "menu" del control
// izquierdo por polling de UnityEngine.XR (legacy input) cada frame; aca se
// llama a Toggle() desde el evento "Select Entered" del Boton_Menu que ya
// existe en la escena, que es el mecanismo de interaccion que usa el resto del
// proyecto (evita depender de que el runtime XR exponga el boton "menu" via la
// API legacy, que no siempre esta mapeado igual entre visores).
public class ToggleMenuVR : MonoBehaviour
{
    private const float DistanciaCamara = 0.8f;
    private const float DesplazamientoY = -0.2f;

    public GameObject menuUI;

    private Transform camaraJugador;

    private void Start()
    {
        camaraJugador = Camera.main.transform;
        if (menuUI != null) menuUI.SetActive(false);
    }

    // Llamar desde el evento "Select Entered" de Boton_Menu.
    public void Toggle()
    {
        if (menuUI == null) return;

        bool seVaAMostrar = !menuUI.activeSelf;
        menuUI.SetActive(seVaAMostrar);

        if (seVaAMostrar)
            PosicionarFrenteAlJugador();
        else
            StateManager.Instance.ChangeState(AppState.Recorrido);
    }

    private void PosicionarFrenteAlJugador()
    {
        Vector3 posicionMenu = camaraJugador.position + camaraJugador.forward * DistanciaCamara;
        posicionMenu.y = camaraJugador.position.y + DesplazamientoY;
        menuUI.transform.position = posicionMenu;

        Vector3 direccionAlMenu = menuUI.transform.position - camaraJugador.position;
        direccionAlMenu.y = 0f;

        if (direccionAlMenu.sqrMagnitude > 0.01f)
            menuUI.transform.rotation = Quaternion.LookRotation(direccionAlMenu);
    }
}
