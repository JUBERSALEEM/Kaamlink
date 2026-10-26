"""
KaamLink redesign mockups — screen compositions.

Every screen is drawn from a structured layout (see mockup_kit.py) using the
app's real fonts, so the text in the PNGs is exact — no AI-generated imagery.

Run: python3 render_mockups.py
"""
from __future__ import annotations

import os

from mockup_kit import (
    H_DP, ICONS, STYLES, T, TONES, W_DP, Canvas, font, new_screen, section_label,
)

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)))
HEIGHTS: list[tuple[str, float, float]] = []

# ---------------------------------------------------------------------------
# Composite helpers
# ---------------------------------------------------------------------------


def identity(cv: Canvas, initials: str, name: str, meta: str, pill: tuple[str, str] | None = None,
             size: float = 52, tone: str = "primary", gap: float = 12, name_style="h2"):
    col_x = size + gap
    reserved = 0.0
    if pill:
        fpill = font(11.5, "semibold")
        reserved = fpill.getlength(pill[0]) / 2 + 18 + 10
    col_w = cv.width - col_x - reserved
    probe = cv.probe(inset_x=col_x, width=col_w)
    probe.text(name, name_style, gap=2)
    if meta:
        probe.text(meta, "small")
    text_h = probe.y
    row_h = max(size, text_h, 24)
    if cv.enabled:
        cv.avatar(initials, size, tone, y=cv.y)
        text_x = cv.x0 if cv.rtl else cv.x0 + col_x
        sub = Canvas(cv.img, cv.d, text_x, col_w, cv.y + 2, cv.rtl, True)
        sub.text(name, name_style, gap=2)
        if meta:
            sub.text(meta, "small")
        if pill:
            cv.pill(pill[0], pill[1], align="end", y=cv.y)
    cv.y += row_h + 12
    return row_h


def skill_row(cv: Canvas, name: str, years: str, state: str, tone: str, icon: str = "briefcase",
              icon_color: str = "TextMuted"):
    """Passport row: icon + skill name (start) with state pill and years (end)."""
    fstate = font(11.5, "semibold")
    fyears = font(STYLES["caption"].size)
    end_w = fstate.getlength(state) / 2 + 18 + fyears.getlength(years) / 2 + 14
    avail = cv.width - 26 - end_w - 10
    probe = cv.probe(inset_x=26, width=avail)
    probe.text(name, "body-strong", max_lines=1)
    text_h = probe.y
    h = max(text_h, 24)
    if cv.enabled:
        cv.icon(icon, 16, icon_color, cy=cv.y + 9)
        sub = Canvas(cv.img, cv.d, cv.x0 + (0 if cv.rtl else 26), avail, cv.y, cv.rtl, True)
        sub.text(name, "body-strong", max_lines=1)
        # years then pill at the end
        px_right = cv.x0 + cv.width
        pill_w = fstate.getlength(state) / 2 + 18
        pill_canvas = Canvas(cv.img, cv.d, px_right - pill_w, pill_w, cv.y, cv.rtl, True)
        pill_canvas.pill(state, tone, w=pill_w, y=cv.y, align="start")
        yw = fyears.getlength(years) / 2
        yc = Canvas(cv.img, cv.d, px_right - pill_w - 8 - yw, yw, cv.y + 2, cv.rtl, True)
        yc.text_at(0, 0, years, STYLES["caption"], align="end")
    cv.y += h + 10


def kv_rows(cv: Canvas, rows: list[tuple[str, str, str]]):
    """rows = [(icon, label, value)] rendered as an icon + stacked label/value list."""
    for i, (icon, label, value) in enumerate(rows):
        probe = cv.probe(inset_x=26, width=cv.width - 26)
        probe.text(label, "caption", gap=1)
        probe.text(value, "body-strong", max_lines=2)
        h = probe.y
        if cv.enabled:
            cv.icon(icon, 16, "TextMuted", cy=cv.y + h / 2)
            sub = Canvas(cv.img, cv.d, cv.x0 + (0 if cv.rtl else 26), cv.width - 26, cv.y, cv.rtl, True)
            sub.text(label, "caption", gap=1)
            sub.text(value, "body-strong", max_lines=2)
        cv.y += h + (12 if i < len(rows) - 1 else 0)
        if i < len(rows) - 1:
            cv.divider(gap=11)


def tile(cv: Canvas, icon: str, title: str, sub: str | None = None, tone: str = "primary",
         w: float | None = None, h: float = 84, pill: tuple[str, str] | None = None,
         icon_size: float = 22, radius: float = 13):
    width = w if w is not None else cv.width
    if cv.enabled:
        cv.rect(cv.y, h, width, T["CardSurface"], radius, T["Border"])
        tile_canvas = Canvas(cv.img, cv.d, cv.x0, width, cv.y, cv.rtl, True)
        # icon tile at 12,12
        fg, bg = TONES[tone]
        ix = 12 if not cv.rtl else width - 12 - 34
        cv.fill_rect(cv.px(cv.x0) + cv.px(ix), cv.px(cv.y + 12), cv.px(34), cv.px(34), T[bg], 10)
        fi = font(icon_size, family="icon")
        glyph_w = fi.getlength(chr(ICONS[icon])) / 2.0
        cv.d.text((cv.px(cv.x0 + ix) + (cv.px(34) - glyph_w) / 2, cv.px(cv.y + 12 + 17 - icon_size / 2)),
                  chr(ICONS[icon]), font=fi, fill=T[fg], anchor="lt")
        ty = cv.y + 12 + 34 + 8
        tw = width - 22
        sub_canvas = Canvas(cv.img, cv.d, cv.x0 + 11, tw, ty, cv.rtl, True)
        sub_canvas.text(title, "small-strong", gap=0, max_lines=2, align="center")
        if sub:
            sub_canvas.text(sub, "caption", gap=0, max_lines=1, align="center")
    cv.y += h + 12


def vacancy_card(cv: Canvas, initials: str, company: str, verified: bool, title: str,
                 location: str, salary: str, skills: str, openings: str, gap: float = 12):
    def body(c: Canvas):
        head_meta = f"{company} · {openings}"
        probe = c.probe(inset_x=44, width=c.width - 44)
        probe.text(title, "card-title", gap=2)
        probe.text(head_meta, "caption", gap=0)
        text_h = max(34, probe.y)
        if c.enabled:
            c.avatar(initials, 34, "neutral", y=c.y)
            sub = Canvas(c.img, c.d, c.x0 + (0 if c.rtl else 44), c.width - 44, c.y, c.rtl, True)
            sub.text(title, "card-title", gap=2, max_lines=2)
            sub.text(head_meta, "caption", gap=0)
        c.y += text_h + 8
        c.icon_label_row("pin", location, "caption", gap=6)
        c.icon_label_row("banknote", salary, "small-strong", color="TextPrimary",
                         icon_color="TextSecondary", gap=8)
        if verified:
            c.pill("Verified company", "success", icon="badge-check")
            c.gap(6)
        c.pill_row([(x.strip(), "neutral") for x in skills.split("·") if x.strip()], gap=6, y_after=2)

    cv.card(body, pad=14, gap=gap)


def app_card(cv: Canvas, title: str, company: str, date: str, pill: tuple[str, str], action: str | None = None):
    def body(c: Canvas):
        c.text_row(title, "card-title", end_pill=pill, gap=4)
        c.text(f"{company} · {date}", "small", gap=8)
        if action:
            c.button(action, "danger", h=34, gap=0, align="start")
        else:
            c.gap(2)

    cv.card(body, pad=14, gap=12)


