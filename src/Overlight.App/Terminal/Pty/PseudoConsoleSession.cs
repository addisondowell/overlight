using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static Overlight.App.Terminal.Pty.ConPtyInterop;

namespace Overlight.App.Terminal.Pty;

/// <summary>
/// Spawns a real shell attached to a ConPTY pseudo-console and exposes
/// its raw output as a byte stream. This is the thing that makes
/// TerminalHostControl an actual terminal emulator rather than a themed
/// text box: whatever the shell would print to a real console comes
/// through OutputReceived byte-for-byte, escape sequences included, for
/// xterm.js to interpret.
///
/// Assumes UTF-8 in both directions, matching how Windows Terminal talks
/// to ConPTY — verify against a real shell (PowerShell vs. classic cmd
/// codepage behavior can differ) once this builds on Windows.
/// </summary>
public sealed class PseudoConsoleSession : IDisposable
{
    private const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;

    private IntPtr _pseudoConsoleHandle;
    private IntPtr _attributeListHandle;
    private IntPtr _processHandle;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;
    private bool _disposed;

    public event Action<ReadOnlyMemory<byte>>? OutputReceived;
    public event Action<int>? ProcessExited;

    /// <summary>
    /// Set on the most recent failed step of <see cref="Start"/>, with
    /// enough detail (including the Win32 error code) to actually
    /// diagnose a failure instead of a bare "didn't work" — surfaced by
    /// TerminalHostControl straight into the terminal pane.
    /// </summary>
    public string? LastError { get; private set; }

    /// <summary>
    /// Starts the shell. <paramref name="environmentOverrides"/> is
    /// layered on top of this process's own inherited environment
    /// (which the child would otherwise get verbatim, including
    /// whatever credentials/config paths are already in scope) — this is
    /// the actual mechanism behind per-account isolation in the
    /// Accounts panel; see Terminal/Services/AccountManager.cs.
    /// </summary>
    public bool Start(
        string commandLine, int columns, int rows,
        IReadOnlyDictionary<string, string>? environmentOverrides = null)
    {
        if (!CreatePipe(out SafeFileHandle inputReadSide, out SafeFileHandle inputWriteSide, IntPtr.Zero, 0)
            || !CreatePipe(out SafeFileHandle outputReadSide, out SafeFileHandle outputWriteSide, IntPtr.Zero, 0))
        {
            LastError = $"CreatePipe failed (Win32 error {Marshal.GetLastWin32Error()})";
            return false;
        }

        var size = new COORD { X = (short)columns, Y = (short)rows };
        int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out _pseudoConsoleHandle);
        if (hr != 0)
        {
            LastError = $"CreatePseudoConsole failed (HRESULT 0x{hr:X8})";
            return false;
        }

        // ConPTY duplicates the handles it needs internally — our copies
        // of the "far" ends are no longer needed once creation succeeds.
        inputReadSide.Dispose();
        outputWriteSide.Dispose();

        _inputStream = new FileStream(inputWriteSide, FileAccess.Write);
        _outputStream = new FileStream(outputReadSide, FileAccess.Read);

