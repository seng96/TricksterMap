using System.Drawing;
using TricksterMap.Data;

namespace TricksterMap
{
    internal sealed class ViewportObjectMarkerStyle
    {
        public string Caption { get; }
        public string Glyph { get; }
        public Color Color { get; }
        private ViewportObjectMarkerStyle(string caption, string glyph, int red, int green, int blue)
        { Caption = caption; Glyph = glyph; Color = Color.FromArgb(red, green, blue); }

        public static readonly ViewportObjectMarkerStyle Npc = new ViewportObjectMarkerStyle("NPC", "N", 85, 242, 207);
        public static readonly ViewportObjectMarkerStyle MonsterSpawn = new ViewportObjectMarkerStyle("怪物孵化", "M", 255, 118, 147);
        public static readonly ViewportObjectMarkerStyle Portal = new ViewportObjectMarkerStyle("傳送", "T", 192, 156, 255);
        public static readonly ViewportObjectMarkerStyle Respawn = new ViewportObjectMarkerStyle("角色重生", "R", 255, 220, 95);
        public static readonly ViewportObjectMarkerStyle Other = new ViewportObjectMarkerStyle("其他", "·", 215, 229, 245);
        public static readonly ViewportObjectMarkerStyle Movement = new ViewportObjectMarkerStyle("怪物移動", "W", 255, 178, 97);
        public static readonly ViewportObjectMarkerStyle Gather = new ViewportObjectMarkerStyle("怪物集合", "G", 130, 188, 255);
        public static readonly ViewportObjectMarkerStyle Battle = new ViewportObjectMarkerStyle("公會戰出生", "B", 191, 241, 114);
        public static readonly ViewportObjectMarkerStyle[] Points = { Npc, MonsterSpawn, Portal, Respawn, Other };
        public static readonly ViewportObjectMarkerStyle[] Ranges = { Portal, Npc, MonsterSpawn, Movement, Gather, Battle, Other };

        public static ViewportObjectMarkerStyle ForPoint(int type)
        {
            switch (type)
            {
                case 0x01: return Portal;
                case 0x02: return Respawn;
                case 0x03: return MonsterSpawn;
                case 0x04: case 0x05: case 0x09: case 0x0a: return Npc;
                default: return Other;
            }
        }

        public static ViewportObjectMarkerStyle ForRange(int type)
        {
            switch (type)
            {
                case 0x00: return Portal;
                case 0x02: return Movement;
                case 0x05: return Gather;
                case 0x06: return Npc;
                case 0x09: return MonsterSpawn;
                case 0x0a: return Battle;
                default: return Other;
            }
        }

        public static ViewportObjectMarkerStyle For(object item)
            => item is PointObject point ? ForPoint(point.Type) : ForRange(((RangeObject)item).Type);
    }
}
