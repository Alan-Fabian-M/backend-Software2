# 📊 Análisis Detallado: Escena Sala_MVP.unity

**Proyecto:** InmobiliariaVR  
**Escena:** Sala_MVP.unity  
**Fecha de análisis:** 2026-09-15  
**Total de GameObjects:** 67

---

## 🏗️ Estructura General

La escena está organizada en **6 categorías principales**:

### 1. **Elementos de Escena** (Arquitectura base)
Geometría estática del entorno VR:
- **Paredes:** Pared_Norte, Pared_Sur, Pared_Este, Pared_Oeste
- **Estructura:** Piso, Puerta, Ventana, Environment
- **Interacción VR:** Teleport Area, Teleport Area Setup

*Estado:* ✅ Todas las paredes tienen `MaterialChangerVR` para cambiar colores.

---

### 2. **Muebles Interactivos** (Objetos personalizables)

#### Objetos Principales:
- **Sofa** → Cicla entre 3 materiales (Cuero, Azul, Verde)
- **Mesa** → Movible y rotable
- **TV** → Movible y rotable

#### Botones de Control de Muebles:
```
Sofa:
  - Btn_Sofa0 → Material: Cuero
  - Btn_Sofa1 → Material: Azul
  - Btn_Sofa2 → Material: Verde

Paredes:
  - Btn_Pared0 → Color: Blanco
  - Btn_Pared1 → Color: Beige
  - Btn_Pared2 → Color: Gris
```

*Scripts asociados:*
- `MaterialChangerVR.cs` → Cambio de materiales
- `FurnitureManipulatorVR.cs` → Mover/rotar muebles
- `FurnitureManipulatorManager` → Gestor central

---

### 3. **UI - Sistema de Menús**

#### Menú Principal (Pestañas)
```
MenuPrincipal
├── MenuTabs (Selector de pestañas)
├── MenuPersonalizacion (Cambio de colores)
├── MenuPintura (Modo pintura con rayo)
├── MenuDisenoIA (Generador de layout por texto)
├── MenuCroquisIA (Carga de croquis/IA visual)
└── MenuExportar (Exportación a STL)
```

#### Gestión de Menús:
- **MenuPrincipalManager** → Controla apertura/cierre
- **Boton_Menu** → Botón de activación
- **Scripts:** `ToggleMenuVR.cs`, `SideMenuVR.cs`

---

### 4. **UI - Botones de Acción**

#### Botones de Movimiento de Muebles:
- `Btn_Adelante`, `Btn_Atras`, `Btn_Izquierda`, `Btn_Derecha`
- `Btn_RotarIzq`, `Btn_RotarDer`

#### Botones de Generación de Diseño:
- `Btn_DisenoIA` → Abre menú de diseño por IA (texto)
- `Btn_Generar` → Genera layout de sala
- `Btn_CargarCroquis` → Carga imagen de croquis
- `Btn_CroquisIA` → Procesa croquis con IA visual

#### Botones de Exportación:
- `Btn_Exportar` → Inicia exportación a STL
- `Btn_ExportarAhora` → Ejecuta la exportación

#### Botones de Entrada:
- `Btn_Enviar` → Envía comando de IA
- `Btn_Micro` → Activa reconocimiento de voz

#### Botones de Navegación:
- `Btn_Cerrar` → Cierra menús

---

### 5. **UI - Paneles de Entrada/Salida**

#### Panel de IA Local:
- **IA_Panel** → Panel flotante del asistente IA
- **Campo_Comando** → Campo de texto para comandos (ej: "cambia la pared a gris")
- **Texto_Estado** → Muestra estado de operaciones
- *Script:* `OllamaAIController.cs` → Conecta con Ollama (`localhost:11434`)

#### Panel de Diseño por IA:
- **MenuDisenoIA** → Entrada de descripción natural de sala
- **Campo_Descripcion** → Campo para describir layout (ej: "sofá pared oeste, mesa centro")
- *Script:* `RoomLayoutAIController.cs` → Repositor muebles automáticamente

#### Panel de Croquis:
- **MenuCroquisIA** → Carga y procesa imágenes
- **Btn_CargarCroquis** → Selecciona foto de croquis
- **Btn_Generar** → Procesa con YOLOv8 + OpenCV + OCR
- *Script:* `CroquisSceneCompilerController.cs` → Comunica con servidor Python (`localhost:8765`)

