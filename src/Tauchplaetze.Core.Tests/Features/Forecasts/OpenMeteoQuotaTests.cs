using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class OpenMeteoQuotaTests
{
    private static readonly (double, double)[] TwoPoints = [(47.28, 8.6), (47.13, 8.49)];

    [Fact]
    public async Task Die_Calls_an_Open_Meteo_bleiben_im_Kontingent_der_Minute()
    {
        // Arrange
        var time = new SteppingTime();
        var handler = new QueuedHandler(time, Ok(), Ok(), Ok());
        var client = Client(handler, time, pointsPerMinute: 4);

        // Act
        for (var i = 0; i < 3; i++)
        {
            await client.GetHourly("meteoswiss_icon_ch2", TwoPoints, 3, 5, CancellationToken.None);
        }

        // Assert
        Assert.Equal([TimeSpan.Zero, TimeSpan.Zero, TimeSpan.FromMinutes(1)], handler.SentAt);
    }

    [Fact]
    public async Task Ein_429_wegen_des_Minutenlimits_wird_nach_einer_Minute_einmal_wiederholt()
    {
        // Arrange
        var time = new SteppingTime();
        var handler = new QueuedHandler(time, TooMany("Minutely API request limit exceeded. Please try again in one minute."), Ok());
        var client = Client(handler, time);

        // Act
        var result = await client.GetHourly("meteoswiss_icon_ch2", TwoPoints, 3, 5, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Value?.Count);
        Assert.Equal([TimeSpan.Zero, OpenMeteoClient.MinutelyPause], handler.SentAt);
    }

    [Theory]
    [InlineData("Daily API request limit exceeded. Please try again tomorrow.")]
    [InlineData("Hourly API request limit exceeded. Please try again in the next hour.")]
    public async Task Ist_das_Kontingent_der_Stunde_oder_des_Tages_erschoepft_geht_kein_weiterer_Call_hinaus(string reason)
    {
        // Arrange
        var time = new SteppingTime();
        var handler = new QueuedHandler(time, TooMany(reason), Ok());
        var client = Client(handler, time);

        // Act
        var first = await client.GetHourly("meteoswiss_icon_ch2", TwoPoints, 3, 5, CancellationToken.None);
        var second = await client.GetHourly("icon_seamless", TwoPoints, 3, 7, CancellationToken.None);

        // Assert
        Assert.Null(first.Value);
        Assert.Null(second.Value);
        Assert.Single(handler.SentAt);
    }

    private static OpenMeteoClient Client(QueuedHandler handler, SteppingTime time, int pointsPerMinute = 400) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.com/") },
            new OpenMeteoBudget(time, Options.Create(new OpenMeteoOptions { PointsPerMinute = pointsPerMinute })),
            time,
            NullLogger<OpenMeteoClient>.Instance);

    private static Func<HttpResponseMessage> Ok() =>
        () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TestData.Read("open-meteo", "forecast-meteoswiss_icon_ch2-two-points-20261019.json")) };

    private static Func<HttpResponseMessage> TooMany(string reason) =>
        () => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent($$"""{"reason":"{{reason}}","error":true}""") };

    /// <summary>Eine Uhr, die bei jedem Warten sofort um die Wartezeit vorrückt.</summary>
    private sealed class SteppingTime : TimeProvider
    {
        public static readonly DateTimeOffset Start = new(2026, 10, 21, 16, 0, 0, TimeSpan.Zero);

        public DateTimeOffset Now { get; private set; } = Start;

        public override DateTimeOffset GetUtcNow() => Now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            if (dueTime != Timeout.InfiniteTimeSpan)
            {
                Now += dueTime;
                ThreadPool.QueueUserWorkItem(_ => callback(state));
            }

            return new NoTimer();
        }

        private sealed class NoTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class QueuedHandler(SteppingTime time, params Func<HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> queue = new(responses);

        public List<TimeSpan> SentAt { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentAt.Add(time.Now - SteppingTime.Start);
            return Task.FromResult(queue.Dequeue()());
        }
    }
}
