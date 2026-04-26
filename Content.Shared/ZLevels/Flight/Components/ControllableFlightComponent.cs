using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.ZLevels.Flight.Components;

/// <summary>
/// Allows an entity to control its own flight status
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true),
 Access(typeof(SharedZFlightSystem))]
public sealed partial class ControllableFlightComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "ActionZFlightUp";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelUpActionEntity;

    [DataField]
    public EntProtoId DownActionProto = "ActionZFlightDown";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelDownActionEntity;

    [DataField]
    public EntProtoId ToggleActionProto = "ActionZFlightToggle";

    [DataField, AutoNetworkedField]
    public EntityUid? ZLevelToggleActionEntity;

    [DataField]
    public TimeSpan? StartFlightDoAfter;
}
