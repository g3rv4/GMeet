using System;

namespace GMeet.Models
{
    public class CalendarEvent
    {
        public string Id { get; set; }
        public string Summary { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTimeRequest Start { get; set; }
        public DateTimeRequest End { get; set; }
        public ConferenceDataClass ConferenceData { get; set; }


        public class DateTimeRequest
        {
            public string Timezone => "UTC";
            public DateTime DateTime { get; set; }
        }

        public class ConferenceDataClass
        {
            public CreateRequestClass CreateRequest { get; set; }
            public EntryPoint[] EntryPoints { get; set; }

            public class CreateRequestClass
            {
                public string RequestId { get; set; }
                public StatusClass Status { get; set; }

                public class StatusClass
                {
                    public string StatusCode { get; set; }
                }
            }

            public class EntryPoint
            {
                public string EntryPointType { get; set; }
                public string Uri { get; set; }
            }
        }
    }
}