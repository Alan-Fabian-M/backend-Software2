using UnityEngine;
using System;
using System.IO;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Cliente FTPS (FTP sobre TLS implícito) mínimo para subir archivos a una impresora Bambu Lab
/// en modo LAN.
///
/// POR QUÉ EXISTE ESTE ARCHIVO (2026-09-19): Bambu Lab NO transfiere el archivo de impresión
/// por MQTT (la versión anterior de este proyecto intentaba meter el STL en base64 dentro de
/// un mensaje MQTT, truncado a 1000 caracteres -- nunca hubiera funcionado). El protocolo real
/// (documentado en https://github.com/Doridian/OpenBambuAPI) es:
///   1. Subir el archivo ya sliceado (.gcode.3mf) por FTPS a ftps://{IP}:990 (TLS implícito),
///      usuario "bblp", password = Access Code.
///   2. Publicar un comando MQTT "project_file" que le dice a la impresora qué archivo (de los
///      que ya tiene en su almacenamiento) debe imprimir -- ver BambuLabMQTT.cs.
///
/// .NET's FtpWebRequest NO soporta FTPS implícito (solo FTPS explícito con AUTH TLS en el
/// puerto 21), así que este archivo implementa el protocolo FTP mínimo a mano sobre un
/// TcpClient + SslStream, que sí funciona igual en el Editor y en Android/IL2CPP.
///
/// El certificado de la impresora es autofirmado, así que la validación de certificado se
/// omite deliberadamente (igual que hace Bambu Studio al conectarse en modo LAN).
/// </summary>
public class BambuLabFTPS
{
    private const int FTPS_PORT = 990;
    private const int TIMEOUT_MS = 8000;

    public class FtpResult
    {
        public bool Success;
        public string Message;
    }

    /// <summary>
    /// Sube un archivo de forma asíncrona (en un hilo de fondo, para no congelar Unity) y
    /// entrega el resultado a través de una IEnumerator que el llamador debe iterar con
    /// StartCoroutine. Uso:
    ///   yield return StartCoroutine(ftps.SubirArchivoCoroutine(ip, "bblp", code, rutaLocal, "modelo.gcode.3mf", r => resultado = r));
    /// </summary>
    public IEnumerator SubirArchivoCoroutine(string ip, string user, string password,
        string rutaArchivoLocal, string nombreRemoto, Action<FtpResult> onComplete)
    {
        FtpResult resultado = null;
        bool terminado = false;

        Task.Run(() =>
        {
            try
            {
                resultado = SubirArchivoSincrono(ip, user, password, rutaArchivoLocal, nombreRemoto);
            }
            catch (Exception ex)
            {
                resultado = new FtpResult { Success = false, Message = $"Excepción FTPS: {ex.Message}" };
            }
            finally
            {
                terminado = true;
            }
        });

        while (!terminado)
            yield return null;

        onComplete?.Invoke(resultado);
    }

