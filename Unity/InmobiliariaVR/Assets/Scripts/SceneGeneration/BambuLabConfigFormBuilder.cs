// IMPORTANTE: este archivo usa UnityEditor.SerializedObject para poder asignar los campos
// [SerializeField] PRIVADOS de BambuLabConfigPanel sin tener que volverlos públicos. UnityEditor
// NO existe fuera del Editor -- por eso TODO el contenido está envuelto en #if UNITY_EDITOR:
// sin esto, Unity fallaría al compilar el build para Android con un error de "no se encontró
// el ensamblado UnityEditor". El script simplemente no existe en el player final, lo cual es
// correcto: es una herramienta de editor que se usa una sola vez para generar la UI.
#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;

/// <summary>
/// Script constructor automático para el formulario Bambu Lab en Canvas.
/// Crea toda la UI necesaria de una sola vez, incluyendo IP, Access Code y Serial Number
/// (Device ID) -- este último es obligatorio (ver BambuLabMQTT.cs) porque los topics MQTT de
/// Bambu Lab requieren el Serial Number real de la impresora, no un wildcard.
///
/// USO:
/// 1. Crea un GameObject vacío en tu Canvas
/// 2. Agrega este script al GameObject
/// 3. En el Inspector, click en "Generar UI Bambu Lab"
/// 4. Espera a que termine
/// 5. LISTO - el formulario está completo
/// </summary>
public class BambuLabConfigFormBuilder : MonoBehaviour
{
    [ContextMenu("Generar UI Bambu Lab")]
    public void GenerarUI()
    {
        Debug.Log("🔨 Generando UI Bambu Lab...");

        // Obtener o crear Canvas
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("❌ No hay Canvas en la escena. Crea uno primero.");
            return;
        }

        // Crear panel principal.
        // IMPORTANTE (bug real encontrado 2026-09-19): panelGO es la raíz que lleva el
        // componente BambuLabConfigPanel -- este objeto NUNCA debe desactivarse a sí mismo,
        // porque BambuLabConfigPanel.Start() oculta "panelBackground" apenas arranca el juego,
        // y si panelBackground fuera el mismo panelGO, todo el GameObject (incluido el propio
        // componente) quedaría inactivo desde el primer frame. Object.FindAnyObjectByType (sin
        // FindObjectsInactive.Include) ignora objetos inactivos, así que CrudPanelController
        // nunca lo hubiera encontrado -- el botón "⚙️ Bambu Config" siempre habría fallado con
        // "No se encontró el panel...". Por eso todo el contenido visual (fondo, textos, inputs,
        // botones) va en un hijo separado ("Contenido") que es lo único que se muestra/oculta;
        // panelGO en sí queda siempre activo y siempre encontrable.
        GameObject panelGO = new GameObject("BambuLabConfigPanel");
        panelGO.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panelGO.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        GameObject contenidoGO = new GameObject("Contenido");
        contenidoGO.transform.SetParent(panelGO.transform, false);
        RectTransform contenidoRect = contenidoGO.AddComponent<RectTransform>();
        contenidoRect.anchorMin = Vector2.zero;
        contenidoRect.anchorMax = Vector2.one;
        contenidoRect.offsetMin = Vector2.zero;
        contenidoRect.offsetMax = Vector2.zero;

        Image panelImage = contenidoGO.AddComponent<Image>();
        panelImage.color = new Color(0.08f, 0.08f, 0.09f, 0.97f);

        LayoutElement layoutElement = contenidoGO.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = 600f;
        layoutElement.preferredHeight = 860f;

        // ===== TÍTULO =====
        GameObject tituloGO = new GameObject("TituloPanel");
        tituloGO.transform.SetParent(contenidoGO.transform, false);
        RectTransform tituloRect = tituloGO.AddComponent<RectTransform>();
        tituloRect.anchorMin = new Vector2(0.5f, 1f);
        tituloRect.anchorMax = new Vector2(0.5f, 1f);
        tituloRect.anchoredPosition = new Vector2(0f, -40f);
        tituloRect.sizeDelta = new Vector2(500f, 60f);

        TextMeshProUGUI titulo = tituloGO.AddComponent<TextMeshProUGUI>();
        titulo.text = "⚙️ Configurar Bambu Lab";
        titulo.fontSize = 36;
        titulo.alignment = TextAlignmentOptions.Center;
        titulo.color = Color.white;

        // ===== LABEL IP =====
        CrearLabel(contenidoGO, "LabelIP", "📍 IP de la impresora:", -120f);

        // ===== INPUT IP =====
        TMP_InputField inputIP = CrearInputField(contenidoGO, "InputIP", -170f, "192.168.1.100",
            TMP_InputField.ContentType.Standard, TMP_InputField.InputType.Standard, TouchScreenKeyboardType.Default);

