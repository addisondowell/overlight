using System.Collections.ObjectModel;
using System.Text.Json;
using Overlight.App.Terminal.Models;

namespace Overlight.App.Terminal.Services;

/// <summary>
/// Tracks which locally-defined account label the user is currently
/// "in" and persists the list + active selection to a small JSON file
/// under %LOCALAPPDATA%\Overlight. This is bookkeeping only — it does
/// not authenticate anything. See Models/ClaudeAccount.cs for why that
/// line is drawn here.
/// </summary>
public sealed class AccountManager
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Overlight", "accounts.json");

    public ObservableCollection<ClaudeAccount> Accounts { get; } = new();

    private ClaudeAccount? _activeAccount;
    public ClaudeAccount? ActiveAccount
    {
        get => _activeAccount;
        private set
        {
            if (_activeAccount == value)
            {
                return;
            }

            _activeAccount = value;
            ActiveAccountChanged?.Invoke(value);
        }
    }

    public event Action<ClaudeAccount?>? ActiveAccountChanged;

    public AccountManager()
    {
        Load();
    }

    public ClaudeAccount Add(string label, string? note = null)
    {
        var account = new ClaudeAccount { Label = label, Note = note };
        Accounts.Add(account);

        // First account added becomes active automatically — otherwise
        // "which account am I in" has no answer until the user thinks
        // to switch.
        ActiveAccount ??= account;

        Save();
        return account;
    }

    public bool SwitchTo(string labelOrShortId)
    {
        ClaudeAccount? match = Find(labelOrShortId);
        if (match is null)
        {
            return false;
        }

        ActiveAccount = match;
        Save();
        return true;
    }

    public ClaudeAccount? Find(string labelOrShortId) => Accounts.FirstOrDefault(a =>
        a.Label.Equals(labelOrShortId, StringComparison.OrdinalIgnoreCase)
        || a.Id.ToString("N").StartsWith(labelOrShortId, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Sets one environment-variable override for an account's spawned
    /// shell (see ClaudeAccount.EnvironmentOverrides). If this account is
    /// currently active, raises ActiveAccountChanged so an open terminal
    /// restarts its shell with the new value applied immediately —
    /// setting the override should not require closing and reopening the
    /// terminal window to take effect.
    /// </summary>
    public bool SetEnvironmentOverride(string labelOrShortId, string key, string value)
    {
        ClaudeAccount? account = Find(labelOrShortId);
        if (account is null)
        {
            return false;
        }

        account.EnvironmentOverrides[key] = value;
        Save();
        if (account == ActiveAccount)
        {
            ActiveAccountChanged?.Invoke(account);
        }

        return true;
    }

    public bool UnsetEnvironmentOverride(string labelOrShortId, string key)
    {
        ClaudeAccount? account = Find(labelOrShortId);
        if (account is null || !account.EnvironmentOverrides.Remove(key))
        {
            return false;
        }

        Save();
        if (account == ActiveAccount)
        {
            ActiveAccountChanged?.Invoke(account);
        }

        return true;
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(StorePath))
            {
                return;
            }

            string json = File.ReadAllText(StorePath);
            StoredState? state = JsonSerializer.Deserialize<StoredState>(json);
            if (state is null)
            {
                return;
            }

            foreach (ClaudeAccount account in state.Accounts)
            {
                Accounts.Add(account);
            }

            _activeAccount = Accounts.FirstOrDefault(a => a.Id == state.ActiveAccountId);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Corrupt or unreadable store — start clean rather than crash
            // the app over saved account labels.
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StorePath)!);
            var state = new StoredState(Accounts.ToList(), ActiveAccount?.Id);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-fatal: account switching still works for the rest of
            // this session even if persisting to disk failed.
        }
    }

    private sealed record StoredState(List<ClaudeAccount> Accounts, Guid? ActiveAccountId);
}