def _with_pill(body, pill: tuple[str, str]):
    def wrapped(c: Canvas):
        top = c.y
        body(c)
        c.pill(pill[0], pill[1], align="end", y=top)
    return wrapped


def note_panel(cv: Canvas, tone: str, icon: str, title: str, body_text: str, gap: float = 12,
               caption: str | None = None):
    fg, bg = TONES[tone]

    def body(c: Canvas):
        c.text(title, "small-strong", color=fg, gap=4)
        c.text(body_text, "small", gap=0)
        if caption:
            c.gap(6)
            c.text(caption, "caption", gap=0)

    content_probe = cv.probe(inset_x=26, width=cv.width - 26)
    body(content_probe)
    inner_h = content_probe.y
    h = inner_h + 28
    if cv.enabled:
        cv.rect(cv.y, h, cv.width, T[bg], 12)
        cv.icon(icon, 17, fg, cy=cv.y + 14 + 8)
        if cv.rtl:
            sub = Canvas(cv.img, cv.d, cv.x0, cv.width - 26, cv.y + 14, True, True)
            sub_body(cv, sub, title, body_text, caption, fg)
        else:
            sub = Canvas(cv.img, cv.d, cv.x0 + 26, cv.width - 26, cv.y + 14, False, True)
            sub_body(cv, sub, title, body_text, caption, fg)
    cv.y += h + gap


def sub_body(cv: Canvas, sub: Canvas, title: str, body_text: str, caption: str | None, fg: str):
    sub.text(title, "small-strong", color=fg, gap=4)
    sub.text(body_text, "small", gap=0)
    if caption:
        sub.gap(6)
        sub.text(caption, "caption", gap=0)


def option_row(cv: Canvas, letter: str, text: str, selected: bool = False, gap: float = 8):
    probe = cv.probe(inset_x=44, width=cv.width - 44)
    probe.text(text, "small", max_lines=3)
    text_h = max(20, probe.y)
    h = max(44, text_h + 20)
    if cv.enabled:
        border = T["Primary"] if selected else T["Border"]
        fill = T["PrimarySoft"] if selected else T["CardSurface"]
        cv.fill_rect(cv.px(cv.x0), cv.px(cv.y), cv.px(cv.width), cv.px(h), fill, 11, border,
                     1.5 if selected else 1)
        # radio/letter badge
        bx = cv.x0 + 12 if not cv.rtl else cv.x0 + cv.width - 34
        cv.fill_rect(cv.px(bx), cv.px(cv.y + h / 2 - 11), cv.px(22), cv.px(22),
                     T["Primary"] if selected else T["CardSurface"], 11, T["Primary"] if selected else T["Border"])
        f = font(11, "semibold")
        if selected:
            cv.d.text((cv.px(bx) + (cv.px(22) - 6) / 2, cv.px(cv.y + h / 2 - 6)),
                      chr(ICONS["check"]), font=font(12, family="icon"), fill=T["TextOnPrimary"], anchor="lt")
        else:
            cv.d.text((cv.px(bx) + (cv.px(22) - f.getlength(letter)) / 2, cv.px(cv.y + h / 2 - 7)),
                      letter, font=f, fill=T["TextSecondary"], anchor="lt")
        sub = Canvas(cv.img, cv.d, cv.x0 + 44 if not cv.rtl else cv.x0 + 12, cv.width - 56,
                     cv.y + (h - text_h) / 2, cv.rtl, True)
        sub.text(text, "small", color="TextPrimary" if selected else "TextSecondary", max_lines=3)
    cv.y += h + gap


def code_boxes(cv: Canvas, digits: str, total: int = 6, gap: float = 8, h: float = 52):
    box_w = (cv.width - gap * (total - 1)) / total
    if cv.enabled:
        for i in range(total):
            filled = i < len(digits)
            active = i == len(digits)
            x = cv.x0 + (box_w + gap) * i if not cv.rtl else cv.x0 + cv.width - box_w - (box_w + gap) * i
            cv.fill_rect(cv.px(x), cv.px(cv.y), cv.px(box_w), cv.px(h), T["CardSurface"], 11,
                         T["Primary"] if active else T["Border"], 1.6 if active else 1)
            if filled:
                f = font(19, "semibold")
                d = digits[i]
                cv.d.text((cv.px(x) + (cv.px(box_w) - f.getlength(d)) / 2, cv.px(cv.y + 14)), d, font=f,
                          fill=T["TextPrimary"], anchor="lt")
    cv.y += h + 14


def brand_row(cv: Canvas, role_pill: tuple[str, str] | None = None):
    if cv.enabled:
        cv.icon_tile("hard-hat", 30, "primary", 9, y=cv.y)
        pill_w = 0 if not role_pill else 52
        sub = Canvas(cv.img, cv.d, cv.x0 + (0 if cv.rtl else 38), cv.width - 38 - pill_w, cv.y + 5,
                     cv.rtl, True)
        sub.text("KaamLink", STYLES["h2"], color="Primary", gap=0, max_lines=1)
        if role_pill:
            cv.pill(role_pill[0], role_pill[1], align="end", y=cv.y + 4)
    cv.y += 30 + 18


# ---------------------------------------------------------------------------
# 01 · Welcome / role selection
# ---------------------------------------------------------------------------

def screen_welcome():
    img, cv = new_screen()
    cv.status_bar()
    cv.gap(16)
    brand_row(cv)
    cv.text("Find work you're\ngood at.", "display", color="TextPrimary", gap=10)
    cv.text("KaamLink matches workers and employers across the UAE on "
            "practical skill — not CV filters.", "body", gap=20)

    def worker_card(c: Canvas):
        top = c.y
        c.icon_tile("hard-hat", 44, "accent", 12, y=top)
        col = 44 + 12
        sub = Canvas(c.img, c.d, c.x0 + col if not c.rtl else c.x0, c.width - col, top, c.rtl, c.enabled)
        sub.text("I'm looking for work", "h2", gap=3)
        sub.text("Build a Skill Passport, prove your skills, apply to real vacancies.", "small", gap=0)
        c.y = max(top + 44, sub.y) + 14
        c.button("Continue as a worker", "primary", h=44, gap=6)
        c.text("Free · about 3 minutes", "caption", gap=0, align="center")

    cv.card(worker_card, pad=14, gap=12)

    def employer_card(c: Canvas):
        top = c.y
        c.icon_tile("building", 44, "primary", 12, y=top)
        col = 44 + 12
        sub = Canvas(c.img, c.d, c.x0 + col if not c.rtl else c.x0, c.width - col, top, c.rtl, c.enabled)
        sub.text("I'm hiring", "h2", gap=3)
        sub.text("Register your company, post vacancies and review applicants with confidence.", "small", gap=0)
        c.y = max(top + 44, sub.y) + 14
        c.button("Continue as an employer", "secondary", h=44, gap=0)

    cv.card(employer_card, pad=14, gap=20)

    section_label(cv, "Why KaamLink", gap=10)
    rows = [("shield-check", "Admin-reviewed companies", "Every hiring company is verified before it can publish."),
            ("badge-check", "Skill checks, not CVs", "Short practical tests verify what a worker can do."),
            ("globe", "English & Arabic", "The app follows your device language — right to left included.")]
    for i, (icon, title, body) in enumerate(rows):
        probe = cv.probe(inset_x=26, width=cv.width - 26)
        probe.text(title, "small-strong", gap=1)
        probe.text(body, "caption", gap=0)
        row_h = probe.y
        if cv.enabled:
            cv.icon(icon, 17, "Primary", cy=cv.y + 9)
            if cv.rtl:
                sub = Canvas(cv.img, cv.d, cv.x0, cv.width - 26, cv.y, True, True)
            else:
                sub = Canvas(cv.img, cv.d, cv.x0 + 26, cv.width - 26, cv.y, False, True)
            sub.text(title, "small-strong", gap=1)
            sub.text(body, "caption", gap=0)
        cv.y += row_h + 10
        if i < len(rows) - 1:
            cv.divider(gap=9)
    cv.gap(10)
    cv.text("By continuing you accept KaamLink's Terms of Use.", "caption", align="center")
    return img


