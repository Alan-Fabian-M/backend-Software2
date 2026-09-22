using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// Consume el backend de Vision Artificial (backend/, FastAPI + YOLOv8 + OpenCV + Tesseract OCR)
// que lee una foto/croquis, detecta muebles y paredes, lee la escala escrita a mano si la hay,
// y devuelve un Scene Graph JSON -- el MISMO schema que ya usan los presets y SceneGenerator.cs
// (ver InmobiliariaVR/Docs/Backend_Vision_Artificial.md y el schema documentado en el proyecto
// de Claude como json-schema-contrato-escenas.md).
//
// Es la evolucion de este mismo script cuando todavia le pegaba al prototipo standalone de
// SpatialSceneCompiler/ (schema viejo, 5 muebles fijos por Transform). Esa version quedo
// superada (ver SpatialSceneCompiler/README.md): ahora la respuesta del servidor se manda
// directo a SceneGenerator.GenerateSceneAsync, igual que un preset o una escena guardada, asi
// que soporta cualquier cantidad/tipo de muebles y no depende de referencias fijas en el
// Inspector.
//
// LIMITACIONES CONOCIDAS:
// - No hay (todavia) un modelo YOLO-OBB de muebles entrenado, asi que la rotacion que devuelve
//   el servidor siempre viene en 0 grados -- el usuario ajusta a mano en VR con el menu de
//   mover/rotar (FurnitureManipulatorVR). Ver backend/training/cubicasa/ para el pipeline ya
//   armado para entrenar ese modelo a futuro.
public class CroquisSceneCompilerController : MonoBehaviour
{
    private const string PLAYERPREFS_KEY_IP = "CroquisCompiler_ServidorIP";
    private const string DEFAULT_IP = "localhost";
    private const int DEFAULT_PUERTO = 8000;
    private const string DEFAULT_ENDPOINT = "/api/v1/compilar-sala";

    [Header("Servidor del backend de Vision Artificial (misma red WiFi que el Quest, sin cable)")]
    [Tooltip("IP local de la PC/servidor que corre 'uvicorn app.main:app --port 8000'. " +
             "Se puede cambiar en runtime con el InputField (campoIPServidor) o SetServidorIP().")]
    public string servidorIP = DEFAULT_IP;
    public int servidorPuerto = DEFAULT_PUERTO;
    public string endpoint = DEFAULT_ENDPOINT;

    [Header("UI (opcional)")]
    public Text textoEstado;
    [Tooltip("Texto del boton 'Cargar Croquis...', para mostrar el nombre del archivo elegido")]
    public Text textoBotonCargar;
    [Tooltip("InputField opcional para que el usuario escriba/edite la IP del servidor sin recompilar")]
    public InputField campoIPServidor;

    [Header("[LEGACY] Referencias fijas -- ya NO se usan (se dejan solo por compatibilidad " +
            "con escenas viejas que las tengan asignadas; el flujo actual usa SceneGenerator)")]
    public Transform sofa;
    public Transform mesa;
    public Transform tv;
    public Transform cama;
    public Transform silla;

    private byte[] croquisCargadoBytes;
    private string croquisCargadoNombre;
    private SceneGenerator sceneGenerator;

    private void Awake()
    {
        // Restaura la IP guardada en una sesion anterior, si el usuario la habia cambiado.
        servidorIP = PlayerPrefs.GetString(PLAYERPREFS_KEY_IP, servidorIP);

        if (campoIPServidor != null)
        {
            campoIPServidor.text = servidorIP;
            campoIPServidor.onEndEdit.AddListener(SetServidorIP);
        }
    }

    private SceneGenerator EnsureSceneGenerator()
    {
        if (sceneGenerator == null)
            sceneGenerator = UnityEngine.Object.FindAnyObjectByType<SceneGenerator>();

        if (sceneGenerator == null)
        {
            GameObject managerGO = new GameObject("SceneGeneratorManager");
            sceneGenerator = managerGO.AddComponent<SceneGenerator>();
        }

        return sceneGenerator;
    }

    /// <summary>Cambia la IP del servidor en runtime (por ejemplo, desde un InputField) y la recuerda para la próxima vez.</summary>
    public void SetServidorIP(string nuevaIP)
    {
        if (string.IsNullOrWhiteSpace(nuevaIP)) return;
        servidorIP = nuevaIP.Trim();
        PlayerPrefs.SetString(PLAYERPREFS_KEY_IP, servidorIP);
        PlayerPrefs.Save();
        SetEstado($"Servidor configurado en: {GetServidorUrlActual()}");
    }

    public void RestaurarIPPorDefecto()
    {
        SetServidorIP(DEFAULT_IP);
        if (campoIPServidor != null) campoIPServidor.text = servidorIP;
    }

    public string GetServidorUrlActual() => $"http://{servidorIP}:{servidorPuerto}{endpoint}";

    // ------------------------------------------------------------------
    // Carga de la foto del croquis: Editor (panel de archivos) vs Android
    // (selector nativo de fotos del sistema, vía GalleryPickerActivity).
    // ------------------------------------------------------------------

