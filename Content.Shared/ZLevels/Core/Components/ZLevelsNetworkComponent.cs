using Content.Shared.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.ZLevels.Core.Components;

/// <summary>
/// Tracker that tracks all maps added to the zLevel network. Usually, entity in Nullspace,
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedZLevelsSystem))]
public sealed partial class ZLevelsNetworkComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<int, EntityUid?> ZLevels = new();
}
