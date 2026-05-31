using Waypoint.Importer;

if (args.Length == 0)
{
    Console.Error.WriteLine("""
        Usage:
          dump --plane-base <url> --plane-key <key> --workspace <slug> --out <dir>
          load --dump <dir> --waypoint-db <connStr> [--mode dry-run|execute]
        """);
    return 2;
}

return args[0] switch
{
    "dump" => await DumpCommand.RunAsync(
        Arg("--plane-base") ?? throw new ArgumentException("--plane-base required"),
        Arg("--plane-key") ?? throw new ArgumentException("--plane-key required"),
        Arg("--workspace") ?? throw new ArgumentException("--workspace required"),
        Arg("--out") ?? "./plane-dump"),
    "load" => await LoadCommand.RunAsync(
        Arg("--dump") ?? throw new ArgumentException("--dump required"),
        Arg("--waypoint-db") ?? throw new ArgumentException("--waypoint-db required"),
        (Arg("--mode") ?? "dry-run") == "dry-run"),
    _ => 2,
};

string? Arg(string name)
{
    for (var i = 0; i < args.Length - 1; i++)
        if (args[i] == name) return args[i + 1];
    return null;
}
