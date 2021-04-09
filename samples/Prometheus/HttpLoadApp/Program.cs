using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLoadApp
{
    class Program
    {
        static Random rnd = new Random();

        static void Main(string[] args)
        {
            HttpClient[] clients = new HttpClient[10];
            for (int i=0; i<10; i++) { clients[i] = new HttpClient(); }

            string[] planets = { "Mercury", "Venus", "Earth", "Mars", "Jupiter" };

            foreach (var planet in planets)
            {
                Task.Run( () =>
                {
                     CreateLoad("http://localhost:5000/" + planet, clients);
                });
            }
           var c = Console.ReadKey();
        }

        static async void CreateLoad(string UrlPrefix, HttpClient[] clients)
        {
            int i = 0;
            while(true)
            {
                var client = clients[rnd.Next(clients.Length)];
                var url = UrlPrefix + "/" + i;
                var resp = await client.GetAsync(url);
                var result = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"Requested: {url}, Result: {resp.StatusCode}, Length: {result.Length}");
                i++;
            }
        }
    }
}
