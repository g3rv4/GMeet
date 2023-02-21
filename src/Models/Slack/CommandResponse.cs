using System.Runtime.Serialization;

namespace GMeet.Models.Slack
{
    public class CommandResponse
    {
        [DataMember(Name = "response_type")]
        public string ResponseType { get; set; }

        public string Text { get; set; }
    }
}