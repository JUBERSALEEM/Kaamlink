namespace KaamLink.Core.Presentation.Clay
{
    /// <summary>How much a clay surface is lifted off the page.</summary>
    public enum ClayElevation
    {
        None,
        Pill,
        Card,
        Raised
    }

    /// <summary>
    /// The clay material: one attached property that gives any element a soft,
    /// wide cast shadow sized for its elevation.
    ///
    /// Why an attached property instead of a Style setter for <c>Shadow</c>:
    /// a <see cref="Shadow"/> stored in a style would be a single instance shared
    /// by every element that uses the style, and a shadow belongs to one element.
    /// The attached property runs per element, so each surface owns its own.
    ///
    /// The shadow colour comes from the active palette (<c>ClayShadowBrush</c>),
    /// so it follows a theme switch; <see cref="Refresh"/> re-applies every
    /// shadow in a live tree after the palette dictionary is replaced.
    /// </summary>
    public static class ClayMaterial
    {
        public static readonly BindableProperty ElevationProperty = BindableProperty.CreateAttached(
            "Elevation",
            typeof(ClayElevation),
            typeof(ClayMaterial),
            ClayElevation.None,
            propertyChanged: OnElevationChanged);

        public static ClayElevation GetElevation(BindableObject bindable) =>
            bindable.GetValue(ElevationProperty) is ClayElevation elevation ? elevation : ClayElevation.None;

        public static void SetElevation(BindableObject bindable, ClayElevation elevation) =>
            bindable.SetValue(ElevationProperty, elevation);

        private static void OnElevationChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is VisualElement element)
                Apply(element, newValue is ClayElevation elevation ? elevation : ClayElevation.None);
        }

        /// <summary>Applies (or removes) the clay shadow for one element.</summary>
        public static void Apply(VisualElement element, ClayElevation elevation)
        {
            if (elevation == ClayElevation.None)
            {
                element.Shadow = null;
                return;
            }

            // Wide and low-opacity reads as soft material; a tight dark shadow
            // would read as a drop-shadowed card, which is the opposite of clay.
            var (offsetX, offsetY, radius, opacity) = elevation switch
            {
                ClayElevation.Pill => (3f, 4f, 12f, 0.18f),
                ClayElevation.Card => (5f, 7f, 20f, 0.22f),
                _ => (7f, 10f, 26f, 0.26f)
            };

            element.Shadow = new Shadow
            {
                Brush = ResolveBrush("ClayShadowBrush"),
                Offset = new Point(offsetX, offsetY),
                Radius = radius,
                Opacity = opacity
            };
        }

        /// <summary>
        /// Re-applies shadows across a live tree. Called by the theme service after
        /// the palette dictionary is swapped: the gradients repaint themselves
        /// (they are DynamicResource), the shadows cannot.
        /// </summary>
        public static void Refresh(IVisualTreeElement root)
        {
            if (root is VisualElement element && GetElevation(element) != ClayElevation.None)
                Apply(element, GetElevation(element));

            foreach (var child in root.GetVisualChildren())
                Refresh(child);
        }

        private static Brush ResolveBrush(string key)
        {
            // Not written as `Application.Current?.Resources.TryGetValue(key, out var x)`
            // on purpose: with a null-conditional call the out variable is not
            // definitely assigned, so that form does not compile.
            var resources = Application.Current?.Resources;
            if (resources is not null
                && resources.TryGetValue(key, out var value)
                && value is Brush brush)
            {
                return brush;
            }

            return new SolidColorBrush(Color.FromArgb("#4F5C86"));
        }
    }
}
