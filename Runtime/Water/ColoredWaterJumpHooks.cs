using System;
using Celeste.Core.Scheduling;
using Celeste.Mods.PandorasBox.Entities.Water;
using Celeste.Simulation;
using Celeste.Simulation.Gameplay.Environment.Water;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.Water
{
    public sealed class ColoredWaterJumpHooks : IDisposable
    {
        private readonly ISourceEngineContext engine;
        private ILHook jump;

        public ColoredWaterJumpHooks(ISourceEngineContext engine)
        {
            this.engine = engine;
            jump = new ILHook(typeof(RoomPlayerWorld).GetMethod(nameof(RoomPlayerWorld.JumpFromWater)), PatchJump);
        }

        private void PatchJump(ILContext il)
        {
            var cursor = new ILCursor(il);
            cursor.GotoNext(MoveType.After, instruction =>
                instruction.MatchCallOrCallvirt<RoomPlayerWorld>(nameof(RoomPlayerWorld.FirstWaterAt)));
            if (!cursor.Next.MatchStloc(out int waterLocal))
                throw new InvalidOperationException("The original selected water local was not found.");
            cursor.GotoNext(MoveType.Before, instruction =>
                instruction.MatchCallOrCallvirt<RoomPlayerWorld>(nameof(RoomPlayerWorld.Jump)));
            var call = cursor.Next;
            var skipJump = cursor.DefineLabel();
            cursor.Index++;
            cursor.MarkLabel(skipJump);
            cursor.Goto(call.Previous, MoveType.Before);
            if (!cursor.Next.MatchLdarg(0))
                throw new InvalidOperationException("The original surface Jump receiver was not found.");
            cursor.MoveAfterLabels();
            cursor.Emit(OpCodes.Ldloc, waterLocal);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.EmitDelegate<Func<ISourceWater, RoomPlayerWorld, bool>>(CanJump);
            cursor.Emit(OpCodes.Brfalse, skipJump);
        }

        private bool CanJump(ISourceWater water, RoomPlayerWorld world)
            => !ReferenceEquals(world.EngineContext, engine) ||
               !(water is ColoredWater colored) || colored.CanJumpOnSurface;

        public void Dispose()
        {
            jump?.Dispose();
            jump = null;
        }
    }
}
