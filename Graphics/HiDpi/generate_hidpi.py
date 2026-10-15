"""Generates high-DPI (150% and 200%) variants of Janus images.

Usage:
    python generate_hidpi.py themes <images dir> <output dir>
    python generate_hidpi.py bitmaps <output dir> <file> [<file> ...]
    python generate_hidpi.py icon <file.ico> [<file.ico> ...]
    python generate_hidpi.py resx-icons <file.resx> [<file.resx> ...]
    python generate_hidpi.py designer <output.resx> <form.resx>:<control> [...]

`themes` mirrors the theme image tree (Janus/images) into <output dir>/150 and
<output dir>/200 with the same relative paths and file names, so StyleImageManager
can look up the same keys in every set.

`bitmaps` writes <name>_150.<ext> and <name>_200.<ext> next to each other in
<output dir> (used for the docking framework resources).

`icon` rewrites .ico files in place so that they contain every size Windows asks
for at 100%-200% scaling (16, 20, 24, 32, 40, 48, 64), keeping existing frames.

`resx-icons` does the same for form icons ($this.Icon) stored inside .resx files.

`designer` takes bitmaps the WinForms designer stored in form .resx files
(<form.resx>:<control>) and writes their 150%/200% variants to a separate .resx
(keys <Form>.<control>_150/_200), which DesignerImages applies at runtime.

Upscaling uses xBRZ (pip package "xbrz.py"), which is designed for small
pixel-art images and keeps edges crisp where bicubic filtering blurs them.
Requires: Pillow, xbrz.py.
"""
import base64
import io
import math
import os
import re
import struct
import sys

from PIL import Image
import xbrz

SCALES = (150, 200)
ICON_SIZES = (16, 20, 24, 32, 40, 48, 64)
# The same name with 16/24 suffix is the same icon drawn for two sizes
# (see StyleImageManager._imageNameRx).
_SIZE_SUFFIX_RX = re.compile(r'^(?P<name>.+?)(?P<size>16|24)$')


def _split_name(file_name):
    """name[.culture].ext -> (name, culture-with-dot, ext)."""
    stem, ext = os.path.splitext(file_name)
    name, dot, culture = stem.partition('.')
    return name, (dot + culture) if dot else '', ext


def _downscale(image, size):
    # Premultiplied alpha avoids dark fringes along transparent edges.
    return image.convert('RGBa').resize(size, Image.LANCZOS).convert('RGBA')


def upscale(image, size):
    """Upscale an RGBA image to an exact size, keeping pixel-art edges crisp."""
    image = image.convert('RGBA')
    if image.size == size:
        return image
    w, h = image.size
    if size[0] < w or size[1] < h:
        return _downscale(image, size)
    if w > 64 and h > 64:
        # Large rendered artwork (splash, logos): xBRZ makes it look painted.
        return image.convert('RGBa').resize(size, Image.LANCZOS).convert('RGBA')
    exact = size[0] / w
    if exact == int(exact) and size[1] == h * int(exact) and 2 <= exact <= 6:
        return xbrz.scale_pillow(image, int(exact))
    # Scale past the target and filter down: smoother than a fractional resample.
    factor = min(6, 2 * math.ceil(max(size[0] / w, size[1] / h)))
    return _downscale(xbrz.scale_pillow(image, factor), size)


def _target_size(size, percent):
    return round(size[0] * percent / 100), round(size[1] * percent / 100)


def _same_aspect(a, b):
    return a[0] * b[1] == a[1] * b[0]


def _pick_source(path, family, target):
    """Largest image of the 16/24 family that has the same proportions and
    is not larger than the target - real artwork beats upscaled artwork."""
    with Image.open(path) as im:
        own = im.size
    best = (path, own)
    for other in family:
        if other == path:
            continue
        with Image.open(other) as im:
            size = im.size
        if _same_aspect(size, own) and best[1][0] < size[0] <= target[0]:
            best = (other, size)
    return best[0]


