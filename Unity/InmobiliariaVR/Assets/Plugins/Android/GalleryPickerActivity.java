package com.inmobiliariavr.galleryplugin;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Bundle;
import android.provider.MediaStore;
import android.util.Log;

import com.unity3d.player.UnityPlayer;
import com.unity3d.player.UnityPlayerActivity;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Activity principal de la app (reemplaza a UnityPlayerActivity en AndroidManifest.xml)
 * unicamente para poder recibir el resultado del selector de fotos nativo de Android
 * (Photo Picker: MediaStore.ACTION_PICK_IMAGES, disponible desde Android 13 / API 33 sin
 * pedir NINGUN permiso de almacenamiento en tiempo de ejecucion -- y este proyecto ya
 * apunta a minSdk 34, asi que no hace falta manejar permisos viejos en absoluto).
 *
 * Unity no expone un mecanismo propio para abrir la galeria del sistema y recibir el
 * resultado, asi que hace falta esta Activity intermedia: expone un metodo estatico que
 * C# llama via AndroidJavaClass/AndroidJavaObject para lanzar el Intent, y cuando Android
 * devuelve el resultado (onActivityResult), copia la imagen elegida a un archivo temporal
 * "de verdad" (el content:// URI que devuelve el picker no se puede leer directo con
 * File.ReadAllBytes desde C#) y le avisa a Unity con UnitySendMessage.
 *
 * ADVERTENCIA (leer antes de confiar en este archivo):
 * Este plugin no pudo compilarse ni probarse en este entorno (no hay Android SDK/Gradle
 * disponible aca) -- fue escrito siguiendo el patron estandar de Unity para plugins Android
 * con Activity personalizada (el mismo que usan plugins como NativeGallery), pero necesita
 * una build real en un dispositivo/Android Studio para confirmar que compila y funciona.
 * Requiere ademas que el proyecto use la "Classic Activity" de Unity (no "Game Activity") --
 * ver Player Settings > Android > Configuration > Activity Type.
 */
public class GalleryPickerActivity extends UnityPlayerActivity {

    private static final String TAG = "GalleryPickerActivity";
    private static final int REQUEST_CODE_PICK_IMAGE = 48291;

    private static String callbackGameObject = "";
    private static String callbackMethodExito = "";
    private static String callbackMethodCancelado = "";

    /**
     * Llamado desde C# para abrir el picker nativo de fotos.
     * gameObject/metodoExito/metodoCancelado son el destino de UnityPlayer.UnitySendMessage
     * cuando el usuario elige una foto (recibe la ruta local del archivo copiado) o cancela.
     */
    public static void abrirSelectorDeImagen(Activity activity, String gameObject,
                                              String metodoExito, String metodoCancelado) {
        callbackGameObject = gameObject;
        callbackMethodExito = metodoExito;
        callbackMethodCancelado = metodoCancelado;

        try {
            Intent intent = new Intent(MediaStore.ACTION_PICK_IMAGES);
            intent.setType("image/*");
            activity.startActivityForResult(intent, REQUEST_CODE_PICK_IMAGE);
        } catch (Exception e) {
            Log.e(TAG, "No se pudo abrir el selector de imagenes: " + e.getMessage());
            if (!callbackGameObject.isEmpty() && !callbackMethodCancelado.isEmpty())
                UnityPlayer.UnitySendMessage(callbackGameObject, callbackMethodCancelado, "error_abrir");
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);

        if (requestCode != REQUEST_CODE_PICK_IMAGE) return;

        if (resultCode != Activity.RESULT_OK || data == null || data.getData() == null) {
            if (!callbackGameObject.isEmpty() && !callbackMethodCancelado.isEmpty())
                UnityPlayer.UnitySendMessage(callbackGameObject, callbackMethodCancelado, "");
            return;
        }

        Uri uriSeleccionado = data.getData();
        String rutaArchivo = copiarACacheLocal(uriSeleccionado);

        if (rutaArchivo == null) {
            if (!callbackGameObject.isEmpty() && !callbackMethodCancelado.isEmpty())
                UnityPlayer.UnitySendMessage(callbackGameObject, callbackMethodCancelado, "error_copia");
            return;
        }

        if (!callbackGameObject.isEmpty() && !callbackMethodExito.isEmpty())
            UnityPlayer.UnitySendMessage(callbackGameObject, callbackMethodExito, rutaArchivo);
    }

    /**
     * Copia el contenido del content:// URI elegido a un archivo temporal dentro de
     * getCacheDir(), que si es una ruta de archivo normal que Unity/C# puede leer con
     * System.IO.File.ReadAllBytes.
     */
    private String copiarACacheLocal(Uri uri) {
        InputStream in = null;
        OutputStream out = null;
        try {
            String nombreArchivo = "croquis_" + System.currentTimeMillis() + ".jpg";
            File archivoDestino = new File(getCacheDir(), nombreArchivo);

            in = getContentResolver().openInputStream(uri);
            out = new FileOutputStream(archivoDestino);

            byte[] buffer = new byte[8192];
            int leidos;
            while ((leidos = in.read(buffer)) != -1) {
                out.write(buffer, 0, leidos);
            }
            out.flush();

            return archivoDestino.getAbsolutePath();
        } catch (Exception e) {
            Log.e(TAG, "Error copiando imagen elegida: " + e.getMessage());
            return null;
        } finally {
            try { if (in != null) in.close(); } catch (Exception ignored) {}
            try { if (out != null) out.close(); } catch (Exception ignored) {}
        }
    }
}
