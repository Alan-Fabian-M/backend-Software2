#!/bin/bash

echo ""
echo "===================================================="
echo "    STL Transfer Server - Iniciando..."
echo "===================================================="
echo ""

# Verificar que Python está instalado
if ! command -v python3 &> /dev/null; then
    echo "ERROR: Python3 no está instalado"
    echo ""
    echo "Por favor instala Python 3:"
    echo "  Ubuntu/Debian: sudo apt install python3 python3-pip"
    echo "  macOS: brew install python3"
    exit 1
fi

# Instalar dependencias si no están
echo "Verificando dependencias..."
pip3 install -q -r requirements.txt
if [ $? -ne 0 ]; then
    echo "ERROR al instalar dependencias"
    exit 1
fi

echo ""
echo "✅ Dependencias OK"
echo ""

# Iniciar servidor
echo "Iniciando servidor..."
echo ""
python3 stl_receiver_service.py

# Si el servidor se cierra por error
echo ""
echo "ERROR: El servidor se cerró inesperadamente"
read -p "Presiona Enter para salir..."
