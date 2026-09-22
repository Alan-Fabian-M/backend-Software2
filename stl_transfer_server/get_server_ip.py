#!/usr/bin/env python3
"""
Script para obtener la IP local del servidor
Útil para configurar en Quest
"""

import socket
import subprocess
import sys
import platform

def get_local_ip():
    """Obtiene la IP local del servidor"""

    # Método 1: socket (más confiable)
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
        s.close()
        if ip.startswith(("192.", "10.", "172.")):
            return ip
    except:
        pass

    # Método 2: ifconfig (Linux/Mac)
    if sys.platform != "win32":
        try:
            result = subprocess.run(
                ["ifconfig"],
                capture_output=True,
                text=True
            )
            for line in result.stdout.split("\n"):
                if "inet " in line and not "127.0.0.1" in line:
                    ip = line.split()[1]
                    if ip.startswith(("192.", "10.", "172.")):
                        return ip
        except:
            pass

    # Método 3: ip addr (Linux alternativo)
    if sys.platform != "win32":
        try:
            result = subprocess.run(
                ["ip", "addr"],
                capture_output=True,
                text=True
            )
            for line in result.stdout.split("\n"):
                if "inet " in line and not "127.0.0.1" in line:
                    ip = line.split()[1].split("/")[0]
                    if ip.startswith(("192.", "10.", "172.")):
                        return ip
        except:
            pass

    # Método 4: ipconfig (Windows)
    if sys.platform == "win32":
        try:
            result = subprocess.run(
                ["ipconfig"],
                capture_output=True,
                text=True
            )
            for line in result.stdout.split("\n"):
                if "IPv4 Address" in line:
                    ip = line.split(":")[-1].strip()
                    if ip.startswith(("192.", "10.", "172.")):
                        return ip
        except:
            pass

    # Fallback
    return socket.gethostbyname(socket.gethostname())

if __name__ == "__main__":
    ip = get_local_ip()

    print()
    print("=" * 60)
    print("IP Local del Servidor STL")
    print("=" * 60)
    print()
    print(f"📡 http://{ip}:5000")
    print()
    print("Úsala en Quest en STLTransferClient.cs:")
    print(f'   pcServerURL = "http://{ip}:5000/upload-stl"')
    print()
    print("=" * 60)
