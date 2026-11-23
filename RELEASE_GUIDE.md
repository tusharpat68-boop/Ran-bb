# CYBER RUNNER (`com.cyberrunner.game`) — Android Release Guide (Termux + GitHub Actions)

This guide walks through configuring a legitimate **Unity Personal (`.ulf`) license** per current **GameCI v4** documentation, generating your Android release signing keystore in **Termux**, adding your GitHub Secrets, and building signed **APK** and **Google Play AAB** packages via GitHub Actions.

---

## 1. Unity Licensing Setup (Current GameCI v4 Personal License Flow)

> **Important Change**: The obsolete `.github/workflows/unity-activation.yml` workflow (`game-ci/unity-request-activation-file@v2`) has been **removed**. Unity discontinued manual `.alf → .ulf` web activation for Personal licenses, so GameCI deprecated that action (`"This action is no longer supported."`). `game-ci/unity-builder@v4` activates Unity directly during the Android build using your GitHub Secrets.

### Option A — Unity Personal License (Free — Official GameCI v4 Steps)
You must configure **3 GitHub Secrets** in **GitHub → `<Your Repository>` → Settings → Secrets and variables → Actions**:
1. **`UNITY_LICENSE`** — Contents of your legitimate `Unity_lic.ulf` file (raw XML **or** single-line Base64; both are accepted cleanly by `android-build.yml` and `android-release.yml`).
2. **`UNITY_EMAIL`** — Email address of your Unity account.
3. **`UNITY_PASSWORD`** — Password of your Unity account.

#### How to Obtain a Valid `Unity_lic.ulf` (Android-Only / Phone Developer Guide)
Unity requires **Unity Hub** on a desktop OS (**Windows, macOS, or Linux x86_64**) to issue a legitimate Personal `Unity_lic.ulf` file tied to your Unity account. Unity Hub does not run natively on Android ARM64/Termux. You do **not** need to install the Unity Editor itself — **only Unity Hub** — and a `.ulf` file generated on Windows or macOS works on Ubuntu GitHub Actions runners.

Choose either legitimate option:
- **Path 1 — Remote Desktop / Temporary Cloud Desktop from Your Phone**: Connect from your phone (browser or RDP/VNC app) to a temporary Windows or Linux x86_64 cloud desktop VM (e.g., a free-tier/short-lived cloud VM), complete steps 1–6 below, then terminate the VM.
- **Path 2 — Borrow Another Legitimate Computer (5 minutes)**: Use a friend's, family member's, or personal Windows/Mac/Linux computer once, complete steps 1–6 below, and sign out before leaving.

