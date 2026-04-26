using Content.Shared.ZLevels.Core.EntitySystems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ZLevels.Core.Components;

/// <summary>
/// Allows entity to see through Z-levels
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true), UnsavedComponent, Access(typeof(SharedZLevelsSystem))]
public sealed partial class ZLevelViewerComponent : Component
{
    public HashSet<EntityUid> Eyes = new();

    /// <summary>
    /// We can look at 1 z-level up.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LookUp = false;

    [DataField]
    public EntProtoId ActionProto = "ActionToggleLookUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelActionEntity;
}
