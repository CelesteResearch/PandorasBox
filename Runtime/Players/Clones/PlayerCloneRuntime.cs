using System;
using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Celeste.Core.Components;
using Celeste.Core.Components.Animation;
using Celeste.Core.Geometry;
using Celeste.Core.Input;
using Celeste.Core.Scheduling;
using Celeste.Everest.Input;
using Celeste.Everest.Stories.Dialogue;
using Celeste.Game.Components.Talking;
using Celeste.Game.Entities.Hazards.Spinners.Crystal;
using Celeste.Game.Entities.Lookouts;
using Celeste.Game.Execution.Levels;
using Celeste.Input.Bindings;
using Celeste.Input.Runtime;
using Celeste.Mods.PandorasBox.Entities.Players.Clones;
using Celeste.Physics.Collision;
using Celeste.Scene.Tracking;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Player.Collision;
using Celeste.Simulation.Gameplay.Player.Dash;
using Celeste.Simulation.Gameplay.Player.Movement;
using Celeste.Simulation.Gameplay.Player.Respawn;
using Celeste.Simulation.Presentation.Player.Death;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.Players.Clones;

internal sealed partial class PlayerCloneRuntime : IDisposable
{
    private sealed class OffsetValue
    {
        public readonly float Value;
        public OffsetValue(float value) => Value = value;
    }

    private readonly ISourceEngineContext engine;
    private readonly SourceMInputProcess ownedInputProcess;
    private readonly List<IDisposable> hooks = new();
    private readonly ConditionalWeakTable<OriginalCrystalStaticSpinner, OffsetValue> spinnerOffsets = new();
    private readonly ConditionalWeakTable<RoomPlayerSession, RoomPlayerSession> syncedRespawn = new();
    private readonly Dictionary<SourceVirtualButton, bool> consumedPresses = new();
    private readonly Dictionary<SourceVirtualButton, float> bufferCounters = new();
    private RoomPlayerSession closestLookoutPlayer;

    private static readonly Func<OriginalCrystalStaticSpinner, float> SpinnerOffset =
        CloneFieldAccess.Getter<OriginalCrystalStaticSpinner, float>("offset");

    private static readonly Func<PlayerRespawnSequence, SourceTweenState> RespawnTween =
        CloneFieldAccess.Getter<PlayerRespawnSequence, SourceTweenState>("respawnTween");

    private static readonly Func<SourceVirtualButton, float> BufferCounter =
        CloneFieldAccess.Getter<SourceVirtualButton, float>("bufferCounter");

    private static readonly Action<SourceVirtualButton, float> SetBufferCounter =
        CloneFieldAccess.Setter<SourceVirtualButton, float>("bufferCounter");

    private static readonly Action<SourceVirtualButton, bool> SetConsumed =
        CloneFieldAccess.Setter<SourceVirtualButton, bool>("consumed");

    private static readonly Func<SourceVirtualInput, SourceMInputProcess> InputProcess =
        CloneFieldAccess.Getter<SourceVirtualInput, SourceMInputProcess>("process");

    private static readonly Func<OriginalTalkComponent, float> DisableDelay =
        CloneFieldAccess.Getter<OriginalTalkComponent, float>("disableDelay");

