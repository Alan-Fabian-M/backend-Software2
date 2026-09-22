using UnityEngine;

/// <summary>
/// Gestiona la integración de una puerta o ventana dentro de un muro:
/// 1. Mide las dimensiones reales exactas de la abertura (ancho, alto, altura de base)
///    a partir de sus Renderers para que la pared la rodee exactamente según su tamaño.
/// 2. Corta el muro en segmentos contiguos exactos (lateral izquierdo, lateral derecho,
///    dintel superior y antepecho inferior para ventanas), dejando un hueco real y limpio.
/// 3. Oculta el muro original sólido mientras la abertura esté colocada.
/// 4. Si la abertura se mueve o se borra, restaura automáticamente el muro original sin dejar fisuras.
/// 5. Todo el cálculo de posición y escala se realiza en espacio mundial, evitando distorsiones.
/// </summary>
public class AberturaWallHole : MonoBehaviour
{
    [SerializeField] private GameObject muroOriginal;
    [SerializeField] private GameObject segmentosRoot;

    public GameObject MuroOriginal => muroOriginal;
    public GameObject SegmentosRoot => segmentosRoot;

    /// <summary>
    /// Restaura el muro original sólido y destruye los segmentos cortados.
    /// </summary>
    public void RestaurarMuro()
    {
        if (muroOriginal != null)
        {
            muroOriginal.SetActive(true);
            muroOriginal = null;
        }

        if (segmentosRoot != null)
        {
            Destroy(segmentosRoot);
            segmentosRoot = null;
        }
    }

    private void OnDestroy()
    {
        RestaurarMuro();
    }

