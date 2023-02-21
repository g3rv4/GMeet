using System.Runtime.Serialization;

namespace GMeet.Models.Slack
{
    public class UserInfo
    {
        public bool Ok { get; set; }
        public UserData User { get; set; }

        public class UserData
        {
            public ProfileData Profile { get; set; }

            public class ProfileData
            {
                [DataMember(Name = "display_name_normalized")]
                public string DisplayNameNormalized { get; set; }
            }
        }
    }
}