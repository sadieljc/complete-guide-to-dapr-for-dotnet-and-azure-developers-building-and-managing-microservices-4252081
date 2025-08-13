using Dapr.AI.Conversation;
using WisdomPetMedicine.Rescue.Api.ApplicationServices;

namespace WisdomPetMedicine.Rescue.Api.Infrastructure;

public class ConversationService(DaprConversationClient daprConversationClient,
                                 ILogger<ConversationService> logger) : IConversationService
{
    public async Task<string> Ask(string name, string breed, int sex, string color, string species, DateTime dateOfBirth)
    {
        try
        {
            string prompt = $"""
            You are helping create a 'Pets for Adoption' digest.

            For the following pet, generate a warm, engaging, and personal description
            that highlights the pet’s personality and encourages adoption.

            Pet Details:
            - Name: {name}
            - Breed: {breed}
            - Sex: {sex} (0 = male, 1 = female)
            - Color: {color}
            - Species: {species}
            - Date of Birth: {dateOfBirth}

            Instructions:
            1. Mention the breed, color, and species naturally in the description.
            2. Infer likely personality traits or behaviors from the breed and species 
               (e.g., Border Collie → energetic and smart, Persian cat → calm and affectionate).
            3. Adapt tone to the pet’s approximate age 
               (younger = playful/curious, older = calm/wise).
            4. Keep it short: 2–3 sentences, warm, and adoption-focused.
            5. Avoid generic filler text; make it feel unique and tailored.

            Output Example:
            "Milo is a sprightly young Jack Russell Terrier with a snowy white coat and caramel patches. 
            He’s full of curiosity, loves a good game of fetch, and would thrive with an active family who enjoys outdoor adventures."
            """;

            var options = new ConversationOptions() { Temperature = 0.7 };
            var response
                = await daprConversationClient.ConverseAsync("wisdomconversation",
                [new(prompt, DaprConversationRole.Generic)], options: options);

            if (response != null)
            {
                return response.Outputs.First().Result;
            }

            return name;
        }
        catch (Exception ex)
        {
            logger.LogError("Error: " + ex.Message);
            return null;
        }
    }
}