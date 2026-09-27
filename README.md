# WoW 3.3.5a DLSS 5

**English** · [Русский](README.ru.md)

NVIDIA DLSS 5 Neural Rendering for World of Warcraft 3.3.5a (build 12340). One installer with a settings window. Everything except LumeniteFX is built in; see [How it works](#how-it-works).

![DLSS 5 off / on](docs/hero.jpg)

## Requirements

- NVIDIA **RTX 50** series (the neural model runs only on RTX 50)
- NVIDIA driver **616.56+**, 617.14+ recommended
- WoW 3.3.5a with the 4 GB patch

## Install

1. Download `WoW-3.3.5a-DLSS5.exe` from [Releases](../../releases/latest).
2. Point it at the folder with `Wow.exe`.
3. Pick **DirectX 11 (dgVoodoo2)** (recommended) and click **Install**.
4. Start WoW as usual.

In game:

- **Pause** — DLSS 5 on/off, to compare.
- **Home** — ReShade menu.

**Uninstall** puts the client back exactly as it was.

## Screenshots

![](docs/compare-1.jpg)
![](docs/compare-2.jpg)
![](docs/compare-3.jpg)
![](docs/compare-4.jpg)
![](docs/compare-5.jpg)

## How it works

WoW 3.3.5a renders with Direct3D 9, which DLSS does not support. The installer translates it to DirectX 11 with dgVoodoo2, or to Vulkan with DXVK. On top of that it installs:

- ReShade;
- the [DLSS5-Feeder](https://github.com/jlrouzies-fr/DLSS5-Feeder) add-on, which builds a DLSS frame from depth and motion vectors;
- RenoDX DLSS 5 with the NVIDIA NGX runtimes.

All versions are pinned and embedded.

The one exception is LumeniteFX, which provides the motion vectors. Its license only allows distribution through the author's own links, so the installer downloads it from the author's GitHub at a fixed commit and checks every file. If there is no internet, the built-in VORT is used instead.

The Vulkan path includes a fix for a crash in DLSS5-Feeder 1.17.0 when the resolution changes or the game exits ([patch](patches/feeder-1.17.0-vulkan-device-teardown.patch)).

## Good to know

- Expect roughly half the FPS.
- DLSS 5 improves lighting and materials. It does not change models or textures.
- ReShade and DXVK are third-party software inside the game client, so check your server's rules.

## Build

Requires the .NET 9 SDK, with the files from [vendor/README.md](vendor/README.md) placed in `vendor\`. Then run:

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

The result is `dist\WoW-3.3.5a-DLSS5.exe`.
