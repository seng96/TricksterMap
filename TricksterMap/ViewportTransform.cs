using System;

namespace TricksterMap
{
    /// <summary>Shared map-pixel to viewport-pixel transform; pan never changes map data.</summary>
    public sealed class ViewportTransform
    {
        public const double MinimumZoom = 0.01;
        public const double MaximumZoom = 16;
        public double Zoom { get; private set; } = 1;
        public double OriginX { get; private set; }
        public double OriginY { get; private set; }
        public double WorldX(double screenX) => (screenX - OriginX) / Zoom;
        public double WorldY(double screenY) => (screenY - OriginY) / Zoom;
        public double ScreenX(double worldX) => OriginX + worldX * Zoom;
        public double ScreenY(double worldY) => OriginY + worldY * Zoom;

        public void ZoomAt(double screenX, double screenY, double newZoom)
        {
            if (double.IsNaN(newZoom) || double.IsInfinity(newZoom) || newZoom <= 0)
                throw new ArgumentOutOfRangeException(nameof(newZoom));
            double x = WorldX(screenX), y = WorldY(screenY);
            Zoom = Math.Max(MinimumZoom, Math.Min(MaximumZoom, newZoom));
            OriginX = screenX - x * Zoom; OriginY = screenY - y * Zoom;
        }
        public void Pan(double x, double y) { OriginX += x; OriginY += y; }
        public void Fit(double width, double height, int viewportWidth, int viewportHeight)
        {
            ValidateDimensions(width, height);
            Zoom = Math.Max(MinimumZoom, Math.Min(MaximumZoom,
                Math.Min(Math.Max(1, viewportWidth - 40) / width, Math.Max(1, viewportHeight - 40) / height)));
            Center(width, height, viewportWidth, viewportHeight);
        }
        public void Reset(double width, double height, int viewportWidth, int viewportHeight)
        {
            ValidateDimensions(width, height); Zoom = 1;
            Center(width, height, viewportWidth, viewportHeight);
        }
        private void Center(double width, double height, int viewportWidth, int viewportHeight)
        {
            OriginX = (viewportWidth - width * Zoom) / 2;
            OriginY = (viewportHeight - height * Zoom) / 2;
        }
        private static void ValidateDimensions(double width, double height)
        {
            if (width <= 0 || height <= 0 || double.IsInfinity(width) || double.IsInfinity(height) ||
                double.IsNaN(width) || double.IsNaN(height)) throw new ArgumentOutOfRangeException(nameof(width));
        }
    }
}
