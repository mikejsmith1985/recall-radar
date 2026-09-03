// Command-line entry point for ingestion and evaluation. Each verb is a separate command so the
// scheduler, the developer and the tests all drive the same code path.
using RecallRadar.Ingest;

return await IngestCommandLine.RunAsync(args);
