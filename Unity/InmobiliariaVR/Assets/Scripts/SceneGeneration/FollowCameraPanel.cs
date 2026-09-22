using UnityEngine;

/// <summary>
/// Ajuste pedido por Alan (2026-09-19, tercera ronda: "quiero que los modales... sigan al
/// usuario... que no se queden ahí en el aire"): hasta ahora, todos los paneles world-space del
/// proyecto (CrudPanelController, WallPaintPanelController, UpdateModesController,
/// ResizeHandlesController, FurnitureCatalogController, ConfirmDialogUI) se posicionaban UNA SOLA
/// VEZ frente a la cámara justo al abrirse, y de ahí en más quedaban fijos en esas coordenadas del
/// mundo -- si te movías (caminando o con el joystick) mientras el panel seguía abierto, el panel
/// se quedaba flotando atrás tuyo en el aire, en vez de seguir frente a tu vista.
///
/// Este componente, agregado al GameObject del Canvas de cada panel, resuelve eso reposicionándolo
/// frente a la cámara EN CADA FRAME mientras el panel está activo (Update() no corre si el
/// GameObject está inactivo, así que no hace falta ningún chequeo extra de visibilidad). Reemplaza
/// las llamadas puntuales a "PosicionarFrenteACamara"/"MostrarPagina" que hacía cada panel para su
/// posicionamiento inicial -- se puede dejar esa llamada inicial también (no hace daño, es
/// redundante con el primer Update de este componente) para que el panel ya aparezca bien ubicado
/// en el mismo frame en que se activa, en vez de esperar un frame a que corra Update.
/// </summary>
public class FollowCameraPanel : MonoBehaviour
{
    public float distancia = 1.2f;
    public float offsetVertical = 0f;

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 desired = cam.transform.position
            + cam.transform.forward * distancia
            + cam.transform.up * offsetVertical;
        desired = SceneGenerator.ClampInFrontOfObstruction(cam.transform.position, desired);

        transform.position = desired;
        transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
    }
}
