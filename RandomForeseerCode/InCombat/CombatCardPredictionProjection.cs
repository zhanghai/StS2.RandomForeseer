using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;

namespace RandomForeseer.RandomForeseerCode.InCombat;

internal sealed record CombatCardPredictionProjection(
    IReadOnlyList<IHoverTip> HoverTips,
    DamagePrediction DamagePrediction,
    IReadOnlyList<CardModel> HighlightedCards,
    PredictionRisk Risk);
