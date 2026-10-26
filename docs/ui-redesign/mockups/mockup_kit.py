"""
KaamLink mockup kit — a deterministic Android screen renderer.

Why this exists: AI image generators produce malformed interface text, so the
KaamLink redesign mockups are drawn from a structured layout with real fonts
(the app's own OpenSans, plus Lucide for icons and Cairo to emulate Android's
Arabic fallback face). Every screen therefore has pixel-accurate, legible text
and matches the shipped design tokens in Resources/Styles/Colors.xaml.

Units: all geometry is expressed in Android dp; the renderer multiplies by SCALE.
Run:  python3 render_mockups.py        (writes *.png next to the script)
"""
from __future__ import annotations

import os
from dataclasses import dataclass

from PIL import Image, ImageDraw, ImageFont

try:  # Arabic shaping (mockup-only; Android shapes Arabic natively)
    import arabic_reshaper
    from bidi.algorithm import get_display

    _HAS_BIDI = True
except ImportError:  # pragma: no cover
    _HAS_BIDI = False

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
FONT_DIR = os.path.join(REPO, "Resources", "Fonts")

SCALE = 2
W_DP, H_DP = 360, 780
W, H = W_DP * SCALE, H_DP * SCALE

# --------------------------------------------------------------------------
# Design tokens — kept byte-identical to Resources/Styles/Colors.xaml
# (tools/verify-ui-redesign.py asserts this).
# --------------------------------------------------------------------------
T = {
    "Primary": "#0B57A4",
    "PrimaryDark": "#084A8C",
    "PrimarySoft": "#E9F1F9",
    "Accent": "#0E7C74",
    "AccentSoft": "#E4F2F0",
    "Success": "#157F3C",
    "SuccessSoft": "#E7F4EC",
    "Warning": "#9A5B00",
    "WarningSoft": "#FCF3E3",
    "Error": "#B3261E",
    "ErrorSoft": "#FBECEB",
    "Info": "#0B57A4",
    "InfoSoft": "#E9F1F9",
    "TextPrimary": "#111A24",
    "TextSecondary": "#3C4753",
    "TextMuted": "#66717F",
    "TextOnPrimary": "#FFFFFF",
    "PageBackground": "#F5F7F9",
    "CardSurface": "#FFFFFF",
    "CardSurfaceAlt": "#FAFBFC",
    "Border": "#DEE3E9",
    "Divider": "#EDF0F4",
    "White": "#FFFFFF",
    "Black": "#000000",
}

TONES = {
    "primary": ("Primary", "PrimarySoft"),
    "accent": ("Accent", "AccentSoft"),
    "success": ("Success", "SuccessSoft"),
    "warning": ("Warning", "WarningSoft"),
    "danger": ("Error", "ErrorSoft"),
    "neutral": ("TextSecondary", "CardSurfaceAlt"),
    "muted": ("TextMuted", "CardSurfaceAlt"),
}

# --------------------------------------------------------------------------
# Icons — Lucide codepoints resolved from the font's own info.json and
# verified against the TTF cmap (see tools/build-icon-font.py --verify).
# --------------------------------------------------------------------------
ICONS = {
    "briefcase": 0xE062,
    "briefcase-business": 0xE5D9,
    "hard-hat": 0xE0EE,
    "building": 0xE290,
    "users": 0xE1A4,
    "user": 0xE46C,
    "search": 0xE151,
    "pin": 0xE111,
    "banknote": 0xE052,
    "clipboard": 0xE086,
    "list-checks": 0xE1D0,
    "badge-check": 0xE241,
    "file-text": 0xE0CC,
    "plus": 0xE13D,
    "pause": 0xE12E,
    "play": 0xE13C,
    "x": 0xE1B2,
    "check": 0xE06C,
    "circle-check": 0xE226,
    "circle-alert": 0xE077,
    "triangle-alert": 0xE193,
    "info": 0xE0F9,
    "clock": 0xE087,
    "phone": 0xE133,
    "logout": 0xE10E,
    "arrow-left": 0xE048,
    "chevron-right": 0xE06F,
    "chevron-left": 0xE06E,
    "pencil": 0xE1F9,
    "eye": 0xE0BA,
    "thumb-up": 0xE18A,
    "handshake": 0xE5C4,
    "shield-check": 0xE1FF,
    "star": 0xE176,
    "sparkles": 0xE416,
    "lock": 0xE10B,
    "globe": 0xE0E8,
    "sliders": 0xE29A,
    "trending-up": 0xE191,
    "shield": 0xE158,
    "id-card": 0xE61B,
    "user-circle": 0xE466,
    "arrow-right": 0xE049,
    "calendar": 0xE063,
    "tag": 0xE17F,
    "percent": 0xE132,
    "timer": 0xE1E0,
    "bell": 0xE059,
    "settings": 0xE154,
    "house": 0xE0F5,
    "menu": 0xE115,
    "circle-x": 0xE084,
    "badge-alert": 0xE479,
    "circle-pause": 0xE07F,
    "circle-play": 0xE080,
    "user-check": 0xE1A0,
    "coins": 0xE097,
    "landmark": 0xE23A,
    "flag": 0xE0D1,
    "chevron-down": 0xE06D,
    "chevron-up": 0xE070,
    "refresh": 0xE145,
    "trash": 0xE18E,
    "ellipsis": 0xE0B6,
    "circle": 0xE076,
    "square-check": 0xE55D,
    "calendar-days": 0xE2B9,
    "hourglass": 0xE296,
    "send": 0xE152,
    "smartphone": 0xE163,
    "mail": 0xE10F,
    "ban": 0xE051,
    "user-check-round": 0xE46D,
    "shield-alert": 0xE1FE,
    "badge-info": 0xE47B,
    "minus": 0xE11C,
    "circle-plus": 0xE081,
    "arrow-up-right": 0xE04D,
}

_font_cache: dict[tuple[str, int], ImageFont.FreeTypeFont] = {}


def font(size_dp: float, weight: str = "regular", family: str = "sans") -> ImageFont.FreeTypeFont:
    px = max(1, int(round(size_dp * SCALE)))
    key = (family, weight, px)  # type: ignore[assignment]
    if key not in _font_cache:
        if family == "icon":
            path = os.path.join(FONT_DIR, "lucide.ttf")
        elif family == "arabic":
            path = os.path.join(HERE, "fonts",
                                "NotoSansArabic-SemiBold.ttf" if weight == "semibold"
                                else "NotoSansArabic-Regular.ttf")
        else:
            path = os.path.join(
                FONT_DIR, "OpenSans-Semibold.ttf" if weight == "semibold" else "OpenSans-Regular.ttf"
            )
        _font_cache[key] = ImageFont.truetype(path, px)
    return _font_cache[key]


def script_font(text: str, size: float, weight: str = "regular") -> ImageFont.FreeTypeFont:
    """Pick the mockup face for a string: Arabic-capable face when needed."""
    return font(size, weight, "arabic" if has_arabic(text) else "sans")


def _rgb(hex_color: str) -> tuple[int, int, int]:
    value = hex_color.lstrip("#")
    return tuple(int(value[i: i + 2], 16) for i in (0, 2, 4))  # type: ignore[return-value]


ARABIC_RANGES = ((0x0600, 0x06FF), (0x0750, 0x077F), (0x08A0, 0x08FF),
                 (0xFB50, 0xFDFF), (0xFE70, 0xFEFF))


def has_arabic(text: str) -> bool:
    return any(any(lo <= ord(ch) <= hi for lo, hi in ARABIC_RANGES) for ch in text)


def shape(text: str, rtl: bool) -> str:
    """Shape Arabic for PIL when libraqm is unavailable (mockup fidelity only).

    Arabic runs need shaping + bidi even inside an LTR specimen page, and Latin
    runs must be left untouched, so the decision is per-string, not per-canvas.
    """
    if not _HAS_BIDI or not has_arabic(text):
        return text
    return get_display(arabic_reshaper.reshape(text))


def wrap(text: str, f: ImageFont.FreeTypeFont, max_w_px: float, rtl: bool) -> list[str]:
    words = text.split()
    if not words:
        return [""]
    lines: list[str] = []
    current = ""
    for word in words:
        candidate = f"{current} {word}".strip()
        if f.getlength(shape(candidate, rtl)) <= max_w_px or not current:
            current = candidate
        else:
            lines.append(current)
            current = word
    if current:
        lines.append(current)
    return lines


@dataclass
class Style:
    size: float = 15
    weight: str = "regular"
    color: str = "TextSecondary"
    line: float | None = None
    track: float = 0.0


STYLES = {
    "display": Style(25, "semibold", "TextPrimary", 31),
    "title": Style(21, "semibold", "TextPrimary", 27),
    "h2": Style(17, "semibold", "TextPrimary", 23),
    "card-title": Style(15.5, "semibold", "TextPrimary", 21),
    "body": Style(14.5, "regular", "TextSecondary", 20.5),
    "body-strong": Style(14.5, "semibold", "TextPrimary", 20.5),
    "small": Style(13, "regular", "TextSecondary", 18.5),
    "small-strong": Style(13, "semibold", "TextPrimary", 18.5),
    "caption": Style(12, "regular", "TextMuted", 16.5),
    "label": Style(12.5, "semibold", "TextPrimary", 17),
    "overline": Style(10.5, "semibold", "TextMuted", 14, 0.9),
    "button": Style(14.5, "semibold", "TextOnPrimary", 19),
}


class Canvas:
    """A vertical layout cursor with draw/measure modes."""

    def __init__(self, img, draw, x0: float, width: float, y: float, rtl=False, enabled=True):
        self.img = img
        self.d = draw
        self.x0 = x0
        self.width = width
        self.y = y
        self.rtl = rtl
        self.enabled = enabled

    # -- geometry ---------------------------------------------------------
    def px(self, v: float) -> float:
        return v * SCALE

    def start_x(self, w: float, align: str = "start") -> float:
        """Physical x for a box of width w placed at start/center/end of the column."""
        width_px = self.px(self.width)
        w_px = self.px(w)
        if align == "center":
            return self.px(self.x0) + (width_px - w_px) / 2
        if align == "end":
            return self.px(self.x0) + width_px - w_px
        return self.px(self.x0) if not self.rtl else self.px(self.x0) + width_px - w_px

    def child(self, inset_top: float = 0, inset_x: float = 0, width: float | None = None):
        return Canvas(
            self.img, self.d, self.x0 + inset_x, (width if width is not None else self.width - 2 * inset_x),
            self.y + inset_top, self.rtl, self.enabled,
        )

    def probe(self, inset_x: float = 0, width: float | None = None):
        return Canvas(self.img, self.d, self.x0 + inset_x, (width if width is not None else self.width - 2 * inset_x),
                      0, self.rtl, False)

    # -- primitives -------------------------------------------------------
    def gap(self, h: float):
        self.y += h

    def fill_rect(self, x, y, w, h, fill, radius=0.0, stroke=None, stroke_w=1.0):
        if not self.enabled:
            return
        box = [x, y, x + w, y + h]
        if radius:
            self.d.rounded_rectangle(box, radius=radius * SCALE, fill=_rgb(fill),
                                     outline=_rgb(stroke) if stroke else None,
                                     width=int(stroke_w * SCALE) if stroke else 0)
        else:
            self.d.rectangle(box, fill=_rgb(fill), outline=_rgb(stroke) if stroke else None,
                             width=int(stroke_w * SCALE) if stroke else 0)

    def rect(self, y: float, h: float, w: float, fill, radius=0.0, stroke=None, align="start", inset=0.0):
        x = self.start_x(w, align) + (inset * SCALE if not self.rtl else -inset * SCALE)
        self.fill_rect(x, self.px(y), self.px(w), self.px(h), fill, radius, stroke)

    def text_at(self, x_dp: float, y_dp: float, text: str, style: Style, align: str = "start",
                max_w: float | None = None, color: str | None = None):
        """Draw a single pre-wrapped line at an absolute dp position relative to column start."""
        if not self.enabled:
            return
        f = script_font(text, style.size, style.weight)
        content = shape(text, self.rtl)
        width_px = f.getlength(content)
        base_x = self.px(self.x0)
        col_w = self.px(self.width)
        if align == "center":
            x = base_x + (col_w - width_px) / 2
        elif align == "end":
            x = base_x + col_w - width_px
        else:
            x = base_x if not self.rtl else base_x + col_w - width_px
        if max_w is not None:
            x = max(x, base_x) if not self.rtl else min(x, base_x + col_w - width_px)
        y = self.px(self.y + y_dp)
        fill = _rgb(T[color or style.color])
        if style.track and has_arabic(text):
            style = Style(style.size, style.weight, style.color, style.line, 0.0)
        if style.track:
            for ch in content:  # manual letter-spacing (MAUI CharacterSpacing)
                self.d.text((x, y), ch, font=f, fill=fill, anchor="lt")
                x += f.getlength(ch) + style.track * SCALE * style.size / 10
        else:
            self.d.text((x, y), content, font=f, fill=fill, anchor="lt")

    def text(self, text: str, style: Style | str = "body", color: str | None = None, gap: float = 0,
             align: str = "start", max_lines: int | None = None, max_w: float | None = None) -> float:
        st = STYLES[style] if isinstance(style, str) else style
        lh = st.line or st.size * 1.38
        width_dp = max_w if max_w is not None else self.width
        f = script_font(text, st.size, st.weight)
        lines = wrap(text, f, self.px(width_dp), self.rtl)
        if max_lines and len(lines) > max_lines:
            lines = lines[:max_lines]
            lines[-1] = lines[-1].rstrip(" .") + "…"
        for i, line in enumerate(lines):
            self.text_at(0, i * lh, line, st, align=align, max_w=width_dp, color=color)
        consumed = len(lines) * lh
        self.y += consumed + gap
        return consumed

    def icon(self, name: str, size: float = 18, color: str = "TextSecondary", cy: float | None = None,
             x_dp: float = 0, align: str = "start", gap: float = 0) -> float:
        """Draw a Lucide glyph vertically centred on cy (defaults to current row top + size/2)."""
        code = ICONS[name]
        if not self.enabled:
            self.y += gap
            return size
        f = font(size, family="icon")
        top = self.y if cy is None else cy - size / 2
        x = self.start_x(size, align) + (x_dp * SCALE)
        self.d.text((x, self.px(top)), chr(code), font=f, fill=_rgb(T[color]), anchor="lt")
        if cy is None:
            self.y += size
        self.y += gap
        return size

    def icon_label_row(self, icon: str, text: str, style: Style | str = "small", color: str = "TextSecondary",
                       icon_color: str | None = None, gap: float = 0, icon_size: float = 16,
                       trailing: tuple[str, str] | None = None) -> float:
        st = STYLES[style] if isinstance(style, str) else style
        lh = st.line or st.size * 1.38
        row_h = max(icon_size, lh)
        f = script_font(text, st.size, st.weight)
        trailing_w = 0.0
        if trailing:
            trailing_w = font(STYLES[trailing[1]].size, STYLES[trailing[1]].weight).getlength(shape(trailing[0], self.rtl)) / SCALE
        avail = self.width - icon_size - 8 - (trailing_w + 8 if trailing else 0)
        lines = wrap(text, f, self.px(avail), self.rtl)
        h = max(row_h, len(lines) * lh)
        if self.enabled:
            self.icon(icon, icon_size, icon_color or st.color, cy=self.y + row_h / 2, align="start")
            inner_x = icon_size + 8
            for i, line in enumerate(lines):
                if self.rtl:
                    text_x_align = "end"
                else:
                    text_x_align = "start"
                sub = Canvas(self.img, self.d, self.x0 + inner_x, avail, self.y + i * lh, self.rtl, True)
                sub.text_at(0, 0, line, st, align=text_x_align, color=color)
            if trailing:
                tstyle = STYLES[trailing[1]]
                sub = Canvas(self.img, self.d, self.x0 + self.width - trailing_w, trailing_w,
                             self.y + (h - (tstyle.line or tstyle.size * 1.38)) / 2, self.rtl, True)
                sub.text_at(0, 0, trailing[0], tstyle, align="end")
        self.y += h + gap
        return h

    def text_row(self, text: str, style: Style | str = "body", end_pill: tuple[str, str] | None = None,
                 end_text: str | None = None, end_icon: str | None = None, gap: float = 10,
                 max_lines: int | None = None, tone_color: str | None = None) -> float:
        """A left/right row: wrapping start text + an optional end pill or trailing text."""
        st = STYLES[style] if isinstance(style, str) else style
        lh = st.line or st.size * 1.38
        end_w = 0.0
        if end_pill:
            f = font(11.5, "semibold")
            end_w = f.getlength(shape(end_pill[0], self.rtl)) / SCALE + 18
        elif end_text:
            es = STYLES["caption"]
            f = font(es.size, es.weight)
            end_w = f.getlength(shape(end_text, self.rtl)) / SCALE
        elif end_icon:
            end_w = 18
        reserved = end_w + 10 if end_w else 0
        avail = self.width - reserved
        f = script_font(text, st.size, st.weight)
        lines = wrap(text, f, self.px(avail), self.rtl)
        if max_lines and len(lines) > max_lines:
            lines = lines[:max_lines]
            lines[-1] = lines[-1].rstrip(" .") + "…"
        text_h = len(lines) * lh
        h = max(text_h, 22 if end_pill or end_icon else text_h)
        if self.enabled:
            sub = Canvas(self.img, self.d, self.x0, avail, self.y, self.rtl, True)
            for i, line in enumerate(lines):
                sub.text_at(0, i * lh, line, st, align="start", color=tone_color)
            if end_pill:
                pill_canvas = Canvas(self.img, self.d, self.x0 + self.width - end_w, end_w, self.y,
                                     self.rtl, True)
                pill_canvas.pill(end_pill[0], end_pill[1], w=end_w, y=self.y, align="start")
            elif end_text:
                es = STYLES["caption"]
                sub2 = Canvas(self.img, self.d, self.x0 + self.width - end_w, end_w,
                              self.y + (h - (es.line or es.size * 1.38)) / 2, self.rtl, True)
                sub2.text_at(0, 0, end_text, es, align="end")
            elif end_icon:
                self.icon(end_icon, 15, "TextMuted", cy=self.y + 9, align="end")
        self.y += h + gap
        return h

    def divider(self, gap: float = 0, inset: float = 0):
        self.rect(self.y, 1 / SCALE, self.width - inset, T["Divider"])
        self.y += 1 / SCALE + gap

    # -- components -------------------------------------------------------
    def card(self, body, pad: float = 16, gap: float = 12, radius: float = 14, fill: str = "CardSurface",
             stroke: str | None = "Border", pad_top: float | None = None):
        inner_w = self.width - 2 * pad
        probe = self.probe(inset_x=pad, width=inner_w)
        body(probe)
        content_h = probe.y
        top = pad if pad_top is None else pad_top
        h = content_h + top + pad
        if self.enabled:
            self.rect(self.y, h, self.width, T[fill], radius, T[stroke] if stroke else None)
            inner = Canvas(self.img, self.d, self.x0 + pad, inner_w, self.y + top, self.rtl, True)
            body(inner)
        self.y += h + gap
        return h

    def panel(self, body, pad: float = 14, gap: float = 12, radius: float = 12, tone: str = "primary",
              stroke: bool = False):
        fg, bg = TONES[tone]
        inner_w = self.width - 2 * pad
        probe = self.probe(inset_x=pad, width=inner_w)
        body(probe)
        h = probe.y + 2 * pad
        if self.enabled:
            self.rect(self.y, h, self.width, T[bg], radius, T[fg] if stroke else None)
            inner = Canvas(self.img, self.d, self.x0 + pad, inner_w, self.y + pad, self.rtl, True)
            body(inner)
        self.y += h + gap
        return h

    def pill(self, text: str, tone: str = "neutral", w: float | None = None, align: str = "start",
             y: float | None = None, solid: bool = False, icon: str | None = None) -> float:
        fg, bg = TONES[tone]
        st = Style(11.5, "semibold", fg, 15)
        f = script_font(text, st.size, "semibold")
        tw = f.getlength(shape(text, self.rtl)) / SCALE
        icon_w = 13 if icon else 0
        pad_x = 9
        width = w if w is not None else tw + 2 * pad_x + icon_w + (5 if icon else 0)
        h = 22
        top = self.y if y is None else y
        if self.enabled:
            x = self.start_x(width, align)
            fill = T[fg] if solid else T[bg]
            self.fill_rect(x, self.px(top), self.px(width), self.px(h), fill, h / 2,
                           T[fg] if not solid else None)
            text_color = T["TextOnPrimary"] if solid else T[fg]
            if icon:
                f_icon = font(12, family="icon")
                ix = x + pad_x * SCALE if not self.rtl else x + self.px(width) - self.px(pad_x) - self.px(12)
                self.d.text((ix, self.px(top + 5)), chr(ICONS[icon]), font=f_icon, fill=text_color, anchor="lt")
            tx = x + (pad_x + icon_w + (5 if icon else 0)) * SCALE if not self.rtl else \
                x + self.px(width) - self.px(pad_x + icon_w + (5 if icon else 0))
            sub = Canvas(self.img, self.d, 0, 0, 0, self.rtl, True)
            if not self.rtl:
                sub.d.text((tx, self.px(top + 3.5)), shape(text, self.rtl), font=f, fill=text_color, anchor="lt")
            else:
                tw_px = f.getlength(shape(text, self.rtl))
                sub.d.text((x + self.px(width) - self.px(pad_x) - tw_px - (self.px(icon_w + 5) if icon else 0),
                            self.px(top + 3.5)), shape(text, self.rtl), font=f, fill=text_color, anchor="lt")
        if y is None:
            self.y += h
        return width

    def pill_row(self, parts: list[tuple[str, str]], gap: float = 6, y_after: float = 0):
        """parts = [(text, tone)] laid out start→end (mirrored in RTL)."""
        widths = []
        for text, tone in parts:
            fg, _ = TONES[tone]
            f = script_font(text, 11.5, "semibold")
            widths.append(f.getlength(shape(text, self.rtl)) / SCALE + 18)
        total = sum(widths) + gap * (len(parts) - 1)
        if self.enabled:
            x = self.px(self.x0)
            col_right = self.px(self.x0) + self.px(self.width)
            for (text, tone), w in zip(parts, widths):
                fg, bg = TONES[tone]
                f = script_font(text, 11.5, "semibold")
                tw = f.getlength(shape(text, self.rtl))
                px_w = self.px(w)
                left = x if not self.rtl else col_right - px_w
                self.fill_rect(left, self.px(self.y), px_w, self.px(22), T[bg], 11, T[fg])
                self.d.text((left + self.px(9), self.px(self.y + 3.5)), shape(text, self.rtl), font=f,
                            fill=_rgb(T[fg]), anchor="lt")
                if not self.rtl:
                    x += px_w + self.px(gap)
                else:
                    col_right -= px_w + self.px(gap)
        self.y += 22 + y_after
        return total

    def avatar(self, initials: str, size: float = 44, tone: str = "primary", align: str = "start",
               y: float | None = None):
        fg, bg = TONES[tone]
        top = self.y if y is None else y
        if self.enabled:
            self.rect(top, size, size, T[bg], size / 2, align=align)
            f = font(size * 0.36, "semibold")
            label = shape(initials, self.rtl)
            tw = f.getlength(label)
            x = self.start_x(size, align)
            self.d.text((x + (self.px(size) - tw) / 2, self.px(top + size * 0.28)), label, font=f,
                        fill=_rgb(T[fg]), anchor="lt")
        if y is None:
            self.y += size
        return size

    def icon_tile(self, name: str, size: float = 40, tone: str = "primary", radius: float = 11,
                  align: str = "start", y: float | None = None):
        fg, bg = TONES[tone]
        top = self.y if y is None else y
        if self.enabled:
            self.rect(top, size, size, T[bg], radius, align=align)
            f = font(size * 0.5, family="icon")
            x = self.start_x(size, align) + (self.px(size) - self.px(size * 0.5)) / 2
            self.d.text((x, self.px(top + size * 0.25)), chr(ICONS[name]), font=f, fill=_rgb(T[fg]), anchor="lt")
        if y is None:
            self.y += size
        return size

    def button(self, text: str, kind: str = "primary", icon: str | None = None, h: float = 48,
               gap: float = 0, align: str = "stretch", w: float | None = None, size: float = 14.5):
        if align == "stretch":
            width = self.width
            x_align = "start"
        else:
            width = w if w is not None else 0
            x_align = align
        f = script_font(text, size, "semibold")
        if align != "stretch":
            tw = f.getlength(shape(text, self.rtl)) / SCALE
            width = w if w is not None else tw + 34 + (20 if icon else 0)
        fills = {
            "primary": ("Primary", "Primary", "TextOnPrimary"),
            "secondary": ("CardSurface", "Border", "Primary"),
            "ghost": ("CardSurface", "Border", "Primary"),
            "subtle": ("PrimarySoft", None, "Primary"),
            "danger": ("ErrorSoft", None, "Error"),
            "danger-solid": ("Error", None, "TextOnPrimary"),
            "success": ("Success", None, "TextOnPrimary"),
            "text": (None, None, "Primary"),
            "text-muted": (None, None, "TextSecondary"),
        }
        bg, stroke, fg = fills[kind]
        if self.enabled:
            x = self.start_x(width, x_align)
            if bg:
                self.fill_rect(x, self.px(self.y), self.px(width), self.px(h), T[bg], 12,
                               T[stroke] if stroke else None)
            label = shape(text, self.rtl)
            tw = f.getlength(label)
            icon_w = self.px(17) if icon else 0
            total = tw + icon_w
            tx = x + (self.px(width) - total) / 2
            if icon:
                fi = font(15, family="icon")
                self.d.text((tx, self.px(self.y + h / 2 - 8.5)), chr(ICONS[icon]), font=fi,
                            fill=_rgb(T[fg]), anchor="lt")
                tx += icon_w
            self.d.text((tx, self.px(self.y + (h - size * 1.34) / 2)), label, font=f, fill=_rgb(T[fg]), anchor="lt")
        self.y += h + gap
        return h

    def activity(self, size: float = 22, color: str = "Primary", align: str = "center", gap: float = 0):
        if self.enabled:
            x = self.start_x(size, align)
            self.d.arc([x, self.px(self.y), x + self.px(size), self.px(self.y + size)], 30, 300,
                       fill=_rgb(T[color]), width=int(2.4 * SCALE))
        self.y += size + gap
        return size

    def progress(self, pct: float, h: float = 6, tone: str = "primary", gap: float = 0, track: str = "Divider"):
        fg, _ = TONES[tone]
        if self.enabled:
            self.rect(self.y, h, self.width, T[track], h / 2)
            if pct > 0:
                w = max(h, self.width * min(pct, 1.0))
                x = self.px(self.x0) if not self.rtl else \
                    self.px(self.x0) + self.px(self.width) - self.px(w)
                self.fill_rect(x, self.px(self.y), self.px(w), self.px(h), T[fg], h / 2)
        self.y += h + gap
        return h

    def field(self, label: str | None, value: str = "", placeholder: str = "", hint: str | None = None,
              icon: str | None = None, gap: float = 12, multiline: int = 0, bad: bool = False):
        if label:
            self.text(label, "label", gap=6)
        h = 46 if not multiline else 46 + 18 * multiline
        # probe field height with value/placeholder
        if self.enabled:
            self.rect(self.y, h, self.width, T["CardSurface"], 10, T["Error"] if bad else T["Border"])
            f = script_font(value or placeholder, 14.5)
            fg = "TextMuted" if (placeholder and not value) else "TextPrimary"
            tx_off = 14 + (22 if icon else 0)
            if icon:
                fi = font(16, family="icon")
                ix = self.px(self.x0) + self.px(13) if not self.rtl else \
                    self.px(self.x0) + self.px(self.width) - self.px(13) - self.px(16)
                self.d.text((ix, self.px(self.y + 15)), chr(ICONS[icon]), font=fi, fill=_rgb(T["TextMuted"]),
                            anchor="lt")
            label_text = value or placeholder
            inner_w = self.width - tx_off - 12
            lines = wrap(label_text, f, self.px(inner_w), self.rtl)
            max_lines = 3 if multiline else 1
            if len(lines) > max_lines:
                lines = lines[:max_lines]
                lines[-1] = lines[-1].rstrip(" .,") + "…"
            for i, line in enumerate(lines):
                content = shape(line, self.rtl)
                tx = self.px(self.x0) + self.px(tx_off) if not self.rtl else \
                    self.px(self.x0) + self.px(self.width) - self.px(tx_off) - f.getlength(content)
                self.d.text((tx, self.px(self.y + 13 + i * 19)), content, font=f, fill=_rgb(T[fg]), anchor="lt")
        self.y += h + gap
        if hint:
            self.text(hint, "caption", gap=gap)

    def steps(self, current: int, labels: list[str], gap: float = 16):
        """Segmented progress rail with numbered dots and labels."""
        count = len(labels)
        seg_gap = 6
        seg_w = (self.width - seg_gap * (count - 1)) / count
        if self.enabled:
            for i in range(count):
                x = self.start_x(seg_w) if i == 0 else self.px(self.x0) + (
                    (seg_w + seg_gap) * i * SCALE if not self.rtl else self.px(self.width) - (seg_w + seg_gap) * i * SCALE - self.px(seg_w))
                color = T["Primary"] if i < current else T["Divider"]
                self.fill_rect(x, self.px(self.y), self.px(seg_w), self.px(4), color, 2)
        self.y += 4 + 10
        # labels row
        for i, label in enumerate(labels):
            f = script_font(label, 11)
            active = i == current - 1
            done = i < current - 1
            color = T["Primary"] if active else (T["TextSecondary"] if done else T["TextMuted"])
            content = shape(f"{i + 1}. {label}", self.rtl)
            tw = f.getlength(content)
            seg_x = self.px(self.x0) + ((seg_w + seg_gap) * i * SCALE if not self.rtl
                                        else self.px(self.width) - (seg_w + seg_gap) * i * SCALE - self.px(seg_w))
            if self.enabled:
                self.d.text((seg_x, self.px(self.y)), content, font=f, fill=_rgb(color), anchor="lt")
        self.y += 17 + gap

    def search(self, placeholder: str, gap: float = 12):
        if self.enabled:
            self.rect(self.y, 44, self.width, T["CardSurface"], 11, T["Border"])
            fi = font(17, family="icon")
            ix = self.px(self.x0) + self.px(13) if not self.rtl else \
                self.px(self.x0) + self.px(self.width) - self.px(13) - self.px(17)
            self.d.text((ix, self.px(self.y + 13)), chr(ICONS["search"]), font=fi, fill=_rgb(T["TextMuted"]),
                        anchor="lt")
            f = script_font(placeholder, 14)
            content = shape(placeholder, self.rtl)
            tx = self.px(self.x0) + self.px(38) if not self.rtl else \
                self.px(self.x0) + self.px(self.width) - self.px(38) - f.getlength(content)
            self.d.text((tx, self.px(self.y + 12)), content, font=f, fill=_rgb(T["TextMuted"]), anchor="lt")
        self.y += 44 + gap

    def chips(self, labels: list[str], active: int = 0, gap: float = 12, tone: str = "primary"):
        widths = [script_font(t, 13, "semibold").getlength(shape(t, self.rtl)) / SCALE + 24 for t in labels]
        if self.enabled:
            x = self.px(self.x0)
            right = self.px(self.x0) + self.px(self.width)
            for i, (label, w) in enumerate(zip(labels, widths)):
                pw = self.px(w)
                left = x if not self.rtl else right - pw
                if i == active:
                    self.fill_rect(left, self.px(self.y), pw, self.px(32), T["Primary"], 16)
                    color = T["TextOnPrimary"]
                else:
                    self.fill_rect(left, self.px(self.y), pw, self.px(32), T["CardSurface"], 16, T["Border"])
                    color = T["TextSecondary"]
                content = shape(label, self.rtl)
                f = script_font(label, 13, "semibold")
                tw = f.getlength(content)
                self.d.text((left + (pw - tw) / 2, self.px(self.y + 7.5)), content, font=f, fill=_rgb(color),
                            anchor="lt")
                if not self.rtl:
                    x += pw + self.px(6)
                else:
                    right -= pw + self.px(6)
        self.y += 32 + gap

    # -- chrome -----------------------------------------------------------
    def status_bar(self, time_text: str = "9:41"):
        if not self.enabled:
            return
        d = self.d
        d.rectangle([0, 0, W, int(24 * SCALE)], fill=_rgb(T["PageBackground"]))
        f = font(11.5, "semibold")
        d.text((self.px(16), self.px(5.5)), time_text, font=f, fill=_rgb(T["TextPrimary"]), anchor="lt")
        # signal bars
        bx = W - self.px(64)
        for i in range(4):
            bh = self.px(3 + i * 2.2)
            d.rectangle([bx + self.px(i * 3.4), self.px(15) - bh, bx + self.px(i * 3.4 + 2.2), self.px(15)],
                        fill=_rgb(T["TextPrimary"]))
        # wifi arc-ish triangle
        wx = W - self.px(44)
        d.pieslice([wx, self.px(6), wx + self.px(12), self.px(18)], 200, 340, fill=_rgb(T["TextPrimary"]))
        # battery
        bat_x = W - self.px(26)
        d.rounded_rectangle([bat_x, self.px(7), bat_x + self.px(17), self.px(15)], radius=self.px(3),
                            outline=_rgb(T["TextPrimary"]), width=int(1.2 * SCALE))
        d.rounded_rectangle([bat_x + self.px(2), self.px(9), bat_x + self.px(13), self.px(13)], radius=self.px(1.5),
                            fill=_rgb(T["TextPrimary"]))

    def gesture_bar(self):
        if not self.enabled:
            return
        d = self.d
        d.rounded_rectangle([W / 2 - self.px(48), H - self.px(10), W / 2 + self.px(48), H - self.px(6)],
                            radius=self.px(2), fill=_rgb(T["TextMuted"]))

    def appbar(self, title: str, back: bool = True, action: str | None = None, subtitle: str | None = None):
        h = 52
        if self.enabled:
            self.d.rectangle([0, self.px(24), W, self.px(24 + h)], fill=_rgb(T["PageBackground"]))
            if back:
                fi = font(19, family="icon")
                glyph = "arrow-right" if self.rtl else "arrow-left"
                ix = self.px(14) if not self.rtl else W - self.px(14) - self.px(19)
                self.d.text((ix, self.px(24 + 16)), chr(ICONS[glyph]), font=fi, fill=_rgb(T["TextPrimary"]),
                            anchor="lt")
            f = script_font(title, 15.5, "semibold")
            content = shape(title, self.rtl)
            tw = f.getlength(content)
            if self.rtl:
                tx = W - self.px(46) - tw
            else:
                tx = self.px(46)
            self.d.text((tx, self.px(24 + 17)), content, font=f, fill=_rgb(T["TextPrimary"]), anchor="lt")
            if action:
                fa = font(18, family="icon")
                ax = W - self.px(14) - self.px(18) if not self.rtl else self.px(14)
                self.d.text((ax, self.px(24 + 17)), chr(ICONS[action]), font=fa, fill=_rgb(T["Primary"]), anchor="lt")
        self.y = 24 + h

    def bottom_dock(self, body, h: float = 68, note: str | None = None):
        """Reserve a sticky action bar at the bottom and render body inside it."""
        dock_top = H_DP - h
        if self.enabled:
            self.d.rectangle([0, self.px(dock_top), W, H], fill=_rgb(T["CardSurface"]))
            self.d.rectangle([0, self.px(dock_top), W, self.px(dock_top) + int(SCALE)], fill=_rgb(T["Divider"]))
            inner = Canvas(self.img, self.d, 16, W_DP - 32, dock_top + 10, self.rtl, True)
            body(inner)
        return dock_top


def new_screen(rtl: bool = False):
    img = Image.new("RGB", (W, H), _rgb(T["PageBackground"]))
    d = ImageDraw.Draw(img)
    cv = Canvas(img, d, 16, W_DP - 32, 24, rtl=rtl, enabled=True)
    return img, cv


def section_label(cv: Canvas, text: str, action: str | None = None, gap: float = 10):
    if action:
        f = script_font(action, 12.5, "semibold")
        action_w = f.getlength(shape(action, cv.rtl)) / SCALE
        inner = cv.child(width=cv.width - action_w - 8)
        inner.text(text.upper(), "overline", gap=0)
        sub = Canvas(cv.img, cv.d, cv.x0 + cv.width - action_w, action_w, cv.y, cv.rtl, cv.enabled)
        sub.text_at(0, 0, action, Style(12.5, "semibold", "Primary", 15))
        cv.y += 15 + gap
    else:
        cv.text(text.upper(), "overline", gap=gap)