    /// <summary>
    /// Aplica el corte del hueco en el muro seleccionado y ajusta la posición y rotación
    /// del objeto para que quede empotrado y rodeado por la pared según su tamaño exacto.
    /// </summary>
    public bool AplicarHueco(GameObject wallObj, GameObject aberturaObj, Vector3 aberturaPos, bool esVentana, Vector3 wallNormal, out Vector3 posCentradaEnHueco, out Quaternion rotacionMuro)
    {
        posCentradaEnHueco = aberturaPos;
        rotacionMuro = Quaternion.identity;

        if (wallObj == null || aberturaObj == null) return false;

        // Si ya teníamos cortado otro muro (o el mismo en otra posición), restaurar primero
        RestaurarMuro();

        Transform wt = wallObj.transform;
        Vector3 wallPos = wt.position;
        Quaternion wallRot = wt.rotation;
        Vector3 wallScale = wt.lossyScale;

        // Determinar qué eje horizontal local es la longitud del muro (el mayor entre X y Z)
        bool longitudEsX = wallScale.x >= wallScale.z;
        float longitudMuro = longitudEsX ? wallScale.x : wallScale.z;
        float espesorMuro = longitudEsX ? wallScale.z : wallScale.x;
        float alturaMuro = wallScale.y;

        // Eje longitudinal del muro en el mundo (siempre consistente con la rotación del muro)
        Vector3 ejeLongitudinal = (longitudEsX ? wt.right : wt.forward).normalized;
        ejeLongitudinal.y = 0f;
        ejeLongitudinal.Normalize();

        // Base y tope vertical del muro en el mundo
        float yMuroBase = wallPos.y - (alturaMuro * 0.5f);
        float yMuroTope = wallPos.y + (alturaMuro * 0.5f);

        // Medir dimensiones físicas reales de la abertura según sus Renderers
        float anchoAbertura = esVentana ? 1.4f : 1.25f;
        float altoAbertura = esVentana ? 1.55f : 2.25f;

        Renderer[] rends = aberturaObj.GetComponentsInChildren<Renderer>();
        if (rends != null && rends.Length > 0)
        {
            Bounds bTotal = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i].enabled) bTotal.Encapsulate(rends[i].bounds);
            }

            // Proyección horizontal del tamaño de la abertura sobre el eje longitudinal del muro
            Vector3 ext = bTotal.extents;
            float projRadius = Mathf.Abs(Vector3.Dot(new Vector3(ext.x, 0f, 0f), ejeLongitudinal))
                             + Mathf.Abs(Vector3.Dot(new Vector3(0f, 0f, ext.z), ejeLongitudinal));
            if (projRadius > 0.25f)
                anchoAbertura = projRadius * 2f;

            if (bTotal.size.y > 0.4f)
                altoAbertura = bTotal.size.y;
        }

        // Si el muro es más angosto que la abertura, no cortar
        if (longitudMuro < anchoAbertura * 0.5f) return false;

        // Altura de la base de la abertura sobre la base del muro
        float baseAberturaY = 0f;
        if (esVentana)
        {
            // Para ventana: colocar antepecho debajo, asegurando que quede espacio de dintel arriba
            float deseado = aberturaPos.y - yMuroBase;
            float maxBase = alturaMuro - altoAbertura - 0.15f;
            baseAberturaY = Mathf.Clamp(deseado, 0.4f, Mathf.Max(0.4f, maxBase));
        }

        // Desfasaje a lo largo del muro (en metros desde el centro del muro)
        float posL = Vector3.Dot(aberturaPos - wallPos, ejeLongitudinal);

        // Rango de corte a lo largo del eje longitudinal (clampeado dentro del muro con margen)
        float mitadLong = longitudMuro * 0.5f;
        float halfAncho = anchoAbertura * 0.5f;
        float margen = 0.04f;

        float centerL_Hueco = Mathf.Clamp(posL, -mitadLong + halfAncho + margen, mitadLong - halfAncho - margen);
        float cutMin = centerL_Hueco - halfAncho;
        float cutMax = centerL_Hueco + halfAncho;

        if (cutMax <= cutMin) return false;

        float anchoHueco = cutMax - cutMin;

        // Límites verticales de la abertura en el mundo
        float yAbMin = yMuroBase + baseAberturaY;
        float yAbMax = Mathf.Min(yMuroTope, yAbMin + altoAbertura);

        // Material del muro original
        Renderer wallRend = wallObj.GetComponent<Renderer>();
        if (wallRend == null) wallRend = wallObj.GetComponentInChildren<Renderer>();
        Material wallMat = wallRend != null ? wallRend.sharedMaterial : null;

        // Contenedor de segmentos (sin escala para no distorsionar hijos)
        segmentosRoot = new GameObject(wallObj.name + "_Hueco");
        segmentosRoot.transform.position = Vector3.zero;
        segmentosRoot.transform.rotation = Quaternion.identity;
        segmentosRoot.transform.localScale = Vector3.one;
        if (wt.parent != null)
            segmentosRoot.transform.SetParent(wt.parent, true);

        // 1. Segmento Izquierdo
        float anchoIzq = cutMin - (-mitadLong);
        if (anchoIzq > 0.02f)
        {
            float centerL = (-mitadLong + cutMin) * 0.5f;
            Vector3 centerPos = wallPos + (ejeLongitudinal * centerL);
            centerPos.y = wallPos.y; // misma altura media del muro
            Vector3 scale = longitudEsX
                ? new Vector3(anchoIzq, alturaMuro, espesorMuro)
                : new Vector3(espesorMuro, alturaMuro, anchoIzq);
            CrearSegmentoEnMundo(centerPos, wallRot, scale, wallMat, wallObj);
        }

        // 2. Segmento Derecho
        float anchoDer = mitadLong - cutMax;
        if (anchoDer > 0.02f)
        {
            float centerL = (cutMax + mitadLong) * 0.5f;
            Vector3 centerPos = wallPos + (ejeLongitudinal * centerL);
            centerPos.y = wallPos.y;
            Vector3 scale = longitudEsX
                ? new Vector3(anchoDer, alturaMuro, espesorMuro)
                : new Vector3(espesorMuro, alturaMuro, anchoDer);
            CrearSegmentoEnMundo(centerPos, wallRot, scale, wallMat, wallObj);
        }

        // 3. Dintel Superior (parte de la pared arriba de la puerta o ventana)
        float altoDintel = yMuroTope - yAbMax;
        if (altoDintel > 0.02f && anchoHueco > 0.02f)
        {
            Vector3 centerPos = wallPos + (ejeLongitudinal * centerL_Hueco);
            centerPos.y = (yAbMax + yMuroTope) * 0.5f;
            Vector3 scale = longitudEsX
                ? new Vector3(anchoHueco, altoDintel, espesorMuro)
                : new Vector3(espesorMuro, altoDintel, anchoHueco);
            CrearSegmentoEnMundo(centerPos, wallRot, scale, wallMat, wallObj);
        }

        // 4. Antepecho Inferior (muro debajo de la ventana)
        if (esVentana)
        {
            float altoAntepecho = yAbMin - yMuroBase;
            if (altoAntepecho > 0.02f && anchoHueco > 0.02f)
            {
                Vector3 centerPos = wallPos + (ejeLongitudinal * centerL_Hueco);
                centerPos.y = (yMuroBase + yAbMin) * 0.5f;
                Vector3 scale = longitudEsX
                    ? new Vector3(anchoHueco, altoAntepecho, espesorMuro)
                    : new Vector3(espesorMuro, altoAntepecho, anchoHueco);
                CrearSegmentoEnMundo(centerPos, wallRot, scale, wallMat, wallObj);
            }
        }

        // Posición exacta en el centro del hueco empotrado en el muro:
        // - En longitud: en el centro exacto del hueco (centerL_Hueco)
        // - En espesor: exactamente en el plano medio del muro (wallPos)
        // - En Y: apoyada en la base del hueco
        posCentradaEnHueco = wallPos + (ejeLongitudinal * centerL_Hueco);
        posCentradaEnHueco.y = yAbMin;

        // Ambas (puerta y ventana) tienen su ancho a lo largo de su eje local Z -> orientar Z a lo largo del muro
        Vector3 forwardDir = ejeLongitudinal;
        if (Vector3.Dot(aberturaObj.transform.forward, forwardDir) < 0f)
            forwardDir = -forwardDir;
        rotacionMuro = Quaternion.LookRotation(forwardDir, Vector3.up);

        // Ocultar muro sólido original
        muroOriginal = wallObj;
        wallObj.SetActive(false);

        return true;
    }

    private GameObject CrearSegmentoEnMundo(Vector3 worldPos, Quaternion worldRot, Vector3 scale, Material wallMat, GameObject wallOriginal)
    {
        GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seg.name = wallOriginal.name + "_Segmento";
        seg.layer = wallOriginal.layer;
        seg.tag = wallOriginal.tag;
        seg.transform.SetParent(segmentosRoot.transform, false);
        seg.transform.position = worldPos;
        seg.transform.rotation = worldRot;
        seg.transform.localScale = scale;

        Renderer rend = seg.GetComponent<Renderer>();
        if (rend != null && wallMat != null)
        {
            rend.sharedMaterial = wallMat;
        }

        // Preservar capacidad de pintura de muros si correspondía
        MuroEditable originalEditable = wallOriginal.GetComponent<MuroEditable>();
        if (originalEditable != null)
        {
            seg.AddComponent<MuroEditable>();
        }

        SceneElementMetadata meta = seg.AddComponent<SceneElementMetadata>();
        meta.elementId = seg.name;
        meta.elementType = "muro";
        meta.jsonData = "";

        return seg;
    }
}
