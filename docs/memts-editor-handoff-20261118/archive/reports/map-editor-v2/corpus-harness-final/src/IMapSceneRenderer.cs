using System.Collections.Generic;
using System.Drawing;

namespace TricksterMap
{
    public interface IMapSceneRenderer
    {
        IEnumerable<string> Warnings { get; }
        void Render(Graphics graphics, Rectangle viewport, double zoom, double originX, double originY);
    }
}
