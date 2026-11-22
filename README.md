# CYBER RUNNER (Phase 3 — Five Futuristic Environments)

Original futuristic 3D endless runner built with **Unity 6000.0 LTS (`6000.0.86f1`)**, **URP 17.0.4**, and **C#**, engineered for smooth gameplay on low-end Android phones (2GB–3GB RAM) and automated Android APK/AAB builds via **Termux + GitHub Actions**.

- **Package ID**: `com.cyberrunner.game`
- **Orientation**: Portrait
- **Unity Version**: `6000.0.86f1` (Unity 6.0 3-Year LTS)
- **Current Build Version**: `v0.3.0` (Phase 3 — Environments)

---

## Five Futuristic Environments Included (Zero-GC Pooled)
1. **Sector 01 — Neon City**: Flanking skyscrapers, holographic billboards, overhead neon arches, and flying skyline hover-cars.
2. **Sector 02 — Cyber Underground**: Enclosed ribbed-steel industrial railway tunnels, glowing red/amber conduits, spinning ceiling exhaust fans, maglev trains, and laser traps.
3. **Sector 03 — Sky Highway**: Elevated floating bridge with suspension towers, distant neon skyscraper tips far below, passing sky-cars, and **fair jumpable Road Gaps** with warning chevrons & coin arcs!
4. **Sector 04 — Industrial Zone**: Heavy cyber-factories, pulsing green/amber energy generator cores, steel gantry cranes with animated swinging robotic arms, and spinning **Industrial Rotor** obstacles.
5. **Sector 05 — Digital Core**: Glowing circuit grid deck, floating data monoliths, rotating neon magenta/cyan energy rings, and vertical **Electric Energy Wall** obstacles.

Transitions automatically every **200 meters**, or tap **`NEXT ZONE >>`** on the HUD to immediately jump to and test any of the 5 sectors!

---

## Termux Quickstart (Using `CyberRunner-Phase3.zip`)
```bash
cp ~/storage/downloads/CyberRunner-Phase3.zip ~
unzip -o ~/CyberRunner-Phase3.zip -d ~
cd ~/CyberRunner

git init -b main
git add .
git commit -m "Phase 3: Environments - 5 futuristic biomes, dynamic atmosphere, road gaps, biome obstacles"
# Push to your GitHub repo and run Actions -> CYBER RUNNER Android Build
```
