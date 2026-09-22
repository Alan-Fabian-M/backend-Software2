using System;
using System.Collections.Generic;
using UnityEngine;

// Estructura serializable con el diseno personalizado por el cliente:
// muebles colocados y acabados (color/material) elegidos por superficie.
// Version simplificada de MRAnchoredLayoutData de Room Designer, sin
// OVRSpatialAnchor porque nuestro inmueble ya viene modelado (no se escanea
// un cuarto real).
[Serializable]
public class DesignLayoutData
{
    public List<FurnitureData> furniture = new List<FurnitureData>();
    public List<SurfaceData> surfaces = new List<SurfaceData>();
}

[Serializable]
public class FurnitureData
{
    public string modelId;
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}

[Serializable]
public class SurfaceData
{
    public string surfaceTag; // ej. "Wall", "Floor", "Ceiling"
    public int materialIndex; // indice dentro de MaterialChangerVR.materials
}
