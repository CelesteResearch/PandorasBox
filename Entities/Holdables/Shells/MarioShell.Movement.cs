// SPDX-License-Identifier: MIT
// Copyright (C) 2018-2020 Cruor

using System;
using System.Numerics;
using Celeste.Core.Display;
using Celeste.Core.Mathematics;
using Celeste.Core.Mathematics.Extrema;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Mods.PandorasBox.Components.Transport.Pipes;

namespace Celeste.Mods.PandorasBox.Entities.Holdables.Shells
{
    internal partial class MarioShell
    {
        private bool DangerousCheck(object collider)
        {
            return !Hold.IsHeld && Speed != Vector2.Zero;
        }

        private void updateColors()
        {
            if (colors != null && colors.Count > 1 && colorSpeed > 0f)
            {
                int num = (int)Math.Floor(timeAcc / colorSpeed % (float)colors.Count);
                float num2 = timeAcc / colorSpeed % 1f;
                PackedColor color = PackedColor.Lerp(colors[num], colors[(num + 1) % colors.Count], num2);
                shellMoving.Color = shellIdle.Color = color;
            }
        }

        private void updateDiscoShell()
        {
            if (!disco)
            {
                return;
            }

            turnTime = Math.Max(0f, turnTime - engine.DeltaTime);
            if (turnTime != 0f || !OnGround(1))
            {
                return;
            }

            RoomPlayerSession nearestEntity = NearestPlayer();
            if (nearestEntity != null)
            {
                if (Speed.X == 0f)
                {
                    Speed.X = ((X > nearestEntity.X) ? (0f - baseSpeed) : baseSpeed);
                    turnTime = DISCO_TURN_DELAY;
                }
                else if (X > nearestEntity.X + 16f)
                {
                    Speed.X = 0f - baseSpeed;
                    turnTime = DISCO_TURN_DELAY;
                }
                else if (X < nearestEntity.X - 16f)
                {
                    Speed.X = baseSpeed;
                    turnTime = DISCO_TURN_DELAY;
                }
            }
        }

        public override void Update()
        {
            grace = Math.Max(0f, grace - engine.DeltaTime);
            springGrace = Math.Max(0f, springGrace - engine.DeltaTime);
            timeAcc += engine.DeltaTime;
            updateColors();
            updateDiscoShell();
            bool flag = Speed.X != 0f || Get<ClearPipeInteraction>()?.CurrentClearPipe != null;
            decorationMoving.Visible = shellMoving.Visible = flag;
            decorationIdle.Visible = shellIdle.Visible = !flag;
            if (!Hold.IsHeld)
            {
                bool flag2 = OnGround(1);
                Speed.Y = ((flag2 && Speed.Y >= 0f)
                    ? 0f
                    : ScalarMath.Approach(Speed.Y, 200f, 400f * engine.DeltaTime,
                        ((ISourceExtremaEnvironment)engine).ExtremaArithmetic));
                MoveH(Speed.X * engine.DeltaTime, onCollideH, null);
                MoveV(Speed.Y * engine.DeltaTime, onCollideV, null);
                if (flag2 && LiftSpeed == Vector2.Zero && prevLiftspeed != Vector2.Zero)
                {
                    Speed = prevLiftspeed;
                    Speed.X = (float)Math.Sign(Speed.X) * baseSpeed;
                    prevLiftspeed = Vector2.Zero;
                }

                prevLiftspeed = LiftSpeed;
            }
            else
            {
                prevLiftspeed = Vector2.Zero;
            }

            if (!Hold.IsHeld && (object)Collider == shellHeldCollider && grace == 0f)
            {
                Collider = shellNotHeldCollider;
                if (Body.CollideSolidAt(Position))
                {
                    Collider = shellHeldCollider;
                }
            }

            Hold.CheckAgainstColliders();
            Hold.PickupCollider = ((grabbable && (double)Math.Abs(Speed.X) <= 1E-05)
                ? pickupIdleCollider
                : pickupMovingCollider);
            base.Update();
        }

        private RoomPlayerSession NearestPlayer()
        {
            RoomPlayerSession result = null;
            float distance = 0f;
            foreach (var entity in ((ISourceScene)Scene).SourceScene.Entities.GetTrackedEntities(playerKey))
            {
                var player = (RoomPlayerSession)entity;
                float candidate =
                    ((ISourceVectorEnvironment)engine).VectorArithmetic.LengthSquared(Position - player.Position);
                if (result == null || candidate < distance)
                {
                    result = player;
                    distance = candidate;
                }
            }

            return result;
        }
    }
}
