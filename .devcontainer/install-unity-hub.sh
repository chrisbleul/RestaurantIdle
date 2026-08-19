#!/usr/bin/env bash
# Einmaliger Setup-Schritt, der ausschliesslich in diesem Codespace laeuft --
# fuer Leute ohne eigenen Windows/Mac/Linux-Rechner, um trotzdem eine
# Unity-Personal-Lizenz zu aktivieren (siehe README "Unity-CI einrichten").
# Unity Hub braucht dafuer eine echte GUI mit Login -- das devcontainer-Feature
# "desktop-lite" stellt genau das ueber noVNC im Browser bereit (Port 6080).
set -euo pipefail

sudo mkdir -p /etc/apt/keyrings
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /etc/apt/keyrings/unityhub.gpg > /dev/null
echo "deb [signed-by=/etc/apt/keyrings/unityhub.gpg] https://hub.unity3d.com/linux/repos/deb stable main" \
  | sudo tee /etc/apt/sources.list.d/unityhub.list > /dev/null

sudo apt-get update -qq
sudo apt-get install -y -qq unityhub libgtk-3-0 libnss3 libasound2t64 xdg-utils

echo ""
echo "Unity Hub installiert. Naechste Schritte:"
echo "1. Port 6080 im 'Ports'-Tab von Codespaces oeffnen (Passwort: unity)"
echo "2. Im Desktop-Terminal (Rechtsklick -> Terminal) 'unityhub' eingeben"
echo "3. Mit Unity-Account einloggen, Preferences -> Licenses -> Add -> kostenlose Personal-Lizenz"
echo "4. Lizenzdatei liegt danach unter ~/.local/share/unity3d/Unity/Unity_lic.ulf"
echo "   -- im normalen Codespaces-Datei-Explorer (nicht im Desktop) oeffnen und Inhalt kopieren"
