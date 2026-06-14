# Unturned Images Generator

An in-game **Unturned client module** that renders clean, transparent icons of **items** and
**vehicles** (vanilla *and* workshop) straight to PNG files — with a live in-game editor (press
**F10**) where you pose the model, tune lighting and post-processing, and see exactly what will be
exported.

> The in-game menu is in Russian; this guide maps every control to what it does.

---

## Features

- Renders **items** and **vehicles** to PNG with a transparent background.
- **Live WYSIWYG preview** — the preview uses the exact same render path as the export, so what you
  see is what you get.
- Stable **bounding-sphere framing** — the model always fits the frame at any size and never jitters
  while you rotate it.
- Adjustable per export:
  - rotation (X / Y / Z) for both vehicles and items, camera zoom;
  - key **and** rim light position, intensity and color;
  - point or **directional** key light (size-independent lighting for huge vehicles);
  - soft **shadow catcher** and **supersampling** (1×–4×) for crisp edges;
  - optional **transparent-margin trim** (auto-crop to a fixed padding);
  - optional **solid background** color instead of transparency.
- Items export as a **3D render** or as the exact **vanilla UI icon**.
- File naming by **asset ID** or **GUID**.
- Filters for official vs workshop assets, plus one-click export of a single workshop mod.
- **Cancel** button to stop a running export.
- Headless **console command** for scripted/batch export without the menu.
- Writes `UnturnedImages.override.yaml` hint files for workshop mods.

---

## Installation

1. Download **`UnturnedImages.zip`** from the [latest release](../../releases/latest).
2. Extract it into your Unturned **`Modules`** folder, so you end up with:
   ```
   …/steamapps/common/Unturned/Modules/UnturnedImages/UnturnedImages.module
   …/steamapps/common/Unturned/Modules/UnturnedImages/UnturnedImages.Module.dll
   …/steamapps/common/Unturned/Modules/UnturnedImages/  (+ the other .dll files)
   ```
3. Launch Unturned. Press **F10** in the main menu or in-game to open the generator.

> It is a **client** module (it renders on your machine), not a server plugin.

---

## Quick start

1. Press **F10** to open **Unturned Images Generator**.
2. In **Превью** (Preview), type an asset ID into **ID для превью** and use
   **Режим: транспорт / предмет** to switch between vehicle and item preview.
3. Tune the pose/lighting in the middle and right columns — the preview updates live.
4. Click an export button (right column): **Экспорт: Предметы**, **Экспорт: Транспорт**, or
   **Экспорт workshop-мода**.
5. Find your PNGs under `Unturned/Extras/UnturnedImagesGenerator/…` (see below).

---

## Menu reference

**Превью (Preview)**
| Control | Meaning |
|---|---|
| `ID для превью` | Asset ID shown in the preview. |
| `Режим: транспорт / предмет` | Toggle vehicle ↔ item preview/export target. |
| `Снять PNG-превью` | Render a full-size preview into the preview box. |

**Поза и камера (Pose & camera)**
| Control | Meaning |
|---|---|
| `Поворот X / Y / Z` | Rotation of the model (works for vehicles **and** items). |
| `Зум` | Camera zoom (0.5×–2.5×). |

**Свет (Light)**
| Control | Meaning |
|---|---|
| `Key X / Y / Z` | Key (main) light position. |
| `Rim X / Y / Z` | Rim (back) light position. |
| `Key: … / Rim: …` | Cycle the key/rim light color. |
| `Directional-свет` | Use a size-independent directional key light. |

**Изображение (Image)**
| Control | Meaning |
|---|---|
| `Размер PNG` | Output resolution (64–4096). |
| `Сглаживание: N×` | Supersampling (1/2/4×) — renders bigger then downscales. |
| `Имя: ID / GUID` | File name = asset ID or GUID. |
| `Предмет: 3D / иконка` | Item export mode: custom 3D render or exact vanilla UI icon. |
| `Тень` | Soft shadow catcher under the model. |
| `Обрезка` + `Отступ` | Auto-crop transparent margins to a fixed padding. |
| `Фон` + `Фон: …` | Fill the background with a solid color (incl. chroma-key green). |

**Экспорт (Export)**
| Control | Meaning |
|---|---|
| `Официальные` / `Workshop` | Include official / workshop assets. |
| `Workshop ID` | Filter to a single workshop mod (used by the workshop export button). |
| `Экспорт: Предметы / Транспорт` | Export all items / all vehicles (per filters). |
| `Экспорт workshop-мода` | Export items + vehicles of the `Workshop ID` mod. |
| `Отмена экспорта` | Cancel the running export. |

---

## Output & settings

Exported files:
```
Unturned/Extras/UnturnedImagesGenerator/{Items|Vehicles}/{Official|Workshop/<modId>}/<name>.png
```

Settings persist in `Unturned/Extras/UnturnedImagesGenerator/settings.json`. A few options are
only available there:
- `OutputDirectoryOverride` — export somewhere other than the default path (empty = default).
- `ItemSkinId`, exact light intensities/colors, background RGB, etc.

---

## Console command (headless export)

Run from the in-game console (`/`) for scripted exports — uses the saved `settings.json`:
```
exportimages items            # all items
exportimages vehicles         # all vehicles
exportimages all              # both
exportimages workshop <id>    # one workshop mod
```

---

## Building from source

Requires the .NET SDK and a local Unturned install. The module references the game API via the
`OpenMod.Unturned.Redist` NuGet package and `HintPath`s in
`tools/UnturnedImages.Module/UnturnedImages.Module.csproj`.

```
dotnet build tools/UnturnedImages.Module/UnturnedImages.Module.csproj -c Release
```

Then copy the non-game DLLs + `UnturnedImages.module` from
`tools/UnturnedImages.Module/bin/Release/netstandard2.1/` into `Unturned/Modules/UnturnedImages/`
(the same set that ships in the release zip).

> Keep `OpenMod.Unturned.Redist` on a version that matches your game — it defines the game API the
> module compiles against.

---

## Credits

- Original Unturned Images project and CDN concept: [SilK](https://github.com/SilKsPlugins/UnturnedImages).
- Bootstrapper / hotloader: [OpenMod](https://github.com/openmod/OpenMod).
- In-game UI toolkit: [DanielWillett/UnturnedUITools](https://github.com/DanielWillett/UnturnedUITools).
