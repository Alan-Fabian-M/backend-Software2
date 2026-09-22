# 🏗️ ARQUITECTURA FINAL - Sistema de Generación de Escenas desde Croquis

**Proyecto:** InmobiliariaVR  
**Módulo:** Generador de Escenas desde Croquis (JSON → 3D)  
**Versión:** 2.0 (Async/Await, Fade-in Progresivo)  
**Fecha:** 2026-09-15

---

## 📊 ANÁLISIS DE PREFABS DISPONIBLES

### **Inventario Actual:**
```
Bathroom:     40 prefabs (Vanity, Tub, Shower, Toilet, Sink, etc.)
Beds:         50 prefabs (Bed01 - Bed50)
Chairs:       50 prefabs (Chair01 - Chair50)
Closets:      50 prefabs (Closet01 - Closet50)
Cushions:     50 prefabs (Cushion01 - Cushion50)
Drawers:      50 prefabs (Drawer01 - Drawer50)
Kitchen:     105 prefabs (Cabinets, Stove, Fridge, Sink, etc.)
Sofas:        50 prefabs (Sofa01 - Sofa50)
Tables:       50 prefabs (Table01 - Table50)
─────────────────────────────
TOTAL:       ~445 prefabs
```

### **¿QUÉ FALTA?**
- ❌ Muros/Paredes (crear dinámicamente con Cubes)
- ❌ Piso (crear dinámicamente)
- ❌ Puertas (crear dinámicamente)
- ❌ Ventanas (crear dinámicamente)
- ❌ Lámparas/Iluminación (podrían ser del Kitchen o nuevos)

---

## 🎯 MAPEO YOLO → PREFAB (Estrategia)

### **Estrategia de Selección:**

Para tipos con **múltiples variantes** (Bed01-50, Chair01-50, etc.), usaré:

```csharp
// Pseudo-código
int SelectVariantIndex(string type, int confidence)
{
    // Si confianza alta, usar variante 01 (más estándar)
    // Si confianza media, usar variante aleatoria 15-30
    // Si confianza baja, usar variante simple 05
    
    if (confidence > 0.85) return 1;
    if (confidence > 0.70) return Random.Range(15, 30);
    return Random.Range(1, 15);
}
```

### **MAPEO COMPLETO:**

```json
{
  "FURNITURE_TYPES": {
    "sofa": {
      "category": "Sofas",
      "variants": ["Sofa01", "Sofa02", ..., "Sofa50"],
      "default": "Sofa01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "mesa": {
      "category": "Tables",
      "variants": ["Table01", "Table02", ..., "Table50"],
      "default": "Table01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "mesa_noche": {
      "category": "Tables",
      "variants": ["Table35", "Table36", "Table37"],
      "default": "Table35",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "cama": {
      "category": "Beds",
      "variants": ["Bed01", "Bed02", ..., "Bed50"],
      "default": "Bed01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "silla": {
      "category": "Chairs",
      "variants": ["Chair01", "Chair02", ..., "Chair50"],
      "default": "Chair01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "escritorio": {
      "category": "Tables",
      "variants": ["Table40", "Table41", "Table42"],
      "default": "Table40",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "estanteria": {
      "category": "Closets",
      "variants": ["Closet01", "Closet10", "Closet20"],
      "default": "Closet01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "ropero": {
      "category": "Closets",
      "variants": ["Closet25", "Closet26", "Closet50"],
      "default": "Closet25",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "tv": {
      "category": "Tables",
      "variants": ["Table48", "Table49", "Table50"],
      "default": "Table48",
      "selectable": true,
      "deletable": true,
      "colorable": false,
      "movable": true
    },
    "cocina": {
      "category": "Kitchen",
      "variants": ["CabinetA01", "CabinetB01", "CabinetC01"],
      "default": "CabinetA01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": false
    },
    "refrigerador": {
      "category": "Kitchen",
      "variants": ["Refrigerator01", "Refrigerator02", ..., "Refrigerator07"],
      "default": "Refrigerator01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "fregadero": {
      "category": "Kitchen",
      "variants": ["Sink01", "Sink02", ..., "Sink07"],
      "default": "Sink01",
      "selectable": true,
      "deletable": true,
      "colorable": false,
      "movable": false
    },
    "bano_completo": {
      "category": "Bathroom",
      "variants": ["BathroomVanity01", "Toilet01", "BathTub01"],
      "default": "BathroomVanity01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": false
    },
    "inodoro": {
      "category": "Bathroom",
      "variants": ["Toilet01", "Toilet02", ..., "Toilet07"],
      "default": "Toilet01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": false
    },
    "banera": {
      "category": "Bathroom",
      "variants": ["BathTub01", "BathTub02", ..., "BathTub07"],
      "default": "BathTub01",
      "selectable": true,
      "deletable": true,
      "colorable": false,
      "movable": false
    },
    "lavamanos": {
      "category": "Bathroom",
      "variants": ["WashBasin01", "WashBasin02", ..., "WashBasin07"],
      "default": "WashBasin01",
      "selectable": true,
      "deletable": true,
      "colorable": false,
      "movable": false
    },
    "cojin": {
      "category": "Cushions",
      "variants": ["Cushion01", "Cushion02", ..., "Cushion50"],
      "default": "Cushion01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "cajon": {
      "category": "Drawers",
      "variants": ["Drawer01", "Drawer10", "Drawer20"],
      "default": "Drawer01",
      "selectable": true,
      "deletable": true,
      "colorable": true,
      "movable": true
    },
    "muro": {
      "category": "GEOMETRY",
      "type": "Cube",
      "material": "Concrete",
      "selectable": false,
      "deletable": false,
      "colorable": true,
      "movable": false
    },
    "piso": {
      "category": "GEOMETRY",
      "type": "Cube",
      "material": "Concrete",
      "selectable": false,
      "deletable": false,
      "colorable": true,
      "movable": false
    },
    "puerta": {
      "category": "GEOMETRY",
      "type": "Cube",
      "material": "Wood",
      "selectable": false,
      "deletable": false,
      "colorable": true,
      "movable": false
    },
    "ventana": {
      "category": "GEOMETRY",
      "type": "Cube",
      "material": "Glass",
      "selectable": false,
      "deletable": false,
      "colorable": true,
      "movable": false
    }
  }
}
```