---

### 6. **Sistemas de Control y Rendering**

#### Managers (Controladores globales):
- **StateManager** → Máquina de estados (`AppState.Recorrido`, `.Personalizacion`, `.Pintura`)
- **IALocalManager** → Gestor de conexión con Ollama
- **FurnitureManipulatorManager** → Control de interacción con muebles
- **StlExportManager** → Exportación a STL 3D

#### Sistema de Entrada:
- **EventSystem** → XR UI Input Module (raycast, selección por mano)

#### Iluminación y Rendering:
- **Directional Light** → Luz principal de la escena
- **Luz_Calida_Living** → Luz ambiental cálida
- **Global Volume** → Configuración de Post-Processing
- **Post Process Volume** → Efectos visuales (bloom, color grading, etc.)
- **Reflection Probe** → Reflejos en tiempo real
- **Light Probe Group** → 27 puntos de iluminación (3×3×3 grid)
- **Lighting** → Datos de lightmapping bakeado

#### Utilidades:
- **Grid** → Rejilla visual (posiblemente para debugging)

---

## 🎮 Flujos Principales de Interacción

### Flujo 1: Cambio de Colores (Toque corto)
```
Usuario toca pared/sofá (corto) 
  → MaterialChangerVR.TryPaint() 
  → Cicla material siguiente
```

### Flujo 2: Mover/Rotar Muebles (Toque largo)
```
Usuario sostiene Select ~0.35s sobre mueble
  → FurnitureSelectable detecta tiempo
  → Abre MenuMoverMueble
  → Botones Adelante/Atras/Izq/Der + RotarIzq/Der
```

### Flujo 3: Cambio por Menú de Personalización
```
Abre MenuPersonalizacion (Btn_Pared0/1/2, Btn_Sofa0/1/2)
  → Click en botón
  → MaterialChangerVR.SetMaterialByIndex(índice)
  → Aplica material exacto
```

### Flujo 4: Pintura con Rayo (Modo Pintura)
```
Abre pestaña "Pintar" en MenuPrincipal
  → Selecciona color (Blanco/Beige/Gris)
  → Apunta con rayo y presiona Grip (tecla G en simulador)
  → RayPaintVR.TryPaint() aplica color
```

### Flujo 5: Generación de Layout por IA (Texto)
```
Abre pestaña "Diseñar" → MenuDisenoIA
  → Escribe descripción: "sofá pared oeste, mesa centro, tv norte"
  → Click "Generar"
  → RoomLayoutAIController interpreta → Repositor muebles
```

### Flujo 6: Procesamiento de Croquis (Imagen + IA)
```
Abre pestaña "Croquis" → MenuCroquisIA
  → Click "Cargar Croquis..." → Selecciona imagen
  → Click "Generar"
  → Envía a servidor Python (localhost:8765)
    → YOLOv8 detecta muebles
    → OpenCV extrae contornos
    → Tesseract OCR lee dimensiones
    → Retorna Scene Graph JSON
  → CroquisSceneCompilerController aplica posiciones
```

### Flujo 7: Exportación a STL (Impresión 3D)
```
Abre pestaña "Exportar" → MenuExportar
  → Click "Exportar a STL"
  → StlExporter recorre la jerarquía
  → Genera STL binario (escala 1:50)
  → Guarda en AppData/Exports/InmobiliariaVR_<fecha>.stl
```

### Flujo 8: IA Local (Comandos por Texto)
```
Escribe en IA_Panel: "cambia la pared a gris"
  → Click "Enviar"
  → OllamaAIController POST a localhost:11434/api/generate
  → Ollama (gemma2:2b) interpreta → JSON: {"target":"pared","color":"gris"}
  → MaterialChangerVR.SetMaterialByIndex aplica
```

### Flujo 9: Reconocimiento de Voz (Opcional - Windows)
```
Click "Hablar" en IA_Panel
  → VoiceCommandController.DictationRecognizer captura audio
  → Windows Speech Recognition transcribe (si está configurado)
  → Resultado se envía como texto a OllamaAIController
  → Mismo flujo que Flujo 8
```

---

## 📋 Scripts Clave Mapeados a GameObjects

