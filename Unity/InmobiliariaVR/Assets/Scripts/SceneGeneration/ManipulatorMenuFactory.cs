using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Construye en runtime el menú compartido de mover/rotar muebles (Canvas World Space)
/// y lo conecta a una instancia de FurnitureManipulatorVR. Se usa una sola vez por escena;
/// todos los muebles generados dinámicamente comparten este mismo menú (igual que en el
/// diseño original de Sala_MVP, donde un único menú cambia de referencia según el mueble activo).
/// </summary>
public static class ManipulatorMenuFactory
{
    public static void BuildMenuFor(FurnitureManipulatorVR manipulator)
    {
        GameObject canvasGO = new GameObject("SharedManipulatorMenuCanvas");

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        // Sin esto los botones de mover/rotar no responden al rayo del control VR (ver
        // PresetLoaderController.CreatePresetMenu para la misma explicacion).
        canvasGO.AddComponent<TrackedDeviceGraphicRaycaster>();

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(400, 320);
        canvasRect.localScale = new Vector3(0.001f, 0.001f, 0.001f);

        Image bg = canvasGO.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

        // Nombre del mueble seleccionado
        GameObject nameGO = new GameObject("NombreMueble");
        nameGO.transform.SetParent(canvasGO.transform, false);
        RectTransform nameRect = nameGO.AddComponent<RectTransform>();
        nameRect.anchoredPosition = new Vector2(0, 130);
        nameRect.sizeDelta = new Vector2(380, 40);
        Text nameText = nameGO.AddComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        nameText.fontSize = 26;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.color = Color.white;

        // Botones de movimiento (coinciden con Mover(0..3) de FurnitureManipulatorVR)
        CreateButton(canvasGO, "Btn_Adelante", "▲ Adelante", new Vector2(-95, 45), () => manipulator.Mover(0));
        CreateButton(canvasGO, "Btn_Atras", "▼ Atras", new Vector2(-95, -15), () => manipulator.Mover(1));
        CreateButton(canvasGO, "Btn_Izquierda", "◄ Izquierda", new Vector2(95, 45), () => manipulator.Mover(2));
        CreateButton(canvasGO, "Btn_Derecha", "► Derecha", new Vector2(95, -15), () => manipulator.Mover(3));

        // Botones de rotación
        CreateButton(canvasGO, "Btn_RotarIzq", "Rotar Izq", new Vector2(-95, -75), () => manipulator.Rotar(-1));
        CreateButton(canvasGO, "Btn_RotarDer", "Rotar Der", new Vector2(95, -75), () => manipulator.Rotar(1));

        // Botón cerrar
        CreateButton(canvasGO, "Btn_Cerrar", "Cerrar", new Vector2(0, -135), () => manipulator.CerrarMenu(),
            new Color(0.6f, 0.2f, 0.2f, 0.9f));

        manipulator.menuRoot = canvasGO;
        manipulator.nombreMueble = nameText;

        canvasGO.SetActive(false);
    }

    private static void CreateButton(GameObject parent, string name, string label, Vector2 position,
        System.Action onClick, Color? color = null)
    {
        GameObject buttonGO = new GameObject(name);
        buttonGO.transform.SetParent(parent.transform, false);

        RectTransform rect = buttonGO.AddComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(170, 50);

        Image img = buttonGO.AddComponent<Image>();
        img.color = color ?? new Color(0.2f, 0.5f, 0.9f, 0.9f);

        Button btn = buttonGO.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick());

        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(buttonGO.transform, false);
        RectTransform textRect = textGO.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(170, 50);
        Text text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;
    }
}
