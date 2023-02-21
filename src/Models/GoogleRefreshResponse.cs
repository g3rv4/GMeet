using System.Runtime.Serialization;

namespace GMeet.Models
{
    public class GoogleRefreshResponse
    {
        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "expires_in")]
        public int ExpiresIn { get; set; }
    }
}