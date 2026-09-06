// The setting that keeps a hosted API from running queued loads during a test.
namespace RecallRadar.Integration;

/// <summary>
/// Turns the background ingest runner off for a hosted API.
/// </summary>
/// <remarks>
/// Every test but the runner's own hosts the API to exercise an endpoint, not to load anything. A
/// live runner there would claim whatever job happened to be queued and fetch from the real NHTSA
/// feeds, which no integration test may touch (Article V) -- and it would load another test's
/// vehicle against a stub that test never configured.
/// </remarks>
public static class RunnerOff
{
    public const string Key = "IngestRunner:IsEnabled";
    public const string Value = "false";
}
