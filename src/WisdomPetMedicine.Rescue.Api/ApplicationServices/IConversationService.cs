namespace WisdomPetMedicine.Rescue.Api.ApplicationServices;

public interface IConversationService
{
    Task<string> Ask(string name,
                     string breed,
                     int sex,
                     string color,
                     string species,
                     DateTime dateOfBirth);
}