        if (!TryCreateAttachedProcess(commandLine, environmentOverrides))
        {
            // LastError already set by TryCreateAttachedProcess.
            Dispose();
            return false;
        }

        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));

        return true;
    }

    private bool TryCreateAttachedProcess(
        string commandLine, IReadOnlyDictionary<string, string>? environmentOverrides)
    {
        IntPtr lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        if (lpSize == IntPtr.Zero)
        {
            LastError = "InitializeProcThreadAttributeList size query returned zero";
            return false;
        }

        _attributeListHandle = Marshal.AllocHGlobal(lpSize);

        if (!InitializeProcThreadAttributeList(_attributeListHandle, 1, 0, ref lpSize))
        {
            LastError = $"InitializeProcThreadAttributeList failed (Win32 error {Marshal.GetLastWin32Error()})";
            return false;
        }

        if (!UpdateProcThreadAttribute(
                _attributeListHandle,
                0,
                (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                _pseudoConsoleHandle,
                (IntPtr)IntPtr.Size,
                IntPtr.Zero,
                IntPtr.Zero))
        {
            LastError = $"UpdateProcThreadAttribute failed (Win32 error {Marshal.GetLastWin32Error()})";
            return false;
        }

        var startupInfo = new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO(),
            lpAttributeList = _attributeListHandle,
        };
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        IntPtr environmentBlock = BuildEnvironmentBlock(environmentOverrides);

        bool created;
        try
        {
            created = CreateProcess(
                null,
                new StringBuilder(commandLine),
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                (uint)EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT,
                environmentBlock,
                null,
                ref startupInfo,
                out PROCESS_INFORMATION processInfo);

            if (!created)
            {
                LastError = $"CreateProcess('{commandLine}') failed (Win32 error {Marshal.GetLastWin32Error()})";
                return false;
            }

            _processHandle = processInfo.hProcess;
            CloseHandle(processInfo.hThread);
            return true;
        }
        finally
        {
            if (environmentBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }
        }
    }

    /// <summary>
    /// Builds a Win32 environment block (KEY=VALUE\0 pairs, double-null
    /// terminated) starting from this process's own inherited
    /// environment with <paramref name="overrides"/> layered on top. A
    /// null/empty overrides set still returns a real block (rather than
    /// IntPtr.Zero / "inherit verbatim") so behavior is identical either
    /// way — only the override step differs.
    /// </summary>
    private static IntPtr BuildEnvironmentBlock(IReadOnlyDictionary<string, string>? overrides)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
            {
                env[key] = value;
            }
        }

        if (overrides is not null)
        {
            foreach ((string key, string value) in overrides)
            {
                env[key] = value;
            }
        }

        var block = new StringBuilder();
        foreach (KeyValuePair<string, string> pair in env.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
        {
            block.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }
        block.Append('\0');

        return Marshal.StringToHGlobalUni(block.ToString());
    }

    public void WriteInput(ReadOnlySpan<byte> data)
    {
        if (_inputStream is null)
        {
            return;
        }

        _inputStream.Write(data);
        _inputStream.Flush();
    }

    public void WriteInput(string text) => WriteInput(Encoding.UTF8.GetBytes(text));

    public void Resize(int columns, int rows)
    {
        if (_pseudoConsoleHandle != IntPtr.Zero)
        {
            ResizePseudoConsole(_pseudoConsoleHandle, new COORD { X = (short)columns, Y = (short)rows });
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        if (_outputStream is null)
        {
            return;
        }

        byte[] buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int read = await _outputStream.ReadAsync(buffer, cancellationToken);
                if (read == 0)
                {
                    break; // shell process exited and closed the pipe
                }

                OutputReceived?.Invoke(new ReadOnlyMemory<byte>(buffer, 0, read));
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on Dispose.
        }
        catch (IOException)
        {
            // Pipe broke because the child process exited.
        }

        ProcessExited?.Invoke(0);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _readLoopCts?.Cancel();
        try
        {
            _readLoopTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Best-effort shutdown — don't let a slow reader block Dispose.
        }

        if (_pseudoConsoleHandle != IntPtr.Zero)
        {
            ClosePseudoConsole(_pseudoConsoleHandle);
            _pseudoConsoleHandle = IntPtr.Zero;
        }

        _inputStream?.Dispose();
        _outputStream?.Dispose();

        if (_attributeListHandle != IntPtr.Zero)
        {
            DeleteProcThreadAttributeList(_attributeListHandle);
            Marshal.FreeHGlobal(_attributeListHandle);
            _attributeListHandle = IntPtr.Zero;
        }

        if (_processHandle != IntPtr.Zero)
        {
            CloseHandle(_processHandle);
            _processHandle = IntPtr.Zero;
        }
    }
}
