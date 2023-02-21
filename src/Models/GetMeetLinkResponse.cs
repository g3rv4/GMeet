namespace GMeet.Models
{
    public class GetMeetLinkResponse
    {
        public Status Result { get; }
        public string Url { get; }

        public GetMeetLinkResponse(Status result, string url = null)
        {
            Result = result;
            Url = url;
        }

        public enum Status
        {
            Unknown = 0,
            Success = 1,
            InvalidName = 2,
            Pending = 3,
            FailureProvisioningMeet = 4,
        }
    }
}