#!/usr/bin/env python3
"""Render the current Aurora Clay app-icon preview from the shipped palettes.

This is a visual aid generated with Pillow, not the Resizetizer output. The
actual shipped assets are the SVGs under Resources/AppIcon and Resources/Splash.
"""
from __future__ import annotations

import argparse
import importlib.util
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent

spec = importlib.util.spec_from_file_location("login_preview", HERE / "render-login-preview.py")
assert spec and spec.loader
mod = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mod)


def render(out_path: str) -> None:
    light = mod.palette(str(ROOT / "Resources/Styles/Colors.xaml"))
    dusk = mod.palette(str(ROOT / "Resources/Themes/ClayDusk.xaml"))
    white = mod.rgb(light["White"])
    mint = mod.rgb(dusk["Accent"])
    violet = mod.rgb(light["PrimaryTop"])

    size = 1024
    img = Image.new("RGBA", (size, size), mod.mix(light["CardSurface"], light["White"], 0.25) + (255,))
    d = ImageDraw.Draw(img)

    # Ambient aurora glow behind the icon.
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((620, -120, 1120, 360), fill=mod.mix(dusk["PrimaryFill"], light["White"], 0.18) + (90,))
    gd.ellipse((-120, 680, 420, 1180), fill=mod.mix(dusk["Accent"], light["White"], 0.12) + (70,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(70)))

    outer = (88, 78, 936, 946)
    w, h = outer[2] - outer[0], outer[3] - outer[1]

    # Soft clay shadow.
    shadow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle((outer[0] + 20, outer[1] + 26, outer[2] + 20, outer[3] + 26),
                         radius=238, fill=mod.rgb(dusk["ClayShadow"]) + (110,))
    img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(34)))

    # Rich violet gradient squircle.
    grad = mod.vertical_gradient((w, h), mod.rgb(dusk["PrimaryFillTop"]), mod.rgb(dusk["PageBackgroundBottom"]))
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, w - 1, h - 1), radius=230, fill=255)
    img.paste(grad, (outer[0], outer[1]), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(outer, radius=230, outline=mod.mix(light["PrimaryTop"], light["White"], 0.35), width=7)
    d.rounded_rectangle((outer[0] + 12, outer[1] + 12, outer[2] - 12, outer[3] - 12),
                        radius=218, outline=mod.mix(dusk["Primary"], light["White"], 0.12), width=3)

    # Linked-K mark.
    d.line((350, 270, 350, 754), fill=white, width=72)
    d.line([(696, 292), (444, 512), (696, 732)], fill=white, width=64, joint="curve")

    # Mint connector + glow node.
    node_glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    nd = ImageDraw.Draw(node_glow)
    nd.ellipse((654, 446, 786, 578), fill=mint + (110,))
    img.alpha_composite(node_glow.filter(ImageFilter.GaussianBlur(30)))
    d = ImageDraw.Draw(img)
    d.line((270, 512, 720, 512), fill=mint, width=54)
    d.ellipse((676, 468, 764, 556), fill=mint)
    d.ellipse((694, 486, 746, 538), outline=white, width=4)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img.convert("RGB").save(out_path, quality=96)
    print(f"wrote {out_path} ({size}x{size})")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(ROOT / "docs/ui-redesign/app-icon-upgraded-preview.png"))
    args = ap.parse_args()
    out = args.out if os.path.isabs(args.out) else str(ROOT / args.out)
    render(out)


if __name__ == "__main__":
    main()
