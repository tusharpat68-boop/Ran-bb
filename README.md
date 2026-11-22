# CYBER RUNNER (Phase 2 — Core Gameplay)

Original futuristic 3D endless runner built with **Unity 6000.0 LTS (`6000.0.86f1`)**, **URP 17.0.4**, and **C#**, engineered for smooth gameplay on low-end Android phones (2GB–3GB RAM) and automated Android APK/AAB builds via **Termux + GitHub Actions** without a desktop computer.

- **Package ID**: `com.cyberrunner.game`
- **Orientation**: Portrait
- **Unity Version**: `6000.0.86f1` (Unity 6.0 3-Year LTS)
- **Current Build Version**: `v0.2.0` (Phase 2 — Core Gameplay)

---

## Phase 2 Core Gameplay Features Included
- **Three-Lane Movement**: Swipe Left / Right to switch lanes smoothly (`-2.9m`, `0m`, `+2.9m`) with lane input buffering and lateral banking animation.
- **Jumping & Cyber-Sliding**: Swipe Up to jump (coyote time + jump buffer); Swipe Down to cyber-slide under laser gates & drones, or swipe down mid-air for a crisp fast-fall slam-to-slide!
- **5 Pooled Cyberpunk Obstacles**:
  1. `LowRoadBarrier` (Amber hazard strip — jump over)
  2. `LaserGate` (Crimson overhead beams — slide under)
  3. `TallCyberBlock` (Security monolith — dodge lanes)
  4. `MovingCyberTrain` (Approaching futuristic maglev train with headlights)
  5. `HoverSecurityDrone` (Oscillating mid-height drone — slide under or dodge)
- **Fair Endless Track Generator**: Pre-warmed 6-segment highway with 2 obstacle-free intro segments and 8 mathematically fair obstacle/coin patterns that always preserve a reachable safe path.
- **Energy Coin System**: Pooled glowing gold/cyan energy cores spawning in lane lines, jump arcs, and slide trails with anti-duplication guards and persistent balance.
- **Side-Graze Stumble Forgiveness + Instant Zero-Lag Restart**: Clipping an edge while switching lanes causes a recoverable stumble with camera shake; head-on collisions show the Run Summary with instant `RUN AGAIN`.

---

## Termux Quickstart (Using `CyberRunner-Phase2.zip`)

### 1. Unzip in Termux & Push to GitHub
```bash
pkg update && pkg upgrade -y
pkg install git gh unzip -y
termux-setup-storage
gh auth login

# Copy zip from Android Downloads into Termux home and extract
cp ~/storage/downloads/CyberRunner-Phase2.zip ~
unzip -o ~/CyberRunner-Phase2.zip -d ~
cd ~/CyberRunner

git init -b main
git add .
git commit -m "Phase 2: Core Gameplay - 3-lane movement, sliding, obstacles, coins, endless track"
gh repo create cyber-runner --private --source=. --remote=origin --push
```
*(If your repo already exists on GitHub, instead of `gh repo create`, run `git remote add origin https://github.com/<YOUR_USER>/cyber-runner.git && git push -u origin main --force` or pull/commit normally.)*

### 2. Activate Free Unity Personal License (One Time, Phone Browser Only)
1. GitHub Repo -> **Actions** -> **Unity License Activation Request** -> **Run workflow**.
2. Download the `.alf` file artifact and upload it at `https://license.unity3d.com/manual` (choose Unity Personal) to get your `.ulf` file.
3. Add repo secrets under **Settings -> Secrets and variables -> Actions**:
   - `UNITY_LICENSE` (full text of `.ulf`)
   - `UNITY_EMAIL`
   - `UNITY_PASSWORD`

### 3. Build & Install Phase 2 APK
1. GitHub Repo -> **Actions** -> **CYBER RUNNER Android Build** -> **Run workflow**.
2. Download `CyberRunner-Android-v0.2.0-build<N>`, extract `CyberRunner.apk`, and install on your Android phone!
