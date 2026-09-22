# 🚀 Quick Start - Scene Generation Pipeline

**Status:** ✅ Todo instalado y listo para usar  
**Tiempo de setup:** ~2 minutos

---

## ⚡ Inicio Rápido (3 pasos)

### Paso 1: Abre Unity Editor
- Abre tu proyecto InmobiliariaVR en Unity 6
- Abre la escena `Sala_MVP.unity`

### Paso 2: Ejecuta Auto-Setup
```
Menu → InmobiliariaVR → Setup Scene Generation
```

Esto crea automáticamente:
- ✅ `SceneGeneratorManager` (orquestador)
- ✅ `PresetLoaderUI` (interfaz de presets)
- ✅ `ToastNotificationManager` (notificaciones)

Verás en la consola:
```
=== Iniciando Setup de Generación de Escenas ===
✓ SceneGenerator creado
✓ PresetLoaderController creado
✓ ToastNotificationUI creado
✓ Escena marcada como modificada (guarda manualmente con Ctrl+S)
=== Setup Completado ===
```

### Paso 3: Guarda y Prueba
1. Presiona **Ctrl+S** para guardar la escena
2. Presiona **Play** ▶️ en el editor
3. Verás un **menú flotante** con 4 opciones:

```
┌─────────────────────────┐
│ Selecciona Monoambiente │
│   Listo                 │
├─────┬─────┬─────┬──────┤
│Estándar │Compacto│Grande│ Loft │
│4.2×5.5m │3.5×4m │5×6.5m│6×7m  │
└─────┴─────┴─────┴──────┘
```

4. **Haz click en cualquier preset** → La escena se genera en ~3-3.5 segundos

---

## 🎮 Interacciones (Una Vez Generada la Escena)

### Movimiento & Rotación
```
Sostén Select button (~0.35s) en un mueble
↓
Aparecen botones de movimiento
↓
Adelante / Atrás / Izquierda / Derecha
Rotar Izq / Rotar Der
```

### Cambio de Color
```
Toque CORTO en pared o sofá
↓
Material cambia al siguiente (cicla 3 colores)
```

### Eliminar Mueble
```
DOBLE-CLICK en cualquier mueble
↓
Aparece menú: [Confirmar] [Cancelar]
↓
Confirmar → Objeto eliminado + Toast "Deleted"
```

### Cargar Otro Preset
```
Cuando terminaste de explorar
↓
Click en botón Menu (esquina)
↓
Aparece menú de presets nuevamente
↓
Escena anterior se limpia, se carga la nueva
```

---

## 📁 Archivos Instalados

```
Assets/Scripts/SceneGeneration/
├── SceneValidator.cs              ← Valida JSON
├── SceneGenerator.cs              ← Orquestador principal
├── PrefabMapper.cs                ← Mapea tipos a prefabs
├── WireframeDrawer.cs             ← Visualización (verde)
├── FadeInAnimator.cs              ← Animaciones fade-in
├── SceneElementMetadata.cs        ← Metadata de objetos
├── FurnitureDeleterVR.cs          ← Deleter por doble-click
├── ToastNotificationUI.cs         ← Notificaciones flotantes
├── PresetLoaderController.cs      ← Controlador de presets
└── SETUP_SceneGeneration.cs       ← Script de auto-setup

Assets/Resources/ScenePresets/
├── preset_standard.json           ← 4.2m × 5.5m, 8 objetos
├── preset_compact.json            ← 3.5m × 4.0m, 6 objetos
├── preset_large.json              ← 5.0m × 6.5m, 10 objetos
└── preset_loft.json               ← 6.0m × 7.0m, 12 objetos

Assets/Docs/
├── IMPLEMENTATION_GUIDE_SceneGeneration.md  ← Guía técnica completa
└── DELIVERABLES_SUMMARY.md                  ← Resumen de features
```

---

## 🔍 Verificación: ¿Está todo OK?

