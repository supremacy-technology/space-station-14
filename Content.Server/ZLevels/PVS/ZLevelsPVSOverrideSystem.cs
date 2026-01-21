using Robust.Server.GameStates;

namespace Content.Server.ZLevels.PVS;

public sealed partial class ZLevelsPVSOverrideSystem : EntitySystem
{
    [Dependency] private readonly PvsOverrideSystem _pvs = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ZLevelsPVSOverrideComponent, ComponentStartup>(OnPvsStartup);
        SubscribeLocalEvent<ZLevelsPVSOverrideComponent, ComponentShutdown>(OnPvsShutdown);
    }

    private void OnPvsShutdown(Entity<ZLevelsPVSOverrideComponent> ent, ref ComponentShutdown args)
    {
        _pvs.RemoveGlobalOverride(ent);
    }

    private void OnPvsStartup(Entity<ZLevelsPVSOverrideComponent> ent, ref ComponentStartup args)
    {
        _pvs.AddGlobalOverride(ent);
    }
}
