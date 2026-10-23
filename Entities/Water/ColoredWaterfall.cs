using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Mods.PandorasBox.Color;

namespace Celeste.Mods.PandorasBox.Entities.Water
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.ColoredWaterfall")]
    [CustomEntity("pandorasBox/coloredWaterfall")]
    public sealed class ColoredWaterfall : OriginalWaterFallState
    {
        private readonly ISourceEngineContext engine;
        private readonly PackedColor baseColor;
        private bool visibleOnCamera;
        protected override PackedColor FillColor => baseColor * 0.3f;
        protected override PackedColor SurfaceColor => baseColor * 0.8f;

        protected override bool ShouldRipple => water != null && water.Active && water.TopSurface != null;

        // Original helper tests water before scanning its height. It is null at that point.
        protected override bool HasDeepWaterEntry => false;

        public ColoredWaterfall(EntityData data, Vector2 offset, ISourceEngineContext engine) : base(data, offset,
            engine)
        {
            this.engine = engine;
            baseColor = ColorReader.GetColor(data.Attr("color", "#87CEFA"));
        }

        public override void Update()
        {
            var level = (OriginalLevelSceneRoot)Scene;
            if (level.OnInterval(0.05f))
            {
                var camera = level.Camera;
                visibleOnCamera = X < camera.Right + 32 && X > camera.Left - 32 && Y < camera.Bottom + 32 &&
                                  Y + height > camera.Top - 32;
            }

            base.Update();
        }

        protected override void EmitSplash(OriginalLevelSceneRoot level, Vector2 position)
        {
            if (visibleOnCamera)
                level.ParticlesFG.Emit(((IOriginalGameplayProcess)engine).ParticleTypes.Water.P_Splash,
                    1, new Vector2(X + 4f, (float)((double)(Y + height) + 2.0)), new Vector2(8f, 2f), baseColor,
                    (float)Math.Atan2(-1f, 0f));
        }

        public override void Render(ISourceRenderContext context)
        {
            if (visibleOnCamera) base.Render(context);
        }
    }
}
