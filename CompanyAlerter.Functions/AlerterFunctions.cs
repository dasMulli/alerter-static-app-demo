using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using CompanyAlerter.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;

namespace CompanyAlerter.Functions
{
    public class AlerterFunctions
    {
        private readonly AlerterSecurity security;
        private readonly IGraphServiceClient graphClient;

        public AlerterFunctions(AlerterSecurity security, IGraphServiceClient graphClient)
        {
            this.security = security;
            this.graphClient = graphClient;
        }

        [FunctionName(nameof(SendAlert))]
        public async Task<IActionResult> SendAlert(
            [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = null)]
            HttpRequestMessage req,
            ILogger log
        )
        {
            var principal = await security.ValidateTokenAsync(req.Headers.Authorization);

            if (principal is null || principal.HasClaim("scp", "Alert.Send"))
            {
                log.LogInformation("Principal is null or does not contain required scope claims");
                return new ForbidResult();
            }

            SendAlertRequest sendAlertRequest;
            try
            {
                sendAlertRequest = await req.Content.ReadAsAsync<SendAlertRequest>();
            }
            catch
            {
                return new BadRequestErrorMessageResult("Unable to deserialize request");
            }

            if (sendAlertRequest.RecipientIds is null || sendAlertRequest.RecipientIds.Count == 0 || string.IsNullOrWhiteSpace(sendAlertRequest.Message))
            {
                return new BadRequestErrorMessageResult("Request does not contain all required fields");
            }

            var result = await graphClient.DirectoryObjects
                .GetByIds(sendAlertRequest.RecipientIds, new[] { "user", "group" })
                .Request().PostAsync();

            var phoneTargets = new HashSet<string>();
            var mailTargets = new HashSet<string>();

            void ProcessUser(User user)
            {
                log.LogInformation("Processing user {UserDisplayName}", user.DisplayName);

                if (user.MobilePhone is { } mobilePhone)
                {
                    phoneTargets.Add(mobilePhone);
                }

                if (user.BusinessPhones != null)
                {
                    foreach (var businessPhone in user.BusinessPhones)
                    {
                        phoneTargets.Add(businessPhone);
                    }
                }

                if (user.Mail != null)
                {
                    mailTargets.Add(user.Mail);
                }

                if (user.OtherMails != null)
                {
                    foreach (var mail in user.OtherMails)
                    {
                        mailTargets.Add(mail);
                    }
                }
            }

            foreach (var directoryObject in result)
            {
                switch (directoryObject)
                {
                    case User user:
                        ProcessUser(user);
                        break;
                    case Group group:
                        var membersResult = await graphClient.Groups[@group.Id].TransitiveMembers.Request().GetAsync();
                        log.LogInformation("Processing group {GroupName}", group.DisplayName);
                        foreach (var member in membersResult)
                        {
                            if (member is User user)
                            {
                                ProcessUser(user);
                            }
                        }
                        break;
                }
            }

            log.LogInformation("Would have sent SMS to: {PhoneNumbers}", string.Join(", ", phoneTargets));
            // TODO: Implement SMS..

            log.LogInformation("Sending Mail to: {EMail}", string.Join(", ", mailTargets));

            // send each mail individually
            await Task.WhenAll(mailTargets.Select(mailTarget =>
                graphClient.Users["47ff295a-2486-437e-8129-e7640781fea2"].SendMail(new Message
                    {
                        ToRecipients = new[] {new Recipient {EmailAddress = new EmailAddress {Address = mailTarget}}},
                        Subject = "Alert",
                        Body = new ItemBody
                        {
                            ContentType = BodyType.Text,
                            Content = sendAlertRequest.Message
                        }
                    })
                    .Request().PostAsync()
            ));

            return new OkResult();
        }
    }
}