| Script | GameObject | Responsabilidad |
|--------|-----------|-----------------|
| `StateManager.cs` | StateManager | Estado global (Recorrido/Personalización/Pintura) |
| `MaterialChangerVR.cs` | Pared_* / Sofa | Cambio de materiales por índice |
| `FurnitureManipulatorVR.cs` | Sofa/Mesa/TV | Mover y rotar en runtime |
| `FurnitureSelectable.cs` | Sofa/Mesa/TV | Detectar tiempo de sostenido para abrir menú |
| `MenuControllerVR.cs` | MenuPersonalizacion | Botones del menú de colores |
| `ToggleMenuVR.cs` | MenuPrincipalManager | Abrir/cerrar menú principal |
| `SideMenuVR.cs` | MenuTabs | Cambio de pestañas (Recorrido/Personalizar/Pintar) |
| `OllamaAIController.cs` | IALocalManager | Comunicación con Ollama local |
| `VoiceCommandController.cs` | IALocalManager | Reconocimiento de voz (Windows) |
| `RoomLayoutAIController.cs` | (En MenuDisenoIA) | Generación de layout por texto |
| `CroquisSceneCompilerController.cs` | (En MenuCroquisIA) | Procesamiento de croquis (YOLO+OCR) |
| `RayPaintVR.cs` | (Sistema de raycast) | Pintura en modo Pintura |
| `StlExporter.cs` | StlExportManager | Exportación a STL 3D |

---

## 🔧 Componentes Unity por Categoría

### Interacción VR:
- ✅ XRRig (heredado del template)
- ✅ XRInteractionManager
- ✅ XRSimpleInteractable (en paredes, muebles, botones)
- ✅ XRUIInputModule (manejo de UI en VR)

### Rendering:
- ✅ Canvas (World Space) para menús 3D
- ✅ TrackedDeviceGraphicRaycaster (input en Canvas)
- ✅ Lightmaps bakeadas (1 × 512×512 por escena)
- ✅ Reflection Probes (bakeadas)
- ✅ Light Probe Group (27 probes en grid 3×3×3)

### Iluminación:
- ✅ Baked Global Illumination (activado)
- ✅ Mixed Lighting en modo Shadowmask
- ✅ Lightmapper: Progressive GPU

---

## 🚀 Estado de Completitud

| Aspecto | Estado |
|--------|--------|
| Geometría base | ✅ Completa (primitivas) |
| Muebles reales | ✅ 3 modelos (Sofa, Mesa, TV) |
| Interacción básica | ✅ 100% funcional |
| Cambio de materiales | ✅ Implementado y probado |
| Movimiento de muebles | ✅ Funcional (sostener Select) |
| Menú de UI | ✅ 5 pestañas funcionales |
| IA Local (Texto) | ✅ Ollama integrado |
| IA Visual (Croquis) | ✅ YOLO+OpenCV+OCR en servidor Python |
| Generación de Layout | ✅ Funcionando (~45-50s) |
| Reconocimiento de voz | ⚠️ Código listo, no validado en micrófono real |
| Exportación 3D | ✅ STL binario implementado |
| Iluminación | ✅ Bakeada (512×512 lightmap) |
| Modelo definitivo de Blender | ⏳ Disponible en `Monoambiente_Jazmin.unity` |

---

## 🎯 Próximos Pasos Recomendados

1. **Integrar modelo de Blender completo** → Reemplazar primitivas en `Sala_MVP`
2. **Validar voz con micrófono real** → Confirmar `KeywordRecognizer` en Windows
3. **Probar en Meta Quest 3** → Hardware real, no solo simulador
4. **Croquis real dibujado a mano** → Validar OCR con escritura real
5. **Hornear iluminación del modelo final** → Re-bakear lightmaps

---

## 📊 Resumen Ejecutivo

**Sala_MVP** es una escena VR **completamente funcional** con:
- 🏗️ 4 paredes + piso + mobiliario interactivo
- 🎮 3 sistemas de interacción (toque, menú, pintura)
- 🤖 IA local (Ollama) + IA visual (YOLO+OCR)
- 📐 Generación automática de layouts por descripción
- 🖼️ Exportación a STL para impresión 3D
- 💾 Estado global centralizado
- ⚡ Rendering optimizado con lightmaps bakeadas

**Calidad:** Prototipo MVP listo para demostraciones y pruebas en VR real.
