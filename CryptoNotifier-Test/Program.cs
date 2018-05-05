using System;
using System.IO;

using Amazon.Lambda.APIGatewayEvents;

using CryptoNotifier;

namespace CryptoNotifierTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Handlers handler = new Handlers();

            APIGatewayProxyRequest request = new APIGatewayProxyRequest();
            request.Body = File.ReadAllText(Environment.CurrentDirectory + "\\APIConfig.txt");

            Console.WriteLine(handler.GetBalance(request, null).Body);
            Console.ReadLine();
        }
    }
}
