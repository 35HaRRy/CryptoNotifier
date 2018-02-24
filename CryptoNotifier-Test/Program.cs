using System;
using System.IO;

using Newtonsoft.Json;

using CryptoNotifier;
using CryptoNotifier.Entities;

namespace CryptoNotifierTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Handlers handler = new Handlers();

            string apiConfigStr = File.ReadAllText(Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\APIConfig.txt");
            Requests request = JsonConvert.DeserializeObject<Requests>(apiConfigStr);

            Console.WriteLine(JsonConvert.SerializeObject(handler.GetBalance(request)));
            Console.ReadLine();
        }
    }
}
