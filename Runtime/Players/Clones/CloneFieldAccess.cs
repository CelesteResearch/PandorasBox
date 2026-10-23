using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Celeste.Mods.PandorasBox.Runtime.Players.Clones;

internal static class CloneFieldAccess
{
    public static Func<T, TValue> Getter<T, TValue>(string name)
    {
        var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var method = new DynamicMethod("get_" + name, typeof(TValue), new[] { typeof(T) }, typeof(CloneFieldAccess),
            true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, field);
        il.Emit(OpCodes.Ret);
        return (Func<T, TValue>)method.CreateDelegate(typeof(Func<T, TValue>));
    }

    public static Action<T, TValue> Setter<T, TValue>(string name)
    {
        var field = typeof(T).GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        var method = new DynamicMethod("set_" + name, typeof(void), new[] { typeof(T), typeof(TValue) },
            typeof(CloneFieldAccess), true);
        var il = method.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Stfld, field);
        il.Emit(OpCodes.Ret);
        return (Action<T, TValue>)method.CreateDelegate(typeof(Action<T, TValue>));
    }
}
