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
using Celeste.Game.Gameplay.Hazards.Puffer;

namespace Celeste.Mods.PandorasBox.Gameplay.Transport.Pipes
{
    public static class ClearPipePufferInteraction
    {
        public static bool Add(SceneEntityState entity, ClearPipeRuntime runtime)
        {
            OriginalPufferState puffer = entity as OriginalPufferState;

            if (puffer != null && !runtime.HasClearPipeInteraction(entity))
            {
                ClearPipeInteraction interaction = new ClearPipeInteraction(Vector2.Zero);

                interaction.OnPipeBlocked = PufferOnPipeBlocked;
                interaction.OnPipeEnter = PufferOnPipeEnter;
                interaction.OnPipeExit = PufferOnPipeExit;
                interaction.OnPipeUpdate = PufferOnPipeUpdate;
                interaction.CanEnterPipe = PufferCanEnterPipe;

                entity.Add(interaction);

                return true;
            }

            return false;
        }

        public static void PufferOnPipeBlocked(SceneEntityState entity, ClearPipeInteraction interaction)
        {
        }

        public static void PufferOnPipeEnter(SceneEntityState entity, ClearPipeInteraction interaction)
        {
        }

        public static void PufferOnPipeExit(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            OriginalPufferState puffer = entity as OriginalPufferState;

            if (puffer != null)
            {
                puffer.LaunchFromHit(interaction.DirectionVector * interaction.TravelSpeed);
            }
        }

        public static bool PufferCanEnterPipe(SceneEntityState entity, ClearPipeDirection direction)
        {
            OriginalPufferState puffer = entity as OriginalPufferState;

            if (puffer != null)
            {
                Vector2 speed = puffer.HitSpeed;

                switch (direction)
                {
                    case ClearPipeDirection.Left:
                        return speed.X > 0;

                    case ClearPipeDirection.Right:
                        return speed.X < 0;

                    case ClearPipeDirection.Up:
                        return speed.Y > 0;

                    case ClearPipeDirection.Down:
                        return speed.Y < 0;

                    default:
                        return false;
                }
            }

            return false;
        }

        public static void PufferOnPipeUpdate(SceneEntityState entity, ClearPipeInteraction interaction)
        {
        }
    }
}
