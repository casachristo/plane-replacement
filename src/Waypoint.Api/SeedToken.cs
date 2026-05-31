using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Waypoint.Api.Auth;
using Waypoint.Domain;
using Waypoint.Domain.Entities;
using Waypoint.Domain.Enums;

namespace Waypoint.Api;

/// <summary>
/// CLI mode: `dotnet Waypoint.Api.dll seed-token --label <name> --scopes <comma> --kind service`
/// Generates a new service token, hashes it, persists the row, and prints the full secret once.
/// </summary>
public static class SeedToken
{
    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        var label = ArgValue(args, "--label") ?? "unnamed";
        var scopesArg = ArgValue(args, "--scopes") ?? "*";
        var kindArg = ArgValue(args, "--kind") ?? "service";

        if (!Enum.TryParse<TokenKind>(kindArg, ignoreCase: true, out var kind))
        {
            Console.Error.WriteLine($"Unknown --kind value: {kindArg}");
            return 2;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WaypointDbContext>();
        await db.Database.MigrateAsync();

        var (prefix, fullToken) = TokenHasher.GenerateNew();
        var hash = TokenHasher.Hash(fullToken);
        db.ApiTokens.Add(new ApiToken
        {
            Name = label,
            Prefix = prefix,
            TokenHash = hash,
            Scopes = scopesArg.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            Kind = kind,
        });
        await db.SaveChangesAsync();

        Console.Out.WriteLine(fullToken);
        Console.Error.WriteLine($"# Token created. Prefix: {prefix}. Label: {label}. Scopes: {scopesArg}. Save the line above — it will never be shown again.");
        return 0;
    }

    private static string? ArgValue(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
