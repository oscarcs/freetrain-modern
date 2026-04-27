namespace FreeTrain.Modern;

public enum ModernAccountGenre
{
    Construction,
    Land,
    Railway,
    Road,
    Train,
    Subsidiary,
    Income,
    Debt,
    Other
}

public sealed record ModernAccountTransaction(
    long AbsoluteMinute,
    long Amount,
    ModernAccountGenre Genre,
    string Description);

public sealed record ModernAccountState(
    long Cash,
    long TotalDebt,
    ModernAccountTransaction[] Transactions)
{
    public static ModernAccountState Default => new(15_000_000_000L, 0, Array.Empty<ModernAccountTransaction>());

    public ModernAccountState(long cash)
        : this(cash, 0, Array.Empty<ModernAccountTransaction>())
    {
    }

    public ModernAccountState Spend(long amount, ModernAccountGenre genre, ModernWorldClock clock, string description)
    {
        if (amount <= 0)
        {
            return this;
        }

        return AddTransaction(-amount, genre, clock, description);
    }

    public ModernAccountState Earn(long amount, ModernAccountGenre genre, ModernWorldClock clock, string description)
    {
        if (amount <= 0)
        {
            return this;
        }

        return AddTransaction(amount, genre, clock, description);
    }

    public ModernAccountState AddDebt(long amount, ModernWorldClock clock, string description)
    {
        if (amount <= 0)
        {
            return this;
        }

        return this with
        {
            Cash = Cash + amount,
            TotalDebt = TotalDebt + amount,
            Transactions = AppendTransaction(amount, ModernAccountGenre.Debt, clock, description)
        };
    }

    public ModernAccountState RepayDebt(long amount, ModernWorldClock clock, string description)
    {
        if (amount <= 0)
        {
            return this;
        }

        long repayment = Math.Min(amount, TotalDebt);
        return this with
        {
            Cash = Cash - repayment,
            TotalDebt = TotalDebt - repayment,
            Transactions = AppendTransaction(-repayment, ModernAccountGenre.Debt, clock, description)
        };
    }

    private ModernAccountState AddTransaction(long signedAmount, ModernAccountGenre genre, ModernWorldClock clock, string description)
    {
        return this with
        {
            Cash = Cash + signedAmount,
            Transactions = AppendTransaction(signedAmount, genre, clock, description)
        };
    }

    private ModernAccountTransaction[] AppendTransaction(
        long signedAmount,
        ModernAccountGenre genre,
        ModernWorldClock clock,
        string description)
    {
        ModernAccountTransaction transaction = new(clock.AbsoluteMinute, signedAmount, genre, description);
        ModernAccountTransaction[] existing = Transactions ?? Array.Empty<ModernAccountTransaction>();
        ModernAccountTransaction[] next = new ModernAccountTransaction[existing.Length + 1];
        Array.Copy(existing, next, existing.Length);
        next[^1] = transaction;
        return next;
    }
}
