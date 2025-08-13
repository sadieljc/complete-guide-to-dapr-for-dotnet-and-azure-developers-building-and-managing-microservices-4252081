using WisdomPetMedicine.Rescue.Api.ApplicationServices;

namespace WisdomPetMedicine.Rescue.Api.Infrastructure;

public class DigestEmailService(ILogger<DigestEmailService> logger) : IEmailService
{
    public Task SendEmail(string to, string subject)
    {
        logger.LogInformation($"Sending: '{subject}' to: {to}");
        return Task.CompletedTask;
    }
}