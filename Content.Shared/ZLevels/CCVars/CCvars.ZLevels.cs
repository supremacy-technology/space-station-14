using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<float>
        BaseFallingDamage = CVarDef.Create("zlevels.base_falling_damage", 1.5f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingOtherDamage = CVarDef.Create("zlevels.base_falling_other_damage", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingStunTime = CVarDef.Create("zlevels.base_falling_stun_time", 0.1f, CVar.SERVER | CVar.REPLICATED);

    public static readonly CVarDef<float>
        BaseFallingOtherStunTime = CVarDef.Create("zlevels.base_falling_other_stun_time", 0.01f, CVar.SERVER | CVar.REPLICATED);
}
