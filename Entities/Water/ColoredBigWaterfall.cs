using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Mods.PandorasBox.Color;

namespace Celeste.Mods.PandorasBox.Entities.Water
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.ColoredBigWaterfall")]
    [CustomEntity("pandorasBox/coloredBigWaterfall")]
    public sealed class ColoredBigWaterfall : OriginalBigWaterfallState
    {
        private readonly PackedColor baseColor;

        public ColoredBigWaterfall(EntityData data, Vector2 offset, ISourceEngineContext engine) : base(data, offset,
            engine)
            => baseColor = ColorReader.GetColor(data.Attr("color", "#87CEFA"));

        public override void Awake(object scene)
        {
            // Upstream changes only the fields here; it deliberately does not call base Awake.
            surfaceColor = baseColor * 0.8f;
            fillColor = baseColor * 0.3f;
        }
    }
}
