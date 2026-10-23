using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Gameplay.Environment.Water;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Everest.Tracking.Attributes;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Game.Gameplay.Environment.Water;
using Celeste.Graphics.Geometry.TwoD.Camera;
using Celeste.Mods.PandorasBox.Color;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation.Gameplay.Environment.Water;
using Rectangle = Celeste.Core.Geometry.IntRect;
using WaterInteraction = Celeste.Simulation.Gameplay.Environment.Water.WaterInteractionState;

namespace Celeste.Mods.PandorasBox.Entities.Water
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.ColoredWater")]
    [CustomEntity("pandorasBox/coloredWater")]
    [EverestTrackedAs(typeof(OriginalWaterState))]
    public sealed class ColoredWater : EverestWaterState
    {
        private PackedColor baseColor;

        private PackedColor fillColor;

        private bool visibleOnCamera;

        private bool hasTopSurface;

        private bool hasBottomSurface;

        private bool hasLeftSurface;

        private bool hasRightSurface;

        private Vector2 previousPosition;

        private bool trackedPosition = false;

        private bool hasUpdatedFill = false;

        public Surface LeftSurface;

        public Surface RightSurface;

        private bool hasTopRays;

        private bool hasBottomRays;

        private bool hasLeftRays;

        private bool hasRightRays;

        public bool CanJumpOnSurface;

        private const int horizontalVisiblityBuffer = 40;

        private const int verticalVisiblityBuffer = 40;

        private float cameraTop, cameraBottom, cameraLeft, cameraRight;
        private readonly WaterAppearance appearance;
        protected override PackedColor FillColor => fillColor;
        internal CameraState Camera => ((OriginalLevelSceneRoot)Scene).Camera;

        public ColoredWater(EntityData data, Vector2 offset, ISourceEngineContext engine)
            : base(engine, data.Position + offset, data.Bool("hasTop", true), data.Bool("hasBottom"), data.Width,
                data.Height)
        {
            baseColor = ColorReader.GetColor(data.Attr("color", "#87CEFA"));
            fillColor = baseColor * 0.3f;
            hasLeftSurface = data.Bool("hasLeft");
            hasRightSurface = data.Bool("hasRight");
            hasTopRays = data.Bool("hasTopRays", true);
            hasBottomRays = data.Bool("hasBottomRays", true);
            hasLeftRays = data.Bool("hasLeftRays", true);
            hasRightRays = data.Bool("hasRightRays", true);
            CanJumpOnSurface = data.Bool("canJumpOnSurface", true);
            appearance =
                new WaterAppearance(baseColor, ((IOriginalWaterStaticsEnvironment)engine).WaterStatics.RayAngle);
        }

        private void InitializeSurfaces()
        {
            hasTopSurface = Surfaces.Contains(TopSurface);
            hasBottomSurface = Surfaces.Contains(BottomSurface);
            Surfaces.Clear();
            if (hasTopSurface)
            {
                TopSurface = new ColoredWaterSurface(this, engine, Position + new Vector2(base.Width / 2f, 8f),
                    new Vector2(0f, -1f), base.Width, base.Height, appearance);
                Surfaces.Add(TopSurface);
                if (!hasTopRays)
                {
                    TopSurface.Rays.Clear();
                }
            }

            if (hasBottomSurface)
            {
                BottomSurface = new ColoredWaterSurface(this, engine,
                    Position + new Vector2(base.Width / 2f, base.Height - 8f), new Vector2(0f, 1f), base.Width,
                    base.Height,
                    appearance);
                Surfaces.Add(BottomSurface);
                if (!hasBottomRays)
                {
                    BottomSurface.Rays.Clear();
                }
            }

            if (hasLeftSurface)
            {
                LeftSurface = new ColoredWaterSurface(this, engine, Position + new Vector2(8f, base.Height / 2f),
                    new Vector2(-1f, 0f), base.Height, base.Width, appearance);
                Surfaces.Add(LeftSurface);
                if (!hasLeftRays)
                {
                    LeftSurface.Rays.Clear();
                }
            }

            if (hasRightSurface)
            {
                RightSurface = new ColoredWaterSurface(this, engine,
                    Position + new Vector2(base.Width - 8f, base.Height / 2f), new Vector2(1f, 0f), base.Height,
                    base.Width,
                    appearance);
                Surfaces.Add(RightSurface);
                if (!hasRightRays)
                {
                    RightSurface.Rays.Clear();
                }
            }

            if (!hasUpdatedFill && (hasLeftSurface || hasRightSurface))
            {
                Rectangle rectangle = fill;
                int num = rectangle.X;
                int num2 = rectangle.Width;
                if (hasLeftSurface)
                {
                    num += 8;
                    num2 -= 8;
                }

                if (hasRightSurface)
                {
                    num2 -= 8;
                }

                Rectangle rectangle2 = new Rectangle(num, rectangle.Y, num2, rectangle.Height);
                fill = rectangle2;
                hasUpdatedFill = true;
            }
        }

        private void UpdateVisiblity()
        {
            bool flag = base.X < cameraRight + (float)horizontalVisiblityBuffer &&
                        base.X + base.Width > cameraLeft - (float)horizontalVisiblityBuffer;
            bool flag2 = base.Y < cameraBottom + (float)verticalVisiblityBuffer &&
                         base.Y + base.Height > cameraTop - (float)verticalVisiblityBuffer;
            visibleOnCamera = flag && flag2;
        }

        public override void Render(ISourceRenderContext context)
        {
            if (visibleOnCamera) base.Render(context);
        }

        private void RippleLeftRightSurfaces()
        {
            if (!visibleOnCamera)
            {
                return;
            }

            foreach (WaterInteraction component in ((ISourceScene)Scene).SourceScene.Components.GetTrackedComponents(
                         "Celeste.WaterInteraction"))
            {
                Rectangle bounds = ((ISourceWaterCollisionInteraction)component).Bounds;
                bool flag = contains.Contains(component);
                bool flag2 = SourceEntityCollision.CheckRect(this, bounds);
                if (flag != flag2)
                {
                    if (LeftSurface != null && (float)bounds.Center.X <= base.Center.X)
                    {
                        LeftSurface.DoRipple(new Vector2(bounds.Center.X, bounds.Center.Y), 1f);
                    }

                    if (RightSurface != null && (float)bounds.Center.X > base.Center.X)
                    {
                        RightSurface.DoRipple(new Vector2(bounds.Center.X, bounds.Center.Y), 1f);
                    }
                }
            }
        }

        private void UpdateSurfacePositionsAndSize()
        {
            if (!trackedPosition)
            {
                previousPosition = Position;
                trackedPosition = true;
            }

            if (previousPosition != Position)
            {
                Vector2 vector = new Vector2((float)Math.Floor(Position.X), (float)Math.Floor(Position.Y));
                if (hasTopSurface)
                {
                    TopSurface.Position = vector + new Vector2(base.Width / 2f, 8f);
                }

                if (hasBottomSurface)
                {
                    BottomSurface.Position = vector + new Vector2(base.Width / 2f, base.Height - 8f);
                }

                if (hasLeftSurface)
                {
                    LeftSurface.Position = vector + new Vector2(8f, base.Height / 2f);
                }

                if (hasRightSurface)
                {
                    RightSurface.Position = vector + new Vector2(base.Width - 8f, base.Height / 2f);
                }

                previousPosition = Position;
            }
        }

        private void UpdateCamera(CameraState camera)
        {
            cameraTop = camera.Top;
            cameraBottom = camera.Bottom;
            cameraLeft = camera.Left;
            cameraRight = camera.Right;
        }

        public override void Update()
        {
            OriginalLevelSceneRoot level = (OriginalLevelSceneRoot)Scene;
            UpdateCamera(level.Camera);
            UpdateVisiblity();
            RippleLeftRightSurfaces();
            base.Update();
            UpdateSurfacePositionsAndSize();
        }

        public override void Added(object scene)
        {
            InitializeSurfaces();
            base.Added(scene);
        }
    }
}
