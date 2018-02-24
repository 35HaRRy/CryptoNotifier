using System;
using System.Collections.Generic;

namespace CryptoNotifier.Entities
{
    public class APIConfig
    {
        public string BasePath { get; set; }
        public string ApiSecret { get; set; }
        public string ApiKey { get; set; }
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

    public interface IStockExchange
    {
        List<Balance> GetBalance();
        List<Ticker> GetTickers();
        decimal GetTickerByPair(string pair);
        List<Order> GetLastBuyOrdersByCurrencies(string[] currencies);
    }

    public class BaseStockExchange
    {
        public APIConfig Config;
    }
}
