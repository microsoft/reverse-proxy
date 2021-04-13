using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace HttpLoadApp
{
    public class Program
    {
        private static readonly HttpClient client = new HttpClient();
        public static void Main(string[] args)
        {
            string[] planets = { "Mercury", "Venus", "Earth", "Mars", "Jupiter" };

            foreach (var planet in planets)
            {
                Task.Run( () =>
                {
                     CreateLoad("http://localhost:5000/" + planet);
                });
            }
           var c = Console.ReadKey();
        }

        private static async void CreateLoad(string UrlPrefix)
        {
            var rnd = new Random();
            var i = 0;
            while(true)
            {
                var url = UrlPrefix + "/" + i;
                var resp = await client.GetAsync(url);
                var result = await resp.Content.ReadAsStringAsync();
                Console.WriteLine($"Requested: {url}, Result: {resp.StatusCode}, Length: {result.Length}");
                i++;
            }
        }
    }
}
