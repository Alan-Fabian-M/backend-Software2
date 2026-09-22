# 🏗️ Scene Generation Pipeline - Implementation Guide

**Project:** InmobiliariaVR  
**Status:** Ready for Integration  
**Date:** 2026-09-16

---

## 📋 Overview

This guide covers the complete implementation of the scene generation pipeline that converts JSON data (from YOLO detection server or presets) into interactive VR scenes.

### Core Pipeline Flow

```
JSON Input (Croquis or Preset)
    ↓
SceneValidator → Validate structure
    ↓
SceneGenerator → Orchestrate pipeline
    ├─→ ClearPreviousScene
    ├─→ InstantiateSceneElement
    │   ├─→ CreateDynamicGeometry (for walls, floor, doors, windows)
    │   ├─→ PrefabMapper → Resolve prefab path
    │   └─→ InstantiatePrefab
    ├─→ ApplyTransforms → Position, Rotation, Scale
    ├─→ ApplyMaterials → Colors and textures
    ├─→ ShowWireframePreview → Visual feedback (0.3s)
    ├─→ FadeInAnimator → Progressive appearance (0.5s)
    └─→ BindInteractivity → Attach manipulation scripts
    ↓
Generated & Interactive Scene ✓
```

---

## 🗂️ File Structure

Create these directories in your Assets folder:

```
Assets/
├── Scripts/
│   ├── SceneGeneration/
│   │   ├── SceneValidator.cs
│   │   ├── SceneGenerator.cs
│   │   ├── PrefabMapper.cs
│   │   ├── WireframeDrawer.cs
│   │   ├── FadeInAnimator.cs
│   │   ├── SceneElementMetadata.cs
│   │   ├── FurnitureDeleterVR.cs
│   │   ├── ToastNotificationUI.cs
│   │   └── MaterialApplier.cs (create after)
│   └── Interaction/
│       ├── FurnitureManipulatorVR.cs (already exists)
│       └── MaterialChangerVR.cs (already exists)
│
├── Resources/
│   └── ScenePresets/
│       ├── preset_standard.json
│       ├── preset_compact.json
│       ├── preset_large.json
│       └── preset_loft.json
│
└── Prefabs/
    ├── Sofas/
    ├── Tables/
    ├── Beds/
    ├── Chairs/
    ├── Closets/
    ├── Drawers/
    ├── Cushions/
    ├── Kitchen/
    └── Bathroom/
```

---

## 📦 Scripts Created (7/13)

### ✅ 1. SceneValidator.cs
**Purpose:** Validates JSON structure before generation  
**Key Methods:**
- `ValidateSceneJSON(string json)` → Returns ValidationResult
- `ValidateRootStructure(JObject json, ValidationResult result)`
- `ValidateSceneElements(JArray elements, ValidationResult result)`
- `LogValidationResult(ValidationResult result)` → Debug output

**Checks:**
- Root fields: metadata, room_info, scene_elements
- Element fields: id, type, position, rotation, scale
- Valid coordinate values (x, y, z)
- Valid element types (muro, piso, sofa, mesa, cama, etc.)

**Example Usage:**
```csharp
var result = SceneValidator.ValidateSceneJSON(jsonString);
if (!result.isValid) {
    Debug.LogError($"Validation failed: {result.errors[0]}");
}
```

---

### ✅ 2. SceneGenerator.cs
**Purpose:** Main orchestrator for entire scene generation pipeline  
**Key Methods:**
- `GenerateSceneAsync(string jsonString, GenerationConfig config)`
- `InstantiateSceneElement(JObject elementData)` → GameObject
- `CreateDynamicGeometry(JObject elementData)` → GameObject (walls, floors)
- `InstantiatePrefab(JObject elementData)` → GameObject (furniture)
- `ApplyTransforms(GameObject obj, JObject elementData)`
- `ApplyMaterials(GameObject obj, JObject elementData)`
- `BindInteractivity()` → Attach manipulation scripts
- `ClearPreviousScene()`

**Events:**
- `OnProgress(int current, int total, string message)`
- `OnComplete(bool success, string message)`
- `OnError(string errorMessage)`

