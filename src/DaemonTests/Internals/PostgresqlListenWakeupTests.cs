using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Marten.Events.Daemon.HighWater;
using Marten.Testing.Harness;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Shouldly;
using Xunit;

namespace DaemonTests.Internals;

public class PostgresqlListenWakeupTests: IAsyncLifetime
{
    private NpgsqlDataSource _dataSource = default!;

    public Task InitializeAsync()
    {
        _dataSource = NpgsqlDataSource.Create(ConnectionSource.ConnectionString);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
    }

    [Fact]
    public async Task WaitAsync_returns_immediately_when_notification_received()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wakeup = new PostgresqlListenWakeup(_dataSource, NullLogger.Instance);

        try
        {
            // Establish the LISTEN connection by starting a wait in the background
            var initialWait = Task.Run(() => wakeup.WaitAsync(TimeSpan.FromSeconds(10), cts.Token));

            // Give it a moment to set up LISTEN and start the receive loop
            await Task.Delay(1000);

            // Send NOTIFY from a separate connection
            await using var conn = await _dataSource.OpenConnectionAsync(cts.Token);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"NOTIFY {PostgresqlListenWakeup.DefaultChannel}";
            await cmd.ExecuteNonQueryAsync(cts.Token);

            var sw = Stopwatch.StartNew();
            await initialWait;

            sw.ElapsedMilliseconds.ShouldBeLessThan(3000);
        }
        finally
        {
            // Cancel first to stop the receive loop, then dispose
            await cts.CancelAsync();
            await Task.Delay(100); // let the receive loop exit
            wakeup.Dispose();
        }
    }

    [Fact]
    public async Task WaitAsync_falls_back_to_timeout_when_no_notification()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var wakeup = new PostgresqlListenWakeup(_dataSource, NullLogger.Instance);

        try
        {
            var sw = Stopwatch.StartNew();
            await wakeup.WaitAsync(TimeSpan.FromMilliseconds(500), cts.Token);

            sw.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(400);
            sw.ElapsedMilliseconds.ShouldBeLessThan(2000);
        }
        finally
        {
            await cts.CancelAsync();
            await Task.Delay(100);
            wakeup.Dispose();
        }
    }

    [Fact]
    public void Dispose_stops_listening_cleanly()
    {
        var wakeup = new PostgresqlListenWakeup(_dataSource, NullLogger.Instance);
        wakeup.Dispose();
        wakeup.Dispose(); // double dispose should be safe
    }
}
