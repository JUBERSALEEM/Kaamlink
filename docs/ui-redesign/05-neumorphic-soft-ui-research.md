# Accessible neumorphic soft UI — implementation research (2026)

## Finding

“Neumorphism” is a visual direction, not a current Android/.NET MAUI component standard. A 2026 design review notes that pure, shadow-only neumorphism is mostly suited to concept work and focused instrument UIs; it is a poor whole-product default because identical fills and diffuse shadows obscure affordances. For KaamLink, use an **accessible soft-UI / clay hybrid**: reserve tactile depth for the welcome illustration frame and role cards, keep stronger color and strokes on actions, and retain readable labels, native controls and explicit states. Shadows add material mood, never the only signal that something is interactive.

## Decisions applied

- The light palette now uses a cool, single-family grey-blue base; cards sit close to the page surface, with a restrained edge and a low-opacity cast shadow. The dusk palette uses the equivalent dark surface stack, not inverted bright shadows.
- Primary actions stay saturated blue/teal with high-contrast labels. Secondary actions and the country/theme controls use explicit outline strokes and native `Button` hit-testing; the theme switch is no longer a `Border` gesture target.
- Pressed controls visibly depress/scale; controls remain text-labelled and have semantic descriptions. The phone field has a visible outline independent of shadow.
- The role chooser now carries the same opening brand row, “Welcome to KaamLink” headline, “Find work. Hire with confidence.” subtitle, and shared hero frame as the approved sign-in design. Its worker/employer actions follow the hero while preserving the existing role-selection journey.
- The hero uses the same contained illustration, restrained frame and EN/AR semantic description on both entry pages. Pressing primary/secondary actions removes elevation and compresses the button; focused actions get a thicker border and slight lift. Text and labels—not shadow direction—still identify actions and states.
- The landing/role chooser remains a focused mobile flow. The branded SVG illustration and mark remain separate image assets, not inline image data.
- Runtime palette replacement continues through `DynamicResource`; this is the MAUI-supported mechanism for repainting current views when dictionary values are replaced.
- MAUI exposes one `Shadow` per `VisualElement`, so this pass uses one restrained cast plus tonal surfaces/edges rather than pretending a layered two-shadow stack is a native multi-shadow primitive.

## Accessibility and platform checks

- WCAG 2.2 SC 1.4.11 concerns non-text contrast when a component boundary/state is needed to identify a control. KaamLink keeps the button text and also gives fields/secondary controls explicit edges; the palette gate measures input boundaries at 3:1 and text pairs at 4.5:1.
- The 2026 soft-UI implementation guidance recommends a non-shadow cue, a visible focus/pressed state, and 44px-class touch targets; we use MAUI native controls and 44–52dp heights rather than making shadow-only tappable cards.
- MAUI's official theming documentation specifies `DynamicResource` for runtime theme changes; the app keeps a palette-only engine and persists the selected mode.

## Global phone-number research

Phone numbers are not a single “country code per country” table: calling codes can be shared by several regions, and national dialing prefixes differ. The sign-in picker is backed by an offline, versioned 245-region list derived from `libphonenumber-js` 1.13.15 metadata, with localized device region names, search over name/ISO/calling code, and E.164 normalization. International `+` and `00` input is preserved. The API's identity key remains normalized E.164.

This is intentionally not a claim that the current development SMS sink delivers worldwide. The API defaults to a development log OTP provider; a production SMS gateway and a reachable HTTPS API host must be configured before real people can receive codes globally.

## Sources reviewed

- [.NET MAUI official theming guide](https://github.com/dotnet/docs-maui/blob/main/docs/user-interface/theming.md) — palette dictionaries and `DynamicResource` for live theme replacement.
- [W3C WCAG 2.2 SC 1.4.11](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html) — visual information that is necessary to identify controls or states needs 3:1 adjacent contrast. A separate boundary is not mandatory when visible text/icon already identifies a control, but focus cues still need to be visible.
- [W3C WCAG 2.2 SC 1.4.3](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) — normal text needs at least 4.5:1 contrast.
- [2026 neumorphism design review](https://www.setproduct.com/blog/neumorphism-design-guide) — pure neumorphism is primarily suited to concept art and instrument-style single-purpose screens; for real products it recommends accent/border cues and accessible contrast. Treated as industry commentary, not a normative standard.
- [.NET MAUI visual states](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/visual-states?view=net-maui-10.0) and [semantic accessibility properties](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility?view=net-maui-10.0) — keep press/focus presentation in XAML states and expose useful image/control descriptions to platform screen readers.
- [Google libphonenumber falsehoods](https://github.com/google/libphonenumber/blob/master/FALSEHOODS.md) — shared calling codes and region-specific numbering mean the picker must use region metadata and allow international-format entry.
- [libphonenumber-js metadata project](https://github.com/catamphetamine/libphonenumber-js) — pinned data source for the bundled calling-code index; see `Resources/Raw/NOTICE-phone-calling-codes.txt`.

## Remaining external prerequisites

1. A public HTTPS KaamLink API URL is needed for phones on mobile data or unrelated Wi-Fi networks. The current Debug endpoint is emulator/USB-development-only (`10.0.2.2` / `localhost` + `adb reverse`); Release still has the intentional `.invalid` placeholder.
2. Real global SMS needs a production OTP delivery provider and credentials held in server secret storage.
3. Google sign-in needs a Google OAuth client, a server-side ID-token verification/exchange endpoint, and a product decision about whether Google-only accounts must add/verify a phone before applying or hiring. No auth success is fabricated while these are absent.
