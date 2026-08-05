namespace RandomForeseer.RandomForeseerCode.InCombat.Mirrors.Hooks;

// Shadow amount shared by hook mirrors that consume an existing live power without mutating it.
internal sealed class PowerAmountPredictionState(int amount)
{
    public int Amount { get; set; } = amount;

    public bool IsActive => Amount > 0;

    public void Decrement()
    {
        Amount = Math.Max(0, Amount - 1);
    }
}
