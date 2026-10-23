// SPDX-License-Identifier: MIT
// Copyright (C) 2018-2020 Cruor

using System;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Game.Execution.Processes;
using Celeste.Game.Execution.Startup;
using Celeste.Game.Gameplay.Mechanisms.DashSwitches;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Interaction.Springs;
using Celeste.Mods.PandorasBox.Components.Transport.Pipes;

namespace Celeste.Mods.PandorasBox.Entities.Holdables.Shells
{
    internal partial class MarioShell
    {
        public bool HitSpring(ISourceSpringEntity spring)
        {
            if (!Hold.IsHeld && springGrace <= 0f)
            {
                springGrace = GRACE_SPRING;
                if ((int)spring.Orientation == 2 && Speed.X >= 0f)
                {
                    Speed.X = 0f - baseSpeed;
                    Speed.Y = GRAVITY_HORIZONTAL_SPRING;
                    return true;
                }

                if ((int)spring.Orientation == 1 && Speed.X <= 0f)
                {
                    Speed.X = baseSpeed;
                    Speed.Y = GRAVITY_HORIZONTAL_SPRING;
                    return true;
                }

                if ((int)spring.Orientation == 0 && Speed.Y >= 0f)
                {
                    Speed.Y = GRAVITY_VERTICAL_SPRING;
                    return true;
                }
            }

            return false;
        }

        private void OnPlayer(RoomPlayerSession player)
        {
            if (Hold.IsHeld || grace != 0f || Get<ClearPipeInteraction>()?.CurrentClearPipe != null)
            {
                return;
            }

            if (player.BottomCenter.Y < Center.Y + 2f)
            {
                if (dangerous)
                {
                    player.Die(
                        ((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(player.Center - Position),
                        false, true);
                    return;
                }

                grace = GRACE_BOUNCE;
                player.Bounce(Top);
                Speed.X = (((double)Math.Abs(Speed.X) >= 1E-05)
                    ? 0f
                    : ((float)((!(Center.X - player.Center.X < 0f)) ? 1 : (-1)) * baseSpeed));
                ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/game/general/thing_booped", Position,
                    ((IOriginalGameplayProcess)engine).SaveContext).SetVolume(0.5f);
            }
            else if (Speed.X == 0f)
            {
                Speed = new Vector2((float)((!(Center.X - player.Center.X < 0f)) ? 1 : (-1)) * baseSpeed,
                    GRAVITY_AERIAL_KICK);
                grace = GRACE_PUSH;
                ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/game/general/thing_booped", Position,
                    ((IOriginalGameplayProcess)engine).SaveContext).SetVolume(0.5f);
            }
            else
            {
                player.Die(((ISourceVectorEnvironment)engine).VectorArithmetic.SafeNormalize(player.Center - Position),
                    false, true);
            }
        }

        private void OnCollideH(PixelCollision data)
        {
            if (data.Hit is OriginalDashSwitchState dashSwitch)
                dashSwitch.OnDashCollide(null, Vector2.UnitX * Math.Sign(Speed.X));

            ((IOriginalGameplayProcess)engine).AudioSession.Play("event:/game/general/thing_booped", Position,
                ((IOriginalGameplayProcess)engine).SaveContext).SetVolume(0.3f);
            Speed.X *= -1f;
        }

        private void OnCollideV(PixelCollision data)
        {
            Speed.Y = 0f;
        }

        public override bool IsRiding(ISourceEntityCollider solid)
        {
            return base.IsRiding(solid);
        }

        private void OnPickup()
        {
            Collider = shellHeldCollider;
            AddTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent);
            Speed = new Vector2(0f, 0f);
        }

        private void OnRelease(Vector2 force)
        {
            Speed = new Vector2((float)Math.Sign(force.X) * baseSpeed, force.Y * THROWING_HEIGHT_MULTIPLIER);
            grace = GRACE_THROW;
            RemoveTag(((IOriginalGameTagsEnvironment)engine).Tags.Persistent);
        }

        protected override void OnSquish(PixelCollision data)
        {
            if (TrySquishWiggle(data))
            {
                RemoveSelf();
            }
        }
    }
}
