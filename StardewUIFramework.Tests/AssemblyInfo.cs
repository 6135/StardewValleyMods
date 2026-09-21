using Xunit;

// the harness installs fakes into the framework's static UIServices, so test classes must not run in parallel
[assembly: CollectionBehavior(DisableTestParallelization = true)]
