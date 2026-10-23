// SPDX-License-Identifier: MIT
// Copyright (C) 2018-2020 Cruor

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Core.Scheduling;
using Celeste.Core.Mathematics;
using Celeste.Everest.Modules.Entities.Metadata;
using Celeste.Formats.Map.Entities.Structure;
using Celeste.Graphics.Animation;
using Celeste.Graphics.Content;
using Celeste.Graphics.Content.Atlas;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Physics.Collision;
using Celeste.Physics.Collision.Shapes;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Actors.Foundation;
using Celeste.Simulation.Gameplay.Platforms.Foundation;
using Celeste.Mods.PandorasBox.Runtime.Transport.Pipes;
using Celeste.Mods.PandorasBox.Components.Transport.Pipes;
using Celeste.Mods.PandorasBox.Entities.Transport.Pipes;
using static Celeste.Mods.PandorasBox.Runtime.Transport.Pipes.ClearPipeGeometry;

namespace Celeste.Mods.PandorasBox.Components.Transport.Pipes
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.MarioClearPipeInteraction")]
    [SceneTracked]
    public class ClearPipeInteraction : SourceComponentState
    {
        public delegate void OnPipeInteractionDelegate(SceneEntityState entity, ClearPipeInteraction interaction);

        public delegate bool CanEnterPipeDelegate(SceneEntityState entity, ClearPipeDirection direction);

        public OnPipeInteractionDelegate OnPipeEnter;
        public OnPipeInteractionDelegate OnPipeExit;
        public OnPipeInteractionDelegate OnPipeBlocked;
        public OnPipeInteractionDelegate OnPipeUpdate;

        public CanEnterPipeDelegate CanEnterPipe = (entity, direction) => true;

        public Vector2 PipeRenderOffset;

        public ClearPipe CurrentClearPipe = null;

        public ClearPipeDirection Direction = ClearPipeDirection.None;
        public Vector2 DirectionVector = Vector2.Zero;

        public bool ExitEarly = false;

        public Vector2 From = Vector2.Zero;
        public Vector2 To = Vector2.Zero;

        public float Moved = 0f;
        public float Distance = 0f;

        public float TravelSpeed = 1f;

        public ClearPipeInteraction(Vector2 renderOffset) : base(true, true)
        {
            PipeRenderOffset = renderOffset;
        }
    }
}
