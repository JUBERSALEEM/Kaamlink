using Microsoft.Maui.Controls.Shapes;

using KaamLink.Core.Presentation.Clay;

namespace KaamLink.Controls
{
    /// <summary>
    /// The app's reusable "nothing to show" panel.
    ///
    ///  * Loading — a spinner plus one explanatory line, inside a reserved block
    ///    so the page does not jump when data arrives.
    ///  * Empty  — an icon tile, a short title and one honest explanation. It
    ///    never offers an action the screen cannot actually perform.
    ///
    /// All copy is supplied by the page (from AppStrings), so this control stays
    /// localization-neutral and contains no hardcoded English.
    /// </summary>
    public sealed class StatePanel : ContentView
    {
        public const string KindLoading = "loading";
        public const string KindEmpty = "empty";

        private readonly Border _border;
        private readonly ActivityIndicator _spinner;
        private readonly Border _iconTile;
        private readonly Label _icon;
        private readonly Label _title;
        private readonly Label _message;

        public StatePanel()
        {
            _spinner = new ActivityIndicator
            {
                IsRunning = true,
                HorizontalOptions = LayoutOptions.Center,
                HeightRequest = 24,
                WidthRequest = 24,
            };
            _spinner.SetDynamicResource(ActivityIndicator.ColorProperty, "Primary");

            _icon = new Label
            {
                FontFamily = "Lucide",
                FontSize = 22,
                LineHeight = 1,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            };
            _icon.SetDynamicResource(Label.TextColorProperty, "Primary");

            _iconTile = new Border
            {
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 18 },
                HeightRequest = 44,
                WidthRequest = 44,
                HorizontalOptions = LayoutOptions.Center,
                Content = _icon,
                IsVisible = false,
            };
            _iconTile.SetDynamicResource(VisualElement.BackgroundColorProperty, "PrimarySoft");

            _title = new Label
            {
                FontFamily = "OpenSansSemibold",
                FontSize = 15.5,
                HorizontalTextAlignment = TextAlignment.Center,
                IsVisible = false,
            };
            _title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");

            _message = new Label
            {
                FontFamily = "OpenSansRegular",
                FontSize = 12,
                LineHeight = 1.34,
                HorizontalTextAlignment = TextAlignment.Center,
                IsVisible = false,
            };
            _message.SetDynamicResource(Label.TextColorProperty, "TextMuted");

            var stack = new VerticalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Fill,
                Children = { _spinner, _iconTile, _title, _message },
            };

            _border = new Border
            {
                StrokeThickness = 1,
                Padding = new Thickness(20, 24),
                StrokeShape = new RoundRectangle { CornerRadius = 24 },
                Content = stack,
            };
            _border.SetDynamicResource(VisualElement.BackgroundColorProperty, "CardSurface");
            _border.SetDynamicResource(Border.StrokeProperty, "BorderBrush");
            ClayMaterial.SetElevation(_border, ClayElevation.Card);

            Content = _border;
        }

        // ---------------------------------------------------------------- Kind
        public static readonly BindableProperty KindProperty = BindableProperty.Create(
            nameof(Kind), typeof(string), typeof(StatePanel), KindLoading,
            propertyChanged: (bindable, _, value) => ((StatePanel)bindable).ApplyKind(value as string));

        /// <summary>loading or empty.</summary>
        public string Kind
        {
            get => (string)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        // ---------------------------------------------------------------- Parameters
        public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
            nameof(Glyph), typeof(string), typeof(StatePanel), null,
            propertyChanged: (bindable, _, value) => ((StatePanel)bindable).ApplyGlyph(value as string));

        /// <summary>Lucide glyph for the empty state's icon tile.</summary>
        public string? Glyph
        {
            get => (string?)GetValue(GlyphProperty);
            set => SetValue(GlyphProperty, value);
        }

        public static readonly BindableProperty TitleProperty = BindableProperty.Create(
            nameof(Title), typeof(string), typeof(StatePanel), null,
            propertyChanged: (bindable, _, value) => ((StatePanel)bindable).ApplyContent());

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly BindableProperty MessageProperty = BindableProperty.Create(
            nameof(Message), typeof(string), typeof(StatePanel), null,
            propertyChanged: (bindable, _, value) => ((StatePanel)bindable).ApplyContent());

        public string? Message
        {
            get => (string?)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        // ---------------------------------------------------------------- impl
        private void ApplyKind(string? kind)
        {
            var loading = !string.Equals(kind, KindEmpty, StringComparison.Ordinal);
            _spinner.IsVisible = loading;
            _iconTile.IsVisible = !loading;
        }

        private void ApplyGlyph(string? glyph) => _icon.Text = glyph ?? string.Empty;

        private void ApplyContent()
        {
            _title.Text = Title ?? string.Empty;
            _title.IsVisible = !string.IsNullOrWhiteSpace(Title);

            _message.Text = Message ?? string.Empty;
            _message.IsVisible = !string.IsNullOrWhiteSpace(Message);
        }

    }
}
