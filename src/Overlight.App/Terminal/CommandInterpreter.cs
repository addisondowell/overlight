using Overlight.App.Terminal.Models;
using Overlight.App.Terminal.Services;

namespace Overlight.App.Terminal;

/// <summary>
/// Parses lines typed into the terminal input into loop/account
/// operations. The two side panels (loop countdowns, account list) are
/// the primary UI — typed commands are the other way to drive the same
/// state, per the app's "reduce primitives, don't remove capability"
/// stance from docs/DESIGN.md.
/// </summary>
public static class CommandInterpreter
{
    public static IReadOnlyList<string> Run(string rawInput)
    {
        string input = rawInput.Trim();
        if (input.Length == 0)
        {
            return Array.Empty<string>();
        }

        string[] parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].ToLowerInvariant();
        string rest = parts.Length > 1 ? parts[1] : string.Empty;

        return command switch
        {
            "help" => Help(),
            "loop" => Loop(rest),
            "account" or "acct" => Account(rest),
            "whoami" => WhoAmI(),
            _ => new[] { $"unknown command: {command} (try 'help')" },
        };
    }

    private static IReadOnlyList<string> Help() => new[]
    {
        "loop add <interval> <prompt>   e.g. loop add 5m check for new PRs and summarize",
        "loop list",
        "loop remove <id>",
        "account add <label> [note]",
        "account switch <label-or-id>",
        "account list",
        "account setenv <label-or-id> <KEY>=<VALUE>   — isolate this account's shell env",
        "account unsetenv <label-or-id> <KEY>",
        "account showenv <label-or-id>  — values that look like secrets are redacted",
        "whoami                         — which account is active",
        "clear                          — handled by the input box, clears the transcript",
    };

    private static IReadOnlyList<string> Loop(string rest)
    {
        string[] parts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new[] { "usage: loop <add|list|remove> ..." };
        }

        string sub = parts[0].ToLowerInvariant();
        string args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (sub)
        {
            case "add":
                string[] addParts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                if (addParts.Length < 2)
                {
                    return new[] { "usage: loop add <interval e.g. 5m|1h30m> <prompt>" };
                }

                if (!IntervalParser.TryParse(addParts[0], out TimeSpan interval))
                {
                    return new[] { $"couldn't parse interval '{addParts[0]}' — try things like 30s, 5m, 1h30m" };
                }

                TimedLoop loop = TerminalServices.LoopScheduler.Add(addParts[1], interval);
                return new[] { $"loop {loop.ShortId} added, firing every {interval} — next in {interval}" };

            case "list":
                if (TerminalServices.LoopScheduler.Loops.Count == 0)
                {
                    return new[] { "no active loops" };
                }

                return TerminalServices.LoopScheduler.Loops
                    .Select(l => $"{l.ShortId}  every {l.Interval}  next in {l.Remaining:mm\\:ss}  \"{l.Prompt}\"")
                    .ToList();

            case "remove" or "rm":
                if (args.Length == 0)
                {
                    return new[] { "usage: loop remove <id>" };
                }

                return TerminalServices.LoopScheduler.Remove(args)
                    ? new[] { $"loop {args} removed" }
                    : new[] { $"no loop matching '{args}'" };

            default:
                return new[] { $"unknown loop subcommand: {sub}" };
        }
    }

    private static IReadOnlyList<string> Account(string rest)
    {
        string[] parts = rest.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return new[] { "usage: account <add|switch|list> ..." };
        }

        string sub = parts[0].ToLowerInvariant();
        string args = parts.Length > 1 ? parts[1] : string.Empty;

        switch (sub)
        {
            case "add":
                if (args.Length == 0)
                {
                    return new[] { "usage: account add <label> [note]" };
                }

                string[] addParts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                string label = addParts[0];
                string? note = addParts.Length > 1 ? addParts[1] : null;

                ClaudeAccount account = TerminalServices.AccountManager.Add(label, note);
                return new[] { $"account '{account.Label}' added" + (TerminalServices.AccountManager.ActiveAccount == account ? " and made active" : "") };

            case "switch":
                if (args.Length == 0)
                {
                    return new[] { "usage: account switch <label-or-id>" };
                }

                return TerminalServices.AccountManager.SwitchTo(args)
                    ? new[] { $"switched to account '{args}'" }
                    : new[] { $"no account matching '{args}'" };

            case "list":
                if (TerminalServices.AccountManager.Accounts.Count == 0)
                {
                    return new[] { "no accounts configured — try 'account add <label>'" };
                }

                return TerminalServices.AccountManager.Accounts
                    .Select(a =>
                    {
                        string marker = a == TerminalServices.AccountManager.ActiveAccount ? "*" : " ";
                        string note = a.Note is null ? "" : $"  — {a.Note}";
                        string envCount = a.EnvironmentOverrides.Count > 0
                            ? $"  [{a.EnvironmentOverrides.Count} env override(s)]"
                            : "";
                        return $"{marker} {a.Label}{note}{envCount}";
                    })
                    .ToList();

            case "setenv":
                return SetEnv(args);

            case "unsetenv":
                return UnsetEnv(args);

            case "showenv":
                return ShowEnv(args);

            default:
                return new[] { $"unknown account subcommand: {sub}" };
        }
    }

    private static IReadOnlyList<string> SetEnv(string args)
    {
        string[] parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !parts[1].Contains('='))
        {
            return new[] { "usage: account setenv <label-or-id> <KEY>=<VALUE>" };
        }

        string labelOrId = parts[0];
        int eq = parts[1].IndexOf('=');
        string key = parts[1][..eq].Trim();
        string value = parts[1][(eq + 1)..];

        if (key.Length == 0)
        {
            return new[] { "usage: account setenv <label-or-id> <KEY>=<VALUE>" };
        }

        return TerminalServices.AccountManager.SetEnvironmentOverride(labelOrId, key, value)
            ? new[] { $"set {key} for account '{labelOrId}'" + (IsActive(labelOrId) ? " (shell restarting now)" : "") }
            : new[] { $"no account matching '{labelOrId}'" };
    }

    private static IReadOnlyList<string> UnsetEnv(string args)
    {
        string[] parts = args.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return new[] { "usage: account unsetenv <label-or-id> <KEY>" };
        }

        string labelOrId = parts[0];
        string key = parts[1].Trim();

        return TerminalServices.AccountManager.UnsetEnvironmentOverride(labelOrId, key)
            ? new[] { $"unset {key} for account '{labelOrId}'" + (IsActive(labelOrId) ? " (shell restarting now)" : "") }
            : new[] { $"no account matching '{labelOrId}', or it had no override for '{key}'" };
    }

    private static IReadOnlyList<string> ShowEnv(string args)
    {
        if (args.Length == 0)
        {
            return new[] { "usage: account showenv <label-or-id>" };
        }

        ClaudeAccount? account = TerminalServices.AccountManager.Find(args);
        if (account is null)
        {
            return new[] { $"no account matching '{args}'" };
        }

        if (account.EnvironmentOverrides.Count == 0)
        {
            return new[] { $"'{account.Label}' has no environment overrides — its shell inherits Overlight's own environment as-is" };
        }

        return account.EnvironmentOverrides
            .Select(kv => $"{kv.Key}={RedactIfSecret(kv.Key, kv.Value)}")
            .ToList();
    }

    private static bool IsActive(string labelOrId) =>
        TerminalServices.AccountManager.Find(labelOrId) == TerminalServices.AccountManager.ActiveAccount;

    private static readonly string[] SecretMarkers = { "KEY", "TOKEN", "SECRET", "PASSWORD", "PWD" };

    private static string RedactIfSecret(string key, string value)
    {
        bool looksSecret = SecretMarkers.Any(marker =>
            key.Contains(marker, StringComparison.OrdinalIgnoreCase));

        if (!looksSecret || value.Length == 0)
        {
            return looksSecret ? "(redacted)" : value;
        }

        return value.Length <= 4 ? "****" : $"{value[..2]}…{value[^2..]}";
    }

    private static IReadOnlyList<string> WhoAmI()
    {
        ClaudeAccount? active = TerminalServices.AccountManager.ActiveAccount;
        if (active is null)
        {
            return new[] { "no active account set" };
        }

        string envNote = active.EnvironmentOverrides.Count > 0
            ? $" ({active.EnvironmentOverrides.Count} env override(s) applied to its shell)"
            : " (no env overrides — its shell inherits Overlight's own environment as-is)";

        return new[] { $"working in: {active.Label}{envNote}" };
    }
}
