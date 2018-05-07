using System;
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
        public string mutualCurrency = "eth";
        KeyValuePair<string, APIConfig> globalConfig;
        BaseStockExchange global;

        KeyValuePair<string, APIConfig> localConfig;
        BaseStockExchange local;

        List<Cryptos> cryptos;
        Dictionary<string, BaseStockExchange> platforms;

        public APIGatewayProxyResponse GetBalance(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var configs = JsonConvert.DeserializeObject<Dictionary<string, APIConfig>>(request.Body);
            APIGatewayProxyResponse response;

            try
            {
                #region Prepare
                globalConfig = configs.Where(x => x.Value.IsGlobalPlatform).ElementAt(0);
                global = Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + globalConfig.Key)) as BaseStockExchange;
                global.Config = globalConfig.Value;

                localConfig = configs.Where(x => x.Value.IsLocalPlatform).ElementAt(0);
                local = Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + localConfig.Key)) as BaseStockExchange;
                local.Config = localConfig.Value;

                cryptos = new List<Cryptos>();

                platforms = new Dictionary<string, BaseStockExchange>();
                foreach (var config in configs)
                {
                    var platform = Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + config.Key)) as BaseStockExchange;
                    platform.Config = config.Value;

                    platforms.Add(config.Key, platform);
                }
                #endregion
                
                foreach (var platformConfig in platforms)
                {
                    var platformBalanceList = platformConfig.Value.GetBalance();
                    foreach (var platformBalance in platformBalanceList)
                    {
                        var ieCurrentCrypto = cryptos.Where(x => x.Currency == platformBalance.Currency);
                        if (ieCurrentCrypto.Any())
                            UpdateCrypto(ieCurrentCrypto.ElementAt(0), platformConfig, platformBalance);
                        else
                            AddNewCrypto(platformConfig, platformBalance);
                    }
                }

                #region Total
                Cryptos totalCrypto = new Cryptos()
                {
                    SourcePlatforms = "Toplam",
                    TargetPlatform = local.Config.PlatformCurrency + " - " + global.Config.PlatformCurrency,
                    CurrentUnitPrice = Math.Round(local.GetTickerByPair(mutualCurrency + local.Config.PlatformCurrency) / global.GetTickerByPair(mutualCurrency + global.Config.PlatformCurrency), 6),
                    Purchaseds = new List<PurchasedCryptos>()
                };

                totalCrypto.CurrentValue = Math.Round(cryptos.Sum(x => x.CurrentValue * (!x.SourcePlatforms.Contains(localConfig.Key) ? totalCrypto.CurrentUnitPrice : 1)), 6); // Lokal olmayanları lokale çevirerek topla

                var purchaseds = new List<PurchasedCryptos>();
                foreach (var crypto in cryptos)
                    purchaseds.AddRange(crypto.Purchaseds);

                var iePurchasedPlatforms = purchaseds.GroupBy(x => x.Platform);
                foreach (var iePurchasedPlatform in iePurchasedPlatforms)
                    totalCrypto.Purchaseds.Add(CreateTotalPurchased(iePurchasedPlatform.Key, iePurchasedPlatform.Sum(x => x.CurrentValue), iePurchasedPlatform.Sum(x => x.SpentValue)));

                totalCrypto.Purchaseds.Add(CreateTotalPurchased("Total", purchaseds.Sum(x => x.CurrentValue * (x.Platform != "BTCTurk" ? totalCrypto.CurrentUnitPrice : 1)), purchaseds.Sum(x => x.SpentValue * (x.Platform != "BTCTurk" ? totalCrypto.CurrentUnitPrice : 1))));

                cryptos.Add(totalCrypto);
                #endregion

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
            catch (Exception ex)
            {
                var body = new Dictionary<string, object>()
                {
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

        private void AddNewCrypto(KeyValuePair<string, BaseStockExchange> platformConfig, Balance platformBalance)
        {
            var platform = platformConfig.Value;

            Cryptos crypto = new Cryptos()
            {
                SourcePlatforms = platformConfig.Key,
                Currency = platformBalance.Currency,
                TargetPlatform = platformConfig.Key,
                Amount = platformBalance.Amount,
                Purchaseds = new List<PurchasedCryptos>()
            };

            crypto.CurrentUnitPrice = Math.Round(platform.GetTickerByPair(crypto.Currency + platform.Config.PlatformCurrency), 6);
            crypto.CurrentValue = Math.Round(crypto.CurrentUnitPrice * crypto.Amount, 6);

            AddPurchased(crypto, platformConfig, platformBalance);

            cryptos.Add(crypto);
        }
        private void UpdateCrypto(Cryptos currentCrypto, KeyValuePair<string, BaseStockExchange> platformConfig, Balance platformBalance)
        {
            var platform = platformConfig.Value;

            currentCrypto.SourcePlatforms += ", " + platformConfig.Key;
            currentCrypto.Amount += platformBalance.Amount;

            var currentPlatform = platforms[currentCrypto.TargetPlatform];

            if ((platformConfig.Key != currentCrypto.TargetPlatform) && (platformConfig.Key == localConfig.Key || globalConfig.Key == currentCrypto.TargetPlatform))
            {
                currentCrypto.CurrentValue *= platform.GetTickerByPair(mutualCurrency + platform.Config.PlatformCurrency) / currentPlatform.GetTickerByPair(mutualCurrency + currentPlatform.Config.PlatformCurrency);
                currentCrypto.CurrentValue = Math.Round(currentCrypto.CurrentValue + platformBalance.Amount * platform.GetTickerByPair(currentCrypto.Currency + platform.Config.PlatformCurrency), 6);

                currentCrypto.TargetPlatform = platformConfig.Key;
            }
            else if (platformConfig.Key != currentCrypto.TargetPlatform)
            {
                var exchangeRate = currentPlatform.GetTickerByPair(mutualCurrency + currentPlatform.Config.PlatformCurrency) / platform.GetTickerByPair(mutualCurrency + platform.Config.PlatformCurrency);
                currentCrypto.CurrentValue = Math.Round(currentCrypto.CurrentValue + platformBalance.Amount * platform.GetTickerByPair(platformBalance.Currency + platform.Config.PlatformCurrency) * exchangeRate, 6);
            }
            else
                currentCrypto.CurrentValue = Math.Round(currentCrypto.CurrentValue + platformBalance.Amount * platform.GetTickerByPair(currentCrypto.Currency + platform.Config.PlatformCurrency), 6);

            currentCrypto.CurrentUnitPrice = Math.Round(currentCrypto.CurrentValue / currentCrypto.Amount, 6);

            AddPurchased(currentCrypto, platformConfig, platformBalance);
        }
        private void AddPurchased(Cryptos crypto, KeyValuePair<string, BaseStockExchange> platformConfig, Balance platformBalance)
        {
            var platform = platformConfig.Value;

            var platformOrderHistory = platform.GetLastBuyOrdersByCurrencies(new string[] { platformBalance.Currency });
            if (platformOrderHistory.Any())
            {
                var order = platformOrderHistory.ElementAt(0);

                PurchasedCryptos purchased = new PurchasedCryptos()
                {
                    Currency = crypto.Currency,
                    Platform = platformConfig.Key,
                    Amount = order.Amount,
                    CurrentUnitPrice = Math.Round(platform.GetTickerByPair(crypto.Currency + platform.Config.PlatformCurrency), 6),
                    SpentValue = Math.Round(order.Price * order.Amount, 6)
                };

                if (purchased.SpentValue != 0)
                {
                    purchased.CurrentValue = purchased.Amount * purchased.CurrentUnitPrice;
                    purchased.PurchasedUnitPrice = Math.Round(purchased.SpentValue / order.Amount, 6);
                    purchased.ProfitValue = Math.Round(crypto.CurrentValue - purchased.SpentValue, 6);
                    purchased.ProfitPercentage = Math.Round(purchased.ProfitValue / purchased.SpentValue, 6);

                    crypto.Purchaseds.Add(purchased); 
                }
            }
        }
        private PurchasedCryptos CreateTotalPurchased(string platform, decimal currentValue, decimal spentValue)
        {
            var totalCryptoPurchased = new PurchasedCryptos()
            {
                Platform = platform,
                CurrentValue = Math.Round(currentValue, 6),
                SpentValue = Math.Round(spentValue, 6)
            };

            totalCryptoPurchased.ProfitValue = Math.Round(totalCryptoPurchased.CurrentValue - totalCryptoPurchased.SpentValue, 6);

            if (totalCryptoPurchased.SpentValue > 0)
                totalCryptoPurchased.ProfitPercentage = Math.Round((totalCryptoPurchased.ProfitValue / totalCryptoPurchased.SpentValue) * 100, 6);

            return totalCryptoPurchased;
        }
    }
}
