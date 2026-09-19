# Guía de Entrenamiento CubiCasa5K con YOLOv8

El dataset ha sido descargado y convertido a formato YOLO compatible con **Ultralytics YOLO** tanto para **Segmentación de Instancias** (`YOLOv8-seg`) como para **Detección de Objetos** (`YOLOv8`).

---

## 1. Clases del Dataset

| ID | Clase | Descripción |
| :---: | :--- | :--- |
| **0** | `bathroom` | Baños y zonas húmedas |
| **1** | `bed` | Camas |
| **2** | `door` | Puertas de acceso y paso |
| **3** | `kitchen` | Cocinas y mesones |
| **4** | `room` | Habitaciones / espacios habitables |
| **5** | `stairs` | Escaleras |
| **6** | `wall` | Muros y paredes |
| **7** | `window` | Ventanas |

---

## 2. Archivos del Dataset

- **Configuración:** `YOLO/cubicasa_coco/data.yaml`
- **Imágenes:** `YOLO/cubicasa_coco/images/train/` (4.228) y `images/valid/` (748)
- **Etiquetas YOLO:** `YOLO/cubicasa_coco/labels/train/` (4.228) y `labels/valid/` (748)
- **Anotaciones originales COCO:** `YOLO/cubicasa_coco/annotations/`

---

## 3. Entrenamiento Local

### Segmentación (Recomendado para paredes y habitaciones)
```bash
.venv\Scripts\python -m training.train_yolo --task segment --model yolov8n-seg.pt --epochs 10 --batch 4 --imgsz 640
```

### Detección tradicional (Bounding boxes)
```bash
.venv\Scripts\python -m training.train_yolo --task detect --model yolov8n.pt --epochs 10 --batch 8 --imgsz 640
```

> **Nota:** Al entrenar en CPU local (Intel UHD Graphics), cada época tomará tiempo. Si dispones de GPU NVIDIA o acceso a Google Colab, el proceso es hasta 30 veces más rápido.

---

## 4. Entrenamiento en Google Colab (GPU Gratis T4)

Para entrenar con GPU gratuita en Google Colab:
1. Abre un notebook nuevo en [Google Colab](https://colab.research.google.com).
2. Selecciona Entorno de ejecución > Cambiar tipo de entorno > **T4 GPU**.
3. Ejecuta esta celda única:

```python
# 1. Instalar Ultralytics y datasets
!pip install -q ultralytics datasets huggingface_hub

# 2. Descargar y preparar dataset directamente en Colab
from datasets import load_dataset, Image
import os
from pathlib import Path
from tqdm import tqdm

# Cargar dataset
ds_train = load_dataset("phungpx/cubicassa5k-coco", split="train")
ds_valid = load_dataset("phungpx/cubicassa5k-coco", split="valid")

# Crear carpetas
for s in ["train", "valid"]:
    os.makedirs(f"dataset/images/{s}", exist_ok=True)
    os.makedirs(f"dataset/labels/{s}", exist_ok=True)

# Guardar imagenes y etiquetas
CAT_MAP = {1:0, 2:1, 3:2, 4:3, 5:4, 6:5, 7:6, 8:7}

for split_name, ds in [("train", ds_train), ("valid", ds_valid)]:
    for row in tqdm(ds, desc=split_name):
        fn = Path(row["file_name"]).stem
        # Guardar imagen
        row["image"].save(f"dataset/images/{split_name}/{row['file_name']}")
        # Guardar labels seg
        w, h = float(row["width"]), float(row["height"])
        lines = []
        anns = row["annotations"]
        for cat_id, segs in zip(anns["category_id"], anns["segmentation"]):
            if cat_id in CAT_MAP:
                c = CAT_MAP[cat_id]
                for seg in segs:
                    if len(seg) >= 6:
                        coords = [f"{min(max(seg[i]/w, 0.), 1.):.6f} {min(max(seg[i+1]/h, 0.), 1.):.6f}" for i in range(0, len(seg), 2)]
                        lines.append(f"{c} " + " ".join(coords))
        with open(f"dataset/labels/{split_name}/{fn}.txt", "w") as f:
            f.write("\n".join(lines))

# 3. Crear data.yaml
yaml_content = \"\"\"
path: /content/dataset
train: images/train
val: images/valid
names:
  0: bathroom
  1: bed
  2: door
  3: kitchen
  4: room
  5: stairs
  6: wall
  7: window
\"\"\"
with open("data.yaml", "w") as f:
    f.write(yaml_content)

# 4. Entrenar YOLOv8 con GPU T4
from ultralytics import YOLO
model = YOLO("yolov8n-seg.pt")
results = model.train(data="data.yaml", epochs=30, imgsz=640, batch=16, device=0)
```
