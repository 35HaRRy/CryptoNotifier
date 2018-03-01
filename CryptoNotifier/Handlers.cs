using System.Net;
using System.Linq;
using System.Collections.Generic;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using Newtonsoft.Json;

using CryptoNotifier.Entities;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace CryptoNotifier
{
    public class Handlers
    {
        public APIGatewayProxyResponse GetBalance(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var apiRequest = JsonConvert.DeserializeObject<Requests>(request.Body);
            APIGatewayProxyResponse response;

            try
            {
                #region Prepare
                IStockExchange bitfinex = new Bitfinex() { Config = apiRequest.BTFNX };
                IStockExchange btcTurk = new BTCTurk() { Config = apiRequest.BTCTurk };

                Cryptos totalCrypto = new Cryptos()
                {
                    Source = "Toplam",
                    CurrentUnitPrice = btcTurk.GetTickerByPair("ETHTRY") / bitfinex.GetTickerByPair("ethusd")
                };
                List<Cryptos> cryptos = new List<Cryptos>();

                var btfnxBalanceList = bitfinex.GetBalance();
                var btcTurkBalanceList = btcTurk.GetBalance();

                //var btfnxOrderHistory = bitfinex.GetLastBuyOrdersByCurrencies(btfnxBalanceList.Select(x => x.Currency).ToArray());
                var btcTurkOrderHistory = btcTurk.GetLastBuyOrdersByCurrencies(btcTurkBalanceList.Select(x => x.Currency).ToArray());
                #endregion

                foreach (var btfnxBalance in btfnxBalanceList)
                {
                    #region Balance
                    Cryptos crypto = new Cryptos()
                    {
                        Source = "Bitfinex",
                        Currency = btfnxBalance.Currency,
                        Amount = btfnxBalance.Amount
                    };

                    crypto.CurrentUnitPrice = bitfinex.GetTickerByPair(crypto.Currency + "usd");
                    crypto.CurrentValue = crypto.CurrentUnitPrice * crypto.Amount;

                    IEnumerable<Balance> ieBtcTurkBalance = btcTurkBalanceList.Where(x => x.Currency == crypto.Currency);
                    if (ieBtcTurkBalance.Any())
                    {
                        Balance balance = ieBtcTurkBalance.ElementAt(0);

                        crypto.Source += ", BTC Türk";
                        crypto.Amount += balance.Amount;
                        crypto.CurrentValue = crypto.CurrentValue * totalCrypto.CurrentUnitPrice + balance.Amount * btcTurk.GetTickerByPair(crypto.Currency + "TRY");
                        crypto.CurrentUnitPrice = crypto.CurrentValue / crypto.Amount; // 2 yerde de varsa hesabı TRY' ye çevir yoksa USD kalsın.
                    }
                    #endregion

                    //#region Order
                    //IEnumerable<Order> ieBtfnxOrders = btfnxOrderHistory.Where(x => x.Currency == crypto.Currency);
                    //if (ieBtfnxOrders.Any())
                    //{
                    //    Order order = ieBtfnxOrders.ElementAt(0);

                    //    crypto.PurchasedUnitPrice = order.Price;
                    //    crypto.SpentValue = order.Price * order.Amount;

                    //    IEnumerable<Order> ieBtcTurkOrders = btcTurkOrderHistory.Where(x => x.Currency == crypto.Currency);
                    //    if (ieBtcTurkOrders.Any())
                    //    {
                    //        order = ieBtfnxOrders.ElementAt(0);

                    //        crypto.SpentValue += order.Price * order.Amount;
                    //        crypto.PurchasedUnitPrice = crypto.SpentValue / crypto.Amount;
                    //    }
                    //}
                    //#endregion

                    cryptos.Add(crypto);
                }

                #region Non Bitfinex Cryptos
                string[] btfnxBalanceCurrencies = btfnxBalanceList.Select(x => x.Currency).ToArray();
                IEnumerable<Balance> ieBtcTurkOtherBalance = btcTurkBalanceList.Where(x => !btfnxBalanceCurrencies.Contains(x.Currency));
                foreach (var balance in ieBtcTurkOtherBalance)
                {
                    Cryptos crypto = new Cryptos()
                    {
                        Source = "BTC Türk",
                        Currency = balance.Currency,
                        Amount = balance.Amount
                    };
                    crypto.CurrentUnitPrice = btcTurk.GetTickerByPair(crypto.Currency + "try");
                    crypto.CurrentValue = crypto.CurrentUnitPrice * crypto.Amount;

                    IEnumerable<Order> ieBtcTurkOrders = btcTurkOrderHistory.Where(x => x.Currency == crypto.Currency);
                    if (ieBtcTurkOrders.Any())
                    {
                        Order order = ieBtcTurkOrders.ElementAt(0);

                        crypto.SpentValue += order.Price * order.Amount;
                        crypto.PurchasedUnitPrice = crypto.SpentValue / crypto.Amount;
                    }

                    cryptos.Add(crypto);
                }
                #endregion

                // BTC Türk olmayanları TRY' ye çevirerek topla
                totalCrypto.CurrentValue = cryptos.Sum(x => x.CurrentValue * (!x.Source.Contains("BTC Türk") ? totalCrypto.CurrentUnitPrice : 1));
                totalCrypto.SpentValue = cryptos.Sum(x => x.SpentValue * (!x.Source.Contains("BTC Türk") ? totalCrypto.CurrentUnitPrice : 1));

                if (totalCrypto.SpentValue > 4000)
                {
                    totalCrypto.SpentValue = 0;
                    totalCrypto.ProfitValue = totalCrypto.SpentValue - totalCrypto.CurrentValue;
                    totalCrypto.ProfitPercentage = (totalCrypto.ProfitValue / totalCrypto.SpentValue) * 100;
                }

                cryptos.Add(totalCrypto);

                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.OK,
                    Body = JsonConvert.SerializeObject(cryptos),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    }
                };
            }
            catch (System.Exception ex)
            {
                var body = new Dictionary<string, object>()
                {
                    { "Config", request.Body },
                    { "Message", ex.ToString() }
                };

                response = new APIGatewayProxyResponse
                {
                    StatusCode = (int)HttpStatusCode.InternalServerError,
                    Body = JsonConvert.SerializeObject(body),
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "application/json" }
                    }
                };
            }

            return response;
        }
    }
}
