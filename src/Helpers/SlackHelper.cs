using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using GMeet.Models.Slack;
using Microsoft.Extensions.Configuration;

namespace GMeet.Helpers
{
    public interface ISlackHelper
    {
        Task<string> GetUsernameFromIdAsync(string userId);
    }

    public class SlackHelper : ISlackHelper
    {
        private static string Token { get; set; }
        private static ConcurrentDictionary<string, string> Usernames = new ConcurrentDictionary<string, string>();

        private static HttpClient _client;

        private static HttpClient Client => _client ?? (_client = GetClient());

        private static HttpClient GetClient() =>
            new HttpClient()
            {
                BaseAddress = new Uri("https://slack.com/api/"),
            };

        public SlackHelper(IConfiguration configuration)
        {
            Token = configuration.GetValue<string>("SLACK_TOKEN");
        }

        public async Task<string> GetUsernameFromIdAsync(string userId)
        {
            if (!Usernames.TryGetValue(userId, out var username))
            {
                var content = new List<KeyValuePair<string, string>>();
                content.Add(new KeyValuePair<string, string>("token", Token));
                content.Add(new KeyValuePair<string, string>("user", userId));

                var res = await Client.PostAsync("users.info", new FormUrlEncodedContent(content));
                res.EnsureSuccessStatusCode();

                var body = await res.Content.ReadAsStringAsync();
                var user = Jil.JSON.Deserialize<UserInfo>(body, Jil.Options.CamelCase);

                username = user.User.Profile.DisplayNameNormalized;
                Usernames.TryAdd(userId, username);
            }
            return username;
        }
    }
}