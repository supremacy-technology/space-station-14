using Content.Server.ZLevels.Core;
using Content.Server.Administration;
using Content.Shared.ZLevels.Core.Components;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.ZLevels.Mapping.Commands;

[AdminCommand(AdminFlags.Server | AdminFlags.Mapping)]
public sealed class DeleteZNetworkCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly ZLevelsSystem _zLevels = default!;

    public override string Command => "znetwork-delete";
    public override string Description => "Delete all maps into selected zNetwork + zNetwork entity";

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        var options = new List<CompletionOption>();
        var query = _entities.EntityQueryEnumerator<ZLevelsNetworkComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out var zLevelComp, out var meta))
        {
            options.Add(new CompletionOption(_entities.GetNetEntity(uid).ToString(), meta.EntityName));
        }
        return CompletionResult.FromHintOptions(options, "zNetwork net entity");
    }

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteError("Wrong arguments count.");
            return;
        }

        if (!NetEntity.TryParse(args[0], out var targetNet) ||
            !_entities.TryGetEntity(targetNet, out var target))
        {
            shell.WriteError($"Unable to find entity {args[0]}");
            return;
        }

        if (!_entities.HasComponent<ZLevelsNetworkComponent>(target))
        {
            shell.WriteError($"Target entity doesnt have ZLevelsNetworkComponent {args[0]}");
            return;
        }

        _zLevels.DeleteZNetwork(target.Value);

        shell.WriteLine("ZNetwork and all its maps deleted.");
    }
}
