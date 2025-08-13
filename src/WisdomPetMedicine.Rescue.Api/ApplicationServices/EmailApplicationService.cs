using Dapr.Client;
using Microsoft.EntityFrameworkCore;
using WisdomPetMedicine.Rescue.Api.Infrastructure;

namespace WisdomPetMedicine.Rescue.Api.ApplicationServices;

public class EmailApplicationService(RescueDbContext dbContext,
                                     IEmailService emailService,
                                     DaprClient daprClient)
{
    public async Task SendDigest()
    {
        var adopters = await dbContext.Adopters.ToListAsync();
        var petsForAdoption = await dbContext.RescuedAnimalsMetadata.ToListAsync();
        
        foreach (var adopter in adopters)
        {
            var name = await Decrypt(adopter.Name.Value);
            await emailService.SendEmail(name, $"We have {petsForAdoption.Count} pet(s) for adoption!");
        }
    }

    private async Task<string> Decrypt(string text)
    {
        var nameBytes = Convert.FromBase64String(text);
        var decryptedNameBytes = await daprClient.DecryptAsync("wisdomazurekeyvault",
            nameBytes, "wpmkey");
        var decryptedName = System.Text.Encoding.UTF8.GetString(decryptedNameBytes.ToArray());
        return decryptedName;
    }
}