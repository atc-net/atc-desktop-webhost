namespace Atc.Desktop.WebHost.Tests.Hosting;

public sealed class CloseGuardTests
{
    private readonly ManualTime time = new();
    private readonly CloseGuard guard;

    public CloseGuardTests()
        => guard = new CloseGuard(time);

    [Fact]
    public void NotArmed_Closes()
        => guard.OnUserClose().Should().Be(CloseDecision.Close);

    [Fact]
    public void Armed_AsksThePage()
    {
        guard.SetArmed(armed: true);

        guard.OnUserClose().Should().Be(CloseDecision.AskPage);
    }

    [Fact]
    public void Armed_SecondCloseSoonAfter_Waits()
    {
        guard.SetArmed(armed: true);
        guard.OnUserClose();
        time.Advance(TimeSpan.FromSeconds(1));

        guard.OnUserClose().Should().Be(CloseDecision.Wait);
    }

    [Fact]
    public void Armed_NoAcknowledgement_ClosesOnALaterAttempt()
    {
        guard.SetArmed(armed: true);
        guard.OnUserClose();
        time.Advance(CloseGuard.AnswerTimeout);

        guard.OnUserClose().Should().Be(CloseDecision.Close);
    }

    [Fact]
    public void Acknowledged_LaterCloseAsksAgain()
    {
        guard.SetArmed(armed: true);
        guard.OnUserClose();
        guard.Acknowledge();
        time.Advance(TimeSpan.FromMinutes(1));

        guard.OnUserClose().Should().Be(CloseDecision.AskPage);
    }

    [Fact]
    public void Reset_Disarms()
    {
        guard.SetArmed(armed: true);

        guard.Reset();

        guard.IsArmed.Should().BeFalse();
        guard.OnUserClose().Should().Be(CloseDecision.Close);
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
            => now;

        public void Advance(TimeSpan by)
            => now += by;
    }
}