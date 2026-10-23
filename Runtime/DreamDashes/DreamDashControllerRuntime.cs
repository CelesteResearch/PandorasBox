// Copyright (C) 2018-2020 Cruor
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Scheduling;
using Celeste.Core.Rendering;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution.Levels;
using Celeste.Mods.PandorasBox.Entities.Controllers.DreamDashes;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.DreamDashes
{
    public sealed partial class DreamDashControllerRuntime : IDisposable
    {
        private static readonly ConditionalWeakTable<ISourceEngineContext, DreamDashControllerRuntime> Owners = new();
        private readonly List<IDisposable> hooks = new();
        private readonly ConditionalWeakTable<RoomPlayerSession, DreamDashWallState> walls = new();
        public ISourceEngineContext Engine { get; }

        private bool paletteRegistered;

        internal readonly Func<RoomPlayerSession, bool> DreamDashedIntoSolid;

        public static DreamDashControllerRuntime For(ISourceEngineContext engine) =>
            Owners.GetValue(engine, e => new(e));

        public static void Release(ISourceEngineContext engine)
        {
            if (Owners.TryGetValue(engine, out var v))
            {
                v.Dispose();
                Owners.Remove(engine);
            }
        }

        private static MethodInfo PlayerMethod(string name) => typeof(RoomPlayerSession).GetMethod(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private DreamDashControllerRuntime(ISourceEngineContext engine)
        {
            Engine = engine;
            DreamDashedIntoSolid = (Func<RoomPlayerSession, bool>)PlayerMethod("DreamDashedIntoSolid")
                .CreateDelegate(typeof(Func<RoomPlayerSession, bool>));
            try
            {
                hooks.Add(new Hook(PlayerMethod("Render"),
                    (Action<Action<RoomPlayerSession, ISourceRenderContext>, RoomPlayerSession, ISourceRenderContext>)
                    RenderPlayer));
                hooks.Add(new Hook(PlayerMethod("DreamDashBegin"),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)BeginDreamDash));
                hooks.Add(new Hook(PlayerMethod("DreamDashUpdate"),
                    (Func<Func<RoomPlayerSession, int>, RoomPlayerSession, int>)UpdateDreamDash));
                hooks.Add(new Hook(PlayerMethod("Update"),
                    (Action<Action<RoomPlayerSession>, RoomPlayerSession>)UpdatePlayer));
                DreamBlockPaletteFields.Register(Engine);
                paletteRegistered = true;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetColors(PackedColor activeBack, PackedColor disabledBack, PackedColor activeLine,
            PackedColor disabledLine)
        {
            using (DreamBlockPaletteFields.Enter(Engine))
                DreamBlockPaletteFields.Write(activeBack, disabledBack, activeLine, disabledLine);
        }

        public void ResetColors() => SetColors(PackedColor.Black, new PackedColor(0x1f, 0x2e, 0x2d),
            PackedColor.White, new PackedColor(0x6a, 0x84, 0x80));

        private DreamDashController First(RoomPlayerSession player)
        {
            if (!ReferenceEquals(player.EngineContext, Engine) || player.Scene is not ISourceScene scene) return null;
            var key = scene.SourceScene.Catalog.Find(typeof(DreamDashController)).RegistryKey;
            var entities = scene.SourceScene.Entities.GetTrackedEntities(key);
            return entities.Count > 0 ? (DreamDashController)entities[0] : null;
        }

        private void BeginDreamDash(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            var controller = First(player);
            controller?.DreamDashStartBefore(player);
            var before = player.Data.Motion.Normal.Speed;
            original(player);
            controller?.DreamDashStartAfter(player, before);
        }

        private int UpdateDreamDash(Func<RoomPlayerSession, int> original, RoomPlayerSession player) =>
            First(player)?.AttemptBounce(player) == true ? player.Machine.State : original(player);

        private void UpdatePlayer(Action<RoomPlayerSession> original, RoomPlayerSession player)
        {
            var controller = First(player);
            if (controller != null && player.Input.Dash.Pressed && player.Input.Aim.Value != Vector2.Zero)
                controller.Redirect(player);
            int facing = player.Data.Facing;
            var scale = player.Sprite.Scale;
            original(player);
            if (!ReferenceEquals(player.EngineContext, Engine) || !walls.TryGetValue(player, out var wall) ||
                !wall.HasRotation) return;
            player.Data.Facing = facing;
            player.Sprite.Scale = scale;
            var aim = player.Input.Aim.Value;
            if (aim == Vector2.Zero) return;
            float angle = ((float)Math.Atan2(aim.Y, aim.X) - wall.Rotation + (float)Math.PI * 2f) %
                          ((float)Math.PI * 2f);
            int next = player.Data.Facing;
            if ((double)angle >= Math.PI * 3.0 / 4.0 && (double)angle <= 3.9269908169872414) next = -1;
            else if (((double)angle >= -Math.PI / 4.0 && (double)angle <= Math.PI / 4.0) ||
                     ((double)(angle - (float)Math.PI * 2f) >= -Math.PI / 4.0 &&
                      (double)(angle - (float)Math.PI * 2f) <= Math.PI / 4.0)) next = 1;
            if (player.Data.Facing != next) player.Data.Facing = next;
        }

        internal bool TryGetWall(RoomPlayerSession player, out DreamDashWallState state) =>
            walls.TryGetValue(player, out state);

        internal DreamDashWallState SetWall(RoomPlayerSession player) => walls.GetValue(player, _ => new());
        internal void RemoveWall(RoomPlayerSession player) => walls.Remove(player);

        public void Dispose()
        {
            for (int i = hooks.Count - 1; i >= 0; i--) hooks[i].Dispose();
            hooks.Clear();
            if (paletteRegistered)
            {
                DreamBlockPaletteFields.Unregister(Engine);
                paletteRegistered = false;
            }
        }
    }
}
