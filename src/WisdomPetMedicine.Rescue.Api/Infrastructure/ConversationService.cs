using Dapr.AI.Conversation;
using WisdomPetMedicine.Rescue.Api.ApplicationServices;

namespace WisdomPetMedicine.Rescue.Api.Infrastructure;

public class ConversationService(DaprConversationClient daprConversationClient) : IConversationService
{
    public async Task<string> Ask(string name, string breed)
    {
        string prompt = $"""
            You are helping create a 'Pets for Adoption' digest.

            For the following pet, generate a warm, engaging, and personal description
            that highlights the pet’s personality and encourages adoption.

            Pet Details:
            - Name: {name}
            - Breed: {breed}

            Output Example:
            "Milo is a sprightly young Jack Russell Terrier with a snowy white coat and caramel patches. 
            He’s full of curiosity, loves a good game of fetch, and would thrive with an active family who enjoys outdoor adventures."
            """;

        var response 
            = await daprConversationClient.ConverseAsync("wisdomconversation",
            [new(prompt, DaprConversationRole.Generic)]);

        if (response != null)
        {
            return response.Outputs.First().Result;
        }

        return name;
    }
}
