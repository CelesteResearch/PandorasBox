// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Audio;
using Celeste.Core.Geometry;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Rendering;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Scene.Components.Shaking;
using Celeste.Simulation.Gameplay.Platforms.Foundation;

namespace Celeste.Mods.PandorasBox.Entities.Doors.Gates
{
    [CustomEntity("pandorasBox/gate")]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.Gate")]
    public class Gate : SourceSolidEntityState
    {
        private readonly ISourceEngineContext engine;
        private Sprite sprite;
        private ShakerComponentState shaker;
        private static int openHeight = 8;
        private static int closedHeight = 48;
        private static float waitDelay = .2f;
        private bool open;
        private bool lockState;
        private float drawingHeight;
        private float drawingHeightSpeed;
        private float waitFor;
        private string flag;
        private string textureFolder;
        private bool inverted;
        private int id;

        public Gate(EntityData data, Vector2 offset, ISourceSolidWorldFactory factory)
            : this(data, offset, factory.Create(data.Position + offset, true))
        {
        }

        public static Gate Load(OriginalLevelSceneRoot level,
            Celeste.Game.Content.Maps.Levels.OriginalLevelRoomData room, Vector2 offset, EntityData data) =>
            new(data, offset, level.SolidWorldFactory);

        private Gate(EntityData data, Vector2 offset, ISourceSolidWorld world)
            : base(data.Position + offset, 8f, 48f, true, world)
        {
            engine = world.EngineContext;
            inverted = bool.Parse(data.Attr("inverted", "false"));
            textureFolder = data.Attr("texture", "objects/pandorasBox/gate/");
            flag = data.Attr("flag", "");
            id = data.ID;
            lockState = false;
            Add(sprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                textureFolder + "gate"));
            sprite.AddLoop("gate", "", .06f);
            sprite.Play("gate");
            sprite.OnLastFrame = OnLastFrame;
            sprite.Rate = 0f;
            sprite.Position.X = -Collider.Width / 2f;
            Add(shaker = new ShakerComponentState(engine, false));
            Depth = -9000;
            drawingHeight = sprite.Height;
        }

        private void OnLastFrame(string animation) => sprite.Stop();

        private bool IsActive()
        {
            var level = Scene as OriginalLevelSceneRoot;
            return inverted ? !level.Session.GetFlag(flag) : level.Session.GetFlag(flag);
        }

        public override void Update()
        {
            if (!lockState)
            {
                if (IsActive())
                {
                    if (!open) Open();
                }
                else if (open) Close();
            }

            waitFor = Math.Max(0f, waitFor - engine.DeltaTime);
            if (waitFor <= 0f)
            {
                float height = open ? openHeight : closedHeight;
                SetHeight((int)height);
                float target = open ? Math.Max(openHeight, Height) : Math.Min(closedHeight, Height);
                if (drawingHeight != target)
                    drawingHeight = ScalarMath.Approach(drawingHeight, target, drawingHeightSpeed * engine.DeltaTime);
            }

            base.Update();
        }

        private void SetHeight(int height)
        {
            if ((float)height == Collider.Height) return;
            ClosingSolidResize.SetHeight(this, height);
        }

        public void Open()
        {
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                "event:/game/05_mirror_temple/gate_main_open", Position);
            shaker.ShakeFor(.2f, false);
            sprite.Play("gate", true);
            sprite.Rate = 1f;
            shaker.ShakeFor(.2f, false);
            drawingHeightSpeed = 200f;
            waitFor = waitDelay;
            open = true;
        }

        public void Close()
        {
            ((ISourceSpatialAudioEnvironment)engine).SpatialAudio.Play(
                "event:/game/05_mirror_temple/gate_main_close", Position);
            shaker.ShakeFor(.2f, false);
            sprite.Play("gate", true);
            sprite.Rate = -1f;
            sprite.SetAnimationFrame(sprite.CurrentAnimationTotalFrames - 1);
            shaker.ShakeFor(.2f, false);
            drawingHeightSpeed = 300f;
            waitFor = waitDelay;
            open = false;
        }

        public override void Render(ISourceRenderContext context)
        {
            var offset = new Vector2(Math.Sign(shaker.Value.X), 0f);
            sprite.DrawSubrect(context, offset,
                new IntRect(0, (int)(sprite.Height - drawingHeight), (int)sprite.Width, (int)drawingHeight + 1),
                ((ISourceExtremaEnvironment)engine).ExtremaArithmetic);
        }
    }
}
