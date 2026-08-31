using AbilityKit.Game.Battle.Entity;
using UnityEngine;

namespace AbilityKit.Game.Flow
{
    /// <summary>
    /// Creates shell GameObjects for battle entities.
    /// All implementations must support <see cref="CreateShellGameObject(int,int,BattleEntityKind)"/>.
    /// </summary>
    public interface IBattleViewShellLoader
    {
        /// <summary>
        /// Creates a shell GameObject. Implementations should dispatch to the kind-aware overload.
        /// </summary>
        GameObject CreateShellGameObject(int actorId, int modelId);

        /// <summary>
        /// Creates a shell GameObject for a specific entity kind.
        /// </summary>
        GameObject CreateShellGameObject(int actorId, int modelId, BattleEntityKind kind);
    }
}
