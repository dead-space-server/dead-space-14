using System.Numerics;
using Content.Client.DeadSpace.Stylesheets;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls
{
    public sealed class StripeBack : Container
    {
        private const float PadSize = 4;
        private const float EdgeSize = 2;
        // DS14-start
        private static Color EdgeColor => DeadSpaceStylePalette.ClassicChrome
            ? Color.FromHex("#525252FF")
            : DeadSpaceStylePalette.AccentDim;
        // DS14-end

        private bool _hasTopEdge = true;
        private bool _hasBottomEdge = true;
        private bool _hasMargins = true;

        public const string StylePropertyBackground = "background";

        public bool HasTopEdge
        {
            get => _hasTopEdge;
            set
            {
                _hasTopEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasBottomEdge
        {
            get => _hasBottomEdge;
            set
            {
                _hasBottomEdge = value;
                InvalidateMeasure();
            }
        }

        public bool HasMargins
        {
            get => _hasMargins;
            set
            {
                _hasMargins = value;
                InvalidateMeasure();
            }
        }

        protected override Vector2 MeasureOverride(Vector2 availableSize)
        {
            var padSize = HasMargins ? PadSize : 0;
            var padSizeTotal = 0f;

            if (HasBottomEdge)
                padSizeTotal += padSize + EdgeSize;
            if (HasTopEdge)
                padSizeTotal += padSize + EdgeSize;

            var size = Vector2.Zero;

            availableSize.Y -= padSizeTotal;

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                size = Vector2.Max(size, child.DesiredSize);
            }

            return size + new Vector2(0, padSizeTotal);
        }

        protected override Vector2 ArrangeOverride(Vector2 finalSize)
        {
            var padSize = HasMargins ? PadSize : 0;

            // DS14-start: when the stripe is shorter than its edges, keep the box from inverting (UIBox2 rejects it).
            var top = HasTopEdge ? padSize + EdgeSize : 0f;
            var bottom = finalSize.Y - (HasBottomEdge ? padSize + EdgeSize : 0f);
            if (bottom < top)
                top = bottom = Math.Clamp((top + bottom) / 2f, 0f, finalSize.Y);

            var box = new UIBox2(0, top, finalSize.X, bottom);
            // DS14-end

            foreach (var child in Children)
            {
                child.Arrange(box);
            }

            return finalSize;
        }


        protected override void Draw(DrawingHandleScreen handle)
        {
            var padSize = HasMargins ? PadSize : 0;
            var edge = (padSize + EdgeSize) * UIScale;
            //DS-14 start
            var top = HasTopEdge ? edge : 0f;
            var bottom = PixelHeight - (HasBottomEdge ? edge : 0f);
            if (bottom < top)
                top = bottom = Math.Clamp((top + bottom) / 2f, 0f, PixelHeight);

            if (HasTopEdge && top > padSize * UIScale)
                handle.DrawRect(new UIBox2(0, padSize * UIScale, PixelWidth, top), EdgeColor);

            var bottomEdge = PixelHeight - padSize * UIScale;
            if (HasBottomEdge && bottomEdge > bottom)
                handle.DrawRect(new UIBox2(0, bottom, PixelWidth, bottomEdge), EdgeColor);

            var centerBox = new UIBox2(0, top, PixelWidth, bottom);
            //DS-14 end

            GetActualStyleBox()?.Draw(handle, centerBox, UIScale);
        }

        private StyleBox? GetActualStyleBox()
        {
            return TryGetStyleProperty(StylePropertyBackground, out StyleBox? box) ? box : null;
        }
    }
}
