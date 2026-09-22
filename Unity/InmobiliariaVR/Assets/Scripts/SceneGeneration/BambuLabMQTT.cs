using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

// La librería M2Mqtt se incluye como código fuente en Assets/Plugins/M2Mqtt/ (NO como paquete
// de Package Manager -- el repo oficial de Eclipse no tiene package.json y no se puede instalar
// como paquete UPM vía git URL). El símbolo de compilación "SSL" está activado en Player
// Settings → Scripting Define Symbols para Android y Standalone (necesario para que M2Mqtt
// compile su soporte de TLS -- sin ese símbolo, MqttNetworkChannel ignora sslProtocol y jamás
// usa TLS aunque se lo pidamos aquí).
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;

/// <summary>
/// Conexión MQTT a Bambu Lab en modo LAN, y envío del comando de impresión.
///
/// CORRECCIÓN IMPORTANTE (2026-09-19) respecto a la versión anterior de este archivo -- se
/// verificó el protocolo real contra documentación técnica (https://github.com/Doridian/OpenBambuAPI)
/// y tenía 3 errores que le habrían impedido funcionar:
///   1. Usaba usuario "bic" -- el usuario real y fijo para TODAS las impresoras Bambu es "bblp".
///   2. Conectaba SIN TLS (MqttSslProtocols.None) -- Bambu Lab exige TLS en el puerto 8883,
///      con un certificado autofirmado (por eso aquí se acepta cualquier certificado a
///      propósito, igual que hace Bambu Studio en modo LAN).
///   3. Los topics usaban un wildcard "+" en vez del Serial Number (Device ID) real de la
///      impresora -- un publish a un topic con "+" no es válido y la impresora lo ignora.
///
/// Además, el archivo de impresión YA NO se manda embebido en el mensaje MQTT (la versión
/// anterior lo metía en base64 truncado a 1000 caracteres, lo cual nunca habría transferido un
/// archivo real). Bambu Lab transfiere archivos por FTPS (puerto 990, ver BambuLabFTPS.cs) y
/// luego usa MQTT solo para el comando "project_file" que le dice a la impresora cuál de los
/// archivos ya subidos debe imprimir.
///
/// LIMITACIÓN DE FONDO QUE ESTO NO RESUELVE (y no se puede resolver solo con código de
/// conexión): la impresora ejecuta G-code, no mallas 3D -- el archivo que se sube y manda a
/// imprimir aquí debe ser un .gcode.3mf YA SLICEADO (por ejemplo con Bambu Studio), no el STL
/// crudo que exporta STLExporter. Ver CrudPanelController.EnviarABambuLab() para cómo se
/// resuelve esto en la UI (busca un .gcode.3mf pre-sliceado en vez de intentar imprimir el STL
/// directamente).
/// </summary>
public class BambuLabMQTT : MonoBehaviour
{
    private const int MQTT_PORT = 8883;
    private const string MQTT_USER = "bblp"; // fijo para todas las impresoras Bambu Lab

    private MqttClient mqttClient;
    private string ultimaIP = "";
    // Access Code usado en la última conexión -- se reutiliza para el login FTPS al imprimir
    // (Bambu Lab usa las mismas credenciales para MQTT y FTPS: usuario "bblp" + Access Code).
    private string ultimoAccessCodeUsado = "";
    private string ultimoSerial = "";
    private bool estaConectado = false;
    private string ultimoMensaje = "";

    private BambuLabFTPS ftpsClient = new BambuLabFTPS();

    // NOTA (2026-09-19): este componente ya NO usa un patrón singleton con
    // Destroy(gameObject) -- esa versión anterior destruía el GameObject COMPLETO del panel de
    // configuración (con toda su UI: inputs, botones, textos) apenas se creaba una segunda
    // instancia, lo cual pasaba en cuanto CrudPanelController y BambuLabConfigPanel intentaban
    // cada uno crear su propia copia. La solución real es que exista una sola instancia (la que
    // vive junto a BambuLabConfigPanel, ver ese archivo) y que el resto del código la busque en
    // vez de crear la suya -- ver CrudPanelController.BuildUI().