---

## 🏛️ ARQUITECTURA DE SOFTWARE

### **Layer 1: CORE (Lógica de Negocio)**

```
┌─────────────────────────────────────────┐
│  SceneGenerator (Orquestador Principal) │
├─────────────────────────────────────────┤
│                                         │
│  ├─ ValidateAndPrepare(jsonData)       │
│  ├─ CleanPreviousScene()               │
│  ├─ GenerateSceneAsync(elements)       │
│  │  └─ Para cada elemento:             │
│  │     ├─ ShowWireframe()              │
│  │     ├─ Await delay               │
│  │     ├─ InstantiateObject()         │
│  │     ├─ ApplyTransforms()           │
│  │     ├─ ApplyMaterials()            │
│  │     ├─ FadeIn()                    │
│  │     └─ BindInteractivity()         │
│  │                                     │
│  └─ ShowCompletionMessage()            │
│                                         │
└─────────────────────────────────────────┘
```

### **Layer 2: VALIDATORS (Validación de Datos)**

```
SceneValidator
├─ ValidateJSONStructure()
│  ├─ Verificar "metadata"
│  ├─ Verificar "room_info.bounds"
│  ├─ Verificar "scene_elements[]"
│  └─ Retornar: {valid: bool, errors: string[]}
│
├─ ValidateElement(element)
│  ├─ type != null
│  ├─ transform != null
│  ├─ transform.position != null
│  ├─ confidence >= 0
│  └─ Retornar: {valid: bool, errors: string[]}
│
└─ GetValidationReport()
   └─ Retornar reporte completo con warnings
```

### **Layer 3: MAPPERS (Resolución de Prefabs)**

```
PrefabMapper
├─ InitializeMappings() [En Awake]
│  └─ Cargar diccionario de tipos → prefabs
│
├─ GetPrefabPath(type: string, confidence: float)
│  ├─ Buscar tipo en mapeo
│  ├─ Si variantes múltiples: SelectVariant(confidence)
│  ├─ Si no existe: LogWarning, usar default
│  └─ Retornar: "Assets/Furniture Mega Pack/Sofas/Sofa01.prefab"
│
├─ GetPrefabMetadata(type: string)
│  └─ Retornar: {selectable, deletable, colorable, movable}
│
└─ IsGeometryType(type: string)
   └─ Retornar si es "muro", "piso", "puerta", "ventana"
```

### **Layer 4: INSTANTIATORS (Creación de Objetos)**

