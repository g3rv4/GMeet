using System;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using GMeet.Helpers;
using GMeet.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GMeet.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IGoogleMeetHelper _calendarHelper;

        public HomeController(ILogger<HomeController> logger, IGoogleMeetHelper calendarHelper)
        {
            _logger = logger;
            _calendarHelper = calendarHelper;
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

        [Route("{*meetingName}", Order = 1000)]
        public async Task<IActionResult> GoToMeet(string meetingName)
        {
            if (string.IsNullOrWhiteSpace(meetingName))
            {
                return View("Index");
            }

            var meetResult = await _calendarHelper.GetMeetLinkAsync(meetingName);
            if (meetResult.Result == GetMeetLinkResponse.Status.FailureProvisioningMeet)
            {
                // if two users click on a link to a new room at the same time, one of them is going to provision it
                // and the other one will fail to provision. When that happens, give it 500ms and then try again.
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
