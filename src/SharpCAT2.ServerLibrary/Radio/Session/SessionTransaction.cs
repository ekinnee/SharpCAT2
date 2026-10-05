using SharpCAT2.Core.Radio.Contracts;
using SharpCAT2.ServerLibrary.Radio.Protocols;

namespace SharpCAT2.ServerLibrary.Radio.Session;

/// <summary>A fixed step executed by the existing session worker. The transaction's single
/// timeout includes every step and quiet interval; command timeouts do not restart it.
/// QuietPeriodAfter drains incoming bytes with the existing reader until that interval has
/// elapsed after write completion without traffic. Write-only steps conservatively count as
/// mutations. Quiet drainage is a profile policy, not a tagged-response guarantee.</summary>
public sealed record SessionTransactionStep(
    CommandSpecification Command,
    Func<ReadOnlyMemory<byte>, ReplyParseResult> Matcher,
    bool IsMutation = false,
    TimeSpan QuietPeriodAfter = default);

/// <summary>A pure profile conclusion over the parsed step values. Value must be derived
/// from replies; the session supplies observation time, generation, and completion evidence.</summary>
public sealed record SessionTransactionConclusion(
    RadioOutcome Outcome,
    object? Value = null,
    string? Diagnostic = null);
