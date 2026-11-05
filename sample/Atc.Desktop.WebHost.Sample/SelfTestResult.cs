namespace Atc.Desktop.WebHost.Sample;

/// <summary>One self-test check, as the page reports it.</summary>
/// <param name="Name">The check.</param>
/// <param name="Status"><c>pass</c>, <c>fail</c> or <c>skip</c>.</param>
/// <param name="Ms">How long it took.</param>
/// <param name="Message">Why it failed or was skipped.</param>
public sealed record SelfTestResult(
    string Name,
    string Status,
    double Ms,
    string? Message);