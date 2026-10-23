using Celeste.Core.Reflection.Runtime;

namespace Celeste.Mods.PandorasBox.Triggers.Activation
{
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.EntityActivator+ActivationModes")]
    public enum EntityActivationMode
    {
        OnEnter,
        OnStay,
        OnLeave,
        OnFlagActive,
        OnFlagInactive,
        OnFlagActivated,
        OnFlagDeactivated,
        OnUpdate,
        OnCameraMoved,
        OnAwake
    }
}