    /// <summary>
    /// Conecta a la impresora Bambu Lab por MQTT sobre TLS (puerto 8883).
    /// </summary>
    /// <param name="ip">IP local de la impresora.</param>
    /// <param name="accessCode">Access Code (Configuración → Red / Acerca de en la impresora).</param>
    /// <param name="serialNumber">Serial Number / Device ID de la impresora -- necesario para
    /// los topics MQTT ("device/{serial}/report" y "device/{serial}/request").</param>
    public void ConnectToLocalPrinter(string ip, string accessCode, string serialNumber)
    {
        if (string.IsNullOrEmpty(serialNumber))
            throw new ArgumentException("Falta el Serial Number (Device ID) de la impresora.");

        if (estaConectado)
        {
            Debug.LogWarning("Ya conectado a Bambu Lab");
            return;
        }

        try
        {
            ultimaIP = ip;
            ultimoSerial = serialNumber;
            ultimoAccessCodeUsado = accessCode; // se reutiliza para el login FTPS al imprimir

            Debug.Log($"📡 Intentando conectar a Bambu Lab: {ip}:{MQTT_PORT} (TLS)");

            // TLS 1.2, certificado autofirmado aceptado a propósito (ValidarCertificado
            // siempre retorna true -- igual que hace Bambu Studio en modo LAN).
            mqttClient = new MqttClient(ip, MQTT_PORT, true, MqttSslProtocols.TLSv1_2,
                ValidarCertificado, null);

            mqttClient.MqttMsgPublishReceived += OnMqttMessageReceived;
            mqttClient.MqttMsgSubscribed += OnMqttSubscribed;
            mqttClient.ConnectionClosed += OnMqttConnectionClosed;

            string clientId = $"InmobiliariaVR_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            byte connAck = mqttClient.Connect(clientId, MQTT_USER, accessCode);
            if (connAck != MqttMsgConnack.CONN_ACCEPTED)
            {
                estaConectado = false;
                ultimoMensaje = $"❌ CONNACK rechazado (code {connAck}) -- revisa el Access Code";
                Debug.LogError(ultimoMensaje);
                return;
            }

            // Topics reales de Bambu Lab: device/{SERIAL}/report (recibir) y
            // device/{SERIAL}/request (publicar comandos) -- NO admiten wildcard "+" para publish.
            string topicReport = $"device/{serialNumber}/report";
            mqttClient.Subscribe(new[] { topicReport }, new[] { MqttMsgBase.QOS_LEVEL_AT_MOST_ONCE });

            estaConectado = true;
            ultimoMensaje = $"✅ Conectado a {ip}";
            Debug.Log($"✅ Conectado exitosamente a Bambu Lab: {ip} (serial {serialNumber})");
        }
        catch (Exception ex)
        {
            estaConectado = false;
            ultimoMensaje = $"❌ Error de conexión: {ex.Message}";
            Debug.LogError($"❌ Error conectando a Bambu Lab: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Certificado autofirmado de la impresora -- se acepta siempre a propósito. Bambu Lab
    /// no expone un certificado firmado por una CA pública en modo LAN, así que la validación
    /// estándar de .NET siempre lo rechazaría; Bambu Studio hace lo mismo internamente.
    /// </summary>
    private bool ValidarCertificado(object sender, X509Certificate certificate, X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }

    /// <summary>
    /// Sube el archivo YA SLICEADO (.gcode.3mf) por FTPS y, si la subida tiene éxito, publica
    /// el comando MQTT "project_file" para que la impresora lo imprima. Debe llamarse desde una
    /// corrutina (usa yield return internamente para no bloquear el hilo principal durante la
    /// subida FTPS).
    /// </summary>
    public IEnumerator PrintFileCoroutine(string rutaArchivoLocal, Action<bool, string> onComplete)
    {
        if (!estaConectado || mqttClient == null || !mqttClient.IsConnected)
        {
            onComplete?.Invoke(false, "No conectado a Bambu Lab. Conecta primero.");
            yield break;
        }

        string nombreArchivo = System.IO.Path.GetFileName(rutaArchivoLocal);
        string accessCodeActual = ultimoAccessCodeUsado;

        BambuLabFTPS.FtpResult resultadoFtp = null;
        yield return ftpsClient.SubirArchivoCoroutine(ultimaIP, MQTT_USER, accessCodeActual,
            rutaArchivoLocal, nombreArchivo, r => resultadoFtp = r);

        if (resultadoFtp == null || !resultadoFtp.Success)
        {
            string msg = $"Error subiendo archivo por FTPS: {(resultadoFtp != null ? resultadoFtp.Message : "sin respuesta")}";
            ultimoMensaje = $"❌ {msg}";
            Debug.LogError(msg);
            onComplete?.Invoke(false, msg);
            yield break;
        }

        Debug.Log($"✅ {resultadoFtp.Message}");

        try
        {
            EnviarComandoImprimir(nombreArchivo);
            ultimoMensaje = $"✅ Comando de impresión enviado: {nombreArchivo}";
            onComplete?.Invoke(true, ultimoMensaje);
        }
        catch (Exception ex)
        {
            string msg = $"Archivo subido pero falló el comando MQTT de impresión: {ex.Message}";
            Debug.LogError(msg);
            onComplete?.Invoke(false, msg);
        }
    }

    /// <summary>
    /// Publica el comando "project_file" en device/{serial}/request. Estructura basada en el
    /// protocolo documentado de Bambu Lab (proyectos como bambulabs_api / OpenBambuAPI) -- la
    /// impresora ejecuta este comando sobre un archivo que YA está en su almacenamiento (subido
    /// por FTPS justo antes, ver PrintFileCoroutine).
    /// </summary>
    private void EnviarComandoImprimir(string nombreArchivo)
    {
        if (!estaConectado || mqttClient == null || !mqttClient.IsConnected)
            throw new InvalidOperationException("No conectado a Bambu Lab.");

        string topicRequest = $"device/{ultimoSerial}/request";
        string sequenceId = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();

        string json = "{"
            + "\"print\":{"
            + $"\"sequence_id\":\"{sequenceId}\","
            + "\"command\":\"project_file\","
            + "\"param\":\"Metadata/plate_1.gcode\","
            + $"\"url\":\"file:///sdcard/{EscaparJson(nombreArchivo)}\","
            + $"\"file\":\"{EscaparJson(nombreArchivo)}\","
            + "\"subtask_name\":\"\","
            + "\"project_id\":\"0\","
            + "\"profile_id\":\"0\","
            + "\"task_id\":\"0\","
            + "\"subtask_id\":\"0\","
            + "\"md5\":\"\","
            + "\"timelapse\":false,"
            + "\"bed_type\":\"auto\","
            + "\"bed_leveling\":true,"
            + "\"flow_cali\":false,"
            + "\"vibration_cali\":true,"
            + "\"layer_inspect\":false,"
            + "\"use_ams\":false"
            + "}}";

        Debug.Log($"📤 Publicando comando de impresión en {topicRequest}: {json}");
        mqttClient.Publish(topicRequest, Encoding.UTF8.GetBytes(json), MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);
    }

    private string EscaparJson(string s)
    {
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private void OnMqttMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        try
        {
            string topic = e.Topic;
            string message = Encoding.UTF8.GetString(e.Message);
            Debug.Log($"📩 Mensaje MQTT recibido en {topic}: {message.Substring(0, Math.Min(200, message.Length))}");
            ultimoMensaje = "📩 Respuesta recibida de la impresora";
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error procesando mensaje MQTT: {ex.Message}");
        }
    }

    private void OnMqttSubscribed(object sender, MqttMsgSubscribedEventArgs e)
    {
        Debug.Log("✅ Suscrito a topics de Bambu Lab");
    }

    private void OnMqttConnectionClosed(object sender, EventArgs e)
    {
        estaConectado = false;
        Debug.Log("⚠️ Conexión a Bambu Lab cerrada");
    }

    public bool IsConnected()
    {
        if (mqttClient == null)
            return false;

        try
        {
            return mqttClient.IsConnected;
        }
        catch
        {
            return false;
        }
    }

    public void Disconnect()
    {
        try
        {
            if (mqttClient != null && mqttClient.IsConnected)
            {
                mqttClient.Disconnect();
                Debug.Log("📡 Desconectado de Bambu Lab");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error desconectando: {ex.Message}");
        }
        finally
        {
            estaConectado = false;
        }
    }

    public string ObtenerEstado()
    {
        if (estaConectado && mqttClient?.IsConnected == true)
            return $"✅ Conectado a {ultimaIP}";
        else
            return "❌ Desconectado";
    }

    public string ObtenerUltimoMensaje()
    {
        return ultimoMensaje;
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
