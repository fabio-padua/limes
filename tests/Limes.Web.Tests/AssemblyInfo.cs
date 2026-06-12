using Xunit;

// Several endpoint tests mutate process-wide environment variables
// (LIMES_FOUNDRY_ENDPOINT / LIMES_FOUNDRY_DEPLOYMENT) to exercise agents-mode
// configuration paths. Disable parallelization for this assembly so those tests
// can't race each other — or, when test assemblies are run concurrently, reduce
// the window in which they could clash with other env-var-sensitive suites.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