    /// <summary>
    /// Implementación síncrona y bloqueante del flujo FTPS -- se ejecuta en un hilo de fondo
    /// (ver SubirArchivoCoroutine). NO llamar directamente desde el hilo principal de Unity.
    /// </summary>
    private FtpResult SubirArchivoSincrono(string ip, string user, string password,
        string rutaArchivoLocal, string nombreRemoto)
    {
        if (!File.Exists(rutaArchivoLocal))
            return new FtpResult { Success = false, Message = $"No existe el archivo local: {rutaArchivoLocal}" };

        TcpClient controlClient = null;
        SslStream controlStream = null;
        TcpClient dataClient = null;
        SslStream dataStream = null;

        try
        {
            // ---- Canal de control: TCP + TLS implícito desde el primer byte ----
            controlClient = new TcpClient();
            controlClient.SendTimeout = TIMEOUT_MS;
            controlClient.ReceiveTimeout = TIMEOUT_MS;
            var connectTask = controlClient.ConnectAsync(ip, FTPS_PORT);
            if (!connectTask.Wait(TIMEOUT_MS))
                return new FtpResult { Success = false, Message = $"Timeout conectando a {ip}:{FTPS_PORT} (FTPS)" };

            controlStream = new SslStream(controlClient.GetStream(), false,
                (sender, cert, chain, errors) => true); // certificado autofirmado -- se acepta a propósito
            controlStream.AuthenticateAsClient(ip, null, System.Security.Authentication.SslProtocols.Tls12, false);

            string bienvenida = LeerRespuesta(controlStream);
            if (!bienvenida.StartsWith("220"))
                return new FtpResult { Success = false, Message = $"Respuesta inesperada del servidor FTPS: {bienvenida}" };

            EnviarComando(controlStream, $"USER {user}");
            string respUser = LeerRespuesta(controlStream);
            if (!respUser.StartsWith("331") && !respUser.StartsWith("230"))
                return new FtpResult { Success = false, Message = $"USER rechazado: {respUser}" };

            EnviarComando(controlStream, $"PASS {password}");
            string respPass = LeerRespuesta(controlStream);
            if (!respPass.StartsWith("230"))
                return new FtpResult { Success = false, Message = $"Login rechazado (revisa el Access Code): {respPass}" };

            EnviarComando(controlStream, "TYPE I");
            LeerRespuesta(controlStream); // 200

            EnviarComando(controlStream, "PBSZ 0");
            LeerRespuesta(controlStream); // 200

            EnviarComando(controlStream, "PROT P");
            LeerRespuesta(controlStream); // 200 -- protege también el canal de datos con TLS

            // ---- Modo pasivo: la impresora nos da IP:puerto para el canal de datos ----
            EnviarComando(controlStream, "PASV");
            string respPasv = LeerRespuesta(controlStream);
            if (!respPasv.StartsWith("227"))
                return new FtpResult { Success = false, Message = $"PASV falló: {respPasv}" };

            (string dataIp, int dataPort) = ParsePasvResponse(respPasv);

            dataClient = new TcpClient();
            dataClient.SendTimeout = TIMEOUT_MS;
            dataClient.ReceiveTimeout = TIMEOUT_MS;
            var dataConnectTask = dataClient.ConnectAsync(dataIp, dataPort);
            if (!dataConnectTask.Wait(TIMEOUT_MS))
                return new FtpResult { Success = false, Message = $"Timeout conectando canal de datos {dataIp}:{dataPort}" };

            dataStream = new SslStream(dataClient.GetStream(), false, (sender, cert, chain, errors) => true);
            dataStream.AuthenticateAsClient(ip, null, System.Security.Authentication.SslProtocols.Tls12, false);

            // ---- STOR: subir el archivo ----
            EnviarComando(controlStream, $"STOR {nombreRemoto}");
            string respStor = LeerRespuesta(controlStream);
            if (!respStor.StartsWith("150") && !respStor.StartsWith("125"))
                return new FtpResult { Success = false, Message = $"STOR rechazado: {respStor}" };

            byte[] datos = File.ReadAllBytes(rutaArchivoLocal);
            dataStream.Write(datos, 0, datos.Length);
            dataStream.Flush();

            dataStream.Close();
            dataClient.Close();
            dataStream = null;
            dataClient = null;

            string respFinal = LeerRespuesta(controlStream);
            if (!respFinal.StartsWith("226") && !respFinal.StartsWith("250"))
                return new FtpResult { Success = false, Message = $"La impresora no confirmó la subida: {respFinal}" };

            EnviarComando(controlStream, "QUIT");

            return new FtpResult { Success = true, Message = $"Archivo subido: {nombreRemoto} ({datos.Length} bytes)" };
        }
        catch (Exception ex)
        {
            return new FtpResult { Success = false, Message = $"Error FTPS: {ex.Message}" };
        }
        finally
        {
            try { dataStream?.Close(); } catch { }
            try { dataClient?.Close(); } catch { }
            try { controlStream?.Close(); } catch { }
            try { controlClient?.Close(); } catch { }
        }
    }

    private void EnviarComando(SslStream stream, string comando)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(comando + "\r\n");
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private string LeerRespuesta(SslStream stream)
    {
        // Lee líneas de respuesta FTP; una respuesta multilinea empieza con "NNN-" y termina con "NNN ".
        StringBuilder sb = new StringBuilder();
        string primeraLinea = null;
        while (true)
        {
            string linea = LeerLinea(stream);
            if (linea == null) break;
            sb.AppendLine(linea);
            if (primeraLinea == null) primeraLinea = linea;

            bool esMultilinea = primeraLinea.Length > 3 && primeraLinea[3] == '-';
            if (!esMultilinea) break;
            if (linea.Length > 3 && linea.Substring(0, 3) == primeraLinea.Substring(0, 3) && linea[3] == ' ')
                break;
        }
        return sb.ToString().Trim();
    }

    private string LeerLinea(SslStream stream)
    {
        var bytes = new List<byte>();
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') bytes.Add((byte)b);
        }
        if (bytes.Count == 0 && b == -1) return null;
        return Encoding.ASCII.GetString(bytes.ToArray());
    }

    /// <summary>
    /// Parsea la respuesta "227 Entering Passive Mode (h1,h2,h3,h4,p1,p2)" para obtener IP y puerto.
    /// </summary>
    private (string ip, int port) ParsePasvResponse(string response)
    {
        int inicio = response.IndexOf('(');
        int fin = response.IndexOf(')');
        if (inicio < 0 || fin < 0)
            throw new FormatException($"Respuesta PASV con formato inesperado: {response}");

        string contenido = response.Substring(inicio + 1, fin - inicio - 1);
        string[] partes = contenido.Split(',');
        if (partes.Length != 6)
            throw new FormatException($"Respuesta PASV con formato inesperado: {response}");

        string ip = $"{partes[0]}.{partes[1]}.{partes[2]}.{partes[3]}";
        int port = (int.Parse(partes[4]) << 8) + int.Parse(partes[5]);
        return (ip, port);
    }
}
