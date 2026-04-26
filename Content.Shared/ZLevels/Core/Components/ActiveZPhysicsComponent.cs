using Robust.Shared.GameStates;

namespace Content.Shared.ZLevels.Core.Components;

/// <summary>
/// A marker that indicates entities that can actively move between z-levels.
/// </summary>
[RegisterComponent, NetworkedComponent, UnsavedComponent]
public sealed partial class ActiveZPhysicsComponent : Component;
