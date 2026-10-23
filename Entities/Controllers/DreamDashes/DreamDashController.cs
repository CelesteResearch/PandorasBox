// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using System;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Display;
using Celeste.Core.Randomness;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Game.Content.Maps.Entities;
using Celeste.Game.Content.Maps.Levels;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.PandorasBox.Color;
using Celeste.Mods.PandorasBox.Runtime.DreamDashes;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Presentation.Lighting;
using Celeste.Simulation;

namespace Celeste.Mods.PandorasBox.Entities.Controllers.DreamDashes
{
    [CustomEntity("pandorasBox/dreamDashController")]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.DreamDashController")]
    [SceneTracked(false)]
    public sealed partial class DreamDashController : SceneEntityState
    {
        public bool AllowSameDirectionDash, AllowDreamDashRedirection, OverrideDreamDashSpeed, OverrideColors;
        public bool NeverSlowDown, UseEntrySpeedAngle, BounceOnCollision, CollideStickToWalls;
        private readonly float sameDirectionSpeedMultiplier, dreamDashSpeed;
        private readonly PackedColor activeBackColor, disabledBackColor, activeLineColor, disabledLineColor;
        private readonly PackedColor[][] particleLayerColors;
        private bool addedColors, addedParticleColors;
        private readonly DreamDashControllerRuntime runtime;

        public static DreamDashController Load(OriginalLevelSceneRoot level, OriginalLevelRoomData room, Vector2 offset,
            OriginalLevelEntityData data) => new(data, offset, level.EngineContext);

        public DreamDashController(EntityData data, Vector2 offset, ISourceEngineContext engine) : base(
            ((ISourceEntitySourceDataEnvironment)engine).EntitySourceDataRegistration, data.Position + offset)
        {
            runtime = DreamDashControllerRuntime.For(engine);
            AllowSameDirectionDash = data.Bool("allowSameDirectionDash", false);
            AllowDreamDashRedirection = data.Bool("allowDreamDashRedirect", true);
            OverrideDreamDashSpeed = data.Bool("overrideDreamDashSpeed", false);
            OverrideColors = data.Bool("overrideColors", false);
            NeverSlowDown = data.Bool("neverSlowDown", false);
            UseEntrySpeedAngle = data.Bool("useEntrySpeedAngle", false);
            BounceOnCollision = data.Bool("bounceOnCollision", false);
            CollideStickToWalls = data.Bool("stickOnCollision", false);
            sameDirectionSpeedMultiplier = data.Float("sameDirectionSpeedMultiplier", 1f);
            dreamDashSpeed = data.Float("dreamDashSpeed", 240f);
            activeBackColor = ColorReader.GetColor(data.Attr("activeBackColor", "Black"));
            disabledBackColor = ColorReader.GetColor(data.Attr("disabledBackColor", "1f2e2d"));
            activeLineColor = ColorReader.GetColor(data.Attr("activeLineColor", "White"));
            disabledLineColor = ColorReader.GetColor(data.Attr("disabledLineColor", "6a8480"));
            particleLayerColors = new[]
            {
                Colors(data.Attr("particleLayer0Colors", "ffef11,ff00d0,08a310")),
                Colors(data.Attr("particleLayer1Colors", "5fcde4,7fb25e,e0564c")),
                Colors(data.Attr("particleLayer2Colors", "5b6ee1,CC3B3B,7daa64"))
            };
            Add(new RoomTransitionListener { OnOutBegin = TransitionOnOutBegin });
        }

        private static PackedColor[] Colors(string value) => value.Split(',').Select(ColorReader.GetColor).ToArray();

        private void TransitionOnOutBegin()
        {
            if (((ISourceScene)Scene).SourceScene.Entities.Entities.OfType<DreamDashController>().Count() > 1)
                addedColors = false;
        }

        public void AddColors()
        {
            if (!OverrideColors || addedColors) return;
            runtime.SetColors(activeBackColor, disabledBackColor, activeLineColor, disabledLineColor);
            addedColors = true;
        }

        public void RemoveColors()
        {
            if (!OverrideColors || !addedColors) return;
            runtime.ResetColors();
            addedColors = false;
        }

        public void AddParticleColors(object scene)
        {
            var level = (OriginalLevelSceneRoot)scene;
            var bounds = level.Bounds;
            foreach (var entity in level.SourceScene.Entities.GetTrackedEntities("Celeste.DreamBlock"))
            {
                var block = (OriginalDreamBlockState)entity;
                if (block.Right > bounds.Left && block.Bottom > bounds.Top && block.Left < bounds.Right &&
                    block.Top < bounds.Bottom)
                    for (int i = 0; i < block.DreamParticleCount; i++)
                    {
                        var colors = particleLayerColors[block.GetDreamParticleLayer(i)];
                        block.SetDreamParticleColor(i,
                            colors[((ISourceRandomEnvironment)runtime.Engine).Random.Next(colors.Length)]);
                    }
            }

            addedParticleColors = true;
        }

        public override void Added(object scene)
        {
            AddColors();
            base.Added(scene);
        }

        public override void Awake(object scene)
        {
            if (OverrideColors && !addedParticleColors) AddParticleColors(scene);
            base.Awake(scene);
        }

        public override void Removed(object scene)
        {
            RemoveColors();
            base.Removed(scene);
        }

        public override void SceneEnd(object scene)
        {
            RemoveColors();
            base.SceneEnd(scene);
        }
    }
}
