using UnityEngine;

// Controla que panel de contenido (Personalizar/Pintar/...) se muestra y que
// AppState se activa segun la "pestana" elegida. Adaptado de SideMenu.cs de
// Room Designer (TeamFWS): el original usaba Toggles con resaltado de color a
// mano; aca se simplifico a Botones normales (mismo patron que ya usa el resto
// del proyecto con MaterialChangerVR/FurnitureManipulatorVR), sin necesidad de
// manejar ToggleGroup ni estados visuales de "seleccionado".
public class SideMenuVR : MonoBehaviour
{
    [Tooltip("Panel a mostrar por cada pestana (mismo orden que se llama a SeleccionarPestana). Puede quedar null si esa pestana no tiene panel propio (ej. Recorrido).")]
    public GameObject[] contents;
    [Tooltip("AppState que activa cada pestana, mismo orden que 'contents'")]
    public AppState[] appStates;

    private void OnEnable()
    {
        SeleccionarPestana(0);
    }

    // Llamar desde el boton de cada pestana (0=Recorrido, 1=Personalizar, 2=Pintar, ...)
    public void SeleccionarPestana(int indice)
    {
        if (indice < 0 || indice >= appStates.Length) return;

        for (int i = 0; i < contents.Length; i++)
            if (contents[i] != null) contents[i].SetActive(i == indice);

        StateManager.Instance.ChangeState(appStates[indice]);
    }
}
