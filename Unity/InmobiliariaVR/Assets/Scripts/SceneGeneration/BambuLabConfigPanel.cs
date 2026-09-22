using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Panel de configuración de Bambu Lab - Permite al usuario ingresar IP, Access Code y
/// Serial Number (Device ID) de su impresora.
///
/// CORRECCIÓN (2026-09-19, verificado contra documentación real del protocolo LAN de Bambu
/// Lab -- ver BambuLabMQTT.cs para el detalle): el Serial Number es OBLIGATORIO porque los
/// topics MQTT de Bambu Lab son "device/{SERIAL}/report" y "device/{SERIAL}/request" -- un
/// wildcard "+" (como se usaba antes) no es válido para el topic de publish y la impresora
/// simplemente ignora el mensaje. El Access Code tampoco es de 5 dígitos fijos como se asumía
/// antes -- Bambu Lab usa códigos alfanuméricos de longitud variable según el modelo, así que
/// la validación aquí solo exige que no esté vacío.
///
/// Todo se guarda en PlayerPrefs para persistencia entre sesiones.
/// </summary>
public class BambuLabConfigPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelBackground;
    [SerializeField] private TextMeshProUGUI tituloPanel;

    // Campos de entrada
    [SerializeField] private TMP_InputField inputIP;
    [SerializeField] private TMP_InputField inputAccessCode;
    [SerializeField] private TMP_InputField inputSerialNumber;

    // Botones
    [SerializeField] private Button botonGuardar;
    [SerializeField] private Button botonProbar;
    [SerializeField] private Button botonCancelar;
    [SerializeField] private Button botonLimpiar;

    // Feedback
    [SerializeField] private TextMeshProUGUI textoEstado;
    [SerializeField] private Image imagenEstado;

    // Referencias
    private BambuLabMQTT bambuMQTT;

    /// <summary>
    /// Expone la instancia de BambuLabMQTT que vive en este mismo GameObject. CrudPanelController
    /// la usa para no crear su propia copia (ver nota en BambuLabMQTT.cs sobre por qué crear una
    /// segunda instancia del lado de CrudPanelController rompía este mismo panel de configuración).
    /// </summary>
    public BambuLabMQTT BambuMqtt => bambuMQTT;

    private Color colorExito = new Color(0.2f, 0.8f, 0.2f, 1f);
    private Color colorError = new Color(0.8f, 0.2f, 0.2f, 1f);
    private Color colorInfo = new Color(0.2f, 0.6f, 0.8f, 1f);

    // Constantes de almacenamiento
    private const string PREFS_IP = "BambuLab_IP";
    private const string PREFS_CODE = "BambuLab_AccessCode";
    private const string PREFS_SERIAL = "BambuLab_SerialNumber";

    private void Start()
    {
        bambuMQTT = GetComponent<BambuLabMQTT>();
        if (bambuMQTT == null)
            bambuMQTT = gameObject.AddComponent<BambuLabMQTT>();

        // Configurar botones
        botonGuardar.onClick.AddListener(GuardarConfiguracion);
        botonProbar.onClick.AddListener(ProbarConexion);
        botonCancelar.onClick.AddListener(CerrarPanel);
        botonLimpiar.onClick.AddListener(LimpiarCampos);

        // Cargar valores guardados
        CargarConfiguracion();

        // Inicialmente oculto
        panelBackground.SetActive(false);
    }

    /// <summary>
    /// Abre el panel de configuración
    /// </summary>
    public void AbrirPanel()
    {
        panelBackground.SetActive(true);
        CargarConfiguracion();
        MostrarMensaje("Ingresa IP, Access Code y Serial Number de tu Bambu Lab", colorInfo);
    }

    /// <summary>
    /// Cierra el panel
    /// </summary>
    public void CerrarPanel()
    {
        panelBackground.SetActive(false);
    }

    /// <summary>
    /// Carga la configuración guardada en PlayerPrefs
    /// </summary>
    private void CargarConfiguracion()
    {
        string ipGuardada = PlayerPrefs.GetString(PREFS_IP, "192.168.1.100");
        string codeGuardado = PlayerPrefs.GetString(PREFS_CODE, "");
        string serialGuardado = PlayerPrefs.GetString(PREFS_SERIAL, "");

        inputIP.text = ipGuardada;
        inputAccessCode.text = codeGuardado;
        if (inputSerialNumber != null)
            inputSerialNumber.text = serialGuardado;

        if (!string.IsNullOrEmpty(codeGuardado))
            MostrarMensaje("✅ Configuración cargada", colorExito);
    }

    /// <summary>
    /// Valida y guarda la configuración
    /// </summary>
    private void GuardarConfiguracion()
    {
        string ip = inputIP.text.Trim();
        string code = inputAccessCode.text.Trim();
        string serial = inputSerialNumber != null ? inputSerialNumber.text.Trim() : "";

        // Validar IP
        if (!ValidarIP(ip))
        {
            MostrarMensaje("❌ IP inválida. Ej: 192.168.1.100", colorError);
            return;
        }

        // Validar Access Code (no vacío -- Bambu Lab usa longitudes variables según modelo)
        if (!ValidarAccessCode(code))
        {
            MostrarMensaje("❌ Ingresa el Access Code de tu impresora", colorError);
            return;
        }

        // Validar Serial Number (obligatorio para los topics MQTT -- ver BambuLabMQTT.cs)
        if (!ValidarSerialNumber(serial))
        {
            MostrarMensaje("❌ Ingresa el Serial Number (Device ID) de tu impresora.\nEstá en Configuración → Acerca de.", colorError);
            return;
        }

        // Guardar en PlayerPrefs
        PlayerPrefs.SetString(PREFS_IP, ip);
        PlayerPrefs.SetString(PREFS_CODE, code);
        PlayerPrefs.SetString(PREFS_SERIAL, serial);
        PlayerPrefs.Save();

        MostrarMensaje($"✅ Guardado: {ip}", colorExito);

        // Cerrar después de 1.5 segundos
        Invoke(nameof(CerrarPanel), 1.5f);
    }

    /// <summary>
    /// Prueba la conexión sin guardar
    /// </summary>
    private void ProbarConexion()
    {
        string ip = inputIP.text.Trim();
        string code = inputAccessCode.text.Trim();
        string serial = inputSerialNumber != null ? inputSerialNumber.text.Trim() : "";

        // Validar primero
        if (!ValidarIP(ip))
        {
            MostrarMensaje("❌ IP inválida", colorError);
            return;
        }

        if (!ValidarAccessCode(code))
        {
            MostrarMensaje("❌ Ingresa el Access Code", colorError);
            return;
        }

        if (!ValidarSerialNumber(serial))
        {
            MostrarMensaje("❌ Ingresa el Serial Number (Device ID)", colorError);
            return;
        }

        // Intentar conexión
        MostrarMensaje("🔄 Probando conexión (TLS)...", colorInfo);

        botonProbar.interactable = false;
        StartCoroutine(ProbarConexionCorrutina(ip, code, serial));
    }

    /// <summary>
    /// Corrutina para probar conexión sin bloquear UI
    /// </summary>
    private IEnumerator ProbarConexionCorrutina(string ip, string code, string serial)
    {
        bool exito = false;
        string mensaje = "";

        try
        {
            // Intentar conectar (MQTT sobre TLS, puerto 8883, usuario "bblp" -- ver BambuLabMQTT.cs)
            bambuMQTT.ConnectToLocalPrinter(ip, code, serial);
        }
        catch (System.Exception ex)
        {
            exito = false;
            mensaje = $"❌ Error: {ex.Message}";
            Debug.LogError($"Error probando conexión Bambu: {ex}");
            MostrarMensaje(mensaje, colorError);
            botonProbar.interactable = true;
            yield break;
        }

        // Esperar hasta 5 segundos para que el handshake TLS + CONNACK complete
        float tiempoEspera = 0f;
        while (tiempoEspera < 5f && !bambuMQTT.IsConnected())
        {
            tiempoEspera += Time.deltaTime;
            yield return null;
        }

        if (bambuMQTT.IsConnected())
        {
            exito = true;
            mensaje = $"✅ ¡Conexión exitosa!\n{ip}";
        }
        else
        {
            exito = false;
            mensaje = $"❌ No se conectó a {ip}\nVerifica IP, Access Code y Serial Number,\ny que ambos estén en la misma WiFi.";
        }

        // Mostrar resultado
        MostrarMensaje(mensaje, exito ? colorExito : colorError);
        botonProbar.interactable = true;

        // Desconectar después del test
        bambuMQTT.Disconnect();
    }

    /// <summary>
    /// Limpia los campos del formulario
    /// </summary>
    private void LimpiarCampos()
    {
        inputIP.text = "";
        inputAccessCode.text = "";
        if (inputSerialNumber != null)
            inputSerialNumber.text = "";
        MostrarMensaje("Campos limpios", colorInfo);
    }

    /// <summary>
    /// Valida el formato de IP (xxx.xxx.xxx.xxx)
    /// </summary>
    private bool ValidarIP(string ip)
    {
        if (string.IsNullOrEmpty(ip))
            return false;

        string[] partes = ip.Split('.');
        if (partes.Length != 4)
            return false;

        foreach (string parte in partes)
        {
            if (!int.TryParse(parte, out int numero))
                return false;

            if (numero < 0 || numero > 255)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Valida el Access Code. Bambu Lab usa códigos alfanuméricos cuya longitud varía según
    /// el modelo de impresora (no siempre son 5 dígitos, corrección respecto a la versión
    /// anterior de este archivo) -- solo se exige que no esté vacío y tenga una longitud
    /// razonable.
    /// </summary>
    private bool ValidarAccessCode(string code)
    {
        if (string.IsNullOrEmpty(code))
            return false;

        return code.Length >= 4 && code.Length <= 24;
    }

    /// <summary>
    /// Valida el Serial Number / Device ID. Es alfanumérico (ej: "01P00A000000000"), longitud
    /// variable -- solo se exige que no esté vacío.
    /// </summary>
    private bool ValidarSerialNumber(string serial)
    {
        return !string.IsNullOrEmpty(serial) && serial.Length >= 4;
    }

    /// <summary>
    /// Muestra mensaje de estado con color
    /// </summary>
    private void MostrarMensaje(string mensaje, Color color)
    {
        textoEstado.text = mensaje;
        imagenEstado.color = color;
    }

    /// <summary>
    /// Obtiene la IP guardada
    /// </summary>
    public string ObtenerIPGuardada()
    {
        return PlayerPrefs.GetString(PREFS_IP, "");
    }

    /// <summary>
    /// Obtiene el Access Code guardado
    /// </summary>
    public string ObtenerAccessCodeGuardado()
    {
        return PlayerPrefs.GetString(PREFS_CODE, "");
    }

    /// <summary>
    /// Obtiene el Serial Number (Device ID) guardado
    /// </summary>
    public string ObtenerSerialNumberGuardado()
    {
        return PlayerPrefs.GetString(PREFS_SERIAL, "");
    }

    /// <summary>
    /// Verifica si hay configuración guardada (IP + Access Code + Serial Number)
    /// </summary>
    public bool TieneConfiguracion()
    {
        return !string.IsNullOrEmpty(ObtenerIPGuardada()) &&
               !string.IsNullOrEmpty(ObtenerAccessCodeGuardado()) &&
               !string.IsNullOrEmpty(ObtenerSerialNumberGuardado());
    }
}