    // Llamar desde el boton "Cargar Croquis...".
    public void CargarCroquisDesdeArchivo()
    {
#if UNITY_EDITOR
        string ruta = UnityEditor.EditorUtility.OpenFilePanel("Elegir foto del croquis", "", "png,jpg,jpeg");
        if (string.IsNullOrEmpty(ruta)) return;

        CargarDesdeRutaLocal(ruta);
#elif UNITY_ANDROID
        AbrirSelectorDeGaleriaAndroid();
#else
        SetEstado("Cargar croquis desde archivo no está soportado en esta plataforma.");
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// Abre el selector de fotos nativo de Android (Photo Picker, sin pedir permisos) a través
    /// del plugin GalleryPickerActivity (Assets/Plugins/Android/). El resultado vuelve de forma
    /// asincrónica a OnFotoSeleccionadaDesdeGaleria / OnFotoCanceladaDesdeGaleria via
    /// UnitySendMessage, así que este método no bloquea ni devuelve nada directamente.
    ///
    /// NOTA: este plugin nativo no pudo compilarse/probarse en este entorno de desarrollo (no
    /// hay Android SDK/Gradle acá) -- ver GalleryPickerActivity.java y AndroidManifest.xml en
    /// Assets/Plugins/Android/ para el detalle de qué falta verificar con una build real.
    /// </summary>
    private void AbrirSelectorDeGaleriaAndroid()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var plugin = new AndroidJavaClass("com.inmobiliariavr.galleryplugin.GalleryPickerActivity"))
            {
                plugin.CallStatic("abrirSelectorDeImagen", activity, gameObject.name,
                    nameof(OnFotoSeleccionadaDesdeGaleria), nameof(OnFotoCanceladaDesdeGaleria));
            }
            SetEstado("Abriendo galería...");
        }
        catch (Exception ex)
        {
            SetEstado("No se pudo abrir la galería nativa: " + ex.Message);
        }
    }
#endif

    /// <summary>
    /// Llamado por GalleryPickerActivity (Java) vía UnitySendMessage cuando el usuario elige
    /// una foto. Debe ser público, sin valor de retorno y recibir un único string (requisito de
    /// UnitySendMessage) -- recibe la ruta local del archivo ya copiado por el plugin nativo.
    /// </summary>
    public void OnFotoSeleccionadaDesdeGaleria(string rutaArchivo)
    {
        if (string.IsNullOrEmpty(rutaArchivo))
        {
            SetEstado("La galería no devolvió ninguna imagen.");
            return;
        }
        CargarDesdeRutaLocal(rutaArchivo);
    }

    /// <summary>Llamado por GalleryPickerActivity (Java) cuando el usuario cancela o hay un error.</summary>
    public void OnFotoCanceladaDesdeGaleria(string motivo)
    {
        SetEstado(string.IsNullOrEmpty(motivo) ? "Selección de foto cancelada." : $"No se pudo obtener la foto ({motivo}).");
    }

    private void CargarDesdeRutaLocal(string ruta)
    {
        try
        {
            croquisCargadoBytes = System.IO.File.ReadAllBytes(ruta);
            croquisCargadoNombre = System.IO.Path.GetFileName(ruta);
            if (textoBotonCargar != null) textoBotonCargar.text = "Cargado: " + croquisCargadoNombre;
            SetEstado("Croquis cargado (" + croquisCargadoNombre + "). Apretá 'Generar' para procesarlo.");
        }
        catch (Exception ex)
        {
            SetEstado("No se pudo leer el archivo elegido: " + ex.Message);
        }
    }

    // Llamar desde el boton "Generar desde Croquis" -- usa el ultimo archivo cargado con CargarCroquisDesdeArchivo().
    public void GenerarDesdeCroquisCargado()
    {
        if (croquisCargadoBytes == null)
        {
            SetEstado("Primero cargá una foto del croquis con el botón de arriba.");
            return;
        }
        EnviarCroquis(croquisCargadoBytes, croquisCargadoNombre);
    }

    public void EnviarCroquis(byte[] imagenBytes, string nombreArchivo = "croquis.jpg")
    {
        StartCoroutine(ConsultarServidor(imagenBytes, nombreArchivo));
    }

    private IEnumerator ConsultarServidor(byte[] imagenBytes, string nombreArchivo)
    {
        SetEstado("Analizando el croquis (YOLO + OpenCV + OCR)...");

        var formSections = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", imagenBytes, nombreArchivo, "image/jpeg")
        };

        string url = GetServidorUrlActual();

        using (var www = UnityWebRequest.Post(url, formSections))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                SetEstado($"Error de conexión con el servidor ({url}): {www.error} " +
                          "(¿está corriendo 'uvicorn app.main:app --host 0.0.0.0 --port 8000' " +
                          "y el Quest está en la misma red WiFi?)");
                yield break;
            }

            // A partir de acá el JSON del servidor va DIRECTO a SceneGenerator, igual que un
            // preset o una escena guardada -- mismo schema, mismo camino, sin traducciones ni
            // Transforms fijos de por medio.
            string json = www.downloadHandler.text;
            var generator = EnsureSceneGenerator();

            generator.OnComplete += OnGeneracionCompleta;
            generator.OnError += OnGeneracionError;

            SetEstado("Generando la sala a partir del croquis...");
            generator.GenerateSceneAsync(json);
        }
    }

    private void OnGeneracionCompleta(bool exito, string mensaje)
    {
        sceneGenerator.OnComplete -= OnGeneracionCompleta;
        sceneGenerator.OnError -= OnGeneracionError;
        SetEstado("✓ " + mensaje);
    }

    private void OnGeneracionError(string errorMensaje)
    {
        sceneGenerator.OnComplete -= OnGeneracionCompleta;
        sceneGenerator.OnError -= OnGeneracionError;
        SetEstado("✗ Error generando la escena: " + errorMensaje);
    }

    private void SetEstado(string mensaje)
    {
        if (textoEstado != null) textoEstado.text = mensaje;
        Debug.Log("[CroquisSceneCompilerController] " + mensaje);
    }
}
