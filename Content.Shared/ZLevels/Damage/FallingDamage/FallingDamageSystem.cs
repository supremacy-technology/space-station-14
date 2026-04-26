using Content.Shared.Damage.Systems;

namespace Content.Shared.ZLevels.Damage.FallingDamage;

public sealed class FallingDamageSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FallingDamageComponent, ZFellOnMeEvent>(OnFallOnMe);
    }

    private void OnFallOnMe(Entity<FallingDamageComponent> ent, ref ZFellOnMeEvent args)
    {
        _damageable.TryChangeDamage(args.Fallen, ent.Comp.Damage * args.Speed);
    }
}
