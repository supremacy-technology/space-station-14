using Content.Shared.ZLevels.Core.EntitySystems;

namespace Content.Shared.ZLevels.Ghost;

public abstract class SharedZLevelGhostMoverSystem : EntitySystem
{
    [Dependency] private readonly SharedZLevelsSystem _zLevel = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ZLevelGhostMoverComponent, ZLevelActionUp>(OnZLevelUp);
        SubscribeLocalEvent<ZLevelGhostMoverComponent, ZLevelActionDown>(OnZLevelDown);
    }

    private void OnZLevelDown(Entity<ZLevelGhostMoverComponent> ent, ref ZLevelActionDown args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveDown(ent);
    }

    private void OnZLevelUp(Entity<ZLevelGhostMoverComponent> ent, ref ZLevelActionUp args)
    {
        if (args.Handled)
            return;

        args.Handled = _zLevel.TryMoveUp(ent);
    }
}
