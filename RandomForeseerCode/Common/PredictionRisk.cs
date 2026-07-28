using MegaCrit.Sts2.Core.HoverTips;

namespace RandomForeseer.RandomForeseerCode.Common;

internal enum PredictionRiskReason
{
    MethodNotMirrored,
    MethodMirrorIncomplete,
    UnresolvedPlayerChoice,
    CardDrawLimitExceeded,
    OrbChannelLimitExceeded,
}

internal abstract class PredictionRisk
{
    public static PredictionRisk None { get; } = new EmptyPredictionRisk();

    public abstract bool HasRisk { get; }

    /// <summary>Creates the presentation tips for this risk snapshot.</summary>
    public IEnumerable<IHoverTip> ToHoverTips()
    {
        return HasRisk && RandomForeseerSettings.EnableDriftWarnings
            ? GetHoverTips()
            : [];
    }

    protected abstract IEnumerable<IHoverTip> GetHoverTips();

    private sealed class EmptyPredictionRisk : PredictionRisk
    {
        public override bool HasRisk => false;

        protected override IEnumerable<IHoverTip> GetHoverTips() => [];
    }
}
