#!/usr/bin/env python3
"""Render a faithful static preview of KaamLink's current login screen.

The checked-in `docs/ui-redesign/mockups/02-login.png` documents an older
sign-in design. This renderer reads the *shipped* palette and localized strings
and draws the current XAML layout with Pillow, so the preview cannot silently
drift into a different design.

Usage:
    python3 tools/render-login-preview.py --theme light --out docs/ui-redesign/mockups/login-light.png
    python3 tools/render-login-preview.py --theme dusk  --out docs/ui-redesign/mockups/login-dusk.png

Requires Pillow (already used by docs/ui-redesign/mockups/render_mockups.py).
It is a visual aid, not a MAUI/XAML runtime or a substitute for a device build.
"""
from __future__ import annotations

import argparse
import html
import math
import os
import re
import xml.etree.ElementTree as ET
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
FONT_DIR = os.path.join(ROOT, "Resources", "Fonts")
SCALE = 2
W_DP, H_DP = 360, 780
W, H = W_DP * SCALE, H_DP * SCALE

FONT_CACHE: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def dp(value: float) -> int:
    return int(round(value * SCALE))


def font(size_dp: float, weight: str = "regular", family: str = "sans") -> ImageFont.FreeTypeFont:
    px = dp(size_dp)
    key = (family, px, weight)
    if key not in FONT_CACHE:
        if family == "icon":
            path = os.path.join(FONT_DIR, "lucide.ttf")
        elif family == "symbol":
            path = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
        elif weight == "semibold":
            path = os.path.join(FONT_DIR, "OpenSans-Semibold.ttf")
        else:
            path = os.path.join(FONT_DIR, "OpenSans-Regular.ttf")
        FONT_CACHE[key] = ImageFont.truetype(path, px)
    return FONT_CACHE[key]


def rgb(value: str) -> tuple[int, int, int]:
    value = value.lstrip("#")
    if len(value) == 8:  # #AARRGGBB used by MAUI
        value = value[2:]
    return tuple(int(value[i : i + 2], 16) for i in (0, 2, 4))  # type: ignore[return-value]


def mix(a: str, b: str, t: float) -> tuple[int, int, int]:
    x, y = rgb(a), rgb(b)
    return tuple(int(round(x[i] + (y[i] - x[i]) * t)) for i in range(3))  # type: ignore[return-value]


def palette(path: str) -> dict[str, str]:
    text = open(path, encoding="utf-8").read()
    return dict(re.findall(r'<Color x:Key="([A-Za-z0-9_]+)">\s*(#[0-9A-Fa-f]{6,8})\s*</Color>', text))


def strings(path: str) -> dict[str, str]:
    text = open(path, encoding="utf-8").read()
    data = re.findall(r'<data name="([^"]+)"[^>]*>\s*<value>(.*?)</value>', text, re.S)
    return {k: html.unescape(v.strip()) for k, v in data}


def icon_codes(path: str) -> dict[str, str]:
    text = open(path, encoding="utf-8").read()
    return {k: html.unescape(v.strip()) for k, v in re.findall(
        r'<x:String x:Key="(Icon\w+)">([^<]*)</x:String>', text)}


def text_width(draw: ImageDraw.ImageDraw, text: str, f: ImageFont.FreeTypeFont) -> int:
    return int(round(draw.textlength(text, font=f)))


def wrap(draw: ImageDraw.ImageDraw, text: str, f: ImageFont.FreeTypeFont, max_width: int) -> list[str]:
    words = text.split()
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if text_width(draw, candidate, f) <= max_width or not current:
            current = candidate
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines or [""]


def draw_text(
    draw: ImageDraw.ImageDraw,
    xy: tuple[int, int],
    text: str,
    f: ImageFont.FreeTypeFont,
    fill: tuple[int, int, int],
    *,
    anchor: str | None = None,
    max_width: int | None = None,
    line_gap: int = 0,
) -> int:
    if max_width is None:
        draw.text(xy, text, font=f, fill=fill, anchor=anchor)
        return dp(20)
    x, y = xy
    lines = wrap(draw, text, f, max_width)
    line_height = dp(20) + line_gap
    for i, line in enumerate(lines):
        draw.text((x, y + i * line_height), line, font=f, fill=fill)
    return len(lines) * line_height