        // ===== LABEL ACCESS CODE =====
        CrearLabel(contenidoGO, "LabelAccessCode", "🔐 Access Code:", -240f);

        // ===== INPUT ACCESS CODE =====
        // Bambu Lab usa códigos alfanuméricos de longitud variable según el modelo -- por eso
        // el tipo de contenido es "Standard" y no "Pin" (que solo acepta dígitos).
        TMP_InputField inputCode = CrearInputField(contenidoGO, "InputAccessCode", -290f, "Ej: 12345678",
            TMP_InputField.ContentType.Standard, TMP_InputField.InputType.Standard, TouchScreenKeyboardType.Default);

        // ===== LABEL SERIAL NUMBER =====
        // Obligatorio: los topics MQTT de Bambu Lab son "device/{SERIAL}/report" y
        // "device/{SERIAL}/request" -- sin el Serial Number real, la impresora nunca recibe
        // los comandos (ver BambuLabMQTT.cs).
        CrearLabel(contenidoGO, "LabelSerialNumber", "🔢 Serial Number (Device ID):", -360f);

        // ===== INPUT SERIAL NUMBER =====
        TMP_InputField inputSerial = CrearInputField(contenidoGO, "InputSerialNumber", -410f, "Ej: 01P00A000000000",
            TMP_InputField.ContentType.Standard, TMP_InputField.InputType.Standard, TouchScreenKeyboardType.Default);

        // ===== PANEL DE ESTADO =====
        GameObject estadoPanelGO = new GameObject("EstadoPanel");
        estadoPanelGO.transform.SetParent(contenidoGO.transform, false);
        RectTransform estadoPanelRect = estadoPanelGO.AddComponent<RectTransform>();
        estadoPanelRect.anchorMin = new Vector2(0.5f, 1f);
        estadoPanelRect.anchorMax = new Vector2(0.5f, 1f);
        estadoPanelRect.anchoredPosition = new Vector2(0f, -500f);
        estadoPanelRect.sizeDelta = new Vector2(500f, 90f);

        Image estadoPanelImage = estadoPanelGO.AddComponent<Image>();
        estadoPanelImage.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

        GameObject textoEstadoGO = new GameObject("TextoEstado");
        textoEstadoGO.transform.SetParent(estadoPanelGO.transform, false);
        RectTransform textoEstadoRect = textoEstadoGO.AddComponent<RectTransform>();
        textoEstadoRect.anchorMin = Vector2.zero;
        textoEstadoRect.anchorMax = Vector2.one;
        textoEstadoRect.offsetMin = new Vector2(10f, 5f);
        textoEstadoRect.offsetMax = new Vector2(-10f, -5f);

        TextMeshProUGUI textoEstado = textoEstadoGO.AddComponent<TextMeshProUGUI>();
        textoEstado.text = "➖ Listo para configurar";
        textoEstado.fontSize = 18;
        textoEstado.alignment = TextAlignmentOptions.Center;
        textoEstado.color = new Color(0.8f, 0.8f, 0.8f, 1f);

        // ===== BOTONES =====
        float buttonY = -630f;
        float spacing = 70f;

        Button botonProbar = CreateBoton(contenidoGO, "🔍 Probar Conexión", buttonY,
            new Color(0.2f, 0.55f, 0.8f, 0.85f));
        buttonY -= spacing;

        Button botonGuardar = CreateBoton(contenidoGO, "💾 Guardar", buttonY,
            new Color(0.2f, 0.6f, 0.3f, 0.85f));
        buttonY -= spacing;

        Button botonLimpiar = CreateBoton(contenidoGO, "🗑️ Limpiar", buttonY,
            new Color(0.6f, 0.3f, 0.2f, 0.85f));
        buttonY -= spacing;

        Button botonCancelar = CreateBoton(contenidoGO, "❌ Cancelar", buttonY,
            new Color(0.4f, 0.4f, 0.4f, 0.85f));

        // ===== ASIGNAR A BambuLabConfigPanel =====
        // panelBackground apunta a "Contenido" (NO a panelGO) -- ver el comentario grande más
        // arriba, junto a la creación de panelGO, sobre por qué esto importa.
        BambuLabConfigPanel configPanel = panelGO.AddComponent<BambuLabConfigPanel>();
        var so = new UnityEditor.SerializedObject(configPanel);
        so.FindProperty("panelBackground").objectReferenceValue = contenidoGO;
        so.FindProperty("inputIP").objectReferenceValue = inputIP;
        so.FindProperty("inputAccessCode").objectReferenceValue = inputCode;
        so.FindProperty("inputSerialNumber").objectReferenceValue = inputSerial;
        so.FindProperty("botonProbar").objectReferenceValue = botonProbar;
        so.FindProperty("botonGuardar").objectReferenceValue = botonGuardar;
        so.FindProperty("botonLimpiar").objectReferenceValue = botonLimpiar;
        so.FindProperty("botonCancelar").objectReferenceValue = botonCancelar;
        so.FindProperty("textoEstado").objectReferenceValue = textoEstado;
        so.FindProperty("imagenEstado").objectReferenceValue = estadoPanelImage;
        so.ApplyModifiedProperties();

