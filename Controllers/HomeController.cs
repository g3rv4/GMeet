using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using GMeet.Helpers;
using GMeet.Models;
using GMeet.Models.Slack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace GMeet.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGoogleMeetHelper _calendarHelper;
        private readonly ISlackHelper _slackHelper;
        private readonly string _domain;
        private readonly string _slackToken;

        public HomeController(ILogger<HomeController> logger, IGoogleMeetHelper calendarHelper, ISlackHelper slackHelper, IConfiguration configuration)
        {
            _logger = logger;
            _calendarHelper = calendarHelper;
            _slackHelper = slackHelper;
            _domain = configuration.GetValue<string>("GMEET_DOMAIN");
            _slackToken = configuration.GetValue<string>("SLACK_VERIFICATION_TOKEN");
        }

        [Route("favicon.ico")]
        public IActionResult Favicon() => NotFound();

        [AcceptVerbs("GET")]
        [Route("set-authuser")]
        public IActionResult SetAuthUser()
        {
            return View();
        }

        [AcceptVerbs("POST")]
        [Route("set-authuser")]
        [ValidateAntiForgeryToken]
        public IActionResult SetAuthUserSave(string authuser)
        {
            Response.Cookies.Append("authuser", authuser, new Microsoft.AspNetCore.Http.CookieOptions() { Expires = DateTime.UtcNow.AddYears(10) });
            return Content("Done! From now on, you will be redirected using authuser=" + authuser);
        }

        [AcceptVerbs("POST")]
        [Route("slack-command")]
        public async Task<IActionResult> SlackCommand(string user_name, string user_id, string text, string token)
        {
            if (token != _slackToken)
            {
                return BadRequest();
            }

            // Allow empty text to generate a slug based on the caller's username
            if (string.IsNullOrWhiteSpace(text))
            {
                text = await _slackHelper.GetUsernameFromIdAsync(user_id);
            }

            var matches = Regex.Matches(text, @"<@([^\|>]+)[\|>]");
            string slug;

            string slugify(string str)
            {
                var res = Regex.Replace(str.ToLower(), "[^a-z0-9]", "-");
                res = Regex.Replace(res, "-+", "-");
                res.Trim('-');
                return res;
            }

            if (matches.Count > 0)
            {
                // there are mentions. Fun!
                var userIds = new HashSet<string> { user_id };
                foreach (Match match in matches)
                {
                    userIds.Add(match.Groups[1].Value);
                }

                var usernames = new List<string>();
                foreach (var userId in userIds)
                {
                    usernames.Add(await _slackHelper.GetUsernameFromIdAsync(userId));
                }

                var sortedUsernames = usernames.Select(slugify).OrderBy(u => u);
                slug = string.Join("-", sortedUsernames);
            }
            else
            {
                slug = slugify(text);
            }

            var response = new CommandResponse
            {
                ResponseType = "in_channel",
                Text = $"hangout: {_domain}/{slug}"
            };
            return Content(Jil.JSON.Serialize(response, Jil.Options.CamelCase), new MediaTypeHeaderValue("application/json"));
        }

        [Route("{*meetingName}", Order = 1000)]
        public async Task<IActionResult> GoToMeet(string meetingName)
        {
            if (string.IsNullOrWhiteSpace(meetingName))
            {
                return View("Index");
            }

            var meetResult = await _calendarHelper.GetMeetLinkAsync(meetingName);
            if (meetResult.Result == GetMeetLinkResponse.Status.FailureProvisioningMeet ||
                meetResult.Result == GetMeetLinkResponse.Status.Pending)
            {
                // if two users click on a link to a new room at the same time, one of them is going to provision it
                // and the other one will fail to provision.
                //
                // Also, some times a meet room takes a bit of time to be provisioned.
                //
                // When those things that happen, give it 500ms and then try again.
                await Task.Delay(500);
                meetResult = await _calendarHelper.GetMeetLinkAsync(meetingName);
            }

            switch (meetResult.Result)
            {
                case GetMeetLinkResponse.Status.Success:
                    var url = meetResult.Url;
                    url += url.IndexOf('?') >= 0 ? '&' : '?';
                    url += "gmeet=" + HttpUtility.UrlEncode(meetingName);
                    if (Request.Cookies.TryGetValue("authuser", out var authuser))
                    {
                        url += "&authuser=" + authuser;
                    }
                    return Redirect(url);
                case GetMeetLinkResponse.Status.Pending:
                    return Content("A Google Meet is being provisioned... please refresh the page");
                default:
                    return BadRequest("Error: " + meetResult.Result);
            }
        }

        [Route("error")]
        public IActionResult Error() =>
            new ContentResult { Content = "Ooops, internal error", StatusCode = 500 };
    }
}