def shadow_round(
    img: Image.Image,
    box: tuple[int, int, int, int],
    radius: int,
    fill: tuple[int, int, int],
    shadow: tuple[int, int, int],
    *,
    offset: tuple[int, int] = (dp(4), dp(6)),
    blur: int = dp(12),
    outline: tuple[int, int, int] | None = None,
    width: int = 1,
) -> None:
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    sx1, sy1, sx2, sy2 = box
    ox, oy = offset
    ld.rounded_rectangle((sx1 + ox, sy1 + oy, sx2 + ox, sy2 + oy), radius=radius, fill=shadow + (90,))
    layer = layer.filter(ImageFilter.GaussianBlur(blur))
    img.alpha_composite(layer)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(box, radius=radius, fill=fill, outline=outline, width=width)


def vertical_gradient(
    size: tuple[int, int], top: tuple[int, int, int], bottom: tuple[int, int, int]
) -> Image.Image:
    out = Image.new("RGB", size, top)
    d = ImageDraw.Draw(out)
    for y in range(size[1]):
        t = y / max(1, size[1] - 1)
        color = tuple(int(round(top[i] + (bottom[i] - top[i]) * t)) for i in range(3))
        d.line((0, y, size[0], y), fill=color)
    return out


def button(
    img: Image.Image,
    box: tuple[int, int, int, int],
    label: str,
    *,
    fill: tuple[int, int, int],
    text: tuple[int, int, int],
    border: tuple[int, int, int],
    radius: int = dp(18),
    font_size: float = 15,
    shadow: tuple[int, int, int] = (154, 169, 186),
    disabled: bool = False,
    icon: str | None = None,
    icon_font: ImageFont.FreeTypeFont | None = None,
) -> None:
    x1, y1, x2, y2 = box
    if not disabled:
        shadow_round(img, box, radius, fill, shadow, offset=(dp(3), dp(5)), blur=dp(10), outline=border, width=dp(1))
    else:
        d = ImageDraw.Draw(img)
        d.rounded_rectangle(box, radius=radius, fill=fill, outline=border, width=dp(1))
    d = ImageDraw.Draw(img)
    f = font(font_size, "semibold")
    fg = text if not disabled else tuple(int((c + 255) / 2) for c in text)
    tw = text_width(d, label, f)
    total = tw
    if icon:
        total += dp(22)
    start = (x1 + x2 - total) // 2
    if icon and icon_font:
        d.text((start, (y1 + y2 - dp(18)) // 2), icon, font=icon_font, fill=fg)
        start += dp(22)
    d.text((start, (y1 + y2 - dp(20)) // 2), label, font=f, fill=fg)


def draw_mark(img: Image.Image, box: tuple[int, int, int, int], p: dict[str, str]) -> None:
    """Draw the adaptive foreground K-link mark with a rich clay squircle."""
    x1, y1, x2, y2 = box
    w, h = x2 - x1, y2 - y1
    grad = vertical_gradient((w, h), mix(p["PrimaryTop"], p["White"], 0.06), rgb(p["PrimaryBottom"]))
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle((0, 0, w - 1, h - 1), radius=int(w * 0.28), fill=255)
    img.paste(grad, (x1, y1), mask)
    d = ImageDraw.Draw(img)
    # Soft top highlight and lower clay edge.
    d.rounded_rectangle((x1 + dp(2), y1 + dp(2), x2 - dp(2), y2 - dp(2)), radius=int(w * 0.26),
                        outline=mix(p["PrimaryTop"], p["White"], 0.35), width=dp(1.2))
    lw = max(dp(4), int(w * 0.095))
    accent = rgb(p["AccentTop"]) if "AccentTop" in p else rgb(p["Accent"])
    white = rgb(p["White"])
    cx, cy = (x1 + x2) // 2, (y1 + y2) // 2
    # Left stem of K.
    d.line((x1 + w * 0.32, y1 + h * 0.23, x1 + w * 0.32, y1 + h * 0.77), fill=white, width=lw)
    # Upper and lower diagonal arms.
    d.line((x1 + w * 0.68, y1 + h * 0.27, x1 + w * 0.42, cy), fill=white, width=lw)
    d.line((x1 + w * 0.42, cy, x1 + w * 0.69, y1 + h * 0.75), fill=white, width=lw)
    # Mint connector stroke and node.
    d.line((x1 + w * 0.49, cy, x1 + w * 0.68, cy), fill=accent, width=max(dp(3), int(lw * 0.72)))
    r = max(dp(4), int(w * 0.055))
    d.ellipse((x1 + w * 0.68 - r, cy - r, x1 + w * 0.68 + r, cy + r), fill=accent)


def draw_hero(
    img: Image.Image,
    box: tuple[int, int, int, int],
    p: dict[str, str],
    icons: dict[str, str],
) -> None:
    x1, y1, x2, y2 = box
    w, h = x2 - x1, y2 - y1
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    mask = Image.new("L", (w, h), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle((0, 0, w - 1, h - 1), radius=dp(20), fill=255)
    top = mix(p["CardSurfaceTop"], p["White"], 0.35)
    bottom = mix(p["CardSurfaceBottom"], p["PageBackground"], 0.15)
    layer.paste(vertical_gradient((w, h), top, bottom).convert("RGBA"), (0, 0), mask)
    d = ImageDraw.Draw(layer)
    # Ambient brand orbs, clipped to the card by the final mask.
    d.ellipse((w * 0.66, -h * 0.18, w * 1.08, h * 0.38),
              fill=mix(p["AccentSoft"], p["White"], 0.18))
    d.ellipse((-w * 0.05, h * 0.72, w * 0.42, h * 1.2),
              fill=mix(p["PrimarySoft"], p["White"], 0.22))
    # Profile card on the left.
    card = (w * 0.07, h * 0.16, w * 0.43, h * 0.74)
    d.rounded_rectangle(card, radius=dp(15), fill=rgb(p["White"]), outline=rgb(p["Border"]), width=dp(1))
    d.rounded_rectangle((card[0] + dp(12), card[1] + dp(14), card[0] + dp(70), card[1] + dp(23)),
                        radius=dp(5), fill=rgb(p["Primary"]))
    d.rounded_rectangle((card[0] + dp(12), card[1] + dp(30), card[0] + dp(115), card[1] + dp(37)),
                        radius=dp(4), fill=rgb(p["Gray400"]))
    d.rounded_rectangle((card[0] + dp(12), card[1] + dp(43), card[0] + dp(94), card[1] + dp(50)),
                        radius=dp(4), fill=rgb(p["Gray300"]))
    pill = (card[0] + dp(12), card[1] + h * 0.39, card[0] + dp(78), card[1] + h * 0.54)
    d.rounded_rectangle(pill, radius=dp(14), fill=rgb(p["Accent"]))
    d.text((pill[0] + dp(12), pill[1] + dp(3)), "✓",
           font=font(13, family="symbol"), fill=rgb(p["White"]))
    # Two simplified people connected by the KaamLink accent.
    skin = "#C78761"
    hair = "#182D48"
    icon_check = "✓"
    for cx, cy, scale, coat in (
        (w * 0.63, h * 0.39, 1.0, rgb(p["Primary"])),
        (w * 0.82, h * 0.49, 0.78, rgb(p["Accent"])),
    ):
        r = w * 0.065 * scale
        d.ellipse((cx - r, cy - r, cx + r, cy + r), fill=skin)
        d.pieslice((cx - r * 1.1, cy - r * 1.25, cx + r * 1.1, cy + r * 0.35), 180, 360, fill=hair)
        body_top = cy + r * 0.9
        d.rounded_rectangle((cx - r * 2.0, body_top, cx + r * 2.0, h - dp(4)),
                            radius=int(r * 1.5), fill=coat)
        d.polygon([(cx - r * 0.7, body_top), (cx + r * 0.7, body_top),
                   (cx, body_top + r * 1.7)], fill=rgb(p["White"]))
    # Connection line and verified node.
    d.line((w * 0.56, h * 0.68, w * 0.79, h * 0.68), fill=rgb(p["Primary"]), width=dp(5))
    node = (w * 0.44, h * 0.78)
    d.ellipse((node[0] - dp(11), node[1] - dp(11), node[0] + dp(11), node[1] + dp(11)),
              fill=rgb(p["AccentTop"]) if "AccentTop" in p else rgb(p["Accent"]))
    d.text((node[0] - dp(7), node[1] - dp(9)), icon_check,
           font=font(13, family="symbol"), fill=rgb(p["PrimaryDark"]))
    img.paste(layer, (x1, y1), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((x1, y1, x2, y2), radius=dp(20), outline=rgb(p["Border"]), width=dp(1))

def render(theme: str, out_path: str) -> None:
    palette_path = (os.path.join(ROOT, "Resources", "Themes", "ClayDusk.xaml")
                    if theme == "dusk"
                    else os.path.join(ROOT, "Resources", "Styles", "Colors.xaml"))
    p = palette(palette_path)
    s = strings(os.path.join(ROOT, "Resources", "Strings", "AppStrings.resx"))
    icons = icon_codes(os.path.join(ROOT, "Resources", "Styles", "Icons.xaml"))

    img = Image.new("RGBA", (W, H), rgb(p["PageBackground"]) + (255,))
    # Page gradient from the shipped top/bottom tokens.
    page = vertical_gradient((W, H), rgb(p["PageBackgroundTop"]), rgb(p["PageBackgroundBottom"])).convert("RGBA")
    img.alpha_composite(page)
    d = ImageDraw.Draw(img)

    text_primary = rgb(p["TextPrimary"])
    text_secondary = rgb(p["TextSecondary"])
    text_muted = rgb(p["TextMuted"])
    primary = rgb(p["Primary"])
    white = rgb(p["White"])
    border = rgb(p["Border"])
    border_strong = rgb(p["BorderStrong"])
    card = rgb(p["CardSurface"])
    card_top = rgb(p["CardSurfaceTop"])
    card_bottom = rgb(p["CardSurfaceBottom"])
    shadow = rgb(p["ClayShadow"])
    primary_fill = rgb(p["PrimaryFill"])
    primary_fill_top = rgb(p["PrimaryFillTop"])
    error_soft = rgb(p["ErrorSoft"])

    # Status bar.
    d.text((dp(22), dp(13)), "9:41", font=font(13, "semibold"), fill=text_primary)
    # Wi-Fi / signal / battery glyphs.
    d.arc((W - dp(68), dp(15), W - dp(54), dp(29)), 200, 340, fill=text_primary, width=dp(2))
    d.line((W - dp(61), dp(28), W - dp(61), dp(22)), fill=text_primary, width=dp(2))
    d.polygon([(W - dp(48), dp(28)), (W - dp(42), dp(19)), (W - dp(36), dp(28))], fill=text_primary)
    d.rounded_rectangle((W - dp(28), dp(17), W - dp(16), dp(28)), radius=dp(2), outline=text_primary, width=dp(1.5))
    d.rounded_rectangle((W - dp(14), dp(20), W - dp(12), dp(26)), radius=dp(1), fill=text_primary)

    pad = dp(20)
    y = dp(50)

    # Header: brand mark, name/tagline, theme toggle.
    mark_box = (pad, y, pad + dp(44), y + dp(44))
    draw_mark(img, mark_box, p)
    d = ImageDraw.Draw(img)
    d.text((pad + dp(54), y + dp(2)), s["AppName"], font=font(18, "semibold"), fill=primary)
    draw_text(d, (pad + dp(54), y + dp(25)), s["AppTagline"], font(12.5), text_muted,
              max_width=W - pad * 2 - dp(150))
    toggle_w = dp(104)
    toggle_label = s.get("ThemeSwitchToLight", "Light mode") if theme == "dusk" else s["ThemeSwitchToDusk"]
    toggle_icon = icons.get("IconSun", "☀") if theme == "dusk" else icons.get("IconMoon", "☾")
    button(img, (W - pad - toggle_w, y + dp(1), W - pad, y + dp(45)), toggle_label,
           fill=card, text=primary, border=border_strong, radius=dp(16), font_size=12.5,
           shadow=shadow, icon=toggle_icon, icon_font=font(15, family="icon"))
    y += dp(60)

    # Welcome headline.
    d = ImageDraw.Draw(img)
    y += draw_text(d, (pad, y), s["LoginWelcomeHeadline"], font(26, "semibold"), text_primary,
                   max_width=W - pad * 2, line_gap=dp(4))
    y += dp(3)
    y += draw_text(d, (pad, y), s["LoginWelcomeSubtitle"], font(14.5), text_secondary,
                   max_width=W - pad * 2, line_gap=dp(2))
    y += dp(12)

    # Hero card.
    hero_h = dp(124)
    draw_hero(img, (pad, y, W - pad, y + hero_h), p, icons)
    y += hero_h + dp(15)

    # Disabled Google button and note.
    google_h = dp(52)
    button(img, (pad, y, W - pad, y + google_h), s["LoginContinueGoogle"],
           fill=card, text=primary, border=border_strong, radius=dp(18), font_size=15,
           shadow=shadow, disabled=True)
    y += google_h + dp(5)
    d = ImageDraw.Draw(img)
    y += draw_text(d, (pad + dp(8), y), s["LoginGoogleSetupNote"], font(12.5), text_muted,
                   max_width=W - pad * 2 - dp(16), line_gap=dp(1))
    y += dp(10)

    # Divider with label.
    label_w = text_width(d, s["LoginOrPhone"], font(12.5)) + dp(18)
    line_y = y + dp(9)
    d.line((pad, line_y, (W - label_w) // 2 - dp(6), line_y), fill=rgb(p["Divider"]), width=dp(1))
    d.line(((W + label_w) // 2 + dp(6), line_y, W - pad, line_y), fill=rgb(p["Divider"]), width=dp(1))
    d.text(((W - label_w) // 2 + dp(9), y), s["LoginOrPhone"], font=font(12.5), fill=text_muted)
    y += dp(26)

    # Phone label + inset field.
    d.text((pad, y), s["PhoneLabel"], font=font(13, "semibold"), fill=text_primary)
    y += dp(22)
    field_h = dp(54)
    field_box = (pad, y, W - pad, y + field_h)
    # Inset well: top darker, base lighter.
    grad = vertical_gradient((field_box[2] - field_box[0], field_h), card_bottom, card_top)
    mask = Image.new("L", (field_box[2] - field_box[0], field_h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, field_box[2] - field_box[0] - 1, field_h - 1),
                                           radius=dp(18), fill=255)
    img.paste(grad, (field_box[0], field_box[1]), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle(field_box, radius=dp(18), outline=border_strong, width=dp(1.5))
    # Country code button.
    code_w = dp(88)
    code_box = (pad + dp(6), y + dp(5), pad + dp(6) + code_w, y + field_h - dp(5))
    d.rounded_rectangle(code_box, radius=dp(12), fill=card, outline=border_strong, width=dp(1))
    d.text((code_box[0] + dp(10), y + dp(17)), "+971", font=font(12.5, "semibold"), fill=primary)
    d.line((code_box[2] + dp(8), y + dp(14), code_box[2] + dp(8), y + field_h - dp(14)),
           fill=border_strong, width=dp(1))
    # Entry placeholder.
    d.text((code_box[2] + dp(20), y + dp(17)), s["PhonePlaceholder"], font=font(15), fill=text_muted)
    y += field_h + dp(6)
    y += draw_text(d, (pad + dp(2), y), s["LoginOtpHint"], font(12.5), text_muted,
                   max_width=W - pad * 2 - dp(4), line_gap=dp(1))
    y += dp(16)

    # Primary action.
    primary_h = dp(52)
    # Gradient primary button (Border surfaces support gradients; preview uses one).
    grad = vertical_gradient((W - pad * 2, primary_h), primary_fill_top, primary_fill)
    mask = Image.new("L", (W - pad * 2, primary_h), 0)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, W - pad * 2 - 1, primary_h - 1), radius=dp(18), fill=255)
    shadow_round(img, (pad, y, W - pad, y + primary_h), dp(18), primary_fill, shadow,
                 offset=(dp(4), dp(6)), blur=dp(12), outline=primary_fill_top, width=dp(1))
    img.paste(grad, (pad, y), mask)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle((pad, y, W - pad, y + primary_h), radius=dp(18), outline=primary_fill_top, width=dp(1))
    f = font(15, "semibold")
    tw = text_width(d, s["LoginPhoneContinue"], f)
    d.text(((W - tw) // 2, y + dp(16)), s["LoginPhoneContinue"], font=f, fill=white)
    y += primary_h + dp(15)

    # Terms note.
    y += draw_text(d, (pad + dp(8), y), s["WelcomeTermsNote"], font(12.5), text_muted,
                   max_width=W - pad * 2 - dp(16), line_gap=dp(1))

    # Gesture bar.
    d.rounded_rectangle((W // 2 - dp(52), H - dp(15), W // 2 + dp(52), H - dp(10)),
                        radius=dp(3), fill=text_muted)

    os.makedirs(os.path.dirname(out_path), exist_ok=True)
    img.convert("RGB").save(out_path, quality=95)
    print(f"wrote {out_path} ({W}x{H}, theme={theme})")


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--theme", choices=("light", "dusk"), default="light")
    ap.add_argument("--out", required=True)
    args = ap.parse_args()
    out = args.out if os.path.isabs(args.out) else os.path.join(ROOT, args.out)
    render(args.theme, out)


if __name__ == "__main__":
    main()
