using System;
using System.Collections.Generic;
using System.Linq;

namespace TricksterMap.Scene
{
    /// <summary>Edits tile words while retaining unknown flag bits and coherent undo strokes.</summary>
    public sealed class SceneTileEditService
    {
        private readonly SceneDocument scene;
        private readonly SceneLayer layer;
        private byte[] saved;
        private Dictionary<int, Change> stroke;
        private readonly Stack<Change[]> undo = new Stack<Change[]>(), redo = new Stack<Change[]>();
        public SceneTileEditService(SceneDocument scene, SceneLayer layer)
        {
            if (scene == null || layer == null || layer.Kind != 2 || !scene.Layers.Contains(layer))
                throw new ArgumentException("Tile edits require a tile layer in the scene.");
            if ((long)layer.Width * layer.Height * 4 != layer.Payload.Length)
                throw new ArgumentException("Tile layer dimensions do not match its payload.");
            this.scene = scene; this.layer = layer; saved = (byte[])layer.Payload.Clone();
        }
        public SceneLayer Layer => layer;
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public bool IsDirty => !saved.SequenceEqual(layer.Payload);
        public void MarkSaved() { EndStroke(); saved = (byte[])layer.Payload.Clone(); }
        public void BeginStroke()
        {
            if (stroke != null) throw new InvalidOperationException("A tile stroke is already active.");
            stroke = new Dictionary<int, Change>();
        }
        public void SetCell(int x, int y, uint word)
        {
            uint previous = layer.GetTile(x, y); ValidateWord(word);
            bool single = stroke == null;
            if (single) BeginStroke();
            if (previous != word)
            {
                int index = y * layer.Width + x;
                Change change;
                if (!stroke.TryGetValue(index, out change)) stroke[index] = change = new Change(index, previous, word);
                change.After = word; layer.SetTile(x, y, word);
            }
            if (single) EndStroke();
        }
        public void FillRectangle(int x1, int y1, int x2, int y2, uint word)
        {
            layer.GetTile(x1, y1); layer.GetTile(x2, y2); ValidateWord(word);
            if (x1 > x2 || y1 > y2) throw new ArgumentException("Tile rectangle corners are reversed.");
            bool single = stroke == null; if (single) BeginStroke();
            for (int y = y1; y <= y2; y++)
                for (int x = x1; x <= x2; x++) SetCell(x, y, word);
            if (single) EndStroke();
        }
        public void EndStroke()
        {
            if (stroke == null) return;
            var changes = stroke.Values.Where(change => change.Before != change.After).ToArray(); stroke = null;
            if (changes.Length == 0) return;
            undo.Push(changes); redo.Clear();
        }
        public void Undo()
        {
            EndStroke(); if (!CanUndo) return;
            var changes = undo.Pop(); foreach (var change in changes) Apply(change.Index, change.Before); redo.Push(changes);
        }
        public void Redo()
        {
            EndStroke(); if (!CanRedo) return;
            var changes = redo.Pop(); foreach (var change in changes) Apply(change.Index, change.After); undo.Push(changes);
        }
        private void Apply(int index, uint word) => layer.SetTile(index % layer.Width, index / layer.Width, word);
        private void ValidateWord(uint word)
        {
            uint tile = word & 0xffff, group = word >> 16 & 0xff;
            if (tile == 0xffff) return;
            if (group >= scene.TileGroups.Count || tile >= (long)scene.TileGroups[(int)group].Width * scene.TileGroups[(int)group].Height)
                throw new ArgumentOutOfRangeException(nameof(word), "Tile index or group does not exist in this scene.");
        }
        private sealed class Change
        {
            public Change(int index, uint before, uint after) { Index = index; Before = before; After = after; }
            public int Index { get; }
            public uint Before { get; }
            public uint After { get; set; }
        }
    }
}
