using Project.CLI.Core;
using Project.Infra.Remote;

namespace Project.CLI.Strategies;

/// <summary>
/// Strategy 3: Infrastructure / System commands.
/// Handles persistence (saving/loading), remote API integrations, progress reporting, and session control.
/// </summary>
public class InfrastructureCommandStrategy : ICommandStrategy
{
    public string StrategyName => "Infrastructure System Strategy";
    public string Category => "Infrastructure";
    public string Description => "Persistence storage, Remote REST API synchronizations, and session control.";

    public IReadOnlyList<Command> GetCommands() =>
    [
        new DelegateCommand(
            "save",
            "Persists the current game and character state into the JSON repository.",
            "save [save_name]",
            Category,
            ExecuteSave
        ),
        new DelegateCommand(
            "load",
            "Restores a previously saved state from the JSON repository.",
            "load [save_name]",
            Category,
            ExecuteLoad
        ),
        new DelegateCommand(
            "api-import",
            "Imports character profile and weapon from remote REST API with visual progress tracking.",
            "api-import <character_id> [weapon_id]",
            Category,
            ExecuteApiImport,
            aliases: ["import"]
        ),
        new DelegateCommand(
            "api-query",
            "Fetches and displays live character data from remote REST API.",
            "api-query <character_id>",
            Category,
            ExecuteApiQuery
        ),
        new DelegateCommand(
            "status",
            "Displays current session diagnostics, registered counts, and repository status.",
            "status",
            Category,
            ExecuteStatus
        ),
        new DelegateCommand(
            "exit",
            "Exits the current CLI interactive session.",
            "exit",
            Category,
            (args, ctx) =>
            {
                ctx.ShouldExit = true;
                ctx.Renderer.RenderStatus("Exiting CLI session.", true);
                return CommandResult.Ok("Exit requested.");
            },
            aliases: ["quit", "q"]
        )
    ];

    private static CommandResult ExecuteSave(Args args, CommandExecutionContext ctx)
    {
        var name = args.GetArg(0);
        if (string.IsNullOrWhiteSpace(name))
        {
            name = $"AutoSave_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
        }

        var save = ctx.Game.ExportSave(name);
        ctx.Repository.SaveGameState(name, save);
        ctx.Renderer.RenderStatus($"Game state successfully saved as '{name}' in repository.", true);
        return CommandResult.Ok($"Saved state '{name}'.", save);
    }

    private static CommandResult ExecuteLoad(Args args, CommandExecutionContext ctx)
    {
        var name = args.GetArg(0);
        if (string.IsNullOrWhiteSpace(name))
        {
            var saves = ctx.Repository.GetGameSaveNames().ToList();
            if (saves.Count == 0)
            {
                ctx.Renderer.RenderStatus("No saved states found in repository.", false);
                return CommandResult.Fail("No saves available.");
            }
            ctx.Renderer.RenderLine("Available saves: " + string.Join(", ", saves));
            name = ctx.PromptReader("Enter save name to restore: ");
        }

        var loaded = ctx.Repository.LoadGameState(name);
        if (loaded == null)
        {
            ctx.Renderer.RenderStatus($"Save '{name}' not found.", false);
            return CommandResult.Fail($"Save '{name}' not found.");
        }

        ctx.Game.ImportSave(loaded);
        ctx.Renderer.RenderStatus($"Restored state '{name}' ({loaded.Characters?.Count ?? 0} characters loaded).", true);
        return CommandResult.Ok($"Loaded state '{name}'.", loaded);
    }

