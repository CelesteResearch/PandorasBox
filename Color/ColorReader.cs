using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Core.Display;
using Celeste.Graphics.Display.Colors;

namespace Celeste.Mods.PandorasBox.Color
{
    internal static class ColorReader
    {
        private static readonly Dictionary<string, PackedColor> names = CreateNames();

        private static Dictionary<string, PackedColor> CreateNames()
        {
            var values = new Dictionary<string, PackedColor>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in typeof(PackedColor).GetProperties(BindingFlags.Public | BindingFlags.Static))
                if (p.PropertyType == typeof(PackedColor))
                    values.Add(p.Name, (PackedColor)p.GetValue(null));
            return values;
        }

        public static PackedColor GetColor(string color)
        {
            if (names.TryGetValue(color, out var known)) return known;
            try
            {
                return SourceHexColor.HexToColor(color.Replace("#", string.Empty));
            }
            catch
            {
                return PackedColor.Transparent;
            }
        }
    }
}
