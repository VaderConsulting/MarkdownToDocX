using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using MDConverter;

namespace md2docx;

internal sealed class ConsoleConversionProgressReporter : IConversionProgressReporter, IDisposable
{
    private static readonly TimeSpan HEARTBEAT_INTERVAL = TimeSpan.FromSeconds(15);

    private readonly object _Sync = new();
    private readonly Stopwatch _Elapsed = Stopwatch.StartNew();
    private readonly Timer _Heartbeat;
    private string _CurrentMessage = "Starting conversion";
    private int _CurrentPercent;
    private TimeSpan _LastOutput;

    public ConsoleConversionProgressReporter()
    {
        _Heartbeat = new Timer(WriteHeartbeat, null, HEARTBEAT_INTERVAL, HEARTBEAT_INTERVAL);
    }

    public void Report(int PercentComplete, string Message)
    {
        lock (_Sync)
        {
            _CurrentPercent = PercentComplete;
            _CurrentMessage = Message;
            WriteLine(_Elapsed.Elapsed, PercentComplete, Message);
        }
    }

    public void Dispose() => _Heartbeat.Dispose();

    private void WriteHeartbeat(object? State)
    {
        lock (_Sync)
        {
            TimeSpan Now = _Elapsed.Elapsed;
            if (Now - _LastOutput < HEARTBEAT_INTERVAL)
            {
                return;
            }

            WriteLine(Now, _CurrentPercent, "Still working: " + _CurrentMessage);
        }
    }

    private void WriteLine(TimeSpan Elapsed, int PercentComplete, string Message)
    {
        Console.Error.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"[{Elapsed:hh\\:mm\\:ss}] {PercentComplete,3}% {Message}"));
        Console.Error.Flush();
        _LastOutput = Elapsed;
    }
}
