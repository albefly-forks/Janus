# High-DPI images

Janus is Per-Monitor V2 DPI aware. Every bitmap has prepared 150% and 200% variants;
other scales (125%, 175%, ...) are produced at runtime by downscaling the nearest larger variant.

`generate_hidpi.py` creates the variants from the existing 100% images. Small icons are
upscaled with [xBRZ](https://sourceforge.net/projects/xbrz/), which keeps pixel-art edges sharp
(bicubic upscaling is what makes icons look blurry); large rendered artwork uses Lanczos.

## Setup

```
python -m venv venv
venv\Scripts\pip install pillow setuptools wheel
git clone https://github.com/ioistired/xbrz.py
REM build from a "Developer Command Prompt" (needs MSVC); the PyPI sdist lacks headers
venv\Scripts\pip install --no-build-isolation .\xbrz.py
```

xBRZ is GPL-3.0; it is only a build-time tool here, nothing from it ships with Janus.

## Regenerating (run from the repository root)

| What | Command | Used by |
|---|---|---|
| Theme images | `python Graphics\HiDpi\generate_hidpi.py themes Janus\images Janus\images-hidpi` | `StyleImageManager` (`Images150/200.resx`, packed by ImageUtil on pre-build) |
| Docking bitmaps | `python Graphics\HiDpi\generate_hidpi.py bitmaps Janus\Framework\Docking\Resources <bmp files except *_150/*_200 and HotspotIndex>` | `DpiResources` (`Resources.resx`, `<name>_150/_200` entries) |
| Ticker | `python Graphics\HiDpi\generate_hidpi.py bitmaps Janus\Core\Ticker Janus\Core\Ticker\tickerbg.png Janus\Core\Ticker\urmsgson.png Janus\Core\Ticker\urmsgsoff.png` | `Ticker` (embedded resources) |
| .ico files | `python Graphics\HiDpi\generate_hidpi.py icon <file.ico> ...` | adds 16-64 px frames |
| Form icons | `python Graphics\HiDpi\generate_hidpi.py resx-icons <Form.resx> ...` | `$this.Icon` in form resources |
| Designer images | `python Graphics\HiDpi\generate_hidpi.py designer Janus\Resources\DesignerImages.resx <Form.resx>:<control> ...` | `DesignerImages.Apply(form)` |

When adding a new theme image, add the 100% file to `Janus\images` and rerun the `themes` command.
New docking bitmaps also need `_150`/`_200` entries in `Resources.resx`
(the same `ResXFileRef` form as the existing ones).
