using BreakInfinity;

namespace BalancingCore
{
    /// <summary>
    /// Gästestrom limitiert den Absatz (Plan Abschnitt 1: "Gästestrom limitiert
    /// den Absatz, skaliert über Marketing/Ruf"). Mehr Produktionskapazität als
    /// Gäste bringt keinen zusätzlichen Umsatz -- das hier ist die Bremse dafür.
    /// </summary>
    public static class GuestFlow
    {
        public static readonly BigDouble BaseGuestFlow = 10;
        public static readonly BigDouble MarketingBaseCost = 100;
        public const double MarketingCostGrowthRate = 1.12;
        public static readonly BigDouble MarketingGuestFlowPerLevel = 5;

        public static BigDouble GuestFlowAt(int marketingLevel)
        {
            if (marketingLevel < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(marketingLevel));
            }

            return BaseGuestFlow + MarketingGuestFlowPerLevel * marketingLevel;
        }

        /// <summary>Kosten der nächsten Marketing-Stufe (0-basiert: marketingLevel ist die aktuell besessene Anzahl).</summary>
        public static BigDouble NextMarketingCost(int marketingLevel) =>
            CostCurve.Cost(MarketingBaseCost, MarketingCostGrowthRate, marketingLevel);

        /// <summary>
        /// Anteil (0..1) der potenziellen Produktion, der tatsächlich verkauft
        /// wird -- 1.0 solange genug Gäste da sind, sonst proportional gedeckelt.
        /// </summary>
        public static double CapacityFactor(BigDouble potentialPerSecond, BigDouble guestFlow)
        {
            if (potentialPerSecond <= BigDouble.Zero)
            {
                return 1.0;
            }

            var ratio = (guestFlow / potentialPerSecond).ToDouble();
            return Math.Clamp(ratio, 0.0, 1.0);
        }
    }
}