        Debug.Log("✅ UI Bambu Lab generada exitosamente!");
        Debug.Log("   - Panel creado: BambuLabConfigPanel");
        Debug.Log("   - InputFields: IP, Access Code y Serial Number");
        Debug.Log("   - Botones: Probar, Guardar, Limpiar, Cancelar");
        Debug.Log("   - Referencias asignadas automáticamente");
        Debug.Log("\n   SIGUIENTES PASOS: 1) Instalar M2Mqtt ya está resuelto (código fuente en Assets/Plugins/M2Mqtt).");
        Debug.Log("   2) Confirmar que el proyecto compila sin errores.");
        Debug.Log("   3) Obtener IP, Access Code y Serial Number desde la pantalla de tu impresora (Configuración → Acerca de / Red).");
    }

    private void CrearLabel(GameObject panelGO, string nombre, string texto, float posY)
    {
        GameObject labelGO = new GameObject(nombre);
        labelGO.transform.SetParent(panelGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1f);
        labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(-150f, posY);
        labelRect.sizeDelta = new Vector2(350f, 40f);

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.fontSize = 20;
        label.alignment = TextAlignmentOptions.Left;
        label.color = new Color(0.8f, 0.8f, 0.8f, 1f);
    }

    private TMP_InputField CrearInputField(GameObject panelGO, string nombre, float posY, string placeholder,
        TMP_InputField.ContentType contentType, TMP_InputField.InputType inputType, TouchScreenKeyboardType keyboardType)
    {
        GameObject inputGO = new GameObject(nombre);
        inputGO.transform.SetParent(panelGO.transform, false);
        RectTransform inputRect = inputGO.AddComponent<RectTransform>();
        inputRect.anchorMin = new Vector2(0.5f, 1f);
        inputRect.anchorMax = new Vector2(0.5f, 1f);
        inputRect.anchoredPosition = new Vector2(0f, posY);
        inputRect.sizeDelta = new Vector2(500f, 50f);

        Image inputImage = inputGO.AddComponent<Image>();
        inputImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

        TMP_InputField input = inputGO.AddComponent<TMP_InputField>();

        // Texto que el usuario realmente escribe (empieza vacío) y el placeholder son DOS
        // objetos de texto separados -- TMP_InputField los necesita así para poder ocultar el
        // placeholder automáticamente en cuanto el usuario empieza a tipear.
        TextMeshProUGUI textoIngresado = CrearTextoInterno(inputGO, "Text", "", new Color(0.95f, 0.95f, 0.95f, 1f));
        TextMeshProUGUI textoPlaceholder = CrearTextoInterno(inputGO, "Placeholder", placeholder, new Color(0.6f, 0.6f, 0.6f, 1f));

        input.textComponent = textoIngresado;
        input.placeholder = textoPlaceholder;
        input.contentType = contentType;
        input.inputType = inputType;
        input.keyboardType = keyboardType;

        return input;
    }

    private TextMeshProUGUI CrearTextoInterno(GameObject parent, string nombre, string texto, Color color)
    {
        GameObject textGO = new GameObject(nombre);
        textGO.transform.SetParent(parent.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10f, 0f);
        textRect.offsetMax = new Vector2(-10f, 0f);

        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = texto;
        text.fontSize = 22;
        text.alignment = TextAlignmentOptions.Left;
        text.color = color;

        return text;
    }

    private Button CreateBoton(GameObject parent, string texto, float posY, Color color)
    {
        GameObject btnGO = new GameObject("Boton_" + texto);
        btnGO.transform.SetParent(parent.transform, false);
        RectTransform rect = btnGO.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, posY);
        rect.sizeDelta = new Vector2(480f, 50f);

        Image img = btnGO.AddComponent<Image>();
        img.color = color;

        Button btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = img;

        ColorBlock colors = btn.colors;
        colors.normalColor = color;
        colors.highlightedColor = color * 1.2f;
        colors.pressedColor = color * 0.8f;
        colors.disabledColor = color * 0.5f;
        btn.colors = colors;

        GameObject labelGO = new GameObject("Text");
        labelGO.transform.SetParent(btnGO.transform, false);
        RectTransform labelRect = labelGO.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelGO.AddComponent<TextMeshProUGUI>();
        label.text = texto;
        label.fontSize = 24;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;

        return btn;
    }
}
#endif
