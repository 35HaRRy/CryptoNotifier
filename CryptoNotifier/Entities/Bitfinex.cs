using System;
using System.IO;
using System.Net;
using System.Text;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;

using Newtonsoft.Json;

namespace CryptoNotifier.Entities
{
    public class Bitfinex : BaseStockExchange//, IStockExchange
    {
        private NumberFormatInfo ni = new NumberFormatInfo() { NumberDecimalSeparator = "." };

        private WebRequest GetAuthWebRequest(string path)
        {
            return GetAuthWebRequest(path, new Dictionary<string, object>());
        }
        private WebRequest GetAuthWebRequest(string path, Dictionary<string, object> payload)
        {
            WebRequest request = WebRequest.Create(Config.BasePath + path);
            request.Method = "POST";

            payload.Add("request", path);
            payload.Add("nonce", DateTime.Now.Ticks.ToString());
            
            byte[] payloadBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(payload));
            string payloadBase64 = Convert.ToBase64String(payloadBytes);

            using (HMACSHA384 hmac = new HMACSHA384(Encoding.UTF8.GetBytes(Config.ApiSecret)))
            {
                byte[] signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));

                request.Headers.Add("X-BFX-APIKEY", Config.ApiKey);
                request.Headers.Add("X-BFX-PAYLOAD", payloadBase64);
                request.Headers.Add("X-BFX-SIGNATURE", BitConverter.ToString(signatureBytes).Replace("-", "").ToLower());
            }

            request.ContentLength = payloadBytes.Length;
            using (var writer = request.GetRequestStream())
                writer.Write(payloadBytes, 0, payloadBytes.Length);

            return request;
        }

        public override List<Balance> GetBalance()
        {
            List<Balance> balances = new List<Balance>();

            WebResponse response = GetAuthWebRequest("/v1/balances").GetResponse();
            using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            {
                var balanceList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(reader.ReadToEnd());

                foreach (var balance in balanceList)
                {
                    decimal amount = Convert.ToDecimal(balance["amount"], ni);
                    if (amount > 0)
                    {
                        balances.Add(new Balance()
                        {
                            Currency = balance["currency"].ToString().ToUpper(),
                            Amount = amount,
                            Avaliable = Convert.ToDecimal(balance["available"], ni)
                        }); 
                    }
                }

                reader.Close();
            }

            response.Close();

            return balances;
        }

        public override List<Order> GetLastBuyOrdersByCurrencies(string[] currencies)
        {
            List<Order> orders = new List<Order>();

            //WebResponse response = GetAuthWebRequest("/v1/orders/hist").GetResponse();
            //using (StreamReader reader = new StreamReader(response.GetResponseStream()))
            //{
            //    var orderList = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(reader.ReadToEnd());

            //    //    foreach (string currency in currencies)
            //    //    {
            //    //        var lastBuyOrders = orderList
            //    //                                    .Where(order => order["currency"].ToString() == currency)// && order["operation"].ToString() == "trade")
            //    //                                    .TakeWhile(order => Convert.ToDecimal(order["amount"]) > 0);

            //    //        if (lastBuyOrders.Count() > 0)
            //    //        {
            //    //            Order lastBuyOrder = new Order()
            //    //            {
            //    //                Currency = "TRY",
            //    //                Amount = lastBuyOrders.Sum(order => Convert.ToDecimal(order["amount"])),
            //    //                Price = lastBuyOrders.Sum(order => Convert.ToDecimal(order["amount"]) * Convert.ToDecimal(order["price"])),
            //    //                ExecutionTime = lastBuyOrders.Max(order => Convert.ToDateTime(order["date"]))
            //    //            };
            //    //            lastBuyOrder.Price /= lastBuyOrder.Amount;

            //    //            orders.Add(lastBuyOrder);
            //    //        }
            //    //    }

            //    reader.Close();
            //}

            //response.Close();

            return orders;
        }

        public override List<Ticker> GetTickers()
        {
            List<Ticker> tickers = new List<Ticker>();

            string[] pairs = new string[] { "BTCUSD", "ETHUSD" };
            foreach (string pair in pairs)
            {
                tickers.Add(new Ticker()
                {
                    Pair = pair,
                    Amount = GetTickerByPair(pair)
                });
            }

            return tickers;
        }

        public override decimal GetTickerByPair(string pair)
        {
            decimal price = 1;
            pair = pair.ToLower();

            if (pair.IsPairDifferent())
            {
                WebResponse response = WebRequest.Create(Config.BasePath + "/v1/pubticker/" + pair).GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    var ticker = JsonConvert.DeserializeObject<Dictionary<string, object>>(reader.ReadToEnd());
                    price = Convert.ToDecimal(ticker["last_price"], ni);

                    reader.Close();
                }

                response.Close(); 
            }

            return price;
        }
    }
}
