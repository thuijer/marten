using System.Threading.Tasks;
using DaemonTests.TestingSupport;
using JasperFx.Core;
using Marten.Events.Daemon;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace DaemonTests.Internals;

public class ListenNotifyDaemonTests: DaemonContext
{
    public ListenNotifyDaemonTests(ITestOutputHelper output) : base(output)
    {
        theStore.Options.Events.UseListenNotifyForEventAppends = true;
    }

    [Fact]
    public async Task detect_high_water_mark_with_listen_notify()
    {
        NumberOfStreams = 10;

        Logger.LogDebug("The expected high water mark at the end is {NumberOfEvents}", NumberOfEvents);

        await PublishSingleThreaded();

        using var agent = await StartDaemon();

        await agent.Tracker.WaitForHighWaterMark(NumberOfEvents, 15.Seconds());

        agent.Tracker.HighWaterMark.ShouldBe(NumberOfEvents);

        await agent.StopAllAsync();
    }

    [Fact]
    public async Task listen_notify_wakes_daemon_faster_than_slow_polling()
    {
        // Set slow polling to 10 seconds so if LISTEN/NOTIFY fails,
        // the daemon would not reach the high water mark within 5 seconds
        theStore.Options.Projections.SlowPollingTime = 10.Seconds();

        NumberOfStreams = 5;

        Logger.LogDebug("The expected high water mark at the end is {NumberOfEvents}", NumberOfEvents);

        using var agent = await StartDaemon();

        await PublishSingleThreaded();

        // With LISTEN/NOTIFY, the daemon should detect the high water mark
        // well within 5 seconds, even though polling is set to 10 seconds
        await agent.Tracker.WaitForHighWaterMark(NumberOfEvents, 5.Seconds());

        agent.Tracker.HighWaterMark.ShouldBe(NumberOfEvents);

        await agent.StopAllAsync();
    }
}
