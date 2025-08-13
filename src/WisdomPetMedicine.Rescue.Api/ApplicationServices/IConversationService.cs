namespace WisdomPetMedicine.Rescue.Api.ApplicationServices;

public interface IConversationService
{
    Task<string> Ask(string name, string breed);
}