using System.Collections.Generic;

namespace CryptoNotifier.Entities
{
    public class Cryptos
    {
        public string SourcePlatforms { get; set; }
        public string TargetPlatform { get; set; }
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal CurrentUnitPrice { get; set; }
        public decimal CurrentValue { get; set; }
        public List<PurchasedCryptos> Purchaseds { get; set; }
    }
}
