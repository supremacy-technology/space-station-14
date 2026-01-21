using Content.Shared.ZLevels.Core.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;

namespace Content.Shared.ZLevels.Core.EntitySystems;

public abstract partial class SharedZLevelsSystem
{
    private void InitializeActivation()
    {
        SubscribeLocalEvent<ZPhysicsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZPhysicsComponent, AnchorStateChangedEvent>(OnAnchorStateChange);
        SubscribeLocalEvent<ZPhysicsComponent, PhysicsBodyTypeChangedEvent>(OnPhysicsBodyTypeChange);
        SubscribeLocalEvent<ZPhysicsComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnAnchorStateChange(Entity<ZPhysicsComponent> ent, ref AnchorStateChangedEvent args)
    {
        CheckActivation(ent);
    }

    private void OnMapInit(Entity<ZPhysicsComponent> ent, ref MapInitEvent args)
    {
        CheckActivation(ent);

        if (!TryComp<ZLevelMapComponent>(Transform(ent).MapUid, out var zLevelMap))
            return;

        ent.Comp.CurrentZLevel = zLevelMap.Depth;
        DirtyField(ent, ent.Comp, nameof(ZPhysicsComponent.CurrentZLevel));
    }

    private void OnPhysicsBodyTypeChange(Entity<ZPhysicsComponent> ent, ref PhysicsBodyTypeChangedEvent args)
    {
        CheckActivation(ent);
    }

    private void OnParentChanged(Entity<ZPhysicsComponent> ent, ref EntParentChangedMessage args)
    {
        CheckActivation(ent);

        if (ZPhyzQuery.TryComp(args.OldParent, out var oldParentZPhys))
            SetZPosition((ent, ent), oldParentZPhys.LocalPosition);
    }

    private void CheckActivation(Entity<ZPhysicsComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        var xform = Transform(ent);

        if (xform.ParentUid != xform.MapUid)
        {
            SetActiveStatus(ent, false);
            return;
        }

        if (xform.Anchored)
        {
            SetActiveStatus(ent, false);
            return;
        }

        if (TryComp<PhysicsComponent>(ent, out var physics))
        {
            if (physics.BodyType == BodyType.Static)
            {
                SetActiveStatus(ent, false);
                return;
            }
        }

        SetActiveStatus(ent, true);
    }

    private void SetActiveStatus(EntityUid ent, bool active)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (active)
            EnsureComp<ActiveZPhysicsComponent>(ent);
        else
            RemComp<ActiveZPhysicsComponent>(ent);
    }
}
