using UnityEngine;

// Permite que el usuario, parado frente a la cama, la toque para ciclar entre
// varios modelos de cama disponibles -- mismo patron de interaccion que ya usa
// MaterialChangerVR para cambiar colores (Select/Grip), pero acá se cambia el
// MODELO 3D completo en vez de solo el material.
//
// DISENO: la cama original de Jazmin (varias piezas: marco, colchon, sabanas,
// almohadas) es parte de un Prefab Instance anidado del modelo importado -- no
// se puede reparentar esas piezas fuera de su jerarquia (Unity lo revierte
// automaticamente). Por eso "piezasOriginales" apunta directo a esos 4
// GameObjects donde estan, y las opciones alternativas (bed1, bed2, etc.) son
// GameObjects nuevos e independientes en la misma posicion.
public class CamaSwapperVR : MonoBehaviour
{
    [Tooltip("Las piezas de la cama original del modelo de Blender (se activan/desactivan todas juntas como la 'opcion 0')")]
    public GameObject[] piezasOriginales;

    [Tooltip("Las camas alternativas (cada una ya con su propio modelo/material armado)")]
    public GameObject[] opcionesAlternativas;

    private int indiceActual = 0; // 0 = original, 1..N = opcionesAlternativas[indice-1]

    private void Start()
    {
        AplicarOpcion(0);
    }

    // Solo se permite cambiar mientras el StateManager esta en modo Personalizacion,
    // mismo criterio que MaterialChangerVR (evita cambios accidentales durante el recorrido).
    private bool PuedeCambiar()
    {
        return StateManager.Instance.CurrentState == AppState.Personalizacion;
    }

    // Llamar desde el evento Select Entered del XRSimpleInteractable de la cama.
    public void CiclarSiguienteModelo()
    {
        if (!PuedeCambiar()) return;

        int totalOpciones = 1 + opcionesAlternativas.Length; // original + alternativas
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
