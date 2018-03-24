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
        public string mutualCurrency = "btc";
        KeyValuePair<string, APIConfig> globalConfig;
        BaseStockExchange global;

        KeyValuePair<string, APIConfig> localConfig;
        BaseStockExchange local;

        List<Cryptos> cryptos;
        Dictionary<string, BaseStockExchange>  platforms;

        public APIGatewayProxyResponse GetBalance(APIGatewayProxyRequest request, ILambdaContext context)
        {
            var configs = JsonConvert.DeserializeObject<Dictionary<string, APIConfig>>(request.Body);
            APIGatewayProxyResponse response;

            try
            {
                #region Prepare
                globalConfig = configs.Where(x => x.Value.IsGlobalPlatform).ElementAt(0);
                global = Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + globalConfig.Key), new object[] { globalConfig.Value }) as BaseStockExchange;

                localConfig = configs.Where(x => x.Value.IsLocalPlatform).ElementAt(0);
                local = Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + localConfig.Key), new object[] { localConfig.Value }) as BaseStockExchange;

                cryptos = new List<Cryptos>();

                platforms = new Dictionary<string, BaseStockExchange>();
                foreach (var config in configs)
                    platforms.Add(config.Key, Activator.CreateInstance(Type.GetType("CryptoNotifier.Entities." + config.Key), new object[] { config.Value }) as BaseStockExchange);
                #endregion

                // Main process
                foreach (var processingConfig in platforms)
                {
                    var processingBalanceList = processingConfig.Value.GetBalance();
                    foreach (var processingBalance in processingBalanceList)
                    {
                        AddOrUpdateCrypto(processingBalance, processingConfig);

                        var otherPlatformConfigs = platforms.Where(x => x.Key != processingConfig.Key);
                        foreach (var otherPlatformConfig in otherPlatformConfigs)
                        {
                            var otherPlatformBalanceList = otherPlatformConfig.Value.GetBalance();
                            foreach (var otherPlatformBalance in otherPlatformBalanceList)
                                AddOrUpdateCrypto(otherPlatformBalance, otherPlatformConfig);
                        }
                    }
                }

                #region Total
                Cryptos totalCrypto = new Cryptos()
                {
                    SourcePlatforms = "Toplam",
                    TargetPlatform = local.Config.PlatformCurrency + " - " + global.Config.PlatformCurrency,
                    CurrentUnitPrice = local.GetTickerByPair(mutualCurrency + local.Config.PlatformCurrency) / global.GetTickerByPair(mutualCurrency + global.Config.PlatformCurrency),
                    Purchaseds = new List<PurchasedCryptos>()
                };

                totalCrypto.CurrentValue = cryptos.Sum(x => x.CurrentValue * (!x.SourcePlatforms.Contains(localConfig.Key) ? totalCrypto.CurrentUnitPrice : 1)); // Lokal olmayanları lokale çevirerek topla
                //totalCrypto.SpentValue = cryptos.Sum(x => x.SpentValue * (!x.Source.Contains("BTC Türk") ? totalCrypto.CurrentUnitPrice : 1));

                cryptos.Select(x => x.Purchaseds.)
                foreach (var crypto in cryptos)
                {
                    var totalCryptoPurchased = new PurchasedCryptos()
                    {

                    };

                    totalCryptoPurchased.SpentValue = 0;
                    totalCryptoPurchased.ProfitValue = totalCryptoPurchased.SpentValue - totalCryptoPurchased.CurrentValue;
                    totalCryptoPurchased.ProfitPercentage = (totalCryptoPurchased.ProfitValue / totalCryptoPurchased.SpentValue) * 100;
                }

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

        private void AddOrUpdateCrypto(Balance platformBalance, KeyValuePair<string, BaseStockExchange> platformConfig)
        {
            var ieCurrentCrypto = cryptos.Where(x => x.Currency == platformBalance.Currency);
            if (ieCurrentCrypto.Any())
                UpdateCurrency(ieCurrentCrypto.ElementAt(0), platformConfig, platformBalance);
            else
                AddNewCrypto(platformConfig, platformBalance);
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

            crypto.CurrentUnitPrice = platform.GetTickerByPair(crypto.Currency + platform.Config.PlatformCurrency);
            crypto.CurrentValue = crypto.CurrentUnitPrice * crypto.Amount;

            AddPurchased(crypto, platformConfig, platformBalance);

            cryptos.Add(crypto);
        }
        private void UpdateCurrency(Cryptos currentCrypto, KeyValuePair<string, BaseStockExchange> platformConfig, Balance platformBalance)
        {
            var platform = platformConfig.Value;

            currentCrypto.SourcePlatforms += ", " + platformConfig.Key;
            currentCrypto.Amount += platformBalance.Amount;

            string platformCurrency = platform.Config.PlatformCurrency;
            var currentPlatform = platforms[currentCrypto.TargetPlatform];

            if ((platformCurrency != currentPlatform.Config.PlatformCurrency) && (platformCurrency == local.Config.PlatformCurrency || global.Config.PlatformCurrency == currentPlatform.Config.PlatformCurrency))
            {
                currentCrypto.CurrentValue *= platform.GetTickerByPair(mutualCurrency + platform.Config.PlatformCurrency) / currentPlatform.GetTickerByPair(mutualCurrency + currentPlatform.Config.PlatformCurrency);
                currentCrypto.CurrentValue += platformBalance.Amount * platform.GetTickerByPair(currentCrypto.Currency + platform.Config.PlatformCurrency);

                currentCrypto.TargetPlatform = platformCurrency;
            }
            else if (platformCurrency != currentPlatform.Config.PlatformCurrency)
                currentCrypto.CurrentValue += platformBalance.Amount * currentPlatform.GetTickerByPair(mutualCurrency + currentPlatform.Config.PlatformCurrency) / platform.GetTickerByPair(mutualCurrency + platform.Config.PlatformCurrency);
            else
                currentCrypto.CurrentValue += platformBalance.Amount * platform.GetTickerByPair(currentCrypto.Currency + platform.Config.PlatformCurrency);

            currentCrypto.CurrentUnitPrice = currentCrypto.CurrentValue / currentCrypto.Amount;

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
                    Platform = platformConfig.Key,
                    Amount = order.Amount,
                    CurrentUnitPrice = platform.GetTickerByPair(crypto.Currency + platform.Config.PlatformCurrency),
                    SpentValue = order.Price * order.Amount
                };

                purchased.CurrentValue = purchased.Amount * purchased.CurrentUnitPrice;
                purchased.PurchasedUnitPrice = purchased.SpentValue / order.Amount;
                purchased.ProfitValue = crypto.CurrentValue - purchased.SpentValue;
                purchased.ProfitPercentage = purchased.ProfitValue / purchased.SpentValue;

                crypto.Purchaseds.Add(purchased);
            }
        }
    }
}
