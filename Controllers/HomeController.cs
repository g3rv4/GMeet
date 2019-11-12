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

        [Route("{*meetingName}", Order = 1000)]
        public async Task<IActionResult> GoToMeet(string meetingName)
        {
            if (string.IsNullOrWhiteSpace(meetingName))
            {
                return Content("Specify a meeting name");
            }

            var meetResult = await _calendarHelper.GetMeetLinkAsync(meetingName);
            switch (meetResult.Result)
            {
                case GetMeetLinkResponse.Status.Success:
                    var url = meetResult.Url;
                    url += url.IndexOf('?') >= 0 ? '&' : '?';
                    url += "gmeet=" + HttpUtility.UrlEncode(meetingName);
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
