using System.Numerics;
using Celeste.Core.Display;
using Celeste.Game.Gameplay.Environment.Water;

namespace Celeste.Mods.PandorasBox.Entities.Water
{
    internal sealed class WaterAppearance : IOriginalWaterStatics
    {
        public PackedColor FillColor { get; }
        public PackedColor SurfaceColor { get; }
        public PackedColor RayTopColor { get; }
        public Vector2 RayAngle { get; }

        public WaterAppearance(PackedColor color, Vector2 rayAngle)
        {
            FillColor = color * 0.3f;
            SurfaceColor = color * 0.8f;
            RayTopColor = color * 0.6f;
            RayAngle = rayAngle;
        }
    }
}
