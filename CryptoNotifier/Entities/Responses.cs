using System.Collections.Generic;

namespace CryptoNotifier.Entities
{
    public class Responses
    {
        public List<Cryptos> PurchasedCryptos { get; set; }
        public decimal TotalCurrentValue { get; set; }
        public decimal CurrentExchangeRate { get; set; }
        public decimal TotalSpentValue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal TotalProfitRate { get; set; }
    }
}
