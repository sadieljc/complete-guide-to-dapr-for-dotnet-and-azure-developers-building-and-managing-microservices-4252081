using Dapr.Client;
using System.Collections;
using System.Text;
using WisdomPetMedicine.Rescue.Api.Commands;
using WisdomPetMedicine.Rescue.Domain.Entities;
using WisdomPetMedicine.Rescue.Domain.Events;
using WisdomPetMedicine.Rescue.Domain.Repositories;
using WisdomPetMedicine.Rescue.Domain.ValueObjects;

namespace WisdomPetMedicine.Rescue.Api.ApplicationServices;

public class AdopterApplicationService
{
    private readonly IRescueRepository rescueRepository;
    private readonly DaprClient daprClient;

    public AdopterApplicationService(IRescueRepository rescuedAnimalRepository,
                                     IServiceScopeFactory serviceScopeFactory,
                                     DaprClient daprClient)
    {
        this.rescueRepository = rescuedAnimalRepository;
        this.daprClient = daprClient;

        DomainEvents.AdoptionRequestCreated.Register(async e =>
        {
            using var scope = serviceScopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRescueRepository>();
            var rescuedAnimal = await repo.GetRescuedAnimalAsync(RescuedAnimalId.Create(e.RescuedAnimalId));
            rescuedAnimal.RequestToAdopt(AdopterId.Create(e.AdopterId));
            await repo.UpdateRescuedAnimalAsync(rescuedAnimal);
        });
    }

    public async Task HandleCommandAsync(CreateAdopterCommand command)
    {
        var adopter = new Adopter(AdopterId.Create(command.Id));

        ReadOnlyMemory<byte> textBytes = Encoding.UTF8.GetBytes(command.Name)
                                                      .AsMemory();
        var options = new EncryptionOptions(KeyWrapAlgorithm.Rsa);
        var result = await daprClient.EncryptAsync("wisdomazurekeyvault", 
            textBytes, "wpmkey", options);

        var encryptedName = Convert.ToBase64String(result.ToArray());

        adopter.SetName(AdopterName.Create(encryptedName));
        adopter.SetAddress(AdopterAddress.Create(command.Address.Street,
                                                 command.Address.Number,
                                                 command.Address.City,
                                                 command.Address.PostalCode,
                                                 command.Address.Country));
        adopter.SetQuestionnaire(AdopterQuestionnaire.Create(command.Questionnaire.IsActivePerson,
                                                             command.Questionnaire.DoYouRent,
                                                             command.Questionnaire.HasFencedYard,
                                                             command.Questionnaire.HasChildren));
        adopter.SetPhoneNumber(AdopterPhoneNumber.Create(command.PhoneNumber));
        await rescueRepository.AddAdopterAsync(adopter);
    }

    public async Task HandleCommandAsync(RequestAdoptionCommand command)
    {
        var adopter = await rescueRepository.GetAdopterAsync(AdopterId.Create(command.AdopterId));
        adopter.RequestToAdopt(RescuedAnimalId.Create(command.PetId));
        await rescueRepository.UpdateAdopterAsync(adopter);
    }

    public async Task HandleCommandAsync(ApproveAdoptionCommand command)
    {
        var adopter = await rescueRepository.GetAdopterAsync(AdopterId.Create(command.AdopterId));
        adopter.RequestToAdopt(RescuedAnimalId.Create(command.PetId));
        await rescueRepository.UpdateAdopterAsync(adopter);
    }

    public async Task HandleCommandAsync(RejectAdoptionCommand command)
    {
        var adopter = await rescueRepository.GetAdopterAsync(AdopterId.Create(command.AdopterId));
        adopter.RequestToAdopt(RescuedAnimalId.Create(command.PetId));
        await rescueRepository.UpdateAdopterAsync(adopter);
    }

    public async Task HandleCommandAsync(SetAdopterPhoneNumberCommand command)
    {
        var adopter = await rescueRepository.GetAdopterAsync(AdopterId.Create(command.Id));
        adopter.SetPhoneNumber(AdopterPhoneNumber.Create(command.PhoneNumber));
        await rescueRepository.UpdateAdopterAsync(adopter);
    }
}