**Configuration:**
```csharp
config.showWireframes = true;        // Show green preview boxes
config.wireframeDuration = 0.3f;     // Preview duration
config.fadeInDuration = 0.5f;        // Fade animation duration
config.progressiveInstantiation = true;
config.instantiationParent = transform;
```

**Example Usage:**
```csharp
var generator = GetComponent<SceneGenerator>();
generator.OnProgress += (curr, total, msg) => Debug.Log($"{curr}/{total}: {msg}");
generator.OnComplete += (success, msg) => Debug.Log(msg);
generator.GenerateSceneAsync(jsonString);
```

---

### ✅ 3. PrefabMapper.cs
**Purpose:** Maps YOLO types to prefab paths and selects variants  
**Key Methods:**
- `ResolvePrefab(string type, float confidence)` → PrefabInfo
- `SelectVariant(string category, float confidence)` → int (variant number)
- `BuildPrefabPath(string category, int variant)` → string (full path)
- `GetMetadata(string type)` → FurnitureMetadata

**Type Mappings:**
```
sofa → Sofas (50 variants)
mesa → Tables (50 variants)
cama → Beds (50 variants)
silla → Chairs (50 variants)
estanteria/armario → Closets (50 variants)
cajon → Drawers (50 variants)
cojin → Cushions (50 variants)
muro/piso/puerta/ventana → Dynamic geometry
```

**Variant Selection Strategy:**
```
Confidence ≥ 0.85  → Variant 01 (standard)
0.70-0.85         → Variant 15-30 (medium range)
< 0.70            → Variant 1-15 (high variety)
```

---

### ✅ 4. WireframeDrawer.cs
**Purpose:** Visualizes object bounds during generation preview  
**Key Methods:**
- `DrawBounds(GameObject obj, Color color, float duration)`
- `DrawTransformBox(Transform transform, Color color, float duration)`
- `DrawSphere(Vector3 center, float radius, Color color, float duration)`

**Visualization:**
- Green wireframe boxes (0.3 seconds) show object detection
- Helps user understand scene generation progress
- Uses Debug.DrawLine (Scene view only, or with gizmo toggle)

---

### ✅ 5. FadeInAnimator.cs
**Purpose:** Progressive animations during object appearance  
**Key Methods:**
- `FadeIn(GameObject obj, float duration)` → IEnumerator (alpha 0→1)
- `ScaleIn(GameObject obj, float duration)` → IEnumerator (scale 0→full)
- `FadeAndScaleIn(GameObject obj, float duration)` → IEnumerator (combined)
- `MoveIntoPlace(GameObject obj, Vector3 targetPos, float duration)`
- `RotateIntoPlace(GameObject obj, Quaternion targetRot, float duration)`

**Example Usage:**
```csharp
StartCoroutine(FadeInAnimator.FadeIn(obj, 0.5f));
StartCoroutine(FadeInAnimator.FadeAndScaleIn(obj, 0.5f));
```

---

### ✅ 6. SceneElementMetadata.cs
**Purpose:** Stores metadata for each generated object  
**Fields:**
- `elementId` → Unique identifier
- `elementType` → Type (sofa, mesa, muro, etc.)
- `jsonData` → Original JSON for debugging
- `isSelectable/isDeletable/isColorable/isMovable` → Interaction flags

**Used By:**
- `InteractivityBinder` to determine which scripts to attach
- Scene regeneration to preserve object states
- Debug logging and scene analysis

---

### ✅ 7. FurnitureDeleterVR.cs
**Purpose:** Handles furniture deletion via double-click  
**Key Methods:**
- `DetectDoubleClick()` → Tracks clicks within 0.3s window
- `ShowDeleteConfirmation()` → Displays UI menu
- `ConfirmDelete()` → Destroys object
- `DeleteImmediately()` → Delete without confirmation

**Interaction Flow:**
```
User double-clicks furniture
    ↓
FurnitureDeleterVR detects (0.3s threshold)
    ↓
Confirmation menu appears (Red/Green buttons)
    ↓
User clicks confirm → Object destroyed
User clicks cancel → Menu closes
```

**Toast notification:** Shows when deletion is confirmed

---

