using System.Linq;
using System.Collections.Generic;

using Amazon.Lambda.Core;

using CryptoNotifier.Entities;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
namespace CryptoNotifier
{
    public class Handlers
    {
        public Responses GetBalance(Requests request)
        {
            #region Prepare
            IStockExchange bitfinex = new Bitfinex() { Config = request.BTFNX };
            IStockExchange btcTurk = new BTCTurk() { Config = request.BTCTurk };

            Responses response = new Responses();
            response.CurrentExchangeRate = btcTurk.GetTickerByPair("ETHTRY") / bitfinex.GetTickerByPair("ethusd");
            response.PurchasedCryptos = new List<Cryptos>();

            var btfnxBalanceList = bitfinex.GetBalance();
            var btcTurklanceList = btcTurk.GetBalance();

            //var btfnxOrderHistory = bitfinex.GetLastBuyOrdersByCurrencies(btfnxBalanceList.Select(x => x.Currency).ToArray());
            var btcTurkOrderHistory = btcTurk.GetLastBuyOrdersByCurrencies(btcTurklanceList.Select(x => x.Currency).ToArray());
            #endregion

            foreach (var btfnxBalance in btfnxBalanceList)
            {
                #region Balance
                Cryptos crypto = new Cryptos()
                {
                    Currency = btfnxBalance.Currency,
                    Amount = btfnxBalance.Amount
                };
                crypto.CurrentUnitPrice = bitfinex.GetTickerByPair(crypto.Currency + "usd") * response.CurrentExchangeRate;
                crypto.CurrentValue = crypto.CurrentUnitPrice * crypto.Amount;

                IEnumerable<Balance> ieBtcTurkBalance = btcTurklanceList.Where(x => x.Currency == crypto.Currency);
                if (ieBtcTurkBalance.Any())
                {
                    Balance balance = ieBtcTurkBalance.ElementAt(0);

                    crypto.Amount += balance.Amount;
                    crypto.CurrentValue += balance.Amount * btcTurk.GetTickerByPair(crypto.Currency + "TRY");
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

                response.PurchasedCryptos.Add(crypto);
            }

            #region Non Bitfinex Cryptos
            string[] btfnxBalanceCurrencies = btfnxBalanceList.Select(x => x.Currency).ToArray();
            IEnumerable<Balance> ieBtcTurkOtherBalance = btcTurklanceList.Where(x => !btfnxBalanceCurrencies.Contains(x.Currency));
            foreach (var balance in ieBtcTurkOtherBalance)
            {
                Cryptos crypto = new Cryptos()
                {
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

                response.PurchasedCryptos.Add(crypto);
            }
            #endregion

            response.TotalCurrentValue = response.PurchasedCryptos.Sum(x => x.CurrentValue);
            response.TotalSpentValue = response.PurchasedCryptos.Sum(x => x.SpentValue);

            if (response.TotalSpentValue > 4000)
            {
                response.TotalSpentValue = 0;
                response.TotalProfit = response.TotalSpentValue - response.TotalCurrentValue;
                response.TotalProfitRate = (response.TotalProfit / response.TotalSpentValue) * 100;
            }

            return response;
        }
    }
}