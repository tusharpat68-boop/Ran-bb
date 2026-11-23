# CYBER RUNNER (`v1.0.0` — Final Release)

An original futuristic 3D cyberpunk endless runner set in **2099**, built with **Unity 6000.0 LTS (`6000.0.86f1`)**, **URP 17.0.4**, and **C#** — engineered from the ground up for smooth play on **low-end 2GB–3GB RAM Android phones** and automated APK/AAB builds strictly via **Android Termux + GitHub Actions** without a desktop PC.

- **Android Package ID**: `com.cyberrunner.game`
- **Orientation**: Portrait (`ScreenOrientation.Portrait`)
- **Version**: `1.0.0` (All 8 Phases Complete)
- **Playability**: 100% offline-first, zero IAPs, zero ad/tracking SDKs, zero external API keys

---

## Complete Feature Set (Phases 1–8)
- **Core 3-Lane Runner Gameplay**: Snappy swipe controls with DPI-normalized sensitivity, lane banking, jump coyote time & buffering, cyber-slide + mid-air fast-fall slam, zero-GC PhysX trigger queries, and side-graze stumble forgiveness.
- **5 Procedural Futuristic Environments**: `Neon City`, `Cyber Underground`, `Sky Highway` (with fair jumpable Road Gaps), `Industrial Zone`, and `Digital Core`, with smooth dynamic fog/sky/lighting transitions and 7 pooled obstacle types.
- **6 Power-Ups & Special Ability**: `Energy Magnet`, `Cyber Shield`, `Speed Boost` (rams obstacles), `Double Energy`, `Hover Boots`, and `Drone Support` (companion laser drone) — usable via pickups or the HUD Special Ability cooldown button.
- **4 Playable Game Modes**: `ENDLESS`, `MISSION`, `TIME 90s`, and `OVERDRIVE` (`SpecialChallenge`).
- **Offline Progression & Cyber Hub**: Versioned atomic JSON save with `.bak` recovery & FNV-1a integrity verification, 6 cosmetic runner skins, Lv.0–3 Power-Up Overclock upgrades (in-game Energy only), rotating Daily Missions + Career Missions, 8 Achievements, 7-Day Daily Login Rewards, and Offline Local Top-5 Leaderboard.
- **Futuristic UI, Synthwave Audio & Haptics**: Live 3D runner Main Menu, dedicated Settings screen, zero-copyright procedural 120 BPM cyberpunk synthwave BGM + 12 SFX, Android native `VibrationEffect` haptics, and smooth UI animations.
- **2GB–3GB RAM Android Optimization**: First-launch hardware auto-tiering (`LOW + 30 FPS` on 2–3GB RAM phones), `LOW/MEDIUM/HIGH` URP/camera/fog/LOD scaling, SRP Batcher, GPU Instancing, `Application.lowMemory` protection, IL2CPP `OptimizeSize`, and toggleable on-device `PERF MONITOR`.
- **Production Release Pipeline**: Automated 512×512 cyberpunk App Icon & Splash Screen generator, Pre-Flight Release Validator, signed APK & Google Play AAB GitHub Actions workflows, and Termux keystore guide (`RELEASE_GUIDE.md`).

---

## Termux Quickstart (Deploy Updated CI + Project)
```bash
# Extract update & sync into your Git repository root
cp ~/storage/downloads/CyberRunner-Final-v1.0.2-CI-Fix.zip ~
rm -rf ~/CyberRunner-update && unzip -o ~/CyberRunner-Final-v1.0.2-CI-Fix.zip -d ~/CyberRunner-update
cd ~/Ran-bb   # or your local repo directory
cp -r ~/CyberRunner-update/CyberRunner/. .

# Ensure the obsolete manual ALF workflow is removed from Git history on the branch
git rm -f --ignore-unmatch .github/workflows/unity-activation.yml

git add .
git commit -m "Remove deprecated unity-request-activation-file@v2 workflow and update GameCI v4 Unity 6000.0.86f1 Android CI"
git push origin main
```

See **`RELEASE_GUIDE.md`** for current GameCI v4 Unity Personal `.ulf` licensing instructions, Termux keystore generation, GitHub Secrets setup, and Google Play Console submission steps.