# ---------------------------------------------------------------------------
# 02 · Login
# ---------------------------------------------------------------------------

def screen_login():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Sign in")
    cv.gap(8)
    cv.pill("Worker", "primary", solid=False)
    cv.gap(12)
    cv.text("Enter your mobile number", "title", gap=8)
    cv.text("We'll text you a 6-digit code. Standard SMS rates may apply.", "body", gap=22)
    cv.field("Mobile number", "+971 50 123 4567", icon="smartphone")
    note_panel(cv, "danger", "circle-alert", "Check the number",
               "Enter a valid UAE mobile, for example +971 50 123 4567.")
    cv.gap(6)
    cv.button("Send code", "primary", h=48, gap=10)
    cv.text("We only use your number to sign you in and to let employers contact you "
            "about applications.", "caption", gap=20)
    cv.divider(gap=14)
    cv.text("Chose the wrong role?", "small-strong", gap=4)
    cv.button("Back to role selection", "text", h=36, align="start")
    return img


# ---------------------------------------------------------------------------
# 03 · OTP
# ---------------------------------------------------------------------------

def screen_otp():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Verify your number")
    cv.gap(8)
    cv.text("Enter the 6-digit code", "title", gap=8)
    cv.text("Sent by SMS to", "body", gap=2)
    cv.text("+971 50 123 4567", "body-strong", color="TextPrimary", gap=18)
    code_boxes(cv, "4281")
    cv.card(lambda c: (
        c.icon_label_row("clock", "Resend available in 0:42", "small", icon_color="Warning", gap=0)
    ), pad=12, gap=10)
    note_panel(cv, "danger", "circle-alert", "That code isn't right",
               "Check the SMS and try again. The code expires after 5 minutes.")
    cv.button("Verify and continue", "primary", h=48, gap=8)
    cv.button("Resend code", "text", h=40, align="center")
    cv.gap(6)
    cv.text("Wrong number? Go back to edit it.", "caption", align="center")
    return img


# ---------------------------------------------------------------------------
# 04 · Worker onboarding · step 1
# ---------------------------------------------------------------------------

def onboarding_steps(cv: Canvas, current: int):
    cv.steps(current, ["Basics", "Skills", "Preferences"], gap=14)