### En la Consola de Unity deberías ver:

**Al presionar Play:**
```
=== Scene Validation Report ===
Valid: True
✓ Scene JSON is valid and ready for generation
```

**Al hacer click en un preset:**
```
[SceneGen] 0/8: Validating scene data...
[SceneGen] 0/8: Clearing previous scene...
[SceneGen] 1/8: Creating muro_norte...
[SceneGen] 2/8: Creating muro_sur...
[SceneGen] 3/8: Creating muro_este...
[SceneGen] 4/8: Creating muro_oeste...
[SceneGen] 5/8: Creating piso...
[SceneGen] 6/8: Creating sofa_principal...
[SceneGen] 7/8: Creating mesa_centro...
[SceneGen] 8/8: Creating cama_dormitorio...
[SceneGen] 8/8: Scene generation complete!
```

### En Pantalla deberías ver:

1. **Menú flotante** con 4 botones de preset
2. **Wireframes verdes** alrededor de cada objeto (0.3s preview)
3. **Fade-in gradual** de todos los objetos
4. **Estado en pantalla:** "✓ Listo - Generación completada" (verde)

---

## ⚠️ Si algo NO funciona

### Error: "No prefab at path..."
**Causa:** Furniture Mega Pack no está en la ruta esperada  
**Solución:** Verifica que los prefabs estén en `Assets/Prefabs/Sofas/`, `Assets/Prefabs/Tables/`, etc.

### Error: "preset_standard.json not found"
**Causa:** Archivo JSON no está en Resources  
**Solución:** Verifica que estén en `Assets/Resources/ScenePresets/`

### No aparece menú flotante
**Causa:** PresetLoaderController no se creó  
**Solución:** Ejecuta otra vez: `Menu → InmobiliariaVR → Setup Scene Generation`

### Objetos no se mueven/rotan
**Causa:** FurnitureManipulatorVR no está en los prefabs  
**Solución:** Verifica que los prefabs tengan BoxCollider y XRSimpleInteractable

---

## 📊 Tiempos Esperados

```
Click en preset → Validación      ~10ms
            → Instanciación      ~200ms (8 objetos)
            → Wireframes         ~2.4s (preview por objeto)
            → Animaciones        ~0.5s (fade-in paralelo)
            → Interactividad     ~100ms
────────────────────────────────
Total:                           ~3-3.5 segundos
                                 ✓ Completamente interactivo
```

---

## 🎓 Próximos Pasos (Después de Probar)

### Test 1: Cargar Presets
- ✅ Estándar (8 objetos)
- ✅ Compacto (6 objetos)
- ✅ Grande (10 objetos)
- ✅ Loft (12 objetos)

### Test 2: Interacciones
- ✅ Mover sofá (Hold Select)
- ✅ Cambiar color de pared (Short tap)
- ✅ Eliminar mesa (Double-click)

### Test 3: Croquis Real (Fase 2)
Cuando quieras integrar el servidor Python en localhost:8765:

1. Captura foto de croquis dibujado a mano
2. Envía a servidor Python (YOLO)
3. Servidor retorna JSON similar a presets
4. Generator crea la escena automáticamente

---

## 📞 Support

### Common Issues:

| Problema | Solución |
|----------|----------|
| Menú no aparece | Run Setup nuevamente |
| Wireframes no se ven | Check Scene view gizmos |
| Prefabs no cargan | Verify Assets/Prefabs path |
| Objetos no se mueven | Verify XRSimpleInteractable |
| Toast notifications no aparecen | Verify TextMeshPro import |

---

## ✨ Listo Para Usar

**Que disfrutes generando escenas VR!** 🎉

Si tienes preguntas, revisa:
- `Assets/Docs/IMPLEMENTATION_GUIDE_SceneGeneration.md` (técnico)
- `Assets/Docs/DELIVERABLES_SUMMARY.md` (overview)

---

*Scene Generation Pipeline v1.0 - Ready to Deploy*
