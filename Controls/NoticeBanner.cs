using Microsoft.Maui.Controls.Shapes;

namespace KaamLink.Controls
{
    /// <summary>
    /// The app's reusable message panel: a tinted block with an icon, a bold
    /// title and one line of explanation, used for trust/verification notes,
    /// validation summaries and success confirmations.
    ///
    /// Copy always comes from AppStrings through bindings or the caller's XAML —
    /// this control never contains user-facing text of its own, so Arabic and
    /// English stay in the resource files.
    /// </summary>
    public sealed class NoticeBanner : ContentView
    {
        public const string KindInfo = "info";
        public const string KindSuccess = "success";
        public const string KindWarning = "warning";
        public const string KindDanger = "danger";

        private readonly Border _border;
        private readonly Label _icon;
        private readonly Label _title;
        private readonly Label _message;
        private readonly Label _caption;

        public NoticeBanner()
        {
            _icon = new Label
            {
                FontFamily = "Lucide",
                FontSize = 17,
                LineHeight = 1,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(0, 1, 0, 0),
            };

            _title = new Label
            {
                FontFamily = "OpenSansSemibold",
                FontSize = 13,
                LineHeight = 1.35,
                IsVisible = false,
            };

            _message = new Label
            {
                FontFamily = "OpenSansRegular",
                FontSize = 13,
                LineHeight = 1.38,
                TextColor = Resolve("TextSecondary"),
                IsVisible = false,
            };

            _caption = new Label
            {
                FontFamily = "OpenSansRegular",
                FontSize = 12,
                LineHeight = 1.34,
                TextColor = Resolve("TextMuted"),
                IsVisible = false,
                Margin = new Thickness(0, 6, 0, 0),
            };

            var text = new VerticalStackLayout
            {
                Spacing = 3,
                Children = { _title, _message, _caption },
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                },
                ColumnSpacing = 10,
                Padding = new Thickness(14, 13),
            };
            row.Add(_icon, 0, 0);
            row.Add(text, 1, 0);

            _border = new Border
            {
                Padding = 0,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = row,
            };

            Content = _border;
            ApplyKind(KindInfo);
        }

        // ---------------------------------------------------------------- Kind
        public static readonly BindableProperty KindProperty = BindableProperty.Create(
            nameof(Kind), typeof(string), typeof(NoticeBanner), KindInfo,
            propertyChanged: (bindable, _, value) => ((NoticeBanner)bindable).ApplyKind(value as string));

        /// <summary>One of info / success / warning / danger.</summary>
        public string Kind
        {
            get => (string)GetValue(KindProperty);
            set => SetValue(KindProperty, value);
        }

        // ---------------------------------------------------------------- Title
        public static readonly BindableProperty TitleProperty = BindableProperty.Create(
            nameof(Title), typeof(string), typeof(NoticeBanner), null,
            propertyChanged: (bindable, _, value) => ((NoticeBanner)bindable).ApplyContent());

        public string? Title
        {
            get => (string?)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        // ---------------------------------------------------------------- Message
        public static readonly BindableProperty MessageProperty = BindableProperty.Create(
            nameof(Message), typeof(string), typeof(NoticeBanner), null,
            propertyChanged: (bindable, _, value) => ((NoticeBanner)bindable).ApplyContent());

        public string? Message
        {
            get => (string?)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        // ---------------------------------------------------------------- Caption
        public static readonly BindableProperty CaptionProperty = BindableProperty.Create(
            nameof(Caption), typeof(string), typeof(NoticeBanner), null,
            propertyChanged: (bindable, _, value) => ((NoticeBanner)bindable).ApplyContent());

        public string? Caption
        {
            get => (string?)GetValue(CaptionProperty);
            set => SetValue(CaptionProperty, value);
        }

        // ---------------------------------------------------------------- impl
        private void ApplyContent()
        {
            _title.Text = Title ?? string.Empty;
            _title.IsVisible = !string.IsNullOrWhiteSpace(Title);

            _message.Text = Message ?? string.Empty;
            _message.IsVisible = !string.IsNullOrWhiteSpace(Message);

            _caption.Text = Caption ?? string.Empty;
            _caption.IsVisible = !string.IsNullOrWhiteSpace(Caption);
        }

        private void ApplyKind(string? kind)
        {
            var (foreground, background, glyph) = kind switch
            {
                KindSuccess => ("Success", "SuccessSoft", "\uE226"),   // circle-check
                KindWarning => ("Warning", "WarningSoft", "\uE087"),   // clock
                KindDanger => ("Error", "ErrorSoft", "\uE077"),        // circle-alert
                _ => ("Primary", "PrimarySoft", "\uE0F9"),             // info
            };

            var tone = Resolve(foreground);
            _icon.Text = glyph;
            _icon.TextColor = tone;
            _title.TextColor = tone;
            _border.BackgroundColor = Resolve(background);
        }

        private static Color Resolve(string key) =>
            Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
                ? color
                : Colors.Gray;
    }
}
