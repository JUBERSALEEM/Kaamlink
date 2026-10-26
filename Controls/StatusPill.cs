using Microsoft.Maui.Controls.Shapes;

namespace KaamLink.Controls
{
    /// <summary>
    /// The app's single status-pill component: a 22dp soft-tinted capsule with a
    /// text label (and optionally a leading icon) used for company verification,
    /// vacancy lifecycle, application decisions and skill states.
    ///
    /// Colour is never decided here — callers pass the tone colours that the
    /// existing semantic converters resolved from the API enum name, so the raw
    /// business value still drives meaning (see Core/Presentation/Converters.cs).
    /// A pill always renders text, so status is never conveyed by colour alone.
    /// </summary>
    public sealed class StatusPill : ContentView
    {
        private readonly Border _border;
        private readonly Label _label;
        private readonly Label _icon;

        public StatusPill()
        {
            _icon = new Label
            {
                FontFamily = "Lucide",
                FontSize = 12,
                LineHeight = 1,
                VerticalOptions = LayoutOptions.Center,
                IsVisible = false,
            };

            _label = new Label
            {
                FontFamily = "OpenSansSemibold",
                FontSize = 11.5,
                LineHeight = 1.2,
                VerticalOptions = LayoutOptions.Center,
            };

            var content = new HorizontalStackLayout
            {
                Spacing = 5,
                VerticalOptions = LayoutOptions.Center,
                Children = { _icon, _label },
            };

            _border = new Border
            {
                Padding = new Thickness(9, 2),
                StrokeThickness = 1,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Start,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Content = content,
            };

            Content = _border;

            _label.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
            _icon.SetDynamicResource(Label.TextColorProperty, "TextSecondary");

            // Establish the neutral capsule up front: callers that never set a Tone
            // (e.g. the "self-declared" skill pills) must still get the outline and
            // the soft fill, not bare floating text.
            ApplyTone();
        }

        // ---------------------------------------------------------------- Text
        public static readonly BindableProperty TextProperty = BindableProperty.Create(
            nameof(Text), typeof(string), typeof(StatusPill), string.Empty,
            propertyChanged: (bindable, _, value) => ((StatusPill)bindable).ApplyText(value as string));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        // ---------------------------------------------------------------- Glyph
        /// <summary>Optional Lucide glyph (e.g. the tick/clock shown on trust pills).</summary>
        public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
            nameof(Glyph), typeof(string), typeof(StatusPill), null,
            propertyChanged: (bindable, _, value) => ((StatusPill)bindable).ApplyGlyph(value as string));

        public string? Glyph
        {
            get => (string?)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        // ---------------------------------------------------------------- Tone colours
        /// <summary>Foreground/outline tone (resolved from the enum by a converter).</summary>
        public static readonly BindableProperty ToneProperty = BindableProperty.Create(
            nameof(Tone), typeof(Color), typeof(StatusPill), null,
            propertyChanged: (bindable, _, value) => ((StatusPill)bindable).ApplyTone());

        public Color? Tone
        {
            get => (Color?)GetValue(ToneProperty);
            set => SetValue(ToneProperty, value);
        }

        /// <summary>Soft background tone (resolved from the enum by a converter).</summary>
        public static readonly BindableProperty ToneSoftProperty = BindableProperty.Create(
            nameof(ToneSoft), typeof(Color), typeof(StatusPill), null,
            propertyChanged: (bindable, _, value) => ((StatusPill)bindable).ApplyTone());

        public Color? ToneSoft
        {
            get => (Color?)GetValue(ToneSoftProperty);
            set => SetValue(ToneSoftProperty, value);
        }

        // ---------------------------------------------------------------- impl
        private void ApplyText(string? text)
        {
            _label.Text = text ?? string.Empty;
            IsVisible = !string.IsNullOrWhiteSpace(text);
        }

        private void ApplyGlyph(string? glyph)
        {
            _icon.Text = glyph;
            _icon.IsVisible = !string.IsNullOrWhiteSpace(glyph);
        }

        private void ApplyTone()
        {
            var tone = Tone;
            var soft = ToneSoft;

            if (tone is null)
            {
                // Stroke is Brush-typed: the Color token "Border" would be the wrong
                // type here, so resolve the matching brush token.
                _border.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
                _label.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
                _icon.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
            }
            else
            {
                _border.Stroke = new SolidColorBrush(tone);
                _label.TextColor = tone;
                _icon.TextColor = tone;
            }

            if (soft is null)
                _border.SetDynamicResource(Border.BackgroundColorProperty, "CardSurfaceAlt");
            else
                _border.BackgroundColor = soft;
        }
    }
}
