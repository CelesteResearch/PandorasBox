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

namespace Celeste.Mods.PandorasBox.Gameplay.Transport.Pipes
{
    public static class ClearPipePlayerInteraction
    {
        public static void Add(RoomPlayerSession player, ClearPipeRuntime runtime)
        {
            if (player.Get<ClearPipeInteraction>() != null) return;
            var interaction = new ClearPipeInteraction(new Vector2(0f, 9f))
            {
                OnPipeBlocked = PlayerOnPipeBlocked,
                OnPipeEnter = PlayerOnPipeEnter,
                OnPipeExit = PlayerOnPipeExit,
                CanEnterPipe = PlayerCanEnterPipe,
                OnPipeUpdate = PlayerOnPipeUpdate,
            };
            player.Add(interaction);
            runtime.RememberInteraction(player);
        }

        private static bool canPlayerDashIntoPipe(RoomPlayerSession player, ClearPipeDirection pipeDirection)
        {
            bool startDash = (player.Input.CrouchDashPressed || player.Input.DashPressed) && player.Motor.Dash.CanDash;

            // Make sure the player is actually still DashAttacking and not at the end of it
            // Otherwise the player can enter pipes in weird ways
            if (startDash || player.DashAttacking)
            {
                Vector2 dashDir = startDash ? player.Input.GetAimVector(player.Data.Facing) : player.Data.Direction;

                // Player might have dashed into a wall, check with their aim instead
                if (player.DashAttacking && dashDir == Vector2.Zero)
                {
                    dashDir = player.Input.GetAimVector(player.Data.Facing);
                }

                switch (pipeDirection)
                {
                    case ClearPipeDirection.Up:
                        return dashDir.Y > 0;

                    case ClearPipeDirection.Right:
                        return dashDir.X < 0;

                    case ClearPipeDirection.Down:
                        return dashDir.Y < 0;

                    case ClearPipeDirection.Left:
                        return dashDir.X > 0;

                    default:
                        return false;
                }
            }

            return false;
        }

        public static void PlayerOnPipeBlocked(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            RoomPlayerSession player = entity as RoomPlayerSession;

            if (player != null && !player.Dead)
            {
                player.Die(Vector2.Zero);
            }
        }

        public static void PlayerOnPipeEnter(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            RoomPlayerSession player = entity as RoomPlayerSession;

            if (player != null && !player.Dead)
            {
                if (player.Machine.State != 5)
                {
                    player.Machine.State = 11;
                }

                player.Machine.Locked = true;
                player.Dummy.Gravity = false;
                player.Dummy.AutoAnimate = false;
                player.ForceCameraUpdate = true;
                player.Speed = Vector2.Zero;

                player.Sprite.Play("spin");

                if (!player.Inventory.NoRefills)
                {
                    player.RefillDash();
                }
            }
        }

        public static void PlayerOnPipeExit(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            RoomPlayerSession player = entity as RoomPlayerSession;

            if (player != null && !player.Dead && interaction != null)
            {
                player.Machine.Locked = false;
                player.Dummy.Gravity = true;
                player.Dummy.AutoAnimate = true;
                player.ForceCameraUpdate = false;

                if (player.Machine.State != 5)
                {
                    player.Machine.State = 0;
                    player.Data.CooldownTimer = 0f;
                }

                player.Speed = interaction.DirectionVector * interaction.CurrentClearPipe.TransportSpeed;

                bool redBooster = player.Machine.State == 5;

                if (Math.Abs(player.Speed.X) > 0.707)
                {
                    bool inputTowardsPipe = (player.Speed.X < 0 && player.Input.MoveX.Value > 0 ||
                                             player.Speed.X > 0 && player.Input.MoveX.Value < 0);
                    bool wallClimbable = !Celeste.Simulation.Gameplay.Collision.Blockers.ClimbBlockerState.Check(
                        player.Scene, player,
                        player.Position + Vector2.UnitX * 3f * Math.Sign(player.Input.MoveX.Value));

                    if (interaction.CurrentClearPipe.HasPipeSolids && inputTowardsPipe && !redBooster &&
                        wallClimbable &&
                        player.Input.GrabCheck)
                    {
                        player.Speed = Vector2.Zero;
                    }
                }

                if (!redBooster && player.Input.Jump.Pressed)
                {
                    player.Jump();
                }

                if (player.Machine.State == 5)
                {
                    player.Data.Direction =
                        ((ISourceVectorEnvironment)player.EngineContext).VectorArithmetic.SafeNormalize(player.Speed);
                }

                player.StartJumpGraceTime();

                player.RefillStamina();
            }
        }

        public static bool PlayerCanEnterPipe(SceneEntityState entity, ClearPipeDirection direction)
        {
            RoomPlayerSession player = entity as RoomPlayerSession;

            if (player.Holding.Holding != null)
            {
                return false;
            }

            if (player.OnGround())
            {
                // If the player is visually ducking or pushing up against a solid
                bool canDuckInto = player.Sprite.CurrentAnimationID == "duck" && direction == ClearPipeDirection.Up;
                bool canPushInto = player.Sprite.CurrentAnimationID == "push" &&
                                   (direction == ClearPipeDirection.Left || direction == ClearPipeDirection.Right);
                bool holdingDown = player.Input.MoveY.Value > 0;
                bool holdingHorizontally = player.Input.MoveX.Value != 0;
                bool dashAttacking = player.DashAttacking;

                if ((canDuckInto && holdingDown && !dashAttacking) ||
                    (canPushInto && holdingHorizontally && !dashAttacking))
                {
                    return true;
                }
            }
            else
            {
                // Player holds up near a downwards facing pipe
                if (player.Input.MoveY.Value < 0 && direction == ClearPipeDirection.Down && player.Speed.Y < 0)
                {
                    return true;
                }
            }

            return canPlayerDashIntoPipe(player, direction);
        }

        public static void PlayerOnPipeUpdate(SceneEntityState entity, ClearPipeInteraction interaction)
        {
            RoomPlayerSession player = entity as RoomPlayerSession;

            if (player != null && player.Dead)
            {
                interaction.ExitEarly = true;
            }
        }
    }
}
