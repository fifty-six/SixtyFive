using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace SixtyFive.Services
{
    public class Godbolt : IAsyncInitialized
    {
        public Dictionary<string, HashSet<string>> LanguageCompilers { get; } = new();

        public HashSet<string> Compilers { get; } = new();
        
        public async Task Initialize(IServiceProvider prov, ILogger logger)
        {
            logger.LogInformation("Initializing Godbolt Service");
            
            var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            var req = new HttpRequestMessage
            {
                RequestUri = new Uri("https://godbolt.org/api/compilers"),
                Method = HttpMethod.Get
            };
            
            req.Headers.Add("Accept", "application/json");

            var http = prov.GetRequiredService<HttpClient>();
            
            HttpResponseMessage res = await http.SendAsync(req, cts.Token);

            string str = await res.Content.ReadAsStringAsync(cts.Token);

            JArray json = JArray.Parse(str);

            // ReSharper disable once SuggestVarOrType_Elsewhere
            var pairs = json.Root.Select(x => (x.Value<string>("lang"), x.Value<string>("id")));

            foreach ((string? lang, string? id) in pairs)
            {
                if (id is null || lang is null)
                {
                    logger.LogWarning("Got null in set: {Lang}, {Id}", lang, id);
                    continue;
                }

                if (!LanguageCompilers.TryGetValue(lang, out HashSet<string>? set))
                    LanguageCompilers[lang] = set = new HashSet<string>();

                set.Add(id);

                Compilers.Add(id);
            }
            
            logger.LogInformation("Initialized Godbolt");
        }
    }
}
