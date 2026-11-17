using System;
using System.Collections.Generic;
using System.Linq;

namespace TricksterMap.Scene
{
    /// <summary>Edits object records and list structure without normalizing opaque fields.</summary>
    public sealed class SceneObjectEditService
    {
        private readonly SceneLayer layer;
        private byte[][] saved;
        private readonly Stack<Edit> undo = new Stack<Edit>(), redo = new Stack<Edit>();
        public SceneObjectEditService(SceneLayer layer)
        {
            this.layer = layer ?? throw new ArgumentNullException(nameof(layer));
            saved = Snapshot();
        }
        public bool CanUndo => undo.Count != 0;
        public bool CanRedo => redo.Count != 0;
        public bool IsDirty
        {
            get
            {
                if (layer.Objects.Count != saved.Length) return true;
                for (int i = 0; i < saved.Length; i++) if (!saved[i].SequenceEqual(layer.Objects[i].RawData)) return true;
                return false;
            }
        }
        public void MarkSaved() { saved = Snapshot(); }
        public void SetPosition(int index, int x, int y, int z)
        {
            SceneObject item = Item(index); byte[] before = (byte[])item.RawData.Clone();
            item.X = x; item.Y = y; item.Z = z; Record(index, before, item.RawData);
        }
        public void SetFlags(int index, int flags)
        {
            SceneObject item = Item(index); byte[] before = (byte[])item.RawData.Clone();
            item.Flags = flags; Record(index, before, item.RawData);
        }
        public int Duplicate(int index)
        {
            var item = Item(index); byte[][] before = Snapshot();
            layer.Objects.Insert(index + 1, new SceneObject(item.RawData));
            undo.Push(new Edit { BeforeList = before, AfterList = Snapshot() }); redo.Clear();
            return index + 1;
        }
        public int Add(SceneObject item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (layer.Objects.Any(existing => existing.RawData.Length != item.RawData.Length))
                throw new ArgumentException("Object record layout differs from existing layer objects.");
            byte[][] before = Snapshot(); int index = layer.Objects.Count;
            layer.Objects.Add(new SceneObject(item.RawData));
            undo.Push(new Edit { BeforeList = before, AfterList = Snapshot() }); redo.Clear();
            return index;
        }
        public void Delete(int index)
        {
            Item(index); byte[][] before = Snapshot(); layer.Objects.RemoveAt(index);
            undo.Push(new Edit { BeforeList = before, AfterList = Snapshot() }); redo.Clear();
        }
        public void Undo()
        {
            if (!CanUndo) return; var edit = undo.Peek();
            if (edit.BeforeList != null) ApplyList(edit.BeforeList); else Apply(edit.Index, edit.Before);
            undo.Pop(); redo.Push(edit);
        }
        public void Redo()
        {
            if (!CanRedo) return; var edit = redo.Peek();
            if (edit.AfterList != null) ApplyList(edit.AfterList); else Apply(edit.Index, edit.After);
            redo.Pop(); undo.Push(edit);
        }
        private void Record(int index, byte[] before, byte[] after)
        {
            if (before.SequenceEqual(after)) return;
            undo.Push(new Edit { Index = index, Before = before, After = (byte[])after.Clone() }); redo.Clear();
        }
        private void Apply(int index, byte[] raw)
        {
            SceneObject item = Item(index);
            if (item.RawData.Length != raw.Length) throw new InvalidOperationException("Scene object layout changed outside this history.");
            Buffer.BlockCopy(raw, 0, item.RawData, 0, raw.Length);
        }
        private SceneObject Item(int index)
        {
            if (index < 0 || index >= layer.Objects.Count) throw new ArgumentOutOfRangeException(nameof(index));
            return layer.Objects[index];
        }
        private void ApplyList(byte[][] records)
        {
            var restored = records.Select(raw => new SceneObject(raw)).ToArray();
            layer.Objects.Clear(); layer.Objects.AddRange(restored);
        }
        private byte[][] Snapshot() => layer.Objects.Select(item => (byte[])item.RawData.Clone()).ToArray();
        private sealed class Edit { public int Index; public byte[] Before, After; public byte[][] BeforeList, AfterList; }
    }
}
