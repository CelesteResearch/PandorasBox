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

namespace Celeste.Mods.PandorasBox.Runtime.Transport.Pipes
{
    public static class ClearPipeGeometry
    {
        public static ClearPipeDirection GetPipeExitDirection(Vector2 exit, Vector2 previous)
        {
            if (exit.X < previous.X) return ClearPipeDirection.Left;
            if (exit.X > previous.X) return ClearPipeDirection.Right;
            if (exit.Y < previous.Y) return ClearPipeDirection.Up;
            if (exit.Y > previous.Y) return ClearPipeDirection.Down;
            return ClearPipeDirection.None;
        }
    }
}
