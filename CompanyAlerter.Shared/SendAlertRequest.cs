using System.Collections.Generic;

namespace CompanyAlerter.Shared
{
    public class SendAlertRequest
    {
        public string Message { get; set; }

        public IList<string> RecipientIds { get; set; }
    }
}
