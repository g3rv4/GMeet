using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using GMeet.Models;
using Jil;
using Microsoft.Extensions.Configuration;

namespace GMeet.Helpers
{
    public interface IGoogleMeetHelper
    {
        Task<GetMeetLinkResponse> GetMeetLinkAsync(string meetName);
    }

    public class GoogleMeetHelper : IGoogleMeetHelper
    {
        private string RefreshToken { get; set; }
        private string ClientId { get; set; }
        private string ClientSecret { get; set; }
        private string CalendarId { get; set; }
        private string Domain { get; set; }

        private static readonly ConcurrentDictionary<string, GetMeetLinkResponse> MeetLinks = new ConcurrentDictionary<string, GetMeetLinkResponse>();
        private static string _accessToken { get; set; }
        private static DateTime? _accessTokenExpirationDate { get; set; }
        private static readonly object _accessTokenLock = new object();

        private async Task RefreshTokenIfNeededAsync()
        {
            bool needsRefresh;
            lock (_accessTokenLock)
            {
                needsRefresh = string.IsNullOrEmpty(_accessToken) ||
                               !_accessTokenExpirationDate.HasValue ||
                               _accessTokenExpirationDate.Value <= DateTime.UtcNow;
            }

            if (needsRefresh)
            {
                var form = new Dictionary<string, string>
                {
                    ["client_id"] = ClientId,
                    ["client_secret"] = ClientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = RefreshToken
                };
                var result = await Client.PostAsync("oauth2/v4/token", new FormUrlEncodedContent(form));
                result.EnsureSuccessStatusCode();

                var json = await result.Content.ReadAsStringAsync();
                var response = Jil.JSON.Deserialize<GoogleRefreshResponse>(json);

                lock (_accessTokenLock)
                {
                    _accessToken = response.AccessToken;
                    _accessTokenExpirationDate = DateTime.UtcNow.AddSeconds(response.ExpiresIn - 10);
                    _authenticatedClient = null;
                }
            }
        }

        private static HttpClient _client;
        private static HttpClient Client => _client ?? (_client = GetClient());
        private static HttpClient GetClient(bool authenticated = false)
        {
            var client = new HttpClient()
            {
                BaseAddress = new Uri("https://www.googleapis.com/"),
            };
            if (authenticated)
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + _accessToken);
            }
            return client;
        }

        private static HttpClient _authenticatedClient;
        private static HttpClient AuthenticatedClient => _authenticatedClient ?? (_authenticatedClient = GetClient(authenticated: true));

        private static string GetEventId(string meetName)
        {
            var bytes = Encoding.ASCII.GetBytes(meetName);
            var encoded = Base32HexEncoding.ToString(bytes).Replace("=", "").ToLower();
            if (encoded.Length < 5 || encoded.Length > 1024)
            {
                return null;
            }
            return encoded;
        }

        private async Task<GetMeetLinkResponse> GetMeetUrlFromMeetName(string meetName)
        {
            var eventId = GetEventId(meetName);
            if (eventId == null)
            {
                return new GetMeetLinkResponse(GetMeetLinkResponse.Status.InvalidName);
            }

            await RefreshTokenIfNeededAsync();

            string json;
            bool justCreated = false;

            var result = await AuthenticatedClient.GetAsync($"calendar/v3/calendars/{CalendarId}/events/{eventId}");
            if (result.StatusCode == HttpStatusCode.NotFound)
            {
                // there's no event, create it!
                var request = new CalendarEvent
                {
                    Id = eventId,
                    Summary = meetName,
                    Description = $"https://{Domain}/{meetName}",
                    Start = new CalendarEvent.DateTimeRequest
                    {
                        DateTime = DateTime.UtcNow
                    },
                    End = new CalendarEvent.DateTimeRequest
                    {
                        DateTime = DateTime.UtcNow.AddMinutes(30)
                    },
                    ConferenceData = new CalendarEvent.ConferenceDataClass
                    {
                        CreateRequest = new CalendarEvent.ConferenceDataClass.CreateRequestClass
                        {
                            RequestId = eventId
                        }
                    }
                };
                json = JSON.Serialize(request, Options.ISO8601ExcludeNullsCamelCase);
                var content = new StringContent(json);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                result = await AuthenticatedClient.PostAsync($"calendar/v3/calendars/{CalendarId}/events?conferenceDataVersion=1", content);
                justCreated = true;
            }
            if (result.StatusCode != HttpStatusCode.OK)
            {
                return new GetMeetLinkResponse(GetMeetLinkResponse.Status.FailureProvisioningMeet);
            }

            if (!justCreated)
            {
                // let's update it so that active events keep on moving forward in the calendar. It's really not needed, but I like it better
                var patchRequet = new CalendarEvent
                {
                    Description = $"https://{Domain}/{meetName}",
                    Start = new CalendarEvent.DateTimeRequest
                    {
                        DateTime = DateTime.UtcNow
                    },
                    End = new CalendarEvent.DateTimeRequest
                    {
                        DateTime = DateTime.UtcNow.AddMinutes(30)
                    },
                    Status = "confirmed"
                };
                json = JSON.Serialize(patchRequet, Options.ISO8601ExcludeNullsCamelCase);
                var content = new StringContent(json);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                result = await AuthenticatedClient.PatchAsync($"calendar/v3/calendars/{CalendarId}/events/{eventId}", content);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return new GetMeetLinkResponse(GetMeetLinkResponse.Status.FailureProvisioningMeet);
                }
            }

            json = await result.Content.ReadAsStringAsync();
            var calEvent = JSON.Deserialize<CalendarEvent>(json, Options.ISO8601CamelCase);

            switch (calEvent.ConferenceData?.CreateRequest?.Status?.StatusCode)
            {
                case "pending": return new GetMeetLinkResponse(GetMeetLinkResponse.Status.Pending);
                case "failure": return new GetMeetLinkResponse(GetMeetLinkResponse.Status.FailureProvisioningMeet);
            }

            var url = calEvent.ConferenceData?.EntryPoints?.FirstOrDefault(ep => ep.EntryPointType == "video")?.Uri;
            if (string.IsNullOrEmpty(url))
            {
                return new GetMeetLinkResponse(GetMeetLinkResponse.Status.FailureProvisioningMeet);
            }

            return new GetMeetLinkResponse(GetMeetLinkResponse.Status.Success, url);
        }

        public async Task<GetMeetLinkResponse> GetMeetLinkAsync(string meetName)
        {
            if (!MeetLinks.TryGetValue(meetName, out var response))
            {
                response = await GetMeetUrlFromMeetName(meetName);
                if (response.Result == GetMeetLinkResponse.Status.Success)
                {
                    MeetLinks.TryAdd(meetName, response);
                }
            }

            return response;
        }

        public GoogleMeetHelper(IConfiguration configuration)
        {
            RefreshToken = configuration.GetValue<string>("GOOGLE_REFRESH_TOKEN");
            ClientId = configuration.GetValue<string>("GOOGLE_CLIENT_ID");
            ClientSecret = configuration.GetValue<string>("GOOGLE_CLIENT_SECRET");
            CalendarId = configuration.GetValue<string>("GOOGLE_CALENDAR_ID");
            Domain = configuration.GetValue<string>("GMEET_DOMAIN");
        }
    }
}