```
ObjectInstantiator
├─ InstantiateFromPrefab(prefabPath, transform)
│  ├─ Load prefab desde Resources
│  ├─ Instantiate(prefab, position, rotation)
│  ├─ obj.transform.localScale = scale
│  └─ Retornar: GameObject
│
├─ InstantiateGeometry(type, transform)
│  ├─ Crear Cube
│  ├─ Aplicar escala
│  ├─ Aplicar material (Concrete/Wood/Glass)
│  └─ Retornar: GameObject
│
└─ ConfigureCollider(obj, type)
   ├─ Si no tiene collider, agregar BoxCollider
   ├─ Si movable: crear Rigidbody kinematic
   └─ Actualizar tamaño de collider según bounds
```

### **Layer 5: TRANSFORMERS (Aplicación de Propiedades)**

```
TransformApplier
├─ ApplyTransform(obj, transform)
│  ├─ obj.position = transform.position
│  ├─ obj.rotation = Quaternion.Euler(transform.rotation)
│  └─ obj.localScale = transform.scale
│
└─ ApplyLocalTransform(obj, transform)
   └─ Misma lógica pero con localPosition/localRotation

MaterialApplier
├─ ApplyColor(obj, hexColor: string)
│  ├─ Parse hex color → Color
│  ├─ GetRenderer(obj)
│  ├─ renderer.material.color = color
│  └─ Manejar transparencia si aplica
│
└─ ApplyMaterial(obj, materialType: string)
   ├─ Buscar material en Resources
   ├─ renderer.material = material
   └─ Si no existe: usar default
```

### **Layer 6: ANIMATIONS (Efectos Visuales)**

```
FadeInAnimator
├─ FadeInObject(obj, duration: float)
│  ├─ Si tiene Renderer:
│  │  ├─ Guardar color original
│  │  ├─ Setear alpha a 0
│  │  ├─ Animate alpha: 0 → 1
│  │  └─ Duration = 0.5s
│  │
│  └─ Si no tiene Renderer (Canvas):
│     ├─ Agregar CanvasGroup si no existe
│     ├─ Setear alpha a 0
│     ├─ Animate alpha: 0 → 1
│     └─ Duration = 0.5s

WireframeDrawer
├─ DrawWireframeBounds(bounds, duration: float)
│  ├─ Crear líneas verdes (Debug.DrawLine)
│  ├─ Mostrar durante 0.3s
│  ├─ Color: Green (#00FF00)
│  └─ Limpiar después
│
└─ DrawBoundingBox(min, max, duration)
   └─ Dibujar cubo wireframe completo
```

### **Layer 7: INTERACTIVITY (Scripts Interactivos)**

```
InteractivityBinder
├─ BindScripts(obj, metadata)
│  ├─ Si selectable == true:
│  │  ├─ Agregar XRSimpleInteractable
│  │  ├─ Agregar FurnitureSelectable
│  │  └─ Configurar eventos
│  │
│  ├─ Si movable == true:
│  │  ├─ Agregar FurnitureManipulatorVR
│  │  └─ Configurar opciones
│  │
│  ├─ Si colorable == true:
│  │  ├─ Agregar MaterialChangerVR
│  │  └─ Generar paleta de colores
│  │
│  └─ Si deletable == true:
│     ├─ Agregar FurnitureDeleterVR (NUEVO)
│     └─ Configurar detección de doble-click
│
└─ ConfigurePhysics(obj, type)
   ├─ Si es Furniture: Rigidbody kinematic
   ├─ Si es Geometry: Static
   └─ Agregar colliders si faltan
```

---

## 📋 SCRIPTS A CREAR/MODIFICAR

### **Scripts NUEVOS:**

| Script | Responsabilidad | Métodos Clave |
|--------|-----------------|---|
| **SceneGenerator.cs** | Orquestador principal | `GenerateSceneAsync(json)`, `CleanScene()` |
| **SceneValidator.cs** | Validación de JSON | `ValidateJSON()`, `GetErrors()` |
| **PrefabMapper.cs** | Mapeo tipo→prefab | `GetPrefabPath()`, `SelectVariant()` |
| **ObjectInstantiator.cs** | Creación de objetos | `InstantiateFromPrefab()`, `InstantiateGeometry()` |
| **TransformApplier.cs** | Aplicar transforms | `ApplyTransform()`, `ApplyScale()` |
| **MaterialApplier.cs** | Aplicar materiales | `ApplyColor()`, `ApplyMaterial()` |
| **FadeInAnimator.cs** | Efecto fade-in | `FadeInAsync()` |
| **WireframeDrawer.cs** | Dibuja boxes wireframe | `DrawBounds()`, `DrawBox()` |
| **InteractivityBinder.cs** | Vincula scripts | `BindScripts()`, `ConfigurePhysics()` |
| **FurnitureDeleterVR.cs** | Sistema de eliminación | `DetectDoubleClick()`, `ShowDeleteMenu()` |
| **PresetSelectorUI.cs** | Selector de presets | `ShowPresets()`, `OnPresetSelected()` |
| **LoadingPanelUI.cs** | Panel de carga | `ShowProgress()`, `UpdateObjectName()` |
| **ToastNotificationUI.cs** | Notificaciones flotantes | `ShowMessage()`, `ShowError()` |
| **SketchLoaderUI.cs** | Cargar croquis | `LoadSketchFromCamera()`, `SendToServer()` |

