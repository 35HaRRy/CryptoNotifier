
namespace CryptoNotifier.Entities
{
    public class Cryptos
    {
        public string Source { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal CurrentUnitPrice { get; set; }
        public decimal CurrentValue { get; set; }
        public decimal PurchasedUnitPrice { get; set; }
        public decimal SpentValue { get; set; }
        public decimal ProfitPercentage { get; set; }
        public decimal ProfitValue { get; set; }
    }
}
