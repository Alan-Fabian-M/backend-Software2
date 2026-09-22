# Repositorios de Referencia y Mejores Ideas (GitHub) — InmobiliariaVR

Este documento recopila proyectos de código abierto en GitHub directamente relacionados con el desarrollo de **recorridos inmobiliarios en VR**, **personalización modular de interiores**, **integración con IA local (Ollama)** y **aplicaciones móviles con sensores (AR)**. Sirve como banco de ideas técnicas y arquitectónicas para la entrega del Primer Parcial (ISW2).

---

## 1. Recorridos Inmobiliarios y Diseño de Interiores en VR

Proyectos completos en Unity orientados a la arquitectura interactiva y bienes raíces:

### 🏠 [VR-Real-Estate (NewtonL)](https://github.com/NewtonL/VR-Real-Estate)
- **Descripción:** Visualizador y recorrido inmobiliario interactivo desarrollado para realidad virtual en Unity.
- **Aspectos aplicables al proyecto:**
  - Sistema de navegación suave y teletransportación para evitar mareos (motion sickness).
  - Manejo de colisiones con muros y límites perimetrales del departamento.
  - Configuración óptima de cámaras y campos de visión para interiores.

### 🛋️ [VNE5T — VR Interior Design Studio (pnlt)](https://github.com/pnlt/VNE5T-SolutionForAVR)
- **Descripción:** Aplicación VR orientada a visores Meta Quest que permite manipulación de muebles en tiempo real, cambio dinámico de materiales y ajuste de escala de interiores.
- **Aspectos aplicables al proyecto:**
  - Selección de objetos a distancia mediante punteros/raycast.
  - Interfaz de usuario flotante anclada al espacio para cambiar acabados y texturas (paredes, pisos y tapicería).
  - Jerarquía clara de elementos decorativos modificables vs. estructura fija.

### 📐 [VRHousePlanner (MagazzuGaetano)](https://github.com/MagazzuGaetano/VRHousePlanner)
- **Descripción:** Proyecto Unity multiplataforma (VR / Android) que permite explorar distribuciones arquitectónicas y visualizar viviendas.
- **Aspectos aplicables al proyecto:**
  - Compilación cruzada para visores standalone (Android / Meta Quest).
  - Representación esquemática y modular de ambientes (sala, dormitorios, baños).

---

## 2. Personalización Modular y Cambio de Muebles / Materiales

Repositorios que implementan la lógica de cambio de acabados y configuradores:

### 🧩 [Furniture Constructor (nnivis)](https://github.com/nnivis/furniture-constructor)
- **Descripción:** Configurador modular de muebles y materiales en tiempo de ejecución para Unity, impulsado por archivos **JSON**.
- **Aspectos aplicables al proyecto:**
  - Mapeo directo entre datos JSON y GameObjects/Materiales en Unity.
  - Dado que Ollama devuelve respuestas en JSON (`{"target": "sofa", "color": "azul"}`), este enfoque permite desacoplar la lógica de IA del renderizado.
  - Facilidad para agregar nuevos materiales o muebles sin modificar código C#.

### 🎨 [Beemeral Room Builder (PabloMzGa)](https://github.com/PabloMzGa/Beemeral-Unity-Prototype)
- **Descripción:** Prototipo avanzado de construcción y ambientación de habitaciones con herramientas de edición y asignación dinámica de materiales.
- **Aspectos aplicables al proyecto:**
  - Lógica de asignación de materiales en tiempo real en grupos de objetos (ej. todas las paredes simultáneamente).
  - Manejo eficiente de iluminación dinámica vs. estática.

---

## 3. Integración de IA Local (Unity + Ollama)

Proyectos que resuelven la comunicación eficiente entre Unity y modelos locales:

### 🧠 [ollama-unity (Haoming02)](https://github.com/Haoming02/ollama-unity)
- **Descripción:** Cliente y wrapper de API completo para conectar Unity con servidores Ollama locales.
- **Aspectos aplicables al proyecto:**
  - Manejo robusto de llamadas asíncronas con `UnityWebRequest`.
  - Soporte de streaming (mostrar la respuesta palabra por palabra en la UI en lugar de esperar la respuesta completa).
  - Manejo de excepciones cuando el servicio de Ollama no está activo.

### 💬 [EasyLocalLLM (kamekichi128)](https://github.com/kamekichi128/EasyLocalLLM)
- **Descripción:** Biblioteca para simplificar el uso de LLMs locales en Unity para agentes y asistentes virtuales.
- **Aspectos aplicables al proyecto:**
  - Inyección de contexto y reglas del sistema (*System Prompts*) para forzar al modelo a responder estrictamente en el formato deseado.

---

## 4. Para el Requisito Obligatorio: App Móvil con Sensores (AR)

El examen exige una aplicación móvil pertinente que aproveche sensores (cámara, acelerómetro, micrófono). Proyectos de referencia:

### 📱 [AR Furniture App (shrinivask007)](https://github.com/shrinivask007/AR_Furniture_App)
- **Descripción:** Aplicación móvil en Unity que utiliza Realidad Aumentada para posicionar muebles 3D sobre planos reales detectados por la cámara del teléfono.
- **Aspectos aplicables al proyecto:**
  - Detección de planos horizontales (suelo) mediante AR Foundation / ARCore.
  - Reutilización de los mismos prefabs 3D de muebles del proyecto VR en la app móvil.
  - Uso de sensores: Cámara (sensor visual) + Micrófono para dictarle a Ollama qué mueble colocar.

### 💾 [Room Designer (TeamFWS)](https://github.com/TeamFWS/room-designer)
- **Descripción:** App de diseño de interiores con catálogo y funciones completas de **Guardar y Cargar**.
- **Aspectos aplicables al proyecto:**
  - Persistencia del diseño del usuario en un archivo local o envío hacia el backend/marketplace.

---

## 5. Resumen de Ideas Clave a Incorporar en el Proyecto

1. **Guardado de Configuración (Cotización en JSON):**
   - Implementar un botón *"Confirmar Diseño"* que guarde un JSON con la combinación elegida por el cliente (tipo de piso, color de paredes, modelo de sofá). Cumple el flujo hacia el Backend/Marketplace.
2. **Streaming en el Asistente Ollama:**
   - Mostrar la respuesta del asistente en el panel de VR en tiempo real para reducir la sensación de espera del usuario.
3. **Puntero de Selección con Resaltado (Outline):**
   - Cuando el usuario o la IA interactúe con un mueble, encender un contorno suave para retroalimentación visual inmediata.
4. **Modo Día / Noche:**
   - Botón en el menú para alternar entre iluminación diurna y nocturna interior, aportando gran valor visual inmobiliario con mínimo esfuerzo técnico.
