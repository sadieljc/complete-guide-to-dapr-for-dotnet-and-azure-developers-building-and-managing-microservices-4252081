namespace WisdomPetMedicine.Rescue.Api.ApplicationServices;

public interface IEmailService
{
    Task SendEmail(string to, string subject);
}