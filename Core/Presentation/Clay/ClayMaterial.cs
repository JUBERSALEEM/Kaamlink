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
        // A defensive ceiling: a pathological platform visual tree must never be
        // able to turn a cosmetic refresh into a stack overflow / "keeps stopping".
        private const int MaxRefreshDepth = 64;

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
        /// the palette dictionary is swapped.
        ///
        /// Iterative + visited set on purpose: recursive GetVisualChildren()
        /// traversal can cycle on some platform-specific visual trees, and a
        /// StackOverflowException cannot be caught by ThemeService's try/catch.
        /// </summary>
        public static void Refresh(IVisualTreeElement root)
        {
            var visited = new HashSet<IVisualTreeElement>(ReferenceEqualityComparer.Instance);
            var pending = new Stack<(IVisualTreeElement Element, int Depth)>();
            pending.Push((root, 0));

            while (pending.Count > 0)
            {
                var (current, depth) = pending.Pop();
                if (depth > MaxRefreshDepth || !visited.Add(current))
                    continue;

                ApplyShadowSafely(current);

                IReadOnlyList<IVisualTreeElement> children;
                try
                {
                    children = current.GetVisualChildren().ToArray();
                }
                catch (Exception exception)
                {
                    // One platform view exposing a broken visual child must not
                    // abort the whole refresh or crash the theme toggle.
                    Console.WriteLine($"[KaamLink] clay visual child enumeration failed: {exception}");
                    continue;
                }

                foreach (var child in children)
                    pending.Push((child, depth + 1));
            }
        }

        private static void ApplyShadowSafely(IVisualTreeElement node)
        {
            try
            {
                if (node is VisualElement element && GetElevation(element) != ClayElevation.None)
                    Apply(element, GetElevation(element));
            }
            catch (Exception exception)
            {
                Console.WriteLine($"[KaamLink] clay shadow apply failed: {exception}");
            }
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