    public PlayerCloneRuntime(ISourceEngineContext engine)
    {
        this.engine = engine;
        ownedInputProcess = ((ISourceMInputEnvironment)engine).MInput;
        AddHook(typeof(EverestDialogCutsceneState), "OnBegin",
            (Action<Action<EverestDialogCutsceneState, OriginalLevelSceneRoot>, EverestDialogCutsceneState,
                OriginalLevelSceneRoot>)DialogBegin);
        AddHook(typeof(EverestDialogCutsceneState), "OnEnd",
            (Action<Action<EverestDialogCutsceneState, OriginalLevelSceneRoot>, EverestDialogCutsceneState,
                OriginalLevelSceneRoot>)DialogEnd);
        AddHook(typeof(OriginalCrystalStaticSpinner), "Update",
            (Action<Action<OriginalCrystalStaticSpinner>, OriginalCrystalStaticSpinner>)SpinnerUpdate);
        AddHook(typeof(RoomPlayerSession), "Die",
            (Func<Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation>, RoomPlayerSession, Vector2,
                bool, bool, PlayerDeathPresentation>)PlayerDie);
        AddHook(typeof(RoomPlayerSession), "Added",
            (Action<Action<RoomPlayerSession, object>, RoomPlayerSession, object>)PlayerAdded);
        AddHook(typeof(RoomPlayerSession), "Update",
            (Action<Action<RoomPlayerSession>, RoomPlayerSession>)PlayerUpdate);
        AddHook(typeof(OriginalLookoutState), "Interact",
            (Action<Action<OriginalLookoutState, RoomPlayerSession>, OriginalLookoutState, RoomPlayerSession>)
            LookoutInteract);
        AddHook(typeof(OriginalLookoutState), "LookRoutine",
            (Func<Func<OriginalLookoutState, RoomPlayerSession, IEnumerator>, OriginalLookoutState, RoomPlayerSession,
                IEnumerator>)LookoutRoutine);
        AddHook(typeof(OriginalLookoutState), "StopInteracting",
            (Action<Action<OriginalLookoutState>, OriginalLookoutState>)LookoutStop);
        AddHook(typeof(SourceVirtualButton), "Update",
            (Action<Action<SourceVirtualButton>, SourceVirtualButton>)ButtonUpdate);
        AddHook(typeof(SourceEverestVirtualButton), "Update",
            (Action<Action<SourceEverestVirtualButton>, SourceEverestVirtualButton>)EverestButtonUpdate);
        hooks.Add(new ILHook(Method(typeof(OriginalTalkComponent), "Update"), PatchTalk));
        AddConsumeHook(typeof(RoomPlayerSession), "Jump", false);
        AddConsumeHook(typeof(RoomPlayerSession), "SuperJump", false);
        AddConsumeHook(typeof(RoomPlayerSession), "SuperWallJump", false);
        AddConsumeHook(typeof(DashSequence), "Start", false);
        AddConsumeHook(typeof(SwimSequence), "Update", false);
        AddConsumeHook(typeof(HitSquashSequence), "Update", false);
        AddConsumeHook(typeof(PlayerBoostSequence), "BoostUpdate", true);
    }

