using UnityEngine;

// Version generica de CamaSwapperVR.cs -- permite ciclar entre varios modelos
// 3D para CUALQUIER mueble (ropero, mesa, sillas, etc.), no solo la cama.
// Mismo patron de interaccion que MaterialChangerVR: tocar con Select/Grip
// mientras se esta en modo Personalizacion cicla a la siguiente opcion.
public class MuebleSwapperVR : MonoBehaviour
{
    [Tooltip("Las piezas del mueble original del modelo de Blender (se activan/desactivan todas juntas como la 'opcion 0')")]
    public GameObject[] piezasOriginales;

    [Tooltip("Los modelos alternativos (cada uno ya armado con su propio mesh/material)")]
    public GameObject[] opcionesAlternativas;

    private int indiceActual = 0;

    private void Start()
    {
        AplicarOpcion(0);
    }

    private bool PuedeCambiar()
    {
        return StateManager.Instance.CurrentState == AppState.Personalizacion;
    }

    // Llamar desde el evento Select Entered del XRSimpleInteractable del mueble.
    public void CiclarSiguienteModelo()
    {
        if (!PuedeCambiar()) return;

        int totalOpciones = 1 + opcionesAlternativas.Length;
        indiceActual = (indiceActual + 1) % totalOpciones;
        AplicarOpcion(indiceActual);
    }

    public void SetModeloPorIndice(int indice)
    {
        if (!PuedeCambiar()) return;
        int totalOpciones = 1 + opcionesAlternativas.Length;
        if (indice < 0 || indice >= totalOpciones) return;

        indiceActual = indice;
        AplicarOpcion(indiceActual);
    }

    private void AplicarOpcion(int indice)
    {
        bool mostrarOriginal = indice == 0;
        foreach (var pieza in piezasOriginales)
            if (pieza != null) pieza.SetActive(mostrarOriginal);

        for (int i = 0; i < opcionesAlternativas.Length; i++)
        {
            if (opcionesAlternativas[i] != null)
                opcionesAlternativas[i].SetActive(indice == i + 1);
        }
    }
}
