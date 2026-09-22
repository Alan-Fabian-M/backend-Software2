# ⚡ Setup - 2 Opciones Simples

**Tiempo total:** 2-3 minutos  
**Dificultad:** Muy Fácil

---

## ✅ Opción 1: Auto-Setup Desde Menu (RECOMENDADO)

### Paso 1: Abre Unity
- Abre InmobiliariaVR en Unity 6
- Abre escena `Sala_MVP.unity`

### Paso 2: Ejecuta Setup
```
Menu → InmobiliariaVR → Setup Scene Generation
```

**En la consola verás:**
```
=== Iniciando Setup de Generación de Escenas ===
✓ SceneGenerator creado
✓ PresetLoaderController creado
✓ ToastNotificationUI creado
✓ Escena marcada como modificada
=== Setup Completado ===
```

### Paso 3: Guarda y Prueba
1. Presiona `Ctrl + S` para guardar
2. Presiona `Play ▶️`
3. Verás el **menú flotante** con 4 presets
4. ¡Haz click en uno para generar la escena!

---

## ✅ Opción 2: Manual Setup (Si la Opción 1 no funciona)

### Paso 1: Crea GameObject
En la escena Sala_MVP.unity:
1. Right-click en Hierarchy → Create Empty
2. Renombra a: `SceneGeneratorManager`

### Paso 2: Agrega Componentes
1. Con `SceneGeneratorManager` seleccionado
2. Click "Add Component" → busca `SceneGenerator`
3. Agrega el script
4. Deja los defaults (están bien configurados)

### Paso 3: Ya está!
El script PresetLoaderController se crea automáticamente cuando presionas Play.

1. Presiona `Ctrl + S` para guardar
2. Presiona `Play ▶️`
3. ¡Verás el menú flotante!

---

## 🎯 Resultado Final

Ambas opciones crean la misma estructura:

```
Hierarchy (en Sala_MVP.unity):
├── SceneGeneratorManager
│   └── [SceneGenerator component]
├── PresetLoaderUI
│   └── [PresetLoaderController component]
└── ToastNotificationManager
    └── [ToastNotificationUI component]
```

---

## 🚀 Prueba Inmediata

1. **Presiona Play**
2. **Verás esto:**
   ```
   ┌──────────────────────────┐
   │ Selecciona Monoambiente  │
   │      Listo ✓             │
   ├───────┬──────┬────┬──────┤
   │Estándar│Compac│Gran│Loft │
   │4.2×5.5 │3.5×4 │5×6 │6×7  │
   └───────┴──────┴────┴──────┘
   ```

3. **Haz click en cualquier botón** → La escena se genera

---

## 🎮 Interactúa Con La Escena

Una vez generada, puedes:

### Mover/Rotar Muebles
```
Hold Select button ~0.35s en sofá/mesa/cama
↓
Aparecen botones de movimiento
```

### Cambiar Color
```
Short tap en pared o sofá
↓
Material cambia (cicla 3 colores)
```

### Eliminar
```
Double-click en cualquier mueble
↓
Menú de confirmación aparece (Rojo/Verde)
↓
Click rojo → Eliminar
```

### Cargar Otro Preset
```
Click en cualquier botón del menú nuevamente
↓
Escena se limpia y genera la nueva
```

---

## ✨ Listo!

Si llegaste aquí, **¡ya está todo funcionando!**

**Próximos pasos (opcional):**
- Lee `Assets/Docs/QUICK_START.md` para más detalles
- Lee `Assets/Docs/IMPLEMENTATION_GUIDE_SceneGeneration.md` para detalles técnicos

---

## ❓ ¿Qué está instalado?

```
✅ 10 Scripts (todos funcionales)
✅ 4 JSON Presets (estándar, compacto, grande, loft)
✅ Documentación completa
✅ Auto-setup integrado
```

### Ubicación de archivos:
```
Assets/Scripts/SceneGeneration/     ← Todos los scripts
Assets/Resources/ScenePresets/      ← Archivos JSON
Assets/Docs/                        ← Documentación
```

---

**¡A disfrutar generando escenas VR!** 🎉
