using MegaCrit.Sts2.Core.Models;
using RandomForeseer.RandomForeseerCode.Common;
using RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;

namespace RandomForeseer.RandomForeseerCode.InCombat.Extensions;

internal static class PredictionStateStoreExtensions
{
    extension(PredictionStateStore store)
    {
        public PowerAmountPredictionState GetPowerAmount(PowerModel power)
        {
            return store.Get(power, () => new PowerAmountPredictionState(power.Amount));
        }
    }
}