### **Scripts EXISTENTES (Sin cambios):**

- ✅ `FurnitureManipulatorVR.cs` → Mover/rotar
- ✅ `MaterialChangerVR.cs` → Cambiar colores
- ✅ `FurnitureSelectable.cs` → Detección de selección
- ✅ `CroquisSceneCompilerController.cs` → Comunicación servidor

---

## 📐 FLUJO COMPLETO (Detallado)

```
┌──────────────────────────────────────────────────────────────┐
│ 1. INICIO                                                    │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  PresetSelectorUI.ShowPresets()                             │
│  ├─ [Estándar 4.2×5.5m]                                    │
│  ├─ [Compacto 3.5×4.0m]                                    │
│  ├─ [Amplio 5.0×6.5m]                                      │
│  └─ [📷 Cargar Croquis Propio]                             │
│                                                              │
│  → Usuario selecciona UNO                                    │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 2. CARGAR JSON (Hardcodeado o del Servidor)                │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  IF presetSelected:                                          │
│    → LoadJSON("Resources/ScenePresets/preset_X.json")      │
│                                                              │
│  ELSE (Croquis propio):                                      │
│    → SketchLoaderUI.LoadSketchFromCamera()                 │
│    → SendToServer(texture) → JSON                           │
│                                                              │
│  jsonData = JSON.Parse(response)                            │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 3. VALIDACIÓN                                                │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  SceneValidator.ValidateJSON(jsonData)                      │
│                                                              │
│  IF NOT valid:                                               │
│    → ToastNotificationUI.ShowError(errors, 3s)             │
│    → Return to PresetSelector                               │
│                                                              │
│  ✓ VÁLIDO → Continuar                                       │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 4. LIMPIAR ESCENA ANTERIOR                                  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  SceneGenerator.CleanScene()                                │
│  ├─ DestroyAllGeneratedObjects()                           │
│  └─ Reset StateManager                                      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 5. MOSTRAR PANEL "FABRICANDO..."                            │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  LoadingPanelUI.Show()                                      │
│  ├─ Panel flotante en escena                               │
│  ├─ Progress bar: 0%                                        │
│  ├─ Texto: "Preparando escena..."                          │
│  └─ Color: Oscuro con borde verde                          │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 6. GENERACIÓN PROGRESIVA (ASYNC/AWAIT)                      │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  await SceneGenerator.GenerateSceneAsync(elements)          │
│                                                              │
│  FOR EACH element IN scene_elements:                        │
│                                                              │
│    ┌─ Step A: Wireframe                                     │
│    │  WireframeDrawer.DrawBounds(element.transform)        │
│    │  Duration: 0.3s                                        │
│    │  Color: Verde (#00FF00)                               │
│    │                                                        │
│    ├─ Step B: Delay                                         │
│    │  await Task.Delay(300ms)                              │
│    │                                                        │
│    ├─ Step C: Instanciar                                    │
│    │  IF type == "muro", "piso", etc:                      │
│    │    obj = ObjectInstantiator.InstantiateGeometry()     │
│    │  ELSE:                                                 │
│    │    prefabPath = PrefabMapper.GetPrefabPath(type)      │
│    │    obj = ObjectInstantiator.InstantiateFromPrefab()   │
│    │                                                        │
│    ├─ Step D: Aplicar Transform                            │
│    │  TransformApplier.ApplyTransform(obj, element)        │
│    │                                                        │
│    ├─ Step E: Aplicar Material                             │
│    │  MaterialApplier.ApplyColor(obj, color_hex)           │
│    │                                                        │
│    ├─ Step F: Fade-in                                      │
│    │  await FadeInAnimator.FadeInAsync(obj, 0.5s)          │
│    │                                                        │
│    ├─ Step G: Configurar Interactividad                    │
│    │  metadata = PrefabMapper.GetPrefabMetadata(type)      │
│    │  InteractivityBinder.BindScripts(obj, metadata)       │
│    │                                                        │
│    └─ Step H: Actualizar UI                                │
│       current = i + 1                                       │
│       progress = (current / total) * 100                    │
│       LoadingPanelUI.ShowProgress(progress, current)       │
│                                                              │
│  END FOR                                                     │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 7. FINALIZACIÓN                                              │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  LoadingPanelUI.Hide()                                      │
│                                                              │
│  ToastNotificationUI.ShowMessage(                           │
│    "¡Escena lista! 🎉", 2s)                                │
│                                                              │
│  StateManager.SetState(AppState.Recorrido)                 │
│                                                              │
│  Camera posición: (center_x, 1.7, center_z)               │
│                                                              │
└──────────────────────────────────────────────────────────────┘
                          ↓
┌──────────────────────────────────────────────────────────────┐
│ 8. INTERACCIÓN NORMAL (Usuario puede:)                      │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ✓ Mover/Rotar muebles (Sostener Select ~0.35s)           │
│  ✓ Cambiar colores (Toque corto)                           │
│  ✓ Eliminar (Doble-click → 🗑️ Eliminar)                    │
│  ✓ Cargar nuevo croquis (Botón 📷)                         │
│                                                              │
│  → Vuelve a PASO 1                                          │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 🎨 UI MOCKUP

### **Panel "Fabricando Escena"**
```
┌────────────────────────────────────────┐
│                                        │
│  🔨  Fabricando Escena...             │
│                                        │
│  ████████████░░░░░░░░  60%            │
│                                        │
│  Instanciando: Sofa_1                 │
│  Objetos: 12/20                       │
│  Tiempo estimado: 2.3s                │
│                                        │
└────────────────────────────────────────┘
```

### **Selector de Presets**
```
┌──────────────────────────────────┐
│  Selecciona un Monoambiente      │
├──────────────────────────────────┤
│                                  │
│  [ Estándar 4.2×5.5m ]          │
│  [ Compacto 3.5×4.0m ]          │
│  [ Amplio 5.0×6.5m ]            │
│  [ 📷 Cargar Croquis Propio ]   │
│                                  │
└──────────────────────────────────┘
```

### **Menú Eliminación (Doble-click)**
```
    Sofa_1
      │
      ├─ 🗑️ [Eliminar]
      └─ ❌ [Cancelar]
