// Runs unit tests one at a time so the 10 ms budget measures each test alone, not CPU contention.
using Xunit;

// Parallel classes all start their first test the instant the runtime warm-up finishes and then
// compete for cores; the budget is a per-test measurement and only means something when the test
// has the machine to itself. The whole suite is well under a second either way.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
