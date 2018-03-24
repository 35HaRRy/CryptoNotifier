using System;
using System.Collections.Generic;

namespace CryptoNotifier.Entities
{
    public class APIConfig
    {
        public string BasePath { get; set; }
        public string ApiSecret { get; set; }
        public string ApiKey { get; set; }
        public string PlatformCurrency { get; set; }
        public Boolean IsGlobalPlatform { get; set; }
        public Boolean IsLocalPlatform { get; set; }
    }

    public class Ticker
    {
        public string Pair { get; set; }
        public decimal Amount { get; set; }
    }
    public class Balance
    {
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal Avaliable { get; set; }
    }
    public class Order
    {
        public string Currency { get; set; }
        public decimal Amount { get; set; }
        public decimal Price { get; set; }
        public DateTime ExecutionTime { get; set; }
    }

    public abstract class BaseStockExchange
    {
        public APIConfig Config;

        public abstract List<Balance> GetBalance();
        public abstract List<Ticker> GetTickers();
        public abstract decimal GetTickerByPair(string pair);
        public abstract List<Order> GetLastBuyOrdersByCurrencies(string[] currencies);
    }

    public static class Functions
    {
        public static Boolean IsPairDifferent(this string pair)
        {
            return pair.Substring(3) != pair.Substring(0, 3);
        }
    }
}
