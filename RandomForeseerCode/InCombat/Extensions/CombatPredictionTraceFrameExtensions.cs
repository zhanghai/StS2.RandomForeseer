

using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.CardOnPlay;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.PotionOnUse;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

/// <summary>
/// Provides combat-projection queries over the shared immutable prediction trace.
/// </summary>
internal static class CombatPredictionTraceFrameExtensions
{
    /// <summary>
    /// Finds the nearest card <c>OnPlay</c> or potion <c>OnUse</c> invocation responsible for the current frame.
    /// </summary>
    /// <returns>
    /// The nearest method invocation frame, or <see langword="null"/> when the trace has no such invocation.
    /// </returns>
    public static PredictionTraceFrame? FindOriginatingEffect(this PredictionTraceFrame trace)
    {
        return trace.Ancestors()
            .FirstOrDefault(static frame =>
                CardOnPlayMirrors.IsOnPlayInvocation(frame.Invocation) ||
                PotionOnUseMirrors.IsOnUseInvocation(frame.Invocation));
    }

    /// <summary>
    /// Finds the nearest card-play or potion-use action responsible for the current frame.
    /// </summary>
    /// <returns>
    /// The nearest card-play or potion-use action frame, or <see langword="null"/> when the trace has no such action.
    /// </returns>
    public static PredictionTraceFrame? FindOriginatingAction(this PredictionTraceFrame trace)
    {
        return trace.Ancestors()
            .FirstOrDefault(static frame => frame.Invocation.Action is
                PredictionActionKind.CardPlay or
                PredictionActionKind.PotionUse);
    }
}
