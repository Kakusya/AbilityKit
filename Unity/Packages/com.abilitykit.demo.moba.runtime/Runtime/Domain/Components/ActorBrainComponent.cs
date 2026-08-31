using Entitas;
using Entitas.CodeGeneration.Attributes;

namespace AbilityKit.Demo.Moba.Components
{
    [Actor]
    public sealed class ActorBrainComponent : IComponent
    {
        public int BrainId;
        public int OwnerActorId;
        public int SourceKind;
        public int SourceId;
        public long BehaviorInstanceId;
    }
}
