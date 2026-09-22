using UnityEngine;

public class MenuControllerVR : MonoBehaviour
{
    [Tooltip("El Canvas del menú UI que quieres mostrar u ocultar")]
    public GameObject menuCanvas;

    public void ToggleMenu()
    {
        if (menuCanvas != null)
        {
            bool willBeActive = !menuCanvas.activeSelf;
            menuCanvas.SetActive(willBeActive);

            if (willBeActive)
            {
                // Este menu ES la pantalla de personalizacion (elegir acabados de pared/sofa),
                // por eso activa AppState.Personalizacion en vez de AppState.Menu.
                StateManager.Instance.ChangeState(AppState.Personalizacion);
            }
            else
            {
                StateManager.Instance.RevertPreviousState();
            }
        }
    }

    // Llamar desde los botones del menu para elegir el modo antes de cerrarlo
    // (ej. boton "Pintar paredes" o "Mover muebles").
    public void SetMode(int modeValue)
    {
        switch (modeValue)
        {
            case 0:
                StateManager.Instance.ChangeState(AppState.Recorrido);
                break;
            case 1:
                StateManager.Instance.ChangeState(AppState.Personalizacion);
                break;
            case 2:
                StateManager.Instance.ChangeState(AppState.Pintura);
                break;
        }
    }
}
