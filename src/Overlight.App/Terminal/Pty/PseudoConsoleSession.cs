using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using static Overlight.App.Terminal.Pty.ConPtyInterop;

namespace Overlight.App.Terminal.Pty;

/// <summary>
/// Spawns a real shell attached to a ConPTY pseudo-console and exposes
/// its raw output as a byte stream. This is the thing that makes
/// TerminalGridView an actual terminal emulator rather than a themed
/// text box: whatever the shell would print to a real console comes
/// through OutputReceived byte-for-byte, escape sequences included, for
/// Vt/VtParser to interpret.
///
/// Assumes UTF-8 in both directions, matching how Windows Terminal talks
/// to ConPTY — verify against a real shell (PowerShell vs. classic cmd
/// codepage behavior can differ) once this builds on Windows.
/// </summary>
public sealed class PseudoConsoleSession : IDisposable
{
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

    public bool Start(string commandLine, int columns, int rows)
    {
        if (!CreatePipe(out SafeFileHandle inputReadSide, out SafeFileHandle inputWriteSide, IntPtr.Zero, 0)
            || !CreatePipe(out SafeFileHandle outputReadSide, out SafeFileHandle outputWriteSide, IntPtr.Zero, 0))
        {
            return false;
        }

        var size = new COORD { X = (short)columns, Y = (short)rows };
        int hr = CreatePseudoConsole(size, inputReadSide, outputWriteSide, 0, out _pseudoConsoleHandle);
        if (hr != 0)
        {
            return false;
        }

        // ConPTY duplicates the handles it needs internally — our copies
        // of the "far" ends are no longer needed once creation succeeds.
        inputReadSide.Dispose();
        outputWriteSide.Dispose();

        _inputStream = new FileStream(inputWriteSide, FileAccess.Write);
        _outputStream = new FileStream(outputReadSide, FileAccess.Read);

        if (!TryCreateAttachedProcess(commandLine))
        {
            Dispose();
            return false;
        }

        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token));

        return true;
    }

    private bool TryCreateAttachedProcess(string commandLine)
    {
        IntPtr lpSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref lpSize);
        if (lpSize == IntPtr.Zero)
        {
            return false;
        }

        _attributeListHandle = Marshal.AllocHGlobal(lpSize);

        if (!InitializeProcThreadAttributeList(_attributeListHandle, 1, 0, ref lpSize))
        {
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
            return false;
        }

        var startupInfo = new STARTUPINFOEX
        {
            StartupInfo = new STARTUPINFO(),
            lpAttributeList = _attributeListHandle,
        };
        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();

        bool created = CreateProcess(
            null,
            new StringBuilder(commandLine),
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            (uint)EXTENDED_STARTUPINFO_PRESENT,
            IntPtr.Zero,
            null,
            ref startupInfo,
            out PROCESS_INFORMATION processInfo);

        if (!created)
        {
            return false;
        }

        _processHandle = processInfo.hProcess;
        CloseHandle(processInfo.hThread);
        return true;
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