### ✅ 8. ToastNotificationUI.cs
**Purpose:** Floating 3D notifications (errors, success, info)  
**Key Methods:**
- `Show(string message, float duration, NotificationType type)`
- `ShowError(string message, float duration)`
- `ShowSuccess(string message, float duration)`
- `ShowWarning(string message, float duration)`

**Notification Types:**
```
Error   → Red background (#FF0000)
Success → Green background (#00FF00)
Warning → Yellow background (#FFFF00)
Info    → Blue background (#0080FF)
```

**Features:**
- Auto-dismiss after duration (default 3s)
- Fade in (0.3s) + Display + Fade out (0.3s)
- Positioned in front of camera (offset: 0, 0.5, 2)
- World-space canvas for VR compatibility

---

## 📝 JSON Schema

### Preset JSON Structure
```json
{
  "metadata": {
    "name": "Monoambiente Estándar",
    "description": "...",
    "dimensions": {"width": 4.2, "depth": 5.5, "height": 2.8},
    "furniture_count": 8,
    "created_date": "2026-09-15T00:00:00Z"
  },
  "room_info": {
    "room_type": "monoambiente",
    "dimensions": {"x": 4.2, "y": 2.8, "z": 5.5},
    "flooring_material": "wood",
    "wall_color": "white"
  },
  "scene_elements": [
    {
      "id": "muro_norte",
      "type": "muro",
      "position": {"x": 0, "y": 0, "z": 5.5},
      "rotation": {"x": 0, "y": 0, "z": 0},
      "scale": {"x": 4.2, "y": 2.8, "z": 0.2},
      "material": {"type": "concrete", "color": "#FFFFFF"},
      "metadata": {
        "selectable": false,
        "deletable": false,
        "colorable": true,
        "movable": false
      }
    },
    ...
  ]
}
```

### YOLO Detection JSON (from Python server)
```json
{
  "metadata": {...},
  "room_info": {...},
  "scene_elements": [
    {
      "id": "object_1",
      "type": "sofa",
      "position": {"x": 1.5, "y": 0.5, "z": 2.0},
      "rotation": {"x": 0, "y": 0, "z": 0},
      "scale": {"x": 1.0, "y": 1.0, "z": 1.0},
      "confidence": 0.89,
      "yolo_bbox": [x1, y1, x2, y2],
      "prefab_category": "Sofas",
      "material": {"color": "#2C3E50"},
      "metadata": {
        "selectable": true,
        "deletable": true,
        "colorable": true,
        "movable": true
      }
    }
  ]
}
```

---

## 🔧 Setup Instructions

### Step 1: Copy Scripts to Project
1. Create `Assets/Scripts/SceneGeneration/` directory
2. Copy all 8 .cs files to this directory
3. Unity will auto-compile

### Step 2: Create Resources Folder
1. Create `Assets/Resources/ScenePresets/` directory
2. Copy 4 preset JSON files (preset_standard.json, etc.)
3. Rename them to remove .json extension in Resources (optional, or load with extension)

### Step 3: Add SceneGenerator to Sala_MVP Scene
1. Open Sala_MVP.unity
2. Create empty GameObject: "SceneGeneratorManager"
3. Add component: SceneGenerator
4. Configure in Inspector:
   - Show Wireframes: ✓
   - Wireframe Duration: 0.3
   - Fade In Duration: 0.5
   - Progressive Instantiation: ✓

### Step 4: Update CroquisSceneCompilerController
Replace manual JSON parsing with:
```csharp
private void OnCroquisReceived(string jsonResponse)
{
    SceneValidator.ValidateSceneJSON(jsonResponse);
    
    var generator = GetComponent<SceneGenerator>();
    generator.OnProgress += HandleProgress;
    generator.OnComplete += HandleComplete;
    generator.OnError += HandleError;
    
    generator.GenerateSceneAsync(jsonResponse);
}
```

### Step 5: Create UI Manager for Preset Selection
```csharp
public class PresetSelectorUI : MonoBehaviour
{
    private SceneGenerator sceneGenerator;
    
    public void LoadPreset(string presetName)
    {
        string json = Resources.Load<TextAsset>($"ScenePresets/preset_{presetName}").text;
        sceneGenerator.GenerateSceneAsync(json);
    }
}
```

