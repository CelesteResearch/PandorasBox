using System;
using Celeste.Core.Components;
using Celeste.Core.Geometry;
using Celeste.Core.Input;
using Celeste.Game.Components.Talking;
using Celeste.Input.Bindings;
using Celeste.Physics.Collision;
using Celeste.Simulation;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.Players.Clones;

internal sealed partial class PlayerCloneRuntime
{
    private void AddConsumeHook(Type type, string method, bool press) =>
        hooks.Add(new ILHook(Method(type, method), il => PatchConsume(il, press)));

    private void CaptureBuffer(ISourceVirtualButton source, bool press)
    {
        if (source is not SourceVirtualButton button || !OwnsButton(button)) return;
        bufferCounters[button] = BufferCounter(button);
        if (press) consumedPresses[button] = true;
    }

    private void PatchConsume(ILContext il, bool press)
    {
        string methodName = press ? "ConsumePress" : "ConsumeBuffer";
        var cursor = new ILCursor(il);
        int count = 0;
        while (cursor.TryGotoNext(MoveType.Before, instruction =>
                   instruction.OpCode == OpCodes.Callvirt && instruction.Operand is MethodReference method &&
                   method.DeclaringType.FullName == typeof(ISourceVirtualButton).FullName && method.Name == methodName))
        {
            cursor.Emit(OpCodes.Dup);
            cursor.EmitDelegate<Action<ISourceVirtualButton>>(button => CaptureBuffer(button, press));
            cursor.Index++;
            count++;
        }

        if (count == 0)
            throw new InvalidOperationException("Player clone input anchor missing: " + il.Method.FullName + " " +
                                                methodName);
    }

    private bool PlayerOver(OriginalTalkComponent talker, bool current)
    {
        if (!OwnsScene(talker.Scene)) return current;
        var players = Players(talker.Scene);
        if (players.Count == 1) return current;
        if (DisableDelay(talker) >= 0.05f) return false;
        var position = ((ISourceEntityPosition)talker.Entity).Position;
        var bounds = talker.Bounds;
        foreach (RoomPlayerSession player in players)
            if (SourceEntityCollision.CheckRect(player, new IntRect((int)(position.X + bounds.X),
                    (int)(position.Y + bounds.Y), bounds.Width, bounds.Height)) && player.OnGround(1) &&
                player.Bottom < position.Y + bounds.Bottom + 4f && player.Machine.State == 0 &&
                (!talker.PlayerMustBeFacing || Math.Abs(player.Position.X - position.X) <= bounds.Width ||
                 player.Facing == Math.Sign(position.X - player.Position.X)) &&
                (OriginalTalkComponent.PlayerOver == null || OriginalTalkComponent.PlayerOver == talker))
                return true;
        return false;
    }

    private void PatchTalk(ILContext il)
    {
        VariableDefinition over = null;
        foreach (var variable in il.Body.Variables)
            if (variable.VariableType.MetadataType == MetadataType.Boolean)
            {
                if (over != null) throw new InvalidOperationException("Player clone talk Boolean anchor is ambiguous.");
                over = variable;
            }

        if (over == null) throw new InvalidOperationException("Player clone talk Boolean anchor missing.");
        var cursor = new ILCursor(il);
        int count = 0;
        while (cursor.TryGotoNext(MoveType.After, instruction => instruction.MatchStloc(over.Index)))
        {
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldloc, over);
            cursor.EmitDelegate<Func<OriginalTalkComponent, bool, bool>>(PlayerOver);
            cursor.Emit(OpCodes.Stloc, over);
            count++;
        }

        if (count != 1) throw new InvalidOperationException("Player clone talk over assignment count: " + count);
    }
}
