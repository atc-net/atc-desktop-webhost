namespace Atc.Desktop.WebHost.Sample;

/// <summary>The page's self-test results.</summary>
/// <param name="Results">One entry per check.</param>
public sealed record SelfTestReport(IReadOnlyList<SelfTestResult> Results);