def screen_onboarding_basics():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Set up your profile")
    cv.gap(4)
    onboarding_steps(cv, 1)
    cv.text("Your basics", "title", gap=6)
    cv.text("Employers see this first. Keep it honest and simple.", "body", gap=16)
    section_label(cv, "Identity")
    cv.card(lambda c: (
        c.field("Full name", "Mohammed Abdul Rahman"),
        c.field("Main profession", "Steel fixer", icon=None),
        c.field(None, "Sharjah", icon=None, gap=0),
    ), pad=14, gap=14)
    section_label(cv, "Where you work")
    cv.card(lambda c: (
        c.field("Emirate", "Sharjah"),
        c.field("Area", "Industrial Area 12", gap=0),
    ), pad=14, gap=14)
    note_panel(cv, "primary", "info", "Why we ask",
               "Your profession and area help us show vacancies you can actually reach.")
    cv.bottom_dock(lambda d: d.button("Continue", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 05 · Worker onboarding · step 2 (skills)
# ---------------------------------------------------------------------------

def skill_edit_row(cv: Canvas, name: str, years: str, state: str, tone: str):
    """Onboarding row: skill + years, state pill, remove affordance."""
    fpill = font(11.5, "semibold")
    pill_w = fpill.getlength(state) / 2 + 18
    avail = cv.width - 28 - pill_w - 8
    if cv.enabled:
        sub = Canvas(cv.img, cv.d, cv.x0 + (8 if not cv.rtl else 0), avail - 8, cv.y, cv.rtl, True)
        sub.text(name, "body-strong", gap=1, max_lines=1)
        sub.text(years, "caption", gap=0)
        pc = Canvas(cv.img, cv.d, cv.x0 + cv.width - pill_w - 26, pill_w, cv.y + 2, cv.rtl, True)
        pc.pill(state, tone, w=pill_w, y=cv.y + 2, align="start")
        cv.icon("x", 15, "TextMuted", cy=cv.y + 11, align="end")
    cv.y += 46


def screen_onboarding_skills():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Set up your profile")
    cv.gap(4)
    onboarding_steps(cv, 2)
    cv.text("Add your skills", "title", gap=6)
    cv.text("List what you can actually do. You can prove a skill with a short test later.", "body", gap=16)
    section_label(cv, "Add a skill")
    cv.card(lambda c: (
        c.field("Skill category", "Metal & steel work"),
        c.field("Skill", "Steel fixing"),
        c.field("Years of experience", "6", gap=10),
        c.button("Add skill", "subtle", icon="plus", h=40, gap=0),
    ), pad=14, gap=14)
    section_label(cv, "Your skills · 3")
    cv.card(lambda c: (
        skill_edit_row(c, "Steel fixing", "6 years", "Primary", "primary"),
        c.divider(gap=0),
        skill_edit_row(c, "MIG welding", "4 years", "Self-declared", "neutral"),
    ), pad=14, gap=0)
    cv.gap(10)
    def dock(d: Canvas):
        left = Canvas(d.img, d.d, d.x0, 88, d.y, d.rtl, True)
        left.button("Back", "secondary", h=48, gap=0)
        right_w = d.width - 88 - 10
        right = Canvas(d.img, d.d, d.x0 + (0 if d.rtl else 98), right_w, d.y, d.rtl, True)
        right.button("Continue", "primary", h=48, gap=0)

    cv.bottom_dock(dock)
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 06 · Worker onboarding · step 3 (preferences)
# ---------------------------------------------------------------------------

def screen_onboarding_preferences():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Set up your profile")
    cv.gap(4)
    onboarding_steps(cv, 3)
    cv.text("Availability & pay", "title", gap=6)
    cv.text("We only show this to employers on KaamLink. You can change it later.", "body", gap=16)
    section_label(cv, "Availability")
    cv.card(lambda c: c.field(None, "Immediately available"), pad=14, gap=14)
    section_label(cv, "Expected monthly pay (AED)")
    cv.card(lambda c: (
        c.field("Minimum", "1800"),
        c.field("Maximum", "2500", gap=4),
        c.text("Leave empty if you'd rather discuss pay later.", "caption", gap=0),
    ), pad=14, gap=14)
    section_label(cv, "About you")
    cv.card(lambda c: c.field(None, "", placeholder="Your experience, tools you can use, the work you want.",
                              multiline=2), pad=14, gap=0)
    cv.bottom_dock(lambda d: d.button("Finish and view profile", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 07 · Worker home
# ---------------------------------------------------------------------------

def screen_worker_home():
    img, cv = new_screen()
    cv.status_bar()
    cv.gap(8)
    brand_row(cv, ("Worker", "primary"))
    cv.card(lambda c: (
        identity(c, "MA", "Mohammed Abdul Rahman", "Steel fixer · Sharjah", None, size=48),
        c.divider(gap=10),
        c.text("Profile 80% complete", "small-strong", gap=6),
        c.progress(0.8, 6, "primary", gap=6),
        c.text("Add one more skill to reach 100%.", "caption", gap=0),
    ), pad=14, gap=16)
    section_label(cv, "Quick actions")
    if cv.enabled:
        gap = 10
        w = (cv.width - 2 * gap) / 3
        x = cv.x0
        for icon, label in (("briefcase-business", "Browse jobs"), ("clipboard", "Applications"),
                            ("badge-check", "Assessments")):
            sub = Canvas(cv.img, cv.d, x, w, cv.y, cv.rtl, True)
            tile(sub, icon, label, h=76, tone="primary")
            x += w + gap
    cv.y += 76 + 4
    section_label(cv, "Skill passport", "3 skills")
    cv.card(lambda c: (
        skill_row(c, "Steel fixing", "6 yrs", "Primary", "primary", "star", "Accent"),
        c.divider(gap=9),
        skill_row(c, "MIG welding", "4 yrs", "Self-declared", "neutral", "briefcase", "TextMuted"),
        c.divider(gap=9),
        skill_row(c, "Scaffolding", "3 yrs", "Self-declared", "neutral", "briefcase", "TextMuted"),
    ), pad=14, gap=16)
    section_label(cv, "Availability")
    cv.card(lambda c: (
        c.icon_label_row("clock", "Immediately available", "small", icon_color="TextSecondary", gap=1),
        c.gap(4),
        c.icon_label_row("banknote", "1,800 – 2,500 AED / month", "small-strong", color="TextPrimary",
                         icon_color="TextSecondary", gap=0),
    ), pad=14, gap=18)
    cv.divider(gap=12)
    cv.button("Switch to hiring", "text", h=36, align="start")
    cv.button("Sign out", "text", h=36, align="start")
    return img


# ---------------------------------------------------------------------------
# 08 · Vacancy feed
# ---------------------------------------------------------------------------

def screen_vacancy_feed():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Find work", back=True)
    cv.search("Job title or skill")
    cv.chips(["All", "Dubai", "Sharjah", "Ajman"], active=-1, gap=10)
    if cv.enabled:
        cv.text("", "caption")
    cv.text("6 vacancies", "caption", gap=10)
    vacancy_card(cv, "GS", "Gulf Steel Works", True, "Steel fixer",
                 "Sharjah · Industrial Area 12", "2,000 – 2,800 AED / month",
                 "Steel fixing · MIG welding", "4 openings")
    vacancy_card(cv, "AN", "Al Noor Logistics", False, "Warehouse helper",
                 "Dubai · Al Quoz", "1,800 – 2,200 AED / month",
                 "Loading · Inventory", "3 openings")
    vacancy_card(cv, "KF", "Khaleej Fabrication", True, "MIG welder",
                 "Ajman · Industrial Area", "2,400 – 3,000 AED / month", "MIG welding · Blueprint reading",
                 "2 openings")
    cv.gap(4)
    cv.text("Pull to refresh · new vacancies appear here first", "caption", align="center")
    return img


# ---------------------------------------------------------------------------
# 09 · Vacancy detail
# ---------------------------------------------------------------------------

def screen_vacancy_detail():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Vacancy", back=True, action="flag")
    cv.gap(6)
    cv.pill("4 openings", "primary", solid=False)
    cv.gap(10)
    cv.text("Steel fixer", "title", gap=8)
    identity(cv, "GS", "Gulf Steel Works", "Construction & contracting", None, size=36, tone="neutral", gap=10,
             name_style="small-strong")
    cv.pill("Verified company", "success", align="end", y=cv.y - 58, icon="badge-check")
    cv.card(lambda c: (
        c.icon_label_row("pin", "Sharjah · Industrial Area 12", "small", gap=14),
        c.icon_label_row("banknote", "2,000 – 2,800 AED / month", "small-strong", color="TextPrimary", gap=14),
        c.icon_label_row("users", "4 openings", "small", gap=0),
    ), pad=14, gap=16)
    section_label(cv, "Skills required")
    cv.pill_row([("Steel fixing", "neutral"), ("MIG welding", "neutral"), ("Blueprint reading", "neutral")],
                gap=6, y_after=16)
    section_label(cv, "About this role")
    cv.text("Fix and tie rebar cages for industrial buildings. Read simple drawings, "
            "work safely at height and keep the site tidy. Overtime is available.", "body", gap=16)
    section_label(cv, "Your note (optional)")
    cv.card(lambda c: c.field(None, "", placeholder="Tell the employer why you fit this job.", multiline=2),
            pad=12, gap=8)
    cv.text("You can apply once per vacancy. The employer sees your Skill Passport.", "caption", gap=0)
    cv.bottom_dock(lambda d: d.button("Apply for this job", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 10 · My applications
# ---------------------------------------------------------------------------

def screen_my_applications():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("My applications", back=True)
    cv.gap(6)
    cv.text("4 applications", "caption", gap=12)
    app_card(cv, "Steel fixer", "Gulf Steel Works", "Applied 12 Oct", ("Shortlisted", "success"))
    app_card(cv, "Warehouse helper", "Al Noor Logistics", "Applied 8 Oct", ("Rejected", "danger"))
    app_card(cv, "MIG welder", "Khaleej Fabrication", "Applied 6 Oct", ("Applied", "primary"), action="Withdraw")
    app_card(cv, "Scaffolder", "Al Madina Contracting", "Applied 2 Oct", ("Hired", "success"))
    cv.gap(6)
    cv.text("Statuses update automatically when an employer reviews your application.",
            "caption", gap=0)
    return img


# ---------------------------------------------------------------------------
# 11 · My assessments
# ---------------------------------------------------------------------------

def screen_my_assessments():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Skill assessments", back=True)
    cv.gap(6)
    cv.text("Prove a skill", "title", gap=6)
    cv.text("A short test on a skill you declared. The server scores it, and a pass marks the "
            "skill as assessed on your passport.", "body", gap=14)
    note_panel(cv, "accent", "shield-check", "Why it matters",
               "Employers trust an assessed skill more than a self-declared one. You can retake a test.")
    section_label(cv, "Your skills")

    def row(c: Canvas, name: str, state: str, tone: str, detail: str, action: bool):
        c.text_row(name, "card-title", end_pill=(state, tone), gap=3)
        c.text(detail, "caption", gap=8)
        if action:
            c.button("Start test", "subtle", icon="play", h=36, gap=0, align="start")
        else:
            c.gap(2)

    def body(c: Canvas):
        row(c, "Steel fixing", "Assessed", "success", "Score 85% · passed on 14 Oct", False)
        c.divider(gap=12)
        row(c, "MIG welding", "Self-declared", "neutral", "Not assessed yet", True)
        c.divider(gap=12)
        row(c, "Scaffolding", "Self-declared", "neutral", "Assessment not available for this skill yet", False)

    cv.card(body, pad=14, gap=14)
    note_panel(cv, "primary", "lock", "Fair and private",
               "Answers are graded on the server. Correct answers are never sent to your phone.")
    return img


# ---------------------------------------------------------------------------
# 12 · Assessment runner
# ---------------------------------------------------------------------------

def screen_assessment_run():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Steel fixing", back=True)
    cv.gap(4)
    if cv.enabled:
        cv.pill("Pass mark 70%", "primary", solid=False)
        cv.pill("Question 2 of 5", "neutral", align="end", y=cv.y - 22)
    cv.gap(22)
    cv.text("Answered 2 of 5", "small-strong", gap=6)
    cv.progress(0.4, 6, "primary", gap=18)
    section_label(cv, "Question 2")
    cv.text("Before fixing a rebar cage, which check matters most?", "h2", gap=12)
    option_row(cv, "A", "The spacing and cover of the bars", selected=True)
    option_row(cv, "B", "The colour of the rebar")
    option_row(cv, "C", "The weight of the tie wire")
    option_row(cv, "D", "The time of day")
    cv.gap(4)
    cv.text("You can submit once. Answers can't be changed after that.", "caption", gap=0)
    cv.bottom_dock(lambda d: d.button("Submit answers", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 13 · Assessment result
# ---------------------------------------------------------------------------

def screen_assessment_result():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Result", back=True)
    cv.gap(20)

    def verdict(c: Canvas):
        c.icon_tile("circle-check", 56, "success", 28, align="center")
        c.gap(16)
        c.text("Assessment passed", "title", gap=8, align="center", color="Success")
        c.text("85%", STYLES["display"], color="TextPrimary", gap=6, align="center")
        c.text("You answered 17 of 20 questions correctly.", "small", gap=0, align="center")

    cv.card(verdict, pad=20, gap=14)
    note_panel(cv, "success", "badge-check", "Added to your Skill Passport",
               "Steel fixing is now marked as assessed, with the date of this test.",
               caption="Employers see this state on your profile.")
    cv.gap(6)
    cv.button("Back to my assessments", "primary", h=48, gap=10)
    cv.button("View my passport", "secondary", h=48, gap=0)
    return img


# ---------------------------------------------------------------------------
# 14 · Employer onboarding
# ---------------------------------------------------------------------------

def screen_employer_onboarding():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Register your company", back=True)
    cv.gap(8)
    cv.text("Company details", "title", gap=6)
    cv.text("Tell us about the business. You can prepare vacancies while the review is running.",
            "body", gap=16)
    note_panel(cv, "warning", "clock", "Every company is reviewed",
               "An admin checks your trade licence before your vacancies can be published. "
               "Your company stays Pending until then.")
    section_label(cv, "Business")
    cv.card(lambda c: (
        c.field("Company name", "Gulf Steel Works"),
        c.field("Business category", "Construction & contracting"),
        c.field("Trade licence number", "1234567", gap=4),
        c.text("Exactly as printed on your UAE trade licence.", "caption", gap=0),
    ), pad=14, gap=14)
    section_label(cv, "Location")
    cv.card(lambda c: (
        c.field("Emirate", "Sharjah"),
        c.field("Area", "Industrial Area 12", gap=0),
    ), pad=14, gap=0)
    cv.bottom_dock(lambda d: d.button("Submit for review", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 15 / 16 · Employer home (pending, verified)
# ---------------------------------------------------------------------------

def employer_company_card(cv: Canvas, verified: bool):
    def body(c: Canvas):
        identity(c, "GS", "Gulf Steel Works", "Construction & contracting · Sharjah", None, size=44)
        c.pill("Verified company" if verified else "Pending review",
               "success" if verified else "warning",
               icon="badge-check" if verified else "clock")
        c.divider(gap=10)
        c.icon_label_row("user", "Your role: Owner", "caption", gap=5)
        if verified:
            c.icon_label_row("id-card", "Trade licence 1234567 · approved", "caption", gap=0)
        else:
            c.icon_label_row("id-card", "Trade licence 1234567 · in review", "caption", gap=0)

    cv.card(body, pad=14, gap=16)


def screen_employer_home(verified: bool):
    img, cv = new_screen()
    cv.status_bar()
    cv.gap(8)
    brand_row(cv, ("Employer", "primary"))
    cv.text("Gulf Steel Works", "title", gap=4)
    cv.text("Hiring dashboard", "small", gap=14)
    employer_company_card(cv, verified)
    if verified:
        note_panel(cv, "success", "shield-check", "Your company is verified",
                   "Your vacancies can go live as soon as you publish them.")
    else:
        note_panel(cv, "warning", "clock", "Verification in progress",
                   "An admin is reviewing your trade licence. You can prepare vacancies now, "
                   "but they can't be published until the company is verified.")
    section_label(cv, "Manage hiring")
    if cv.enabled:
        gap = 10
        w = (cv.width - gap) / 2
        sub = Canvas(cv.img, cv.d, cv.x0, w, cv.y, cv.rtl, True)
        tile(sub, "briefcase-business", "Vacancies", h=84, tone="primary")
        x2 = cv.x0 + w + gap if not cv.rtl else cv.x0
        sub2 = Canvas(cv.img, cv.d, x2, w, cv.y, cv.rtl, True)
        tile(sub2, "users", "Applicants", h=84, tone="accent") if False else tile(sub2, "clipboard", "Applicants",
                                                                                 h=84, tone="accent")
    cv.y += 84 + 8
    cv.gap(8)
    cv.divider(gap=12)
    cv.button("Switch to worker view", "text", h=36, align="start")
    cv.button("Sign out", "text", h=36, align="start")
    return img


# ---------------------------------------------------------------------------
# 17 · Company vacancies
# ---------------------------------------------------------------------------

def vacancy_manage_card(cv: Canvas, title: str, status: str, tone: str, meta: str, applicants: str,
                        actions: list[tuple[str, str]]):
    def body(c: Canvas):
        c.text_row(title, "card-title", end_pill=(status, tone), gap=4)
        c.text(meta, "small", gap=8)
        c.button(applicants, "text", icon="users", h=34, gap=2, align="start")
        if c.enabled:
            gap = 8
            widths = []
            f = font(13.5, "semibold")
            for label, kind in actions:
                widths.append(f.getlength(label) / 2 + 30)
            total = sum(widths) + gap * (len(actions) - 1)
            x = c.px(c.x0) if not c.rtl else c.px(c.x0 + c.width) - c.px(total)
            for (label, kind), w in zip(actions, widths):
                px_w = c.px(w)
                left = x
                fills = {"secondary": (T["CardSurface"], T["Border"], T["Primary"]),
                         "danger": (None, None, T["Error"]),
                         "subtle": (T["PrimarySoft"], None, T["Primary"])}
                bg, stroke, fg = fills[kind]
                fg = T.get(fg, fg)
                if bg:
                    c.d.rounded_rectangle([left, c.px(c.y), left + px_w, c.px(c.y + 36)], radius=c.px(11),
                                          fill=bg, outline=stroke, width=int(1.2) if stroke else 0)
                c.d.text((left + (px_w - f.getlength(label)) / 2, c.px(c.y + 9)), label, font=f,
                         fill=_c(fg), anchor="lt")
                x += px_w + c.px(gap)
        cv.y += 36

    cv.card(body, pad=14, gap=12)


def _c(token: str):
    from mockup_kit import _rgb
    return _rgb(T[token]) if token in T else _rgb(token)


def screen_company_vacancies():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Gulf Steel Works", back=True)
    cv.gap(6)
    if cv.enabled:
        cv.pill("Verified company", "success", solid=False, icon="badge-check")
        cv.pill("3 vacancies", "neutral", align="end", y=cv.y - 22)
    cv.gap(24)
    vacancy_manage_card(cv, "Steel fixer", "Active", "success",
                        "Sharjah · 2,000–2,800 AED · 4 openings", "4 applicants",
                        [("Pause", "secondary"), ("Close", "danger")])
    vacancy_manage_card(cv, "MIG welder", "Draft", "muted",
                        "Sharjah · 2,400–3,000 AED · 2 openings", "0 applicants",
                        [("Activate", "subtle"), ("Close", "danger")])
    vacancy_manage_card(cv, "Warehouse helper", "Paused", "warning",
                        "Ajman · 1,800–2,200 AED · 1 opening", "1 applicant",
                        [("Activate", "subtle"), ("Close", "danger")])
    cv.text("Draft vacancies are only visible to your company.", "caption", gap=0)
    cv.bottom_dock(lambda d: d.button("New vacancy", "primary", icon="plus", h=48, gap=0))
    cv.gesture_bar()
    return img


# ---------------------------------------------------------------------------
# 18 · Vacancy editor
# ---------------------------------------------------------------------------

def screen_vacancy_edit():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Edit vacancy", back=True)
    cv.gap(8)
    cv.pill("Draft", "muted", solid=False)
    cv.gap(10)
    cv.text("Steel fixer", "title", gap=6)
    cv.text("Changes are saved to this vacancy only.", "small", gap=14)
    section_label(cv, "Role")
    cv.card(lambda c: (
        c.field("Job title", "Steel fixer"),
        c.field("Description", "", placeholder="What the worker will do, tools, site conditions.", multiline=2),
        c.field("Headcount", "4", gap=0),
    ), pad=14, gap=14)
    section_label(cv, "Location & pay")
    cv.card(lambda c: (
        c.field("Emirate", "Sharjah"),
        c.field("Area", "Industrial Area 12"),
        c.field("Minimum pay (AED)", "2000"),
        c.field("Maximum pay (AED)", "2800", gap=0),
    ), pad=14, gap=14)
    section_label(cv, "Skills · 2 selected")
    cv.card(lambda c: (
        select_row(c, "Steel fixing", True),
        c.divider(gap=0),
        select_row(c, "MIG welding", True),
        c.divider(gap=0),
        select_row(c, "Scaffolding", False),
        c.divider(gap=0),
        select_row(c, "Blueprint reading", False),
    ), pad=6, gap=0)
    cv.bottom_dock(lambda d: d.button("Save vacancy", "primary", h=48, gap=0))
    cv.gesture_bar()
    return img


def select_row(cv: Canvas, text: str, selected: bool):
    h = 44
    if cv.enabled:
        f = font(14)
        if selected:
            cv.fill_rect(cv.px(cv.x0) + cv.px(8), cv.px(cv.y), cv.px(cv.width - 16), cv.px(h), T["PrimarySoft"], 9)
        cx = cv.x0 + 14 if not cv.rtl else cv.x0 + cv.width - 14 - 20
        cv.fill_rect(cv.px(cx), cv.px(cv.y + h / 2 - 10), cv.px(20), cv.px(20),
                     T["Primary"] if selected else T["CardSurface"], 5,
                     T["Primary"] if selected else T["Border"])
        if selected:
            cv.d.text((cv.px(cx) + cv.px(4), cv.px(cv.y + h / 2 - 8)), chr(ICONS["check"]),
                      font=font(13, family="icon"), fill=T["TextOnPrimary"], anchor="lt")
        tx = cv.px(cv.x0 + 44) if not cv.rtl else cv.px(cv.x0) + cv.px(10)
        cv.d.text((tx, cv.px(cv.y + h / 2 - 10)), text, font=f,
                  fill=T["TextPrimary"] if selected else T["TextSecondary"], anchor="lt")
    cv.y += h


# ---------------------------------------------------------------------------
# 19 · Applicants
# ---------------------------------------------------------------------------

def applicant_card(cv: Canvas, initials: str, name: str, meta: str, cover: str | None,
                   status: str, tone: str, decisions: list[tuple[str, str]]):
    def body(c: Canvas):
        identity(c, initials, name, meta, (status, tone), size=40, tone="neutral")
        if cover:
            f = font(13)
            lines = wrap_cover(cover, f, c.px(c.width - 14))
            cover_h = max(22, len(lines) * 18.5) + 4
            if c.enabled:
                c.fill_rect(c.px(c.x0) + (0 if not c.rtl else c.px(c.width) - c.px(2)),
                            c.px(c.y), c.px(2), c.px(cover_h - 4), T["Border"], 1)
                sub = Canvas(c.img, c.d, c.x0 + (0 if c.rtl else 10), c.width - 12, c.y, c.rtl, True)
                sub.text(cover, "small", max_lines=3, gap=0)
            c.y += cover_h
        # decisions: two per row, height accounted in both modes
        gap = 8
        width = (c.width - gap) / 2
        rows = (len(decisions) + 1) // 2
        if c.enabled:
            f = font(13.5, "semibold")
            fills = {"primary": (T["Primary"], None, T["TextOnPrimary"]),
                     "secondary": (T["CardSurface"], T["Border"], T["Primary"]),
                     "danger": (T["ErrorSoft"], None, T["Error"]),
                     "text": (None, None, T["Primary"])}
            for i, (label, kind) in enumerate(decisions):
                row, col = divmod(i, 2)
                x = c.x0 + (width + gap) * col if not c.rtl else c.x0 + c.width - width - (width + gap) * col
                y = c.y + (40 + gap) * row
                bg, stroke, fg = fills[kind]
                fg = T.get(fg, fg)
                if bg:
                    c.d.rounded_rectangle([c.px(x), c.px(y), c.px(x + width), c.px(y + 40)], radius=c.px(11),
                                          fill=bg, outline=stroke, width=int(1.2) if stroke else 0)
                c.d.text((c.px(x) + (c.px(width) - f.getlength(label)) / 2, c.px(y + 11)), label, font=f,
                         fill=_c(fg), anchor="lt")
        c.y += (40 + gap) * rows - gap

    cv.card(body, pad=14, gap=12)


def wrap_cover(text: str, f, max_px: float) -> list[str]:
    words = text.split()
    lines, current = [], ""
    for w in words:
        cand = f"{current} {w}".strip()
        if f.getlength(cand) <= max_px or not current:
            current = cand
        else:
            lines.append(current)
            current = w
    if current:
        lines.append(current)
    return lines[:3]


def screen_applicants():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("Applicants", back=True)
    cv.gap(4)
    cv.text("Steel fixer", "title", gap=4)
    cv.text("4 applicants · Sharjah", "small", gap=12)
    applicant_card(cv, "MA", "Mohammed Abdul Rahman", "Steel fixing · 6 yrs · Sharjah",
                   "I have 6 years of rebar experience on industrial sites in Sharjah and Dubai.",
                   "Applied", "primary", [("Mark viewed", "secondary"), ("Shortlist", "primary"),
                                          ("Hire", "secondary"), ("Reject", "danger")])
    applicant_card(cv, "RK", "Ramesh Kumar", "Steel fixing · 4 yrs · Ajman", None,
                   "Shortlisted", "success", [("Hire", "primary"), ("Reject", "danger")])
    applicant_card(cv, "AS", "Abdullah Salem", "MIG welding · 8 yrs · Dubai",
                   "Available immediately with my own PPE.", "Viewed", "neutral",
                   [("Shortlist", "primary"), ("Reject", "danger")])
    return img


# ---------------------------------------------------------------------------
# 20 · States gallery
# ---------------------------------------------------------------------------

def screen_states():
    img, cv = new_screen()
    cv.status_bar()
    cv.appbar("States", back=False)
    cv.gap(4)
    section_label(cv, "Loading")
    cv.card(lambda c: (
        c.gap(6),
        c.activity(),
        c.text("Loading your profile…", "small", align="center", gap=0),
        c.gap(6),
    ), pad=16, gap=16)
    section_label(cv, "Empty")
    cv.card(lambda c: (
        c.icon_tile("clipboard", 44, "primary", 22, align="center"),
        c.gap(12),
        c.text("No applications yet", "card-title", align="center", gap=4),
        c.text("When you apply to a vacancy it appears here with its live status.", "caption",
               align="center", gap=0),
    ), pad=18, gap=16)
    section_label(cv, "Error")
    note_panel(cv, "danger", "triangle-alert", "Couldn't load vacancies",
               "Check your connection. Nothing was lost — try again.", gap=10)
    cv.button("Try again", "secondary", h=44, gap=18)
    section_label(cv, "Success")
    note_panel(cv, "success", "circle-check", "Application sent",
               "Gulf Steel Works can now see your Skill Passport. You'll see status changes here.", gap=16)
    section_label(cv, "Status pills")
    cv.pill_row([("Applied", "primary"), ("Viewed", "neutral"), ("Shortlisted", "success")], gap=6, y_after=8)
    cv.pill_row([("Hired", "success"), ("Rejected", "danger"), ("Withdrawn", "muted")], gap=6, y_after=8)
    cv.pill_row([("Draft", "muted"), ("Active", "success"), ("Paused", "warning"), ("Closed", "danger")],
                gap=6, y_after=8)
    cv.pill_row([("Pending", "warning"), ("Verified", "success"), ("Rejected", "danger")], gap=6, y_after=8)
    cv.pill_row([("Self-declared", "neutral"), ("Assessed", "success")], gap=6, y_after=10)
    section_label(cv, "Buttons")
    cv.button("Primary action", "primary", h=46, gap=8)
    cv.button("Secondary action", "secondary", h=46, gap=8)
    cv.button("Destructive action", "danger", h=44, gap=8)
    cv.button("Text action", "text", h=38, align="start", gap=8)
    cv.button("Disabled", "secondary", h=44, gap=0)
    return img


# ---------------------------------------------------------------------------
# 21 · Design system specimen (taller canvas)
# ---------------------------------------------------------------------------

def screen_design_system():
    from PIL import Image

    from mockup_kit import SCALE, W, _rgb

    height_dp = 1660
    img = Image.new("RGB", (W, height_dp * SCALE), _rgb(T["PageBackground"]))
    from PIL import ImageDraw
    d = ImageDraw.Draw(img)
    cv = Canvas(img, d, 16, W_DP - 32, 24, False, True)
    cv.status_bar()
    cv.y = 44
    cv.text("KaamLink design system", "title", gap=4)
    cv.text("v1.0 · Android · 360dp baseline · EN / AR", "caption", gap=18)

    section_label(cv, "Color")
    swatches = [("Primary", T["Primary"]), ("Primary soft", T["PrimarySoft"]), ("Accent", T["Accent"]),
                ("Accent soft", T["AccentSoft"]), ("Success", T["Success"]), ("Success soft", T["SuccessSoft"]),
                ("Warning", T["Warning"]), ("Warning soft", T["WarningSoft"]), ("Error", T["Error"]),
                ("Error soft", T["ErrorSoft"]), ("Ink", T["TextPrimary"]), ("Body", T["TextSecondary"]),
                ("Muted", T["TextMuted"]), ("Border", T["Border"]), ("Canvas", T["PageBackground"]),
                ("Surface", T["CardSurface"])]
    cols = 4
    cell_w = (cv.width - 8 * (cols - 1)) / cols
    for i, (name, hexv) in enumerate(swatches):
        row, col = divmod(i, cols)
        if cv.enabled:
            x = cv.x0 + (cell_w + 8) * col
            y = cv.y + 54 * row
            cv.fill_rect(cv.px(x), cv.px(y), cv.px(cell_w), cv.px(34), hexv, 8, T["Border"] if name in
                         ("Canvas", "Surface", "Primary soft", "Accent soft", "Success soft", "Warning soft",
                          "Error soft") else None)
            cv.d.text((cv.px(x), cv.px(y + 38)), f"{name}", font=font(8.5, "semibold"), fill=_c("TextPrimary"),
                      anchor="lt")
            cv.d.text((cv.px(x), cv.px(y + 47)), hexv.upper(), font=font(8, family="sans"),
                      fill=_c("TextMuted"), anchor="lt")
    cv.y += 54 * 4 - 4 + 10

    section_label(cv, "Type scale")
    for name, sample in (("Display 25 / 31", "Find work you're good at."),
                         ("Title 21 / 27", "Enter your mobile number"),
                         ("Section 17 / 23", "Skill passport"),
                         ("Card title 15.5 / 21", "Gulf Steel Works"),
                         ("Body 14.5 / 20.5", "Practical skill, verified by short tests."),
                         ("Small 13 / 18.5", "Sharjah · Industrial Area 12"),
                         ("Caption 12 / 16.5", "Applied 12 Oct · updated today"),
                         ("Overline 10.5", "SKILL PASSPORT")):
        st = name.split(" ")[0].lower()
        style = {"display": "display", "title": "title", "section": "h2", "card": "card-title",
                 "body": "body", "small": "small", "caption": "caption", "overline": "overline"}[st]
        cv.text(sample, style, gap=8)
    cv.gap(6)

    section_label(cv, "Spacing scale")
    if cv.enabled:
        labels = ["4", "8", "12", "16", "20", "24"]
        for i, v in enumerate([4, 8, 12, 16, 20, 24]):
            x = cv.px(cv.x0 + i * 44)
            cv.fill_rect(x, cv.px(cv.y), cv.px(v), cv.px(14), T["Primary"], 3)
            cv.d.text((x, cv.px(cv.y + 18)), labels[i], font=font(9), fill=_c("TextMuted"), anchor="lt")
    cv.y += 40

    section_label(cv, "Buttons")
    cv.button("Primary", "primary", h=44, gap=8)
    cv.button("Secondary", "secondary", h=44, gap=8)
    cv.button("Destructive", "danger", h=42, gap=8)
    cv.button("Text action", "text", h=34, align="start", gap=12)

    section_label(cv, "Pills")
    cv.pill_row([("Active", "success"), ("Pending", "warning"), ("Rejected", "danger")], gap=6, y_after=8)
    cv.pill_row([("Applied", "primary"), ("Self-declared", "neutral")], gap=6, y_after=14)

    section_label(cv, "Form field · valid / error")
    cv.field("Mobile number", "+971 50 123 4567", icon="smartphone")
    cv.field("Mobile number", "+971 5", bad=True, hint="Enter a valid UAE mobile number.")
    cv.gap(6)

    section_label(cv, "Card & list row")
    cv.card(lambda c: (
        identity(c, "GS", "Gulf Steel Works", "Sharjah · 4 openings", None, size=38, tone="neutral"),
        c.text("Steel fixer", "card-title", gap=4),
        c.icon_label_row("banknote", "2,000 – 2,800 AED / month", "small-strong", color="TextPrimary", gap=0),
    ), pad=14, gap=10)

    section_label(cv, "Arabic / RTL specimen")
    def specimen(c: Canvas):
        def body(cc: Canvas):
            ar(cc, "محمد عبد الرحمن", "h2")
            ar(cc, "حداد حديد تسليح · الشارقة", "small")
            cc.gap(6)
            ar(cc, "اكتمل الملف 80%", "caption")
            cc.gap(8)
            cc.button("عرض جواز المهارات", "primary", h=42, gap=4)
            cc.button("تسجيل الخروج", "text", h=30, align="start")
        c.card(body, pad=14, gap=0)

    specimen(cv)
    return img


def ar(cv: Canvas, text: str, style: str):
    from mockup_kit import STYLES as S, script_font as fnt, shape

    st = S[style]
    f = fnt(text, st.size, st.weight)
    shaped = shape(text, True)
    if cv.enabled:
        tw = f.getlength(shaped)
        cv.d.text((cv.px(cv.x0 + cv.width) - tw, cv.px(cv.y)), shaped, font=f, fill=_c(st.color), anchor="lt")
    cv.y += (st.line or st.size * 1.38) + 4


# ---------------------------------------------------------------------------
# 22 / 23 · Arabic screens
# ---------------------------------------------------------------------------

def screen_worker_home_ar():
    img, cv = new_screen(rtl=True)
    cv.status_bar()
    cv.gap(8)
    brand_row(cv, ("عامل", "primary"))
    cv.card(lambda c: (
        ar_row(c, "محمد عبد الرحمن", "حداد حديد تسليح · الشارقة", "MA"),
        c.divider(gap=10),
        ar(c, "اكتمل الملف 80%", "small-strong"),
        c.progress(0.8, 6, "primary", gap=6),
        ar(c, "أضف مهارة أخرى للوصول إلى 100%.", "caption"),
    ), pad=14, gap=16)
    section_label(cv, "إجراءات سريعة")
    if cv.enabled:
        gap = 10
        w = (cv.width - 2 * gap) / 3
        x = cv.x0 + cv.width - w
        for icon, label in (("briefcase-business", "الوظائف"), ("clipboard", "طلباتي"), ("badge-check", "الاختبارات")):
            sub = Canvas(cv.img, cv.d, x, w, cv.y, True, True)
            tile_ar(sub, icon, label, h=76)
            x -= w + gap
    cv.y += 76 + 4
    section_label(cv, "جواز المهارات", "3 مهارات")
    cv.card(lambda c: (
        ar_row_skill(c, "حديد التسليح", "6 سنوات", "أساسية", "primary"),
        c.divider(gap=10),
        ar_row_skill(c, "لحام MIG", "4 سنوات", "معلنة ذاتياً", "neutral"),
        c.divider(gap=10),
        ar_row_skill(c, "السقالات", "3 سنوات", "معلنة ذاتياً", "neutral"),
    ), pad=14, gap=16)
    section_label(cv, "التوفر والأجر")
    cv.card(lambda c: (
        ar(c, "متاح فوراً", "small-strong"),
        c.gap(6),
        ar(c, "1,800 – 2,500 درهم / شهرياً", "small"),
    ), pad=14, gap=18)
    cv.divider(gap=12)
    ar(cv, "التبديل إلى حساب صاحب العمل", "small")
    ar(cv, "تسجيل الخروج", "small")
    return img


def tile_ar(cv: Canvas, icon: str, label: str, h: float = 76):
    from mockup_kit import font as fnt, script_font, shape

    if cv.enabled:
        cv.rect(cv.y, h, cv.width, T["CardSurface"], 13, T["Border"])
        ix = cv.width - 12 - 34
        cv.fill_rect(cv.px(cv.x0 + ix), cv.px(cv.y + 12), cv.px(34), cv.px(34), T["PrimarySoft"], 10)
        fi = fnt(22, family="icon")
        cv.d.text((cv.px(cv.x0 + ix) + cv.px(6), cv.px(cv.y + 18)), chr(ICONS[icon]), font=fi, fill=T["Primary"],
                  anchor="lt")
        f = script_font(label, 10.5, "semibold")
        shaped = shape(label, True)
        cv.d.text((cv.px(cv.x0 + cv.width - 10) - f.getlength(shaped), cv.px(cv.y + 52)), shaped, font=f,
                  fill=T["TextPrimary"], anchor="lt")
    cv.y += h + 12


def ar_row(cv: Canvas, name: str, meta: str, initials: str):
    from mockup_kit import script_font as fnt, shape

    if cv.enabled:
        cv.avatar(initials, 48, "primary", align="start", y=cv.y)
        f1 = fnt(name, 16, "semibold")
        f2 = fnt(meta, 12)
        right = cv.px(cv.x0 + cv.width - 48 - 12)   # RTL: avatar sits at the start (right)
        cv.d.text((right - f1.getlength(shape(name, True)), cv.px(cv.y + 2)), shape(name, True),
                  font=f1, fill=T["TextPrimary"], anchor="lt")
        cv.d.text((right - f2.getlength(shape(meta, True)), cv.px(cv.y + 26)), shape(meta, True),
                  font=f2, fill=T["TextMuted"], anchor="lt")
    cv.y += 48 + 12


def ar_row_skill(cv: Canvas, name: str, years: str, state: str, tone: str):
    from mockup_kit import script_font as fnt, shape

    if cv.enabled:
        f = fnt(name, 14, "semibold")
        cv.d.text((cv.px(cv.x0 + cv.width) - f.getlength(shape(name, True)), cv.px(cv.y)), shape(name, True),
                  font=f, fill=T["TextPrimary"], anchor="lt")
        fg, bg = TONES[tone]
        f2 = fnt(state, 11.5, "semibold")
        label = shape(state, True)
        w = f2.getlength(label) / 2 + 18
        cv.fill_rect(cv.px(cv.x0), cv.px(cv.y), cv.px(w), cv.px(22), T[bg], 11, T[fg])
        cv.d.text((cv.px(cv.x0 + 9), cv.px(cv.y + 3.5)), label, font=f2, fill=T[fg], anchor="lt")
        f3 = fnt(years, 11)
        yl = shape(years, True)
        cv.d.text((cv.px(cv.x0 + cv.width) - f3.getlength(yl), cv.px(cv.y + 26)), yl, font=f3,
                  fill=T["TextMuted"], anchor="lt")
    cv.y += 46


def screen_vacancy_feed_ar():
    img, cv = new_screen(rtl=True)
    cv.status_bar()
    cv.appbar("ابحث عن عمل", back=True)
    cv.search("المسمى الوظيفي أو المهارة")
    cv.chips(["الكل", "دبي", "الشارقة", "عجمان"], active=-1, gap=10)
    ar(cv, "6 وظائف", "caption")
    cv.card(lambda c: (
        ar_row(c, "أعمال حديد التسليح", "شركة الخليج للحديد · الشارقة", "GS"),
        c.gap(4),
        ar(c, "حداد حديد تسليح — 4 وظائف", "body-strong"),
        ar(c, "2,000 – 2,800 درهم / شهرياً", "small"),
    ), pad=14, gap=12)
    cv.card(lambda c: (
        ar_row(c, "مساعد مستودع", "النور للخدمات اللوجستية · دبي", "AN"),
        c.gap(4),
        ar(c, "1,800 – 2,200 درهم / شهرياً", "small"),
    ), pad=14, gap=12)
    return img


# ---------------------------------------------------------------------------
# Runner
# ---------------------------------------------------------------------------

SCREENS = [
    ("01-welcome", screen_welcome),
    ("02-login", screen_login),
    ("03-otp", screen_otp),
    ("04-worker-onboarding-basics", screen_onboarding_basics),
    ("05-worker-onboarding-skills", screen_onboarding_skills),
    ("06-worker-onboarding-preferences", screen_onboarding_preferences),
    ("07-worker-home", screen_worker_home),
    ("08-vacancy-feed", screen_vacancy_feed),
    ("09-vacancy-detail", screen_vacancy_detail),
    ("10-my-applications", screen_my_applications),
    ("11-my-assessments", screen_my_assessments),
    ("12-assessment-run", screen_assessment_run),
    ("13-assessment-result", screen_assessment_result),
    ("14-employer-onboarding", screen_employer_onboarding),
    ("15-employer-home-pending", lambda: screen_employer_home(False)),
    ("16-employer-home-verified", lambda: screen_employer_home(True)),
    ("17-company-vacancies", screen_company_vacancies),
    ("18-vacancy-edit", screen_vacancy_edit),
    ("19-vacancy-applicants", screen_applicants),
    ("20-states", screen_states),
    ("21-design-system", screen_design_system),
    ("22-arabic-worker-home", screen_worker_home_ar),
    ("23-arabic-vacancy-feed", screen_vacancy_feed_ar),
]


def main():
    for name, fn in SCREENS:
        img = fn()
        path = os.path.join(OUT, f"{name}.png")
        img.save(path)
        print(f"  ✓ {name}.png  ({img.size[0]}×{img.size[1]})")


if __name__ == "__main__":
    main()
