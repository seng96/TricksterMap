using System;
using System.Collections.Generic;
using System.Linq;
using TricksterMap.Data;

namespace TricksterMap
{
    /// <summary>Edits raw row-major config cells without interpreting unknown values.</summary>
    public sealed class ConfigLayerEditService
    {
        private readonly ConfigLayer layer;
        private byte[] saved;
        private readonly Stack<CellChange[]> undo = new Stack<CellChange[]>();
        private readonly Stack<CellChange[]> redo = new Stack<CellChange[]>();
        private Dictionary<int, CellChange> stroke;

        public ConfigLayerEditService(ConfigLayer layer)
        {
            if (layer == null || layer.X <= 0 || layer.Y <= 0 ||
                layer.Data == null || (long)layer.X * layer.Y != layer.Data.Length)
                throw new ArgumentException("Config layer dimensions do not match its data.", nameof(layer));
            this.layer = layer;
            saved = (byte[])layer.Data.Clone();
        }

        public bool IsDirty => !saved.SequenceEqual(layer.Data);
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public ConfigLayer Layer => layer;

        public void MarkSaved() { EndStroke(); saved = (byte[])layer.Data.Clone(); }

        public void BeginStroke()
        {
            if (stroke != null) throw new InvalidOperationException("A stroke is already active.");
            stroke = new Dictionary<int, CellChange>();
        }

        public void SetCell(int x, int y, byte value)
        {
            int index = Index(x, y);
            bool single = stroke == null;
            if (single) BeginStroke();
            if (layer.Data[index] != value)
            {
                CellChange change;
                if (!stroke.TryGetValue(index, out change))
                    stroke[index] = change = new CellChange(index, layer.Data[index], value);
                change.After = value;
                layer.Data[index] = value;
            }
            if (single) EndStroke();
        }

        /// <summary>Inclusive cell rectangle. Validate both corners before changing data.</summary>
        public void FillRectangle(int x1, int y1, int x2, int y2, byte value)
        {
            Index(x1, y1); Index(x2, y2);
            if (x1 > x2 || y1 > y2) throw new ArgumentException("Rectangle corners are reversed.");
            bool single = stroke == null;
            if (single) BeginStroke();
            for (int y = y1; y <= y2; y++)
                for (int x = x1; x <= x2; x++) SetCell(x, y, value);
            if (single) EndStroke();
        }

        public void DrawLine(int x1, int y1, int x2, int y2, byte value)
        {
            Index(x1, y1); Index(x2, y2);
            bool single = stroke == null;
            if (single) BeginStroke();
            int dx = Math.Abs(x2 - x1), dy = -Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1, error = dx + dy;
            while (true)
            {
                SetCell(x1, y1, value);
                if (x1 == x2 && y1 == y2) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x1 += sx; }
                if (twice <= dx) { error += dx; y1 += sy; }
            }
            if (single) EndStroke();
        }

        /// <summary>Brush diameter is in stored cells. Edges are clipped, never wrapped.</summary>
        public void PaintBrush(int x, int y, int size, byte value, bool circle)
        {
            Index(x, y);
            ConfigBrush.ValidateSize(size);
            bool single = stroke == null;
            if (single) BeginStroke();
            ConfigBrush.Visit(x, y, size, circle, layer.X, layer.Y,
                (px, py) => SetCell(px, py, value));
            if (single) EndStroke();
        }

        public void DrawBrushLine(int x1, int y1, int x2, int y2, int size, byte value, bool circle)
        {
            Index(x1, y1); Index(x2, y2); ConfigBrush.ValidateSize(size);
            bool single = stroke == null;
            if (single) BeginStroke();
            int dx = Math.Abs(x2 - x1), dy = -Math.Abs(y2 - y1);
            int sx = x1 < x2 ? 1 : -1, sy = y1 < y2 ? 1 : -1, error = dx + dy;
            while (true)
            {
                PaintBrush(x1, y1, size, value, circle);
                if (x1 == x2 && y1 == y2) break;
                int twice = 2 * error;
                if (twice >= dy) { error += dy; x1 += sx; }
                if (twice <= dx) { error += dx; y1 += sy; }
            }
            if (single) EndStroke();
        }

        public void EndStroke()
        {
            if (stroke == null) return;
            var changes = stroke.Values.Where(c => c.Before != c.After).ToArray();
            stroke = null;
            if (changes.Length == 0) return;
            undo.Push(changes); redo.Clear();
        }

        public void Undo()
        {
            EndStroke();
            if (!CanUndo) return;
            var changes = undo.Pop();
            foreach (var change in changes) layer.Data[change.Index] = change.Before;
            redo.Push(changes);
        }

        public void Redo()
        {
            EndStroke();
            if (!CanRedo) return;
            var changes = redo.Pop();
            foreach (var change in changes) layer.Data[change.Index] = change.After;
            undo.Push(changes);
        }

        private int Index(int x, int y)
        {
            if (x < 0 || y < 0 || x >= layer.X || y >= layer.Y)
                throw new ArgumentOutOfRangeException("Cell coordinates are outside the config layer.");
            return y * layer.X + x;
        }

        private sealed class CellChange
        {
            public CellChange(int index, byte before, byte after) { Index = index; Before = before; After = after; }
            public int Index { get; }
            public byte Before { get; }
            public byte After { get; set; }
        }
    }
}
