using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Overlight.App.Terminal.Models;
using Windows.System;

namespace Overlight.App.Terminal;

/// <summary>
/// The terminal emulator window: a real shell (TerminalHostControl,
/// xterm.js over ConPTY) on the left, and the two live panels this
/// feature was asked for on the right — timed loops with a countdown
/// visualizer, and an account switcher that always shows which account
/// is active. The bottom bar is Overlight's own command line for
/// loop/account commands — deliberately separate from the shell pane
/// above it, which owns keyboard focus for whatever's actually running
/// in it. Both panels are also drivable by clicking, driven by the same
/// TerminalServices state the command line mutates.
/// </summary>
public sealed partial class TerminalWindow : Window
{
    public ObservableCollection<string> Transcript { get; } = new();
    public ObservableCollection<TimedLoop> Loops => TerminalServices.LoopScheduler.Loops;
    public ObservableCollection<AccountRow> AccountRows { get; } = new();

    // Kept so the Closed handler can unsubscribe the exact same delegate
    // instances from the shared TerminalServices singletons below —
    // otherwise every open/close cycle would leak a handler pointing at
    // a closed window's UI elements.
    private readonly Action<ClaudeAccount?> _onActiveAccountChanged;
    private readonly System.Collections.Specialized.NotifyCollectionChangedEventHandler _onAccountsCollectionChanged;
    private readonly Action<TimedLoop, string> _onLoopFired;

    public TerminalWindow()
    {
        InitializeComponent();

        Transcript.Add("Overlight command line — type 'help'. This is separate from the shell pane above.");
        Transcript.Add("Loop prompts are not yet wired to a real Claude call (see Terminal/Services/IPromptExecutor.cs) — triggers are simulated.");

        RefreshAccountRows();
        UpdateActiveAccountHeader();

        _onActiveAccountChanged = _ =>
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                RefreshAccountRows();
                UpdateActiveAccountHeader();
            });
        };
        TerminalServices.AccountManager.ActiveAccountChanged += _onActiveAccountChanged;

        _onAccountsCollectionChanged = (_, _) => DispatcherQueue.TryEnqueue(RefreshAccountRows);
        TerminalServices.AccountManager.Accounts.CollectionChanged += _onAccountsCollectionChanged;

        _onLoopFired = (loop, response) =>
        {
            DispatcherQueue.TryEnqueue(() =>
                AppendTranscript($"[loop {loop.ShortId} fired] {response}"));
        };
        TerminalServices.LoopScheduler.LoopFired += _onLoopFired;

        Closed += (_, _) =>
        {
            TerminalServices.AccountManager.ActiveAccountChanged -= _onActiveAccountChanged;
            TerminalServices.AccountManager.Accounts.CollectionChanged -= _onAccountsCollectionChanged;
            TerminalServices.LoopScheduler.LoopFired -= _onLoopFired;
        };
    }

    private void OnCommandInputKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
        {
            return;
        }

        string input = CommandInput.Text;
        CommandInput.Text = string.Empty;

        if (input.Trim().Length == 0)
        {
            return;
        }

        AppendTranscript($"> {input}");

        if (input.Trim().Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            Transcript.Clear();
            return;
        }

        foreach (string line in CommandInterpreter.Run(input))
        {
            AppendTranscript(line);
        }
    }

    private void OnAccountRowClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string label })
        {
            TerminalServices.AccountManager.SwitchTo(label);
        }
    }

    private void AppendTranscript(string line)
    {
        Transcript.Add(line);
        if (Transcript.Count > 500)
        {
            Transcript.RemoveAt(0);
        }

        if (TranscriptList.Items.Count > 0)
        {
            TranscriptList.ScrollIntoView(TranscriptList.Items[^1]);
        }
    }

    private void RefreshAccountRows()
    {
        AccountRows.Clear();
        foreach (ClaudeAccount account in TerminalServices.AccountManager.Accounts)
        {
            AccountRows.Add(new AccountRow
            {
                Account = account,
                IsActive = account == TerminalServices.AccountManager.ActiveAccount,
            });
        }
    }

    private void UpdateActiveAccountHeader()
    {
        ClaudeAccount? active = TerminalServices.AccountManager.ActiveAccount;
        ActiveAccountText.Text = active is null ? "no active account" : $"working in: {active.Label}";
    }
}
