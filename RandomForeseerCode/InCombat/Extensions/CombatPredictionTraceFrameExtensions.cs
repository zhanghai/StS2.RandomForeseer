

using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

/// <summary>
/// Provides combat-card-specific queries over the shared immutable prediction trace.
/// </summary>
internal static class CombatPredictionTraceFrameExtensions
{
    /// <summary>
    /// Finds the nearest card <c>OnPlay</c> invocation responsible for the current frame.
    /// </summary>
    /// <returns>The nearest card-play method frame, or <see langword="null"/> when the trace has no card play.</returns>
    public static PredictionTraceFrame? FindOriginatingCardPlay(this PredictionTraceFrame trace)
    {
        return trace.Ancestors()
            .FirstOrDefault(static frame => CardOnPlayMirrors.IsOnPlayInvocation(frame.Invocation));
    }
}
