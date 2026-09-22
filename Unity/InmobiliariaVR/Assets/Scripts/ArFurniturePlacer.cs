using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// App movil complementaria (requisito del examen: sensores del dispositivo).
// Usa la camara del celular vía AR Foundation/ARCore para detectar el piso y,
// con un tap, coloca uno de los muebles del catalogo (mismos prefabs de
// Assets/HomeStuff/ que ya se usan en Sala_MVP) en Realidad Aumentada.
//
// El Sofa colocado recibe un MaterialChangerVR con la misma paleta de colores
// que el de la version VR, y se lo asigna como "sofa" del OllamaAIController de
// esta escena — asi el asistente de IA (texto, mismo backend Ollama local) puede
// cambiarle el color sin duplicar la logica de interpretacion de comandos.
public class ArFurniturePlacer : MonoBehaviour
{
    [Header("AR")]
    public ARRaycastManager raycastManager;

    [Header("Catalogo de muebles (0 = Sofa, con cambio de color; el resto solo se coloca)")]
    public GameObject[] prefabsMueble;

    [Header("Paleta de colores del Sofa (mismo orden que en Sala_MVP)")]
    public Material[] materialesSofa;

    [Header("Integracion con el asistente de IA")]
    public OllamaAIController ollamaController;

    [Header("UI")]
    public Text textoEstado;

    private int muebleSeleccionado = 0;
    private GameObject ultimoMuebleColocado;

    private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // MaterialChangerVR.SetMaterialByIndex solo actua en AppState.Personalizacion
    // (guard heredado de la version VR). La app movil no tiene menu de modos, asi
    // que arranca directo en ese estado para que el cambio de color por voz/texto
    // funcione sin pasos extra.
    private void Awake()
    {
        StateManager.Instance.ChangeState(AppState.Personalizacion);
    }

    // Llamar desde los botones "Sofa" / "Mesa" / "TV" del panel de la app movil.
    public void SeleccionarMueble(int indice)
    {
        if (prefabsMueble == null || indice < 0 || indice >= prefabsMueble.Length) return;
        muebleSeleccionado = indice;
    }

    private void Update()
    {
        if (raycastManager == null) return;

        Vector2? posicionToque = LeerToqueOClickDeEsteFrame();
        if (posicionToque == null) return;

        if (raycastManager.Raycast(posicionToque.Value, hits, TrackableType.PlaneWithinPolygon))
        {
            ColocarEn(hits[0].pose);
        }
    }

    // Touchscreen real en el celular; Mouse como respaldo para poder probar en el
    // Editor con un entorno de simulacion AR (XR Simulation) o con un dispositivo
    // remoto conectado.
    private Vector2? LeerToqueOClickDeEsteFrame()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.wasPressedThisFrame)
        {
            return touchscreen.primaryTouch.position.ReadValue();
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return mouse.position.ReadValue();
        }

        return null;
    }

    private void ColocarEn(Pose pose)
    {
        if (prefabsMueble == null || prefabsMueble.Length == 0) return;

        GameObject prefab = prefabsMueble[muebleSeleccionado];
        if (prefab == null) return;

        if (ultimoMuebleColocado != null) Destroy(ultimoMuebleColocado);

        GameObject instancia = Instantiate(prefab, pose.position, pose.rotation);
        ultimoMuebleColocado = instancia;

        // Solo el Sofa (indice 0) tiene paleta de color, igual que en Sala_MVP.
        if (muebleSeleccionado == 0 && materialesSofa != null && materialesSofa.Length > 0)
        {
            MaterialChangerVR cambiador = instancia.GetComponent<MaterialChangerVR>();
            if (cambiador == null) cambiador = instancia.AddComponent<MaterialChangerVR>();
            cambiador.materials = materialesSofa;

            if (ollamaController != null) ollamaController.sofa = cambiador;
        }

        SetEstado("Mueble colocado: " + prefab.name);
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[ArFurniturePlacer] " + mensaje);
    }
}
