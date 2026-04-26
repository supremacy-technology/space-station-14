using Content.Shared.Popups;
using Content.Shared.Standing;

namespace Content.Shared.ZLevels.Damage.SoftPaws;

public sealed class SoftPawsSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StandingStateSystem _standingState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SoftPawsComponent, ZFallingDamageCalculateEvent>(OnFallingDamageCalculate);
    }

    private void OnFallingDamageCalculate(Entity<SoftPawsComponent> ent, ref ZFallingDamageCalculateEvent args)
    {
        if (_standingState.IsDown(ent.Owner))
            return;

        if (args.Speed <= ent.Comp.MaxSpeedLimit)
        {
            args.DamageMultiplier *= ent.Comp.DamageMultiplier;
            args.StunMultiplier *= ent.Comp.StunMultiplier;

            _popup.PopupPredicted(Loc.GetString("soft-paws"), ent, ent);
        }
        else
        {
            args.DamageMultiplier *= ent.Comp.DamageHardFallMultiplier;
            args.StunMultiplier *= ent.Comp.StunHardFallMultiplier;

            _popup.PopupPredicted(Loc.GetString("soft-paws-too-high"), ent, ent, PopupType.SmallCaution);
        }
    }
}
