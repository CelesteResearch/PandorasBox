using Celeste.Core.Components;
using Celeste.Core.Reflection.Runtime;
using Celeste.Scene.Entities;
using Celeste.Scene.Tracking.Attributes;
using Celeste.Simulation.Gameplay.Holdables;

namespace Celeste.Mods.PandorasBox.Components.Holdables
{
    [SceneTracked(false)]
    [RuntimeTypeAlias("Celeste.Mod.PandorasBox.InteractibleHoldable")]
    public class InteractibleHoldable : SourceComponentState
    {
        public delegate void InteractionDelegate(HoldableState holdable);

        public InteractionDelegate OnHoldUpdate;
        public InteractionDelegate OnHoldPickup;
        public InteractionDelegate OnHoldRelease;

        public InteractibleHoldable(SceneEntityState entity) : base(true, true)
        {
        }
    }
}
