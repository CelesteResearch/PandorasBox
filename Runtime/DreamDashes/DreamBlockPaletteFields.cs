using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Celeste.Core.Display;
using Celeste.Core.Framework.Components;
using Celeste.Core.Scheduling;
using Celeste.Game.Entities.Platforms.Dream;
using Celeste.Game.Execution;
using MonoMod.RuntimeDetour;

namespace Celeste.Mods.PandorasBox.Runtime.DreamDashes
{
    internal static class DreamBlockPaletteFields
    {
        private static readonly Func<PackedColor>[] Readers = BindReaders();
        private static readonly Action<PackedColor>[] Writers = BindWriters();
        private static ConditionalWeakTable<ISourceEngineContext, Palette> palettes = new();
        private static Palette initial;
        private static Palette current;
        private static ISourceEngineContext currentEngine;
        private static Hook updateHook;
        private static Hook drawHook;
        private static int owners;
        private static int depth;
        private static int generation;

        internal static void Register(ISourceEngineContext engine)
        {
            if (owners == 0)
            {
                initial = new Palette();
                initial.Read();
                current = new Palette(initial);
                currentEngine = engine;
                palettes.Add(engine, current);
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                try
                {
                    updateHook = new Hook(typeof(GameRuntime).GetMethod("Update", flags),
                        (Action<Action<GameRuntime, SourceGameTime>, GameRuntime, SourceGameTime>)Update);
                    drawHook = new Hook(typeof(GameRuntime).GetMethod("Draw", flags),
                        (Action<Action<GameRuntime, SourceGameTime>, GameRuntime, SourceGameTime>)Draw);
                }
                catch
                {
                    updateHook?.Dispose();
                    drawHook?.Dispose();
                    Clear();
                    throw;
                }
            }

            owners++;
        }

        internal static void Unregister(ISourceEngineContext engine)
        {
            if (--owners != 0) return;
            drawHook.Dispose();
            updateHook.Dispose();
            // Original Unload removes hooks but does not reset the four fields.
            Clear();
        }

        private static void Clear()
        {
            palettes = new ConditionalWeakTable<ISourceEngineContext, Palette>();
            initial = null;
            current = null;
            currentEngine = null;
            updateHook = null;
            drawHook = null;
            depth = 0;
            generation++;
        }

        private static void Update(Action<GameRuntime, SourceGameTime> original, GameRuntime game,
            SourceGameTime time)
        {
            using (Enter(game.Process)) original(game, time);
        }

        private static void Draw(Action<GameRuntime, SourceGameTime> original, GameRuntime game,
            SourceGameTime time)
        {
            using (Enter(game.Process)) original(game, time);
        }

        internal static Scope Enter(ISourceEngineContext engine)
        {
            // Scene teardown may still call an existing controller after its module unloaded.
            if (owners == 0) return new Scope(null, false, -1);
            var previous = currentEngine;
            bool restore = depth != 0 && !ReferenceEquals(previous, engine);
            depth++;
            if (!ReferenceEquals(currentEngine, engine)) Switch(engine);
            return new Scope(previous, restore, generation);
        }

        private static void Switch(ISourceEngineContext engine)
        {
            // Capture actual private fields, including writes made by other Mods.
            current.Read();
            current = palettes.GetValue(engine, CreatePalette);
            currentEngine = engine;
            current.Write();
        }

        private static Palette CreatePalette(ISourceEngineContext _) => new(initial);

        internal static void Write(PackedColor activeBack, PackedColor disabledBack, PackedColor activeLine,
            PackedColor disabledLine)
        {
            Writers[0](activeBack);
            Writers[1](disabledBack);
            Writers[2](activeLine);
            Writers[3](disabledLine);
        }

        private static FieldInfo Field(int index)
        {
            string name = index switch
            {
                0 => "activeBackColor",
                1 => "disabledBackColor",
                2 => "activeLineColor",
                _ => "disabledLineColor"
            };
            var field = typeof(OriginalDreamBlockState).GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            if (field == null || field.IsInitOnly || field.FieldType != typeof(PackedColor))
                throw new InvalidOperationException("Original mutable DreamBlock palette field is missing: " + name);
            return field;
        }

        private static Func<PackedColor>[] BindReaders()
        {
            var readers = new Func<PackedColor>[4];
            for (int i = 0; i < readers.Length; i++)
            {
                var method = new DynamicMethod("ReadDreamBlockColor" + i, typeof(PackedColor), Type.EmptyTypes,
                    typeof(DreamBlockPaletteFields).Module, true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldsfld, Field(i));
                il.Emit(OpCodes.Ret);
                readers[i] = (Func<PackedColor>)method.CreateDelegate(typeof(Func<PackedColor>));
            }

            return readers;
        }

        private static Action<PackedColor>[] BindWriters()
        {
            var writers = new Action<PackedColor>[4];
            for (int i = 0; i < writers.Length; i++)
            {
                var method = new DynamicMethod("WriteDreamBlockColor" + i, typeof(void), new[] { typeof(PackedColor) },
                    typeof(DreamBlockPaletteFields).Module, true);
                var il = method.GetILGenerator();
                il.Emit(OpCodes.Ldarg_0);
                il.Emit(OpCodes.Stsfld, Field(i));
                il.Emit(OpCodes.Ret);
                writers[i] = (Action<PackedColor>)method.CreateDelegate(typeof(Action<PackedColor>));
            }

            return writers;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly ISourceEngineContext previous;
            private readonly bool restore;
            private readonly int generation;

            internal Scope(ISourceEngineContext previous, bool restore, int generation)
            {
                this.previous = previous;
                this.restore = restore;
                this.generation = generation;
            }

            public void Dispose()
            {
                // A module reload can replace the hook lifetime inside the original callback.
                if (generation != DreamBlockPaletteFields.generation) return;
                depth--;
                if (restore) Switch(previous);
            }
        }

        private sealed class Palette
        {
            private PackedColor activeBack;
            private PackedColor disabledBack;
            private PackedColor activeLine;
            private PackedColor disabledLine;

            internal Palette()
            {
            }

            internal Palette(Palette original)
            {
                activeBack = original.activeBack;
                disabledBack = original.disabledBack;
                activeLine = original.activeLine;
                disabledLine = original.disabledLine;
            }

            internal void Read()
            {
                activeBack = Readers[0]();
                disabledBack = Readers[1]();
                activeLine = Readers[2]();
                disabledLine = Readers[3]();
            }

            internal void Write() => DreamBlockPaletteFields.Write(activeBack, disabledBack, activeLine, disabledLine);
        }
    }
}