def _quantize_with_transparency(rgba, matte=(255, 255, 255)):
    """RGBA -> palette image with one transparent index (GIF can't do alpha)."""
    alpha = rgba.getchannel('A')
    # Blend semi-transparent edge pixels with the background the image was drawn for.
    background = Image.new('RGBA', rgba.size, matte + (255,))
    flat = Image.alpha_composite(background, rgba).convert('RGB')
    pal = flat.quantize(colors=255, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE)
    palette = pal.getpalette()[:255 * 3]
    palette += [0, 0, 0] * (256 - len(palette) // 3)
    pal.putpalette(palette)
    mask = alpha.point(lambda a: 255 if a < 128 else 0)
    pal.paste(255, mask=mask)
    pal.info['transparency'] = 255
    return pal


def _frames(image):
    for i in range(getattr(image, 'n_frames', 1)):
        image.seek(i)
        yield image.convert('RGBA'), image.info.get('duration', 100)


def _save_scaled(src_path, dst_path, size):
    with Image.open(src_path) as im:
        fmt = im.format
        animated = getattr(im, 'n_frames', 1) > 1
        if fmt == 'GIF':
            frames = [(_quantize_with_transparency(upscale(f, size)), d) for f, d in _frames(im)]
            first, rest = frames[0][0], [f for f, _ in frames[1:]]
            kwargs = dict(save_all=animated, append_images=rest, transparency=255, disposal=2)
            if animated:
                kwargs.update(duration=[d for _, d in frames], loop=im.info.get('loop', 0))
            first.save(dst_path, 'GIF', **kwargs)
        elif fmt == 'BMP':
            scaled = upscale(im, size)
            background = Image.new('RGBA', size, (255, 255, 255, 255))
            Image.alpha_composite(background, scaled).convert('RGB').save(dst_path, 'BMP')
        else:
            upscale(im, size).save(dst_path, 'PNG', optimize=True)


def generate_themes(images_dir, out_dir):
    files = []
    for root, _, names in os.walk(images_dir):
        files += [os.path.join(root, n) for n in names]

    families = {}
    for path in files:
        name, culture, ext = _split_name(os.path.basename(path))
        m = _SIZE_SUFFIX_RX.match(name)
        if m:
            key = (os.path.dirname(path), m.group('name'), culture, ext.lower())
            families.setdefault(key, []).append(path)

    count = 0
    for path in sorted(files):
        try:
            with Image.open(path) as im:
                size = im.size
        except OSError:
            continue  # not an image
        name, culture, ext = _split_name(os.path.basename(path))
        m = _SIZE_SUFFIX_RX.match(name)
        family = families.get(
            (os.path.dirname(path), m.group('name'), culture, ext.lower()), []) if m else []
        rel = os.path.relpath(path, images_dir)
        for percent in SCALES:
            target = _target_size(size, percent)
            source = _pick_source(path, family, target)
            dst = os.path.join(out_dir, str(percent), rel)
            os.makedirs(os.path.dirname(dst), exist_ok=True)
            _save_scaled(source, dst, target)
        count += 1
    print(f'{count} images x {len(SCALES)} scales -> {out_dir}')


def _pixels(image):
    # getdata() is deprecated since Pillow 12
    return getattr(image, 'get_flattened_data', image.getdata)()


def _upscale_keyed(image, size):
    """Color-keyed bitmap (key = top-left pixel, as WinForms code reads it):
    the key must stay exact, so it becomes alpha for scaling and is restored after."""
    rgb = image.convert('RGB')
    key = rgb.getpixel((0, 0))
    rgba = rgb.convert('RGBA')
    rgba.putdata([(r, g, b, 0) if (r, g, b) == key else (r, g, b, 255) for r, g, b, _ in _pixels(rgba)])
    scaled = upscale(rgba, size)
    out = Image.new('RGB', size, key)
    out.paste(scaled.convert('RGB'), mask=scaled.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return out


def _upscale_palette(image, size):
    """Few-color glyph (e.g. black on white, recolored via ColorMap at runtime):
    scale smoothly for shape, then snap every pixel back to an original color."""
    colors = [c for _, c in image.convert('RGB').getcolors(16)]
    scaled = upscale(image.convert('RGB'), size).convert('RGB')
    snap = lambda p: min(colors, key=lambda c: sum((a - b) ** 2 for a, b in zip(p, c)))
    scaled.putdata([snap(p) for p in _pixels(scaled)])
    return scaled


def _upscale_hard_alpha(image, size):
    """Image drawn over a TransparencyKey background: no semi-transparent edges."""
    scaled = upscale(image, size)
    scaled.putalpha(scaled.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    return scaled


def generate_bitmaps(out_dir, paths, nearest=(), keyed=(), hard_alpha=()):
    for path in paths:
        stem, ext = os.path.splitext(os.path.basename(path))
        with Image.open(path) as im:
            size = im.size
            colors = im.convert('RGB').getcolors(16)
            for percent in SCALES:
                target = _target_size(size, percent)
                dst = os.path.join(out_dir, f'{stem}_{percent}{ext}')
                if stem in nearest:
                    # Hit-test maps compare exact colors: no filtering allowed.
                    im.convert('RGB').resize(target, Image.NEAREST).save(dst)
                elif stem in keyed:
                    _upscale_keyed(im, target).save(dst)
                elif stem in hard_alpha:
                    _upscale_hard_alpha(im, target).save(dst)
                elif colors is not None and len(colors) <= 2:
                    _upscale_palette(im, target).save(dst)
                else:
                    _save_scaled(path, dst, target)
    print(f'{len(paths)} bitmaps x {len(SCALES)} scales -> {out_dir}')


def _read_ico_frames(source):
    """All sizes of an .ico (path or file object) as RGBA images; for each
    size the frame with the highest color depth is taken."""
    with Image.open(source) as ico:
        return {size: ico.ico.getimage(size).convert('RGBA') for size in ico.ico.sizes()}


def _bmp_frame(image):
    """32-bit BI_RGB icon frame: bottom-up BGRA + empty AND mask.
    Uncompressed frames load everywhere, including System.Drawing.Icon,
    which can't decode PNG frames below 256x256 on older systems."""
    w, h = image.size
    pixels = image.convert('RGBA').transpose(Image.FLIP_TOP_BOTTOM).tobytes('raw', 'BGRA')
    mask_stride = ((w + 31) // 32) * 4
    header = struct.pack('<IiiHHIIiiII', 40, w, h * 2, 1, 32, 0, len(pixels), 0, 0, 0, 0)
    return header + pixels + bytes(mask_stride * h)


def ico_bytes(frames):
    sizes = sorted(frames)
    blobs = [_bmp_frame(frames[size]) for size in sizes]
    data = struct.pack('<HHH', 0, 1, len(sizes))
    offset = 6 + 16 * len(sizes)
    for (w, h), blob in zip(sizes, blobs):
        data += struct.pack('<BBBBHHII', w % 256, h % 256, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    return data + b''.join(blobs)


def complete_icon_frames(frames):
    """Add the sizes Windows uses at 100%-200% scaling, keeping existing frames."""
    frames = dict(frames)
    for target in ICON_SIZES:
        if (target, target) in frames:
            continue
        # Prefer the largest frame not larger than the target, as for themes.
        smaller = [s for s in frames if s[0] <= target]
        source = frames[max(smaller)] if smaller else frames[min(frames)]
        frames[(target, target)] = upscale(source, (target, target))
    return frames


def generate_icon(path):
    frames = complete_icon_frames(_read_ico_frames(path))
    with open(path, 'wb') as f:
        f.write(ico_bytes(frames))
    print(f'{path}: {sorted(s[0] for s in frames)}')


_RESX_ENTRY_RX = re.compile(
    r'(?P<head><data name="(?P<name>[^"]+)" type="System\.Drawing\.(?P<type>Bitmap|Icon)[^"]*"'
    r'[^>]*mimetype="application/x-microsoft\.net\.object\.bytearray\.base64"[^>]*>\s*<value>)'
    r'(?P<value>[^<]*)(?P<tail></value>)', re.S)


def _resx_value(data):
    """Base64 wrapped like Visual Studio writes it."""
    text = base64.b64encode(data).decode('ascii')
    lines = [text[i:i + 80] for i in range(0, len(text), 80)]
    return '\r\n' + ''.join(f'        {line}\r\n' for line in lines) + '    '


def complete_resx_icons(paths):
    """Add missing high-DPI sizes to form icons ($this.Icon) stored in .resx files."""
    for path in paths:
        text = open(path, 'rb').read().decode('utf-8-sig')
        count = 0

        def replace(m):
            nonlocal count
            if m.group('type') != 'Icon' or m.group('name') == 'Icon1':
                return m.group(0)
            raw = base64.b64decode(re.sub(r'\s+', '', m.group('value')))
            frames = _read_ico_frames(io.BytesIO(raw))
            completed = complete_icon_frames(frames)
            if len(completed) == len(frames):
                return m.group(0)
            count += 1
            return m.group('head') + _resx_value(ico_bytes(completed)) + m.group('tail')

        text = _RESX_ENTRY_RX.sub(replace, text)
        if count:
            with open(path, 'wb') as f:
                f.write(b'\xef\xbb\xbf' + text.encode('utf-8'))
        print(f'{path}: {count} icon(s) completed')


def generate_designer_images(out_resx, specs):
    """specs: '<form .resx>:<control name>' - bitmaps that WinForms designer keeps in form
    resources. Variants go to a separate resx, because the designer drops unknown
    entries from form resources. Keys: '<Form>.<control>_<percent>'."""
    out_dir = os.path.join(os.path.dirname(out_resx), os.path.splitext(os.path.basename(out_resx))[0])
    os.makedirs(out_dir, exist_ok=True)
    entries = ''
    for spec in specs:
        resx, control = spec.rsplit(':', 1)
        form = os.path.splitext(os.path.basename(resx))[0]
        text = open(resx, 'rb').read().decode('utf-8-sig')
        m = next(m for m in _RESX_ENTRY_RX.finditer(text)
                 if m.group('name') == control + '.Image' and m.group('type') == 'Bitmap')
        image = Image.open(io.BytesIO(base64.b64decode(re.sub(r'\s+', '', m.group('value')))))
        image.load()
        for percent in SCALES:
            key = f'{form}.{control}_{percent}'
            target = _target_size(image.size, percent)
            upscale(image, target).save(os.path.join(out_dir, key + '.png'), 'PNG', optimize=True)
            entries += (
                f'  <data name="{key}" type="System.Resources.ResXFileRef, System.Windows.Forms">\r\n'
                f'    <value>{os.path.basename(out_dir)}\\{key}.png;System.Drawing.Bitmap, System.Drawing, '
                f'Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a</value>\r\n'
                f'  </data>\r\n')
    with open(out_resx, 'wb') as f:
        f.write(b'\xef\xbb\xbf' + (_RESX_HEADER + entries + '</root>').encode('utf-8'))
    print(f'{len(specs)} designer images x {len(SCALES)} scales -> {out_resx}')


_RESX_HEADER = '''<?xml version="1.0" encoding="utf-8"?>\r
<root>\r
  <!-- Generated by Graphics/HiDpi/generate_hidpi.py: high-DPI variants of designer images. -->\r
  <resheader name="resmimetype">\r
    <value>text/microsoft-resx</value>\r
  </resheader>\r
  <resheader name="version">\r
    <value>2.0</value>\r
  </resheader>\r
  <resheader name="reader">\r
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\r
  </resheader>\r
  <resheader name="writer">\r
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value>\r
  </resheader>\r
'''


def main(argv):
    if len(argv) >= 3 and argv[0] == 'themes':
        generate_themes(argv[1], argv[2])
    elif len(argv) >= 3 and argv[0] == 'bitmaps':
        generate_bitmaps(
            argv[1], argv[2:],
            nearest={'DockIndicator_PaneDiamond_Hotspot'},
            keyed={'DockIndicator_PaneDiamond'},
            hard_alpha={'tickerbg'})
    elif len(argv) >= 2 and argv[0] == 'icon':
        for path in argv[1:]:
            generate_icon(path)
    elif len(argv) >= 2 and argv[0] == 'resx-icons':
        complete_resx_icons(argv[1:])
    elif len(argv) >= 3 and argv[0] == 'designer':
        generate_designer_images(argv[1], argv[2:])
    else:
        print(__doc__)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