```

### **Toast Errors**
```
┌─────────────────────────────────┐
│ ❌ Error: Falta 'transform'    │
│    en objeto 'sofa_1'           │
│    (Se ocultará en 3s)          │
└─────────────────────────────────┘
```

---

## 🔍 PREGUNTAS RESUELTAS

### **1. ¿Colliders en prefabs?**
→ Asumiré que SÍ tienen. Si no, los agregaremos dinámicamente en `InteractivityBinder`.

### **2. ¿Fade-in sin CanvasGroup?**
→ Usaré `Renderer.material.color` con animación de alpha.

### **3. ¿Qué prefab usar de 50 variantes?**
→ Estrategia: `SelectVariant(confidence)` → más precisión = variante 01 (estándar).

### **4. ¿Muros/Puertas/Ventanas?**
→ Crear dinámicamente con `Cube + Material`.

### **5. ¿Escalas absolutas o relativas?**
→ JSON contiene **escala relativa** (1.0 = tamaño original).

---

## ✅ CHECKLIST ANTES DE CODIFICAR

- [ ] Verificar que prefabs tengan **colliders**
- [ ] Verificar que prefabs tengan **Renderer con Material**
- [ ] Crear carpeta `Assets/Resources/ScenePresets/`
- [ ] Crear 3-4 JSON presets (preset_standard.json, etc.)
- [ ] Crear Material para Muros/Piso/Puertas/Ventanas
- [ ] Verificar que Unity 6 soporta `async/await` ✓
- [ ] Decidir: ¿ScreenSpace o WorldSpace para LoadingPanel?
  - **Recomendación:** WorldSpace (flotante en escena)
- [ ] Decidir: ¿Con sonido al completar?
  - **Recomendación:** Sonido de éxito + visual toast

---

## 📌 RESUMEN EJECUTIVO

**Tienes TODO lo necesario para construir un sistema ROBUSTO:**

✅ 445 prefabs bien organizados  
✅ Estructura JSON clara y validable  
✅ Arquitectura escalable (9 layers)  
✅ Flujo UX progresivo con feedback  
✅ Sistema de interactividad completo  
✅ Manejo de errores definido  
✅ Unity 6 (async/await nativo)  

**Próximo paso:** Codificar cada componente siguiendo esta arquitectura.

---

**Status:** 🟢 **LISTO PARA CODIFICACIÓN**
