using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Interactividad de un muro individual (Fase 3 del plan de edición avanzada, 2026-09-17):
/// antes de esto los muros no se podían seleccionar ni pintar en absoluto (PrefabMapper los
/// marcaba como "selectable = false" y SceneGenerator.HacerInteractivo cortaba de entrada).
///
/// Se agrega a cada GameObject de tipo "muro" (ver SceneGenerator.HacerMuroInteractivo). Guarda
/// el color original de fábrica (para poder "Restaurar original") y se registra en una lista
/// estática compartida (TodosLosMuros) para que WallPaintPanelController pueda ofrecer
/// "aplicar a todos los muros del cuarto" además de "solo este muro" (Sección B del plan).
/// </summary>
public class MuroEditable : MonoBehaviour
{
    private static readonly List<MuroEditable> todosLosMuros = new List<MuroEditable>();
    public static IReadOnlyList<MuroEditable> TodosLosMuros => todosLosMuros;

    private Renderer rend;
    private Color colorOriginal = Color.white;
    private bool colorOriginalCapturado = false;

    /// <summary>
    /// True para un "Muro divisorio" creado desde el catálogo (Sección A del plan de edición
    /// avanzada, para separar ambientes dentro de un monoambiente -- ej. baño/cocina). Estos, a
    /// diferencia de los muros exteriores que vienen del JSON de la sala, se pueden mover/rotar
    /// (con paso de 90°) y su longitud es ajustable -- ver SceneGenerator.HacerMuroInteractivo.
    /// </summary>
    public bool EsDivisorInterior { get; set; } = false;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null && rend.sharedMaterial != null)
        {
            colorOriginal = rend.sharedMaterial.color;
            colorOriginalCapturado = true;
        }

        todosLosMuros.Add(this);
    }

    private void OnDestroy()
    {
        todosLosMuros.Remove(this);
    }

    /// <summary>Cambia el color de ESTE muro únicamente.</summary>
    public void SetColor(Color color)
    {
        if (rend == null) return;

        Shader shader = (rend.sharedMaterial != null && rend.sharedMaterial.shader != null)
            ? rend.sharedMaterial.shader
            : Shader.Find("Universal Render Pipeline/Lit");

        Material mat = new Material(shader);
        mat.color = color;
        rend.sharedMaterial = mat;
    }

    /// <summary>Vuelve ESTE muro a su color original (el que tenía al generar la sala).</summary>
    public void RestoreOriginal()
    {
        if (colorOriginalCapturado)
            SetColor(colorOriginal);
    }

    /// <summary>
    /// Sube o baja la altura de ESTE muro únicamente (Supuesto 1 del plan de edición avanzada:
    /// "necesito aumentar o disminuir la altura"). Los muros son cubos primitivos escalados
    /// directamente en metros (ver SceneGenerator.ApplyTransforms), así que cambiar la altura es
    /// cambiar localScale.y -- pero como el pivot de un cubo de Unity queda en su centro, hay que
    /// reacomodar la posición en Y para que la BASE del muro se quede pegada al piso (y=0) en vez
    /// de crecer/achicarse parejo para los dos lados (lo que dejaría el muro flotando o hundido).
    /// </summary>
    public void AdjustHeight(float deltaMetros)
    {
        const float alturaMinima = 0.5f;
        Vector3 escala = transform.localScale;
        escala.y = Mathf.Max(alturaMinima, escala.y + deltaMetros);
        transform.localScale = escala;

        Vector3 pos = transform.position;
        pos.y = escala.y * 0.5f;
        transform.position = pos;
    }

    /// <summary>Aplica AdjustHeight a todos los muros de la sala actual (interruptor "aplicar a todos").</summary>
    public static void AdjustHeightAll(float deltaMetros)
    {
        foreach (var muro in todosLosMuros.ToArray())
            muro.AdjustHeight(deltaMetros);
    }

    /// <summary>
    /// Estira o achica el "Muro divisorio" a lo largo de su eje local X (la convención usada al
    /// crearlo en SceneGenerator.CreateInteriorWallDivider: X=longitud, Y=altura, Z=espesor) --
    /// solo tiene sentido para EsDivisorInterior, pero no hace daño llamarlo en un muro exterior.
    /// </summary>
    public void AdjustLength(float deltaMetros)
    {
        const float longitudMinima = 0.3f;
        Vector3 escala = transform.localScale;
        escala.x = Mathf.Max(longitudMinima, escala.x + deltaMetros);
        transform.localScale = escala;
    }
}
