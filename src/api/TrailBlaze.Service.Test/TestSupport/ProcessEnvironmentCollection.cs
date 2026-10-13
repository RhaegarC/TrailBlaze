namespace TrailBlaze.Service.Test.TestSupport;

/// <summary>
/// The tests that write a process-wide environment variable, run alone.
/// </summary>
/// <remarks>
/// xUnit runs collections in parallel, so a quality one test sets would be the quality whatever runs
/// beside it encodes at — a difference no assertion would name. This is the one setting read from the
/// environment rather than from configuration, which is what makes a collection necessary at all.
/// </remarks>
[CollectionDefinition(nameof(ProcessEnvironmentCollection), DisableParallelization = true)]
public sealed class ProcessEnvironmentCollection;