---

## 🎮 Testing Workflow

### Test 1: Load Standard Preset
```csharp
string json = Resources.Load<TextAsset>("ScenePresets/preset_standard").text;
generator.GenerateSceneAsync(json);
```
Expected: 8 objects appear with wireframe preview + fade-in animation

### Test 2: Double-Click Delete
1. Generate scene
2. Double-click on furniture (mesa, sofa, cama)
3. Confirm button appears
4. Click confirm → Object deleted + Toast notification

### Test 3: Color Interaction
1. Generate scene
2. Touch wall or sofa (short tap)
3. Material should change
4. Verify MaterialChangerVR still works

### Test 4: Move Furniture
1. Generate scene
2. Hold Select button (~0.35s) on furniture
3. Move/rotate buttons appear
4. Verify FurnitureManipulatorVR still works

### Test 5: Invalid JSON
```csharp
string invalidJson = "{\"metadata\": {}}"; // Missing required fields
generator.GenerateSceneAsync(invalidJson);
```
Expected: OnError event fires, ValidationResult shows errors, Toast notification displays

---

## 🐛 Debugging

### Enable Detailed Logging
```csharp
// In SceneValidator
SceneValidator.LogValidationResult(validationResult);

// In SceneGenerator
generator.OnProgress += (curr, total, msg) => 
    Debug.Log($"[SceneGen] {curr}/{total}: {msg}");
```

### Wireframe Not Showing?
- Wireframes use Debug.DrawLine (Scene view only by default)
- Enable Gizmos in Game view
- Increase wireframe duration in config if too fast

### Objects Not Fading In?
- Check if Renderer component exists on prefabs
- Verify materials support alpha transparency
- Check console for shader warnings

### Prefab Not Found?
- Verify path: `Assets/Prefabs/Sofas/Sofa01.prefab` exists
- Check naming consistency (01 vs 1, case sensitivity)
- Use Resources.Load() without .prefab extension

---

## 📊 Performance Considerations

### Scene Generation Time
```
Validation:      ~10ms
Clear previous:  ~5ms per 10 objects
Instantiation:   ~20-50ms per object
Wireframe:       ~0.3s per object (visual only)
Fade animation:  ~0.5s (parallel for all objects)
Interactivity:   ~10ms per object
────────────────────────
Total:          2-5 seconds for 8 objects
```

### Optimization Tips
1. **Pre-load prefabs** during scene startup
2. **Use Object Pooling** for frequently deleted/added objects
3. **Batch scene elements** - don't generate one at a time
4. **Reduce wireframe duration** (0.1s instead of 0.3s) for faster feedback
5. **Skip fade animation** for smaller objects (config.progressiveInstantiation = false)

---

## 🚀 Next Steps (5 Remaining Scripts)

After these 8 are integrated, create:

1. **MaterialApplier.cs** - Advanced material application
   - Support for different shader types
   - Realistic material mapping (wood, concrete, glass)
   - PBR texture support for furniture

2. **InteractivityBinder.cs** - Automated script binding
   - Attach scripts based on metadata
   - Configure interaction parameters
   - Setup colliders and rigidbodies

3. **PresetSelectorUI.cs** - Preset selection menu
   - Display 4 preset options
   - Load and generate on selection
   - Show scene preview

4. **LoadingPanelUI.cs** - Generation progress feedback
   - Floating progress bar
   - Estimated time remaining
   - Cancel generation option

5. **SketchLoaderUI.cs** - Croquis image capture
   - Camera access in VR
   - Image upload to Python server
   - Handle server response

---

## 📞 Support & Debugging

### Common Issues & Solutions

**"No prefab at path X"**
- Check exact filename matches prefab name
- Verify Resources folder structure
- Use PrefabMapper logging to see resolved path

**"Scene elements not moving after generation"**
- Check if FurnitureManipulatorVR is attached
- Verify XRSimpleInteractable is present
- Check Rigidbody constraints

**"Toast notifications not showing"**
- Ensure TextMeshPro is imported
- Check Canvas render mode is WorldSpace
- Verify ToastNotificationUI instance exists

---

Generated with ❤️ for InmobiliariaVR Project