**Exact Steps Inside Unity Hub (Official GameCI v4 Flow):**
1. Download and install **[Unity Hub](https://unity.com/download)** (no Unity Editor installation needed).
2. Sign in with the Unity account you will use for GitHub Actions (`UNITY_EMAIL` / `UNITY_PASSWORD`).
3. Open **Unity Hub → Preferences → Licenses**, click the **Add** button, and select **Get a free personal license**.
   *(Important: Even if a license already appears in Unity Hub, still click **Add** and complete activation so Unity writes the `Unity_lic.ulf` file to disk.)*
4. Locate and copy **`Unity_lic.ulf`** (enable "Show hidden files"):
   - **Windows**: `C:\ProgramData\Unity\Unity_lic.ulf`
   - **macOS**: `/Library/Application Support/Unity/Unity_lic.ulf`
   - **Linux**: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
5. Add `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` under **GitHub → Repository → Settings → Secrets and variables → Actions**.
6. **Sign out of Unity Hub after copying `Unity_lic.ulf`**: Unity Hub 3.7.0+ returns a Personal license seat when you sign out, ensuring your desktop/VM does not hold an active seat while CI runs.

> **Mobile Clipboard Tip (Optional Base64 Encoding):**
> Pasting multi-line XML on a mobile browser can sometimes alter line breaks. Both workflows automatically detect and decode Base64 if you prefer pasting a single line. In Termux:
> ```bash
> base64 -w 0 ~/storage/downloads/Unity_lic.ulf
> ```
> Paste that single line as `UNITY_LICENSE`.

### Option B — Unity Pro / Plus License
Add **3 GitHub Secrets** (no `.ulf` file needed; return your local seat in Unity Hub first):
- `UNITY_SERIAL` (`XX-XXXX-XXXX-XXXX-XXXX-XXXX`)
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

### Option C — Unity Enterprise Licensing Server
Add **1 GitHub Secret**:
- `UNITY_LICENSING_SERVER` (your licensing server URL)

---

## 2. Generate Your Android Release Keystore in Termux (Optional for Debug APK, Required for Play Store AAB)
> **IMPORTANT**: Never commit `.keystore` or `.jks` files into Git (already ignored by `.gitignore`). Back up your keystore and passwords safely offline — Google Play requires the same signing key for all future app updates.

Run in Termux:
```bash
pkg update && pkg install -y openjdk-17

mkdir -p ~/cyberrunner-keys
cd ~/cyberrunner-keys

keytool -genkey -v \
  -keystore cyberrunner-release.keystore \
  -alias cyberrunner \
  -keyalg RSA \
  -keysize 2048 \
  -validity 10000
```
Enter a strong keystore password, key password, and your developer/studio name when prompted.

Next, encode the keystore into a single-line Base64 string for GitHub Secrets:
```bash
base64 -w 0 cyberrunner-release.keystore > keystore_base64.txt
cat keystore_base64.txt
```
Copy the entire Base64 output (you can also pipe to Android clipboard with `termux-clipboard-set < keystore_base64.txt` if `termux-api` is installed).

---

## 3. Add Signing Secrets to GitHub Repository
On your phone browser, open your GitHub repo → **Settings** → **Secrets and variables** → **Actions** → **New repository secret**, and add these 4 secrets:

| Secret Name | Value |
| --- | --- |
| `ANDROID_KEYSTORE_BASE64` | Single-line Base64 string from `keystore_base64.txt` |
| `ANDROID_KEYSTORE_PASS` | Password you entered for the keystore |
| `ANDROID_KEYALIAS_NAME` | `cyberrunner` (or the alias you chose) |
| `ANDROID_KEYALIAS_PASS` | Password you entered for the alias |

---

## 4. Build Final Signed APK & AAB

### Option A — Manual Trigger (APK, AAB, or Both)
1. Open GitHub → **Actions** → **CYBER RUNNER Android Build (APK & AAB)** → **Run workflow**.
2. Choose:
   - `build_format`: **`both`** (emits both `.apk` for direct install AND `.aab` for Google Play)
   - `version_name`: **`1.0.0`**
   - `include_armv7`: **`false`** (ARM64 only — smallest binary; set `true` only if targeting legacy 32-bit Android phones)
   - `require_signing`: **`true`** (guarantees build fails fast if keystore secrets are missing)
3. Download **`CyberRunner-Android-v1.0.0-build<N>`** from the finished workflow run. It contains:
   - `CyberRunner-v1.0.0.apk`
   - `CyberRunner-v1.0.0.aab`
   - `SHA256SUMS.txt`

### Option B — Automatic Tagged GitHub Release (From Termux!)
```bash
cd ~/CyberRunner
git tag v1.0.0
git push origin v1.0.0
```
The **`CYBER RUNNER Tagged Release (APK + AAB)`** workflow automatically compiles signed APK + AAB, generates `SHA256SUMS.txt`, and publishes a downloadable **GitHub Release**!

---

## 5. Pre-Launch Device Verification Checklist (Including 2GB–3GB RAM Phones)
- [ ] **Install & Cold Boot**: App icon (`512x512` neon chevron) appears crisply on Android launcher; branded boot splash transitions smoothly into the live 3D Main Menu.
- [ ] **Controls & Gameplay**: Swipe Left/Right (3 lanes + banking), Swipe Up (jump + coyote/buffer), Swipe Down (cyber-slide + mid-air fast-fall), collision & side-graze stumble forgiveness.
- [ ] **All 5 Biomes**: Neon City, Cyber Underground, Sky Highway (including Road Gaps), Industrial Zone, Digital Core — smooth fog/lighting transitions.
- [ ] **All 6 Power-Ups**: Energy Magnet, Cyber Shield, Speed Boost, Double Energy, Hover Boots, Drone Support + HUD Ability button cooldown.
- [ ] **All 4 Modes**: `ENDLESS`, `MISSION`, `TIME 90s`, `OVERDRIVE`.
- [ ] **Progression & Offline Save**: Unlock/equip a Skin in `CYBER HUB`, upgrade a Power-Up, claim Daily Reward / Mission / Achievement, force-close the app, reopen -> verify all progress & Energy Coins persist 100% offline.
- [ ] **Audio & Haptics**: Synthwave music plays & ducks cleanly on Pause/Game Over; SFX & vibration respond to Settings toggles; audio mutes cleanly during phone calls / app backgrounding.
- [ ] **Low-End 2GB–3GB RAM Stability**: Enable **Settings → PERF MONITOR: ON**; verify `LOW` + `30 FPS` stays smooth with zero crashes across a 10+ minute multi-run session.

---

## 6. Google Play Console Upload Summary
- **Package ID**: `com.cyberrunner.game`
- **Target Architecture**: 64-bit `ARM64` (`IL2CPP`) — meets Google Play 64-bit requirement
- **Orientation**: Portrait (`ScreenOrientation.Portrait`)
- **Min Android Version**: Android 7.0 (`API 24`) | **Target SDK**: Automatic Latest
- **Monetization (v1.0)**: 100% free, in-game Energy Coins only, **zero IAPs**, **zero ad SDKs**, **offline-first** (no personal data transmitted or third-party tracking SDKs).
