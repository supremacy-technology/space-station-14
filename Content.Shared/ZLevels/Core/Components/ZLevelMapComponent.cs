using Robust.Shared.GameStates;

namespace Content.Shared.ZLevels.Core.Components;

/// <summary>
/// Automatically added to the map when it appears in zLevelNetwork.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UnsavedComponent]
public sealed partial class ZLevelMapComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Depth = 0;
}
