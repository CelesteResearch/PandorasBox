// Copyright (c) Cruor. Licensed under the MIT License.

using Celeste.Core.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Scheduling;
using Celeste.Core.Collections;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Execution.Levels;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Graphics.Content.Tiles.Autotiling;
using Celeste.Mods.PandorasBox.Color;
using Celeste.Scene.Entities;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation.Presentation.PostProcessing.Bloom;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Core.Mathematics;
using Celeste.Mods.PandorasBox.Modules;
using Celeste.Mods.PandorasBox.Runtime.Terrain.Glitches;

namespace Celeste.Mods.PandorasBox.Entities.Lighting
{
    [CustomEntity("pandorasBox/lamp")]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.Lamp")]
    public class Lamp : SourceActorEntityState
    {
        private readonly ISourceEngineContext engine;
        private Sprite startupSprite;
        private Sprite idleSprite;
        private Sprite baseSprite;

        bool inStartupAnimation;
        bool inIdleAnimation;

        private BloomPointState bloom;
        private VertexLightState light;

        private int id;
        private string flag;
        private PackedColor baseColor;
        private PackedColor lightColor;
        private string lightMode = null;
        private int lightStartRadius;
        private int lightEndRadius;
        private bool inverted;

        private float startupLerpAcc = 0f;
        private float startupLerpTotal = 0f;

        private static float bloomStartRadius = 3f;
        private static float bloomEndRadius = 8f;

        private static float lightStartAlpha = 0.15f;
        private static float lightEndAlpha = 1f;

        private static float startupAnimationDelay = 0.1f;

        public Lamp(ISourceEngineContext engine, EntityData data, Vector2 offset)
            : base(new PixelActor(null, data.Position + offset), engine,
                ((ISourcePlatformTargetMotionEnvironment)engine).TargetMotionPolicy)
        {
            this.engine = engine;
            id = data.ID;
            flag = data.Attr("flag", "");
            baseColor = ColorReader.GetColor(data.Attr("baseColor", "White"));
            lightColor = ColorReader.GetColor(data.Attr("lightColor", "White"));
            lightStartRadius = data.Int("lightStartRadius", 48);
            lightEndRadius = data.Int("lightEndRadius", 64);
            inverted = data.Bool("inverted", false);

            Add((startupSprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/pandorasBox/lamp/start")));
            startupSprite.AddLoop("start", "", startupAnimationDelay);
            startupSprite.JustifyOrigin(0.5f, 0.5f);
            startupSprite.Play("start");
            startupSprite.OnLastFrame = onLastFrame;
            startupLerpTotal = startupAnimationDelay * startupSprite.CurrentAnimationTotalFrames;
            startupSprite.Stop();

            Add((idleSprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/pandorasBox/lamp/idle")));
            idleSprite.AddLoop("idle", "", 0.125f);
            idleSprite.JustifyOrigin(0.5f, 0.5f);
            idleSprite.Play("idle");
            idleSprite.Visible = false;

            Add((baseSprite = new Sprite(engine, ((ISourceGameGraphicsEnvironment)engine).GameGraphics.GameAtlas,
                "objects/pandorasBox/lamp/base")));
            baseSprite.AddLoop("base", "", 0.1f);
            baseSprite.JustifyOrigin(0.5f, 0.5f);
            baseSprite.Play("base");
            baseSprite.Color = baseColor;

            inStartupAnimation = false;

            Add((bloom = new BloomPointState(0.5f, 8f)));
            Add((light = new VertexLightState(engine, lightColor, 1f, lightStartRadius, lightEndRadius)));

            Depth = 5;
        }

        public override void Awake(object scene)
        {
            if (isActive())
            {
                bloom.Radius = bloomEndRadius;
                light.Alpha = lightEndAlpha;
            }
            else
            {
                bloom.Radius = bloomStartRadius;
                light.Alpha = lightStartAlpha;
            }
        }

        private void onLastFrame(string s)
        {
            if (isActive())
            {
                idleSprite.Play("idle", true);
                idleSprite.Rate = 1;
                idleSprite.Visible = true;
                startupSprite.Visible = false;

                inIdleAnimation = true;
            }
            else
            {
                inIdleAnimation = false;
            }

            inStartupAnimation = false;
            startupSprite.Stop();
        }

        private bool isActive()
        {
            OriginalLevelSceneRoot level = Scene as OriginalLevelSceneRoot;

            if (inverted)
            {
                return !level.Session.GetFlag(flag);
            }
            else
            {
                return level.Session.GetFlag(flag);
            }
        }

        public override void Update()
        {
            if (isActive())
            {
                if (!inStartupAnimation && !inIdleAnimation)
                {
                    startupSprite.Play("start", true);
                    startupSprite.Rate = 1;
                    startupSprite.Visible = true;
                    idleSprite.Visible = false;

                    inStartupAnimation = true;
                    inIdleAnimation = false;
                    startupLerpAcc = 0f;
                }
            }
            else
            {
                if (!inStartupAnimation && inIdleAnimation)
                {
                    startupSprite.Play("start", true);
                    startupSprite.Rate = -1;
                    startupSprite.SetAnimationFrame(startupSprite.CurrentAnimationTotalFrames - 1);
                    startupSprite.Visible = true;
                    idleSprite.Visible = false;

                    inStartupAnimation = true;
                    inIdleAnimation = false;
                    startupLerpAcc = startupLerpTotal;
                }
            }

            float lerpPercent = isActive() ? 1.0f : 0.0f;

            if (inStartupAnimation && lightMode == "Smooth")
            {
                startupLerpAcc = ScalarMath.Clamp(startupLerpAcc + engine.DeltaTime * startupSprite.Rate, 0f,
                    startupLerpTotal);
                lerpPercent = startupLerpAcc / startupLerpTotal;
            }

            bloom.Radius =
                ((ISourceScalarEnvironment)engine).ScalarArithmetic.Lerp(bloomStartRadius, bloomEndRadius, lerpPercent);
            light.Alpha =
                ((ISourceScalarEnvironment)engine).ScalarArithmetic.Lerp(lightStartAlpha, lightEndAlpha, lerpPercent);

            base.Update();
        }
    }
}
