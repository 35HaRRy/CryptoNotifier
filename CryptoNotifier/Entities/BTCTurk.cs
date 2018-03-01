using System;
using System.IO;
using System.Net;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;

using Newtonsoft.Json;

namespace CryptoNotifier.Entities
{
    public class BTCTurk : BaseStockExchange, IStockExchange
    {
        private WebRequest GetAuthWebRequest(string path)
        {
            WebRequest request = WebRequest.Create(Config.BasePath + path);

            long stamp = DateTime.Now.Ticks;

            using (HMACSHA256 hmac = new HMACSHA256(Convert.FromBase64String(Config.ApiSecret)))
            {
                byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(Config.ApiKey + stamp));

                request.Headers.Add("X-PCK", Config.ApiKey);
                request.Headers.Add("X-Stamp", stamp.ToString());
                request.Headers.Add("X-Signature", Convert.ToBase64String(signatureBytes));
            }

            return request;
        }

        public List<Balance> GetBalance()
        {
            List<Balance> balances = new List<Balance>();

            WebResponse response = GetAuthWebRequest("balance").GetResponse();
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                var balanceDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());
                
                List<string> currencies = new List<string>() { "TRY", "BTC", "ETH" };
                if (balanceDict.ContainsKey("xrp_balance"))
                    currencies.Add("XRP");

                foreach (string currency in currencies)
                {
                    decimal amount = Convert.ToDecimal(balanceDict[currency.ToLower() + "_balance"]);
                    if (amount > 0)
                    {
                        balances.Add(new Balance()
                        {
                            Currency = currency,
                            Amount = amount,
                            Avaliable = Convert.ToDecimal(balanceDict[currency.ToLower() + "_available"])
                        }); 
                    }
                }

                reader.Close();
            }

            response.Close();

            return balances;
        }

        public List<Order> GetLastBuyOrdersByCurrencies(string[] currencies)
        {
            List<Order> orders = new List<Order>();

            WebResponse response = GetAuthWebRequest("userTransactions?limit=100").GetResponse();
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                var orderList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(reader.ReadToEnd());

                foreach (string currency in currencies)
                {
                    var lastBuyOrders = orderList
                                                .Where(order => order["currency"].ToString() == currency)// && order["operation"].ToString() == "trade")
                                                .TakeWhile(order => Convert.ToDecimal(order["amount"]) > 0);

                    if (lastBuyOrders.Count() > 0)
                    {
                        Order lastBuyOrder = new Order()
                        {
                            Currency = currency,
                            Amount = lastBuyOrders.Sum(order => Convert.ToDecimal(order["amount"])),
                            Price = lastBuyOrders.Sum(order => Convert.ToDecimal(order["amount"]) * Convert.ToDecimal(order["price"])),
                            ExecutionTime = lastBuyOrders.Max(order => Convert.ToDateTime(order["date"]))
                        };
                        lastBuyOrder.Price /= lastBuyOrder.Amount;

                        orders.Add(lastBuyOrder);
                    }
                }

                reader.Close();
            }

            response.Close();

            return orders;
        }

        public List<Ticker> GetTickers()
        {
            List<Ticker> tickers = new List<Ticker>();

            WebResponse response = WebRequest.Create(Config.BasePath + "ticker").GetResponse();
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                var tickerList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(reader.ReadToEnd());
                foreach (var ticker in tickerList)
                {
                    tickers.Add(new Ticker()
                    {
                        Pair = ticker["pair"].ToString(),
                        Amount = Convert.ToDecimal(ticker["last"])
                    });
                }

                reader.Close();
            }

            response.Close();

            return tickers;
        }

        public decimal GetTickerByPair(string pair)
        {
            decimal price = 1;
            pair = pair.ToUpper();

            if (pair != "TRYTRY")
            {
                var tickers = GetTickers().Where(ticker => ticker.Pair == pair);
                if (tickers.Count() > 0)
                    price = tickers.ElementAt(0).Amount;
            }

            return price;
        }
    }
}