    private static CommandResult ExecuteApiImport(Args args, CommandExecutionContext ctx)
    {
        var charId = args.GetArg(0);
        if (string.IsNullOrWhiteSpace(charId))
        {
            charId = ctx.PromptReader("Remote character ID (e.g., raiden, diluc): ");
            if (string.IsNullOrWhiteSpace(charId))
                return CommandResult.Fail("Character ID cannot be empty.");
        }

        var weaponId = args.GetArg(1);

        ctx.Renderer.RenderLine($"Initiating REST API import for '{charId}'...");

        // Progress bar visual stages
        ctx.Renderer.RenderProgressBar(1, 4, "Connecting to remote REST endpoint");
        var remoteChar = ctx.ApiClient.GetCharacterAsync(charId).GetAwaiter().GetResult();
        if (remoteChar == null)
        {
            ctx.Renderer.RenderStatus($"Remote character '{charId}' not found on server.", false);
            return CommandResult.Fail($"Character '{charId}' not found.");
        }

        ctx.Renderer.RenderProgressBar(2, 4, $"Parsing remote profile [{remoteChar.Name}]");
        var localChar = RemoteDataMapper.ToDomainCharacter(remoteChar);

        if (!string.IsNullOrWhiteSpace(weaponId))
        {
            ctx.Renderer.RenderProgressBar(3, 4, $"Fetching weapon [{weaponId}]");
            var remoteWeapon = ctx.ApiClient.GetWeaponAsync(weaponId).GetAwaiter().GetResult();
            if (remoteWeapon != null)
            {
                var localWeapon = RemoteDataMapper.ToDomainItem(remoteWeapon);
                ctx.Game.CreateItem(localWeapon.Name, localWeapon.HpMod, localWeapon.PowerMod);
                localChar.AddItem(localWeapon);
            }
        }
        else
        {
            ctx.Renderer.RenderProgressBar(3, 4, "Skipping weapon import");
        }

        ctx.Renderer.RenderProgressBar(4, 4, "Registering domain entities");
        ctx.Game.CreateCharacter(localChar.Name, localChar.BaseHealth, localChar.BasePower, localChar.BaseResistance);

        ctx.Renderer.RenderStatus($"Imported remote entity '{localChar.Name}' into local catalog.", true);
        return CommandResult.Ok($"Imported {localChar.Name}.", localChar);
    }

    private static CommandResult ExecuteApiQuery(Args args, CommandExecutionContext ctx)
    {
        var charId = args.GetArg(0);
        if (string.IsNullOrWhiteSpace(charId))
        {
            charId = ctx.PromptReader("Remote character ID to query: ");
            if (string.IsNullOrWhiteSpace(charId))
                return CommandResult.Fail("Character ID cannot be empty.");
        }

        var data = ctx.ApiClient.GetCharacterAsync(charId).GetAwaiter().GetResult();
        if (data == null)
        {
            ctx.Renderer.RenderStatus($"Remote character '{charId}' not found.", false);
            return CommandResult.Fail("Character not found.");
        }

        var lines = new[]
        {
            $"Remote Name:    {data.Name}",
            $"Vision/Element: {data.Vision}",
            $"Weapon Type:    {data.Weapon}",
            $"Nation:         {data.Nation}",
            $"Rarity:         {data.Rarity} Stars",
            $"Description:    {data.Description}"
        };

        ctx.Renderer.RenderBox($"REMOTE REST API QUERY: {data.Name.ToUpperInvariant()}", lines);
        return CommandResult.Ok($"Queried remote character {data.Name}.", data);
    }

    private static CommandResult ExecuteStatus(Args args, CommandExecutionContext ctx)
    {
        var savesCount = ctx.Repository.GetGameSaveNames().Count();
        var charsCount = ctx.Game.Characters.Count;
        var itemsCount = ctx.Game.Items.Count;
        var abCount = ctx.Game.Abilities.Count;

        var lines = new[]
        {
            $"Active Characters: {charsCount}",
            $"Inventory Items:   {itemsCount}",
            $"Grimoire Skills:   {abCount}",
            $"Repository Saves:  {savesCount}",
            $"System Timestamp:  {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC"
        };

        ctx.Renderer.RenderBox("SYSTEM DIAGNOSTICS & METRICS", lines);
        return CommandResult.Ok("Status displayed.");
    }
}
