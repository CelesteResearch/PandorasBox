using Celeste.Core.Reflection.Runtime;

namespace Celeste.Mods.PandorasBox.Triggers.Activation
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.EntityActivator+EffectModes")]
    public enum EntityActivationEffect
    {
        ActivateInsideDeactivateOutside,
        ActivateInside,
        ActivateOutside,
        DeactivateInside,
        DeactivateOutside,
        ActivateOnScreenDeactivateOffScreen
    }
}
