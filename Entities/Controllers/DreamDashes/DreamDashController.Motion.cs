// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using System;
using System.Numerics;
using Celeste.Core.Mathematics;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Scene.Tracking;
using Celeste.Scene.Entities;
using Celeste.Simulation;

namespace Celeste.Mods.PandorasBox.Entities.Controllers.DreamDashes;

public sealed partial class DreamDashController
{
    public bool Redirect(RoomPlayerSession player)
    {
        if (player.Machine.State != 9) return false;
        player.Data.CooldownTimer = 0f;
        if (!player.Motor.Dash.CanDash) return false;
        bool same = player.Input.GetAimVector(1) == player.Data.Direction;
        if (!((AllowDreamDashRedirection && !same) || (AllowSameDirectionDash && same))) return false;
        player.Data.Dashes = Math.Max(0, player.Data.Dashes - 1);
        player.Play("event:/char/madeline/dreamblock_enter");
        if (runtime.Engine.Effects.TimeRate > .25f) player.Freeze(.05f);
        if (same)
        {
            player.Data.Motion.Normal.Speed *= sameDirectionSpeedMultiplier;
            player.Data.Direction *= Math.Sign(sameDirectionSpeedMultiplier);
        }
        else
        {
            player.Data.Direction = player.Input.GetAimVector(1);
            player.Data.Motion.Normal.Speed = player.Data.Direction * player.Data.Motion.Normal.Speed.Length();
        }

        player.Input.Dash.ConsumeBuffer();
        return true;
    }

    public void DreamDashStartBefore(RoomPlayerSession player)
    {
        var value = runtime.TryGetWall(player, out var wall) ? wall.Speed : player.Data.Motion.Normal.Speed;
        runtime.RemoveWall(player);
        if (UseEntrySpeedAngle)
            player.Data.Motion.Normal.Speed =
                ((ISourceVectorEnvironment)runtime.Engine).VectorArithmetic.SafeNormalize(value) * value.Length();
    }

    public void DreamDashStartAfter(RoomPlayerSession player, Vector2 before)
    {
        var direction = UseEntrySpeedAngle
            ? ((ISourceVectorEnvironment)runtime.Engine).VectorArithmetic.SafeNormalize(before)
            : player.Data.Direction;
        if (OverrideDreamDashSpeed) player.Data.Motion.Normal.Speed = direction * dreamDashSpeed;
        if (NeverSlowDown && player.Data.Motion.Normal.Speed.LengthSquared() < before.LengthSquared())
            player.Data.Motion.Normal.Speed = direction * before.Length();
    }

    private static OriginalDreamBlockState FirstDream(RoomPlayerSession player) =>
        SourceTrackedEntityCollision.First<OriginalDreamBlockState>(((ISourceScene)player.Scene).SourceScene.Entities,
            "Celeste.DreamBlock", player);

    private bool OutsideAfterMove(RoomPlayerSession player, Vector2 offset)
    {
        player.NaiveMove(offset);
        bool outside = FirstDream(player) == null;
        player.NaiveMove(-offset);
        return outside;
    }

    public bool AttemptBounce(RoomPlayerSession player)
    {
        if (!BounceOnCollision && !CollideStickToWalls) return false;
        var offset = player.Data.Motion.Normal.Speed * runtime.Engine.DeltaTime;
        player.NaiveMove(offset);
        if (FirstDream(player) == null && runtime.DreamDashedIntoSolid(player))
        {
            player.NaiveMove(-offset);
            if (BounceOnCollision) BouncePlayer(player);
            else StickPlayer(player);
            return true;
        }

        player.NaiveMove(-offset);
        return false;
    }

    public void BouncePlayer(RoomPlayerSession player)
    {
        var speed = player.Data.Motion.Normal.Speed;
        bool x = OutsideAfterMove(player, new(speed.X * runtime.Engine.DeltaTime, 0));
        bool y = OutsideAfterMove(player, new(0, speed.Y * runtime.Engine.DeltaTime));
        if (x) player.Data.Motion.Normal.Speed.X *= -1f;
        if (y) player.Data.Motion.Normal.Speed.Y *= -1f;
    }

    public void StickPlayer(RoomPlayerSession player)
    {
        var block = FirstDream(player);
        if (block == null) return;
        var wall = runtime.SetWall(player);
        wall.Speed = player.Data.Motion.Normal.Speed;
        var collider = player.Collider;
        var solid = block.Collider;
        bool x = OutsideAfterMove(player, new(player.Data.Motion.Normal.Speed.X * runtime.Engine.DeltaTime, 0));
        bool y = OutsideAfterMove(player, new(0, player.Data.Motion.Normal.Speed.Y * runtime.Engine.DeltaTime));
        player.Machine.State = 0;
        player.World.Contacts.SetDucking(true);
        float moveX = 0, moveY = 0, renderX = 0, renderY = 0;
        double angle = 0;
        if (x)
        {
            if (player.Data.Motion.Normal.Speed.X < 0)
            {
                angle = Math.PI / 2;
                moveX = solid.AbsoluteLeft - player.Position.X + collider.Width / 2f;
                renderX = -collider.Width / 2f;
                renderY = -collider.Height / 2f;
            }
            else
            {
                angle = 4.71238898038469;
                moveX = solid.AbsoluteRight - player.Position.X - collider.Width / 2f;
                renderX = collider.Width / 2f;
                renderY = -collider.Height / 2f;
            }
        }

        if (y)
        {
            if (player.Data.Motion.Normal.Speed.Y < 0)
            {
                angle = Math.PI;
                moveY = solid.AbsoluteTop - player.Position.Y + collider.Height - 1;
                renderY = -collider.Height;
            }
            else
            {
                angle = 0;
                moveY = solid.AbsoluteBottom - player.Position.Y + 1;
            }
        }

        wall.Rotation = (float)angle;
        wall.HasRotation = true;
        wall.RenderOffset = new(renderX, renderY);
        player.NaiveMove(new(moveX, moveY));
    }
}
