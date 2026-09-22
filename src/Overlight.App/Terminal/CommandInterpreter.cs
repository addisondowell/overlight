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
                    .Select(a => $"{(a == TerminalServices.AccountManager.ActiveAccount ? "*" : " ")} {a.Label}" + (a.Note is null ? "" : $"  — {a.Note}"))
                    .ToList();

            default:
                return new[] { $"unknown account subcommand: {sub}" };
        }
    }

    private static IReadOnlyList<string> WhoAmI()
    {
        ClaudeAccount? active = TerminalServices.AccountManager.ActiveAccount;
        return new[] { active is null ? "no active account set" : $"working in: {active.Label}" };
    }
}