    private static MethodInfo Method(Type type, string name) => type.GetMethod(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private void AddHook(Type type, string name, Delegate target) => hooks.Add(new Hook(Method(type, name), target));

    private static List<object> Players(object scene) =>
        ((ISourceScene)scene).SourceScene.Entities.GetTrackedEntities(typeof(RoomPlayerSession));

    private bool OwnsScene(object scene) =>
        scene is OriginalLevelSceneRoot level && ReferenceEquals(level.EngineContext, engine);

    private bool OwnsButton(SourceVirtualButton button) => ReferenceEquals(InputProcess(button), ownedInputProcess);

    private void DialogBegin(Action<EverestDialogCutsceneState, OriginalLevelSceneRoot> original,
        EverestDialogCutsceneState self, OriginalLevelSceneRoot level)
    {
        if (ReferenceEquals(level.EngineContext, engine))
        {
            var players = Players(self.Scene);
            if (players.Count > 1)
                foreach (RoomPlayerSession player in players)
                {
                    player.Machine.State = 11;
                    player.Machine.Locked = true;
                    player.ForceCameraUpdate = true;
                }
        }

        original(self, level);
    }

    private void DialogEnd(Action<EverestDialogCutsceneState, OriginalLevelSceneRoot> original,
        EverestDialogCutsceneState self, OriginalLevelSceneRoot level)
    {
        if (ReferenceEquals(level.EngineContext, engine))
        {
            var players = Players(self.Scene);
            if (players.Count > 1)
                foreach (RoomPlayerSession player in players)
                {
                    player.Machine.Locked = false;
                    player.Machine.State = 0;
                    player.ForceCameraUpdate = false;
                }
        }

        original(self, level);
    }

    private void SpinnerUpdate(Action<OriginalCrystalStaticSpinner> original, OriginalCrystalStaticSpinner self)
    {
        bool visible = self.Visible;
        original(self);
        if (!ReferenceEquals(((ISourceEngineContextEntity)self).EngineContext, engine) || !visible ||
            self.Collidable) return;
        if (!spinnerOffsets.TryGetValue(self, out var offset))
        {
            offset = new OffsetValue(SpinnerOffset(self));
            spinnerOffsets.Add(self, offset);
        }

        if (!((ISourceScene)self.Scene).SourceScene.Clock.OnInterval(0.05f, offset.Value, engine)) return;
        var players = Players(self.Scene);
        if (players.Count <= 1) return;
        foreach (RoomPlayerSession player in players)
            self.Collidable = self.Collidable || Math.Abs(player.Position.X - self.Position.X) < 128f &&
                Math.Abs(player.Position.Y - self.Position.Y) < 128f;
    }

    private PlayerDeathPresentation PlayerDie(
        Func<RoomPlayerSession, Vector2, bool, bool, PlayerDeathPresentation> original, RoomPlayerSession self,
        Vector2 direction, bool evenIfInvincible, bool registerDeathsInStats)
    {
        if (!ReferenceEquals(self.EngineContext, engine))
            return original(self, direction, evenIfInvincible, registerDeathsInStats);
        var players = Players(self.Scene);
        int dead = 0;
        foreach (RoomPlayerSession player in players)
            if (player.Dead)
                dead++;
        if (dead != 0 || self.Settings.Invincible && !evenIfInvincible) return null;
        foreach (RoomPlayerSession player in players)
        {
            if (player == self) continue;
            foreach (var follower in player.Leader.Followers) self.Leader.Followers.Add(follower);
            if (!player.Dead)
                ((ISourceScene)player.Scene).SourceScene.QueueSourceEntity(
                    new CustomPlayerDeadBody(player, Vector2.Zero));
            ((ISourceScene)player.Scene).SourceScene.RemoveEntity(player);
        }

        return original(self, direction, evenIfInvincible, registerDeathsInStats);
    }

    private void PlayerAdded(Action<RoomPlayerSession, object> original, RoomPlayerSession self, object scene)
    {
        original(self, scene);
        if (!ReferenceEquals(self.EngineContext, engine)) return;
        var registry = ((ISourceScene)scene).SourceScene.Entities;
        var clones = registry.GetTrackedEntities(typeof(CloneSpawner));
        foreach (RoomPlayerSession player in Players(scene))
        {
            bool clone = false;
            foreach (CloneSpawner spawner in clones)
                if (spawner.Clone == player)
                {
                    clone = true;
                    break;
                }

            if (!clone && RespawnTween(player.Respawn) != null && player != self)
            {
                self.Machine.State = player.Machine.State;
                syncedRespawn.Add(self, player);
            }
        }
    }

    private void PlayerUpdate(Action<RoomPlayerSession> original, RoomPlayerSession self)
    {
        if (!ReferenceEquals(self.EngineContext, engine))
        {
            original(self);
            return;
        }

        foreach (var pair in consumedPresses)
            if (pair.Value)
                SetConsumed(pair.Key, false);
        foreach (var pair in bufferCounters) SetBufferCounter(pair.Key, pair.Value);
        original(self);
        if (syncedRespawn.TryGetValue(self, out var source) && RespawnTween(source.Respawn) == null)
        {
            self.Machine.State = source.Machine.State;
            self.Data.Facing = source.Facing;
            syncedRespawn.Remove(self);
        }
    }

    private void ClearButton(SourceVirtualButton button)
    {
        if (!OwnsButton(button)) return;
        consumedPresses.Remove(button);
        bufferCounters.Remove(button);
    }

    private void ButtonUpdate(Action<SourceVirtualButton> original, SourceVirtualButton self)
    {
        ClearButton(self);
        original(self);
    }

    private void EverestButtonUpdate(Action<SourceEverestVirtualButton> original, SourceEverestVirtualButton self)
    {
        ClearButton(self);
        original(self);
    }

    private static void SetPlayerState(List<object> players, RoomPlayerSession ignore, int state)
    {
        foreach (RoomPlayerSession player in players)
            if (player != ignore)
                player.Machine.State = state;
    }

    private void LookoutInteract(Action<OriginalLookoutState, RoomPlayerSession> original, OriginalLookoutState self,
        RoomPlayerSession player)
    {
        original(self, player);
        if (!OwnsScene(self.Scene)) return;
        var players = Players(self.Scene);
        float distance = float.PositiveInfinity;
        closestLookoutPlayer = null;
        foreach (RoomPlayerSession candidate in players)
        {
            float squared = (candidate.Position - self.Position).LengthSquared();
            if (squared < distance)
            {
                distance = squared;
                closestLookoutPlayer = candidate;
            }
        }

        SetPlayerState(players, closestLookoutPlayer, 11);
    }

    private IEnumerator LookoutRoutine(Func<OriginalLookoutState, RoomPlayerSession, IEnumerator> original,
        OriginalLookoutState self, RoomPlayerSession player)
    {
        if (!OwnsScene(self.Scene))
        {
            var unmodified = original(self, player);
            while (unmodified.MoveNext()) yield return unmodified.Current;
            yield break;
        }

        var players = Players(self.Scene);
        SetPlayerState(players, closestLookoutPlayer, 11);
        var enumerator = original(self, closestLookoutPlayer);
        while (enumerator.MoveNext()) yield return enumerator.Current;
        SetPlayerState(players, closestLookoutPlayer, 0);
    }

    private void LookoutStop(Action<OriginalLookoutState> original, OriginalLookoutState self)
    {
        original(self);
        if (OwnsScene(self.Scene))
            foreach (RoomPlayerSession player in Players(self.Scene))
                player.Machine.State = 0;
    }

    public void Dispose()
    {
        foreach (var hook in hooks) hook.Dispose();
    }
}
