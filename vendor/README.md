# vendor

Files embedded into `WoW-DLSS5.exe` by `build.ps1`. They are not stored in git (one of them is larger than GitHub's 100 MB limit); put them here to build.

| Component | Version | Source | License |
|---|---|---|---|
| DXVK (`vulkan/d3d9.dll`, x32) | 3.0.2 | github.com/doitsujin/dxvk | zlib |
| dgVoodoo2 (`dx11/D3D9.dll`, `dgVoodooCpl.exe`) | 2.87.5 | github.com/dege-diosg/dgVoodoo2 | freeware |
| ReShade, add-on build (`layer/`, `dx11/dxgi.dll`, `host64/dxgi.dll`) | 6.8.0 | reshade.me | BSD-3-Clause |
| DLSS5-Feeder (`host64/dlss5-feed-host64.exe`, `DLSS5_Feed.fx`, `Verify-DLSS5Feeder.ps1`) | 1.17.0 | github.com/jlrouzies-fr/DLSS5-Feeder | MIT |
| DLSS5-Feeder `dlss5-feed.addon32` | 1.17.0 + `patches/feeder-1.17.0-vulkan-device-teardown.patch` | built from source | MIT |
| RenoDX DLSS 5 (`renodx-dlss5.addon64`) | 8.5.0-rc10 | github.com/RankFTW/rhi-repo | see source |
| NVIDIA NGX: `nvngx_dlss.dll` 310.9.1, `nvngx_dlssnr.dll` 310.8 (Lecram) | | github.com/RankFTW/rhi-repo | NVIDIA |
| VORT motion shaders | b410b9f | github.com/vortigern11/vort_Shaders | MIT |
| ReShade framework headers (`*.fxh`) | slim | github.com/crosire/reshade-shaders | BSD-3-Clause |

LumeniteFX is **not** embedded: its license only allows distribution through the author's own links, so the installer downloads it from github.com/umar-afzaal/LumeniteFX at a pinned commit and checks SHA-256 (`src/WowDlss5/Lumenite.cs`).

## Files

| Path | Bytes | SHA-256 |
|---|---:|---|
| `common/dlss5-feed.addon32` | 181 248 | `393d9dbc539a722370489f1face7582a29fe9504460a93983ebd2dc6377100ff` |
| `common/host64/dlss5-feed-host64.exe` | 169 472 | `c835754277a0590780d846443f314a83a30e2d2815ee0b2f7c18f1a24f252936` |
| `common/host64/dxgi.dll` | 5 592 064 | `0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7` |
| `common/host64/nvngx_dlss.dll` | 58 956 912 | `3975567b8943c53acce397f2b72380092f84f162d00b0d2c7d08a1025c563983` |
| `common/host64/nvngx_dlssnr.dll` | 165 840 496 | `f95feb54137ea11979f9b4ec4f00afd84b5c98a5624d3388fbf6a87714a39fcc` |
| `common/host64/renodx-dlss5.addon64` | 3 141 632 | `dcd93881e976ad033d83c2bb01f4bc3e4ddc59c15fe0dd4ca165bc5fc7d1ac68` |
| `common/reshade-shaders/Shaders/DLSS5_Feed.fx` | 52 600 | `c4ba1610df8e1593faa7c203d6d0517058d3a8ecf2dfda18ca67849844391456` |
| `common/reshade-shaders/Shaders/DrawText.fxh` | 9 359 | `b79cc4dfb3e98bcf4c06193d00ea7631d74f467f73a4deeeee13e71336d3e680` |
| `common/reshade-shaders/Shaders/Includes/vort_ACES.fxh` | 37 730 | `1330c6a20383cd5bdbcbf524fdc77420be6b61872003118e943b3fe53d8b49e7` |
| `common/reshade-shaders/Shaders/Includes/vort_Bloom.fxh` | 8 425 | `121a34c6ede2d98c9a7819cbd975f1ee0b397eb7c12e5af796060dec046804f9` |
| `common/reshade-shaders/Shaders/Includes/vort_BlueNoise.fxh` | 363 | `1b51f778ff728b3a893489770eb69762e777a71dca3f61a3edf0dbeb096f80ea` |
| `common/reshade-shaders/Shaders/Includes/vort_ColorChanges.fxh` | 12 915 | `1877f157a52ea7ee62f70526b911a98a0d332a16b2703aa37eab414968bf449a` |
| `common/reshade-shaders/Shaders/Includes/vort_ColorTex.fxh` | 510 | `97f844ef15d230b7b5e58033934a5b5286a6095cf6e381aff4e922231352358f` |
| `common/reshade-shaders/Shaders/Includes/vort_Defs.fxh` | 24 166 | `a25fa87e29be65a350eec31d5200490a055262372b28829e57e300730875fb9a` |
| `common/reshade-shaders/Shaders/Includes/vort_Depth.fxh` | 1 960 | `078cba2dd100cb24eafe70c1ee4c0c36c41cb9e4e7c9f95eddf94636fcbc5667` |
| `common/reshade-shaders/Shaders/Includes/vort_Filters.fxh` | 5 029 | `5d649cb4527ce99c16d6fa4f087cd487d8a585a4da6703aeae29c5235f805da5` |
| `common/reshade-shaders/Shaders/Includes/vort_HDRTex.fxh` | 173 | `e87762ac34cdf98aba0308706c8163e0db8357e028fa4836270e89663b63e25d` |
| `common/reshade-shaders/Shaders/Includes/vort_Motion_UI.fxh` | 5 072 | `3e8f2086f12e9cfd387f6e5cca7a3c7c22b33aec431db1b67601582a78791dc4` |
| `common/reshade-shaders/Shaders/Includes/vort_MotionBlur.fxh` | 20 403 | `ae194f4f338e70cffb5feea54d7a5f384662761738a16cf71c2bee9ecf3a7ada` |
| `common/reshade-shaders/Shaders/Includes/vort_MotionBlur_Cheap.fxh` | 13 507 | `cde87885ffe055fbd37ef3cb0e7899e50bb3cc69bd83be2d86631a12d584287b` |
| `common/reshade-shaders/Shaders/Includes/vort_MotionUtils.fxh` | 5 954 | `9c8168cfa38741b197465e325fd41d8696ddc78c206981ddfa96508946c95b7f` |
| `common/reshade-shaders/Shaders/Includes/vort_MotionVectors.fxh` | 17 521 | `d6d4150178880bf2b19a6094c59efbcc2d25030f88e3d0cdfb9ebba9cc8a8f79` |
| `common/reshade-shaders/Shaders/Includes/vort_OKColors.fxh` | 16 664 | `9b6d53bb5dc66452e8d0ebd2048f427613c45697c70029ff1e988c68314b0e98` |
| `common/reshade-shaders/Shaders/Includes/vort_Static_UI.fxh` | 8 427 | `d60db493bdbbda16a10b62f46bc8e4b25706aecb897044fbf5a87e440f6d52e5` |
| `common/reshade-shaders/Shaders/Includes/vort_TAA.fxh` | 6 005 | `5d7bdb9ee0a4c33e01067e6b557e54d7f945b097fc3f567539bce386a170a3f2` |
| `common/reshade-shaders/Shaders/Includes/vort_Tonemap.fxh` | 3 014 | `27415694e32016e87a33b6858296fddad60e83c2ee5b0cb0bdcf9072142cbae3` |
| `common/reshade-shaders/Shaders/ReShade.fxh` | 4 250 | `6dabfbbaf968c3871905d2ea17f96572ff7b1cec01310b5d0e5252b66b30174f` |
| `common/reshade-shaders/Shaders/ReShadeUI.fxh` | 9 930 | `78adf672df47460297eb9fe6dd238d2aafa24510b52b84feb1a745dff70eb901` |
| `common/reshade-shaders/Shaders/vort_Motion.fx` | 2 432 | `8365f6e1098d5dda5c8ffbf9380b78afeab4a13768e4640c7f4809bdc539254a` |
| `common/reshade-shaders/Textures/vort_BlueNoise.png` | 262 548 | `050d26db9283fbaece3b6883e300d22403631d83a12a5f2f79dc8b789b860305` |
| `common/reshade-shaders/Textures/vort_MLUT.png` | 3 892 952 | `acb25ed926e7e847aeb0ef521745287d4ddda3c7f22e472ee42d61bb3064bbd7` |
| `dx11/D3D9.dll` | 482 304 | `6a0ca214784be04b7c8b547105aa9d79acf4dc26c0b6f8702b437ddca54058b2` |
| `dx11/dgVoodoo.conf` | 21 964 | `79fec8ccfd70a8cfba9ed6981f87dddad3473c4600f9acd760352b0f70cce87f` |
| `dx11/dgVoodooCpl.exe` | 451 072 | `87fb878166dcaef3e75ff170de030527c73f5c9b164c5943da13f8a97b0b75c6` |
| `dx11/dxgi.dll` | 4 398 080 | `da430e0a9c6eecefa0d1b27d05e16c426fb5d04e808b194d914eaac4b31bc0f8` |
| `layer/ReShade32.dll` | 4 398 080 | `da430e0a9c6eecefa0d1b27d05e16c426fb5d04e808b194d914eaac4b31bc0f8` |
| `layer/ReShade32.json` | 526 | `dbfdbadb8c64b398b2f8bf30469841ab8c35e10c9da40ba69b966559d5fa15f6` |
| `layer/ReShade64.dll` | 5 592 064 | `0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7` |
| `layer/ReShade64.json` | 526 | `aa21713718843e531da396e2bfc80772c9cb3c369d6c30b836be6b0ae812d503` |
| `licenses/VORT-LICENSE.txt` | 1 089 | `562a1a335ad0815f60b3b27394d1619951d4b125f4f2161f98e8ca088c67778a` |
| `tools/Verify-DLSS5Feeder.ps1` | 102 574 | `2cdb0eac0f4f8ea9da8e6290c442f846074aae9b87750425e0d2da4a820d0aca` |
| `vulkan/d3d9.dll` | 7 843 854 | `44a2e749694128710cce3a545c954bd9d986dd6a8ea61bb08d931c2ef0190488` |