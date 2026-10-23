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
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.PandorasBox.Gameplay.Transport.Pipes
{
    public static class ClearPipeHoldableInteraction
    {
        public static void HoldableOnPipeBlocked(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            SourceActorEntityState actor = entity as SourceActorEntityState;

            if (actor != null)
            {
                // Attempt to force a squish by moving the colliding solid by 0 pixels
                SourceSolidEntityState solid = ClearPipeRuntime
                    .For(((ISourceEngineContextScene)actor.Scene).EngineContext)
                    .CollideFirst<SourceSolidEntityState>(actor);
                if (solid != null)
                {
                    solid.MoveHExact(0);
                }
            }
            else
            {
                if (entity?.Scene is ISourceScene scene) scene.SourceScene.RemoveEntity(entity);
            }
        }

        public static void HoldableOnPipeEnter(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            HoldableState holdable = entity?.Get<HoldableState>();

            if (holdable != null)
            {
                // Reset speeds and remove holder
                holdable.Release(Vector2.Zero);
            }
        }

        public static void HoldableOnPipeExit(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            HoldableState holdable = entity?.Get<HoldableState>();

            if (holdable != null && entity.Scene != null && !holdable.IsHeld && !interaction.ExitEarly)
            {
                Vector2 speed = Vector2.Zero;

                switch (interaction.Direction)
                {
                    case ClearPipeDirection.Left:
                        speed = new Vector2(-1.0f, 0.1f);
                        break;

                    case ClearPipeDirection.Right:
                        speed = new Vector2(1.0f, -0.1f);
                        break;

                    case ClearPipeDirection.Up:
                        speed = new Vector2(0, -1.0f);
                        break;

                    case ClearPipeDirection.Down:
                        speed = new Vector2(0, 1.0f);
                        break;

                    default:
                        break;
                }

                holdable.Release(speed);
            }
        }

        public static bool HoldableCanEnterPipe(SceneEntityState entity, ClearPipeDirection direction)
        {
            HoldableState holdable = entity?.Get<HoldableState>();

            if (holdable != null && !holdable.IsHeld)
            {
                Vector2 speed = holdable?.SpeedGetter() ?? Vector2.Zero;

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

        public static void HoldableOnPipeUpdate(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            HoldableState holdable = entity.Get<HoldableState>();

            interaction.ExitEarly = holdable?.IsHeld ?? false;
        }

        public static bool Add(SceneEntityState entity, ClearPipeRuntime runtime)
        {
            HoldableState holdable = entity.Get<HoldableState>();

            // Depends on SpeedGetter and OnRelease being defined
            if (holdable != null && holdable.SpeedGetter != null && holdable.OnRelease != null &&
                !runtime.HasClearPipeInteraction(entity))
            {
                Vector2 pipeOffset = Vector2.Zero;

                if (entity.Collider != null)
                {
                    pipeOffset = -entity.Collider.Center;
                }

                ClearPipeInteraction interaction = new ClearPipeInteraction(pipeOffset);

                interaction.OnPipeBlocked = ClearPipeHoldableInteraction.HoldableOnPipeBlocked;
                interaction.OnPipeEnter = ClearPipeHoldableInteraction.HoldableOnPipeEnter;
                interaction.OnPipeExit = ClearPipeHoldableInteraction.HoldableOnPipeExit;
                interaction.OnPipeUpdate = ClearPipeHoldableInteraction.HoldableOnPipeUpdate;
                interaction.CanEnterPipe = ClearPipeHoldableInteraction.HoldableCanEnterPipe;

                entity.Add(interaction);

                return true;
            }

            return false;
        }
    }
}
