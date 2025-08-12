using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using WisdomPetMedicine.PetAggregator.Api.Models;

namespace WisdomPetMedicine.PetAggregator.Api.Controllers;
[ApiController]
[Route("[controller]")]
public class PetAggregatorController(DaprClient daprClient,
                                     ILogger<PetAggregatorController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var lastQuery = await daprClient.GetStateEntryAsync<StateModel>("statestore", "lastquery");
        int lastQueryDurationInSeconds = await GetConfiguredLastQueryDurationInSecondsAsync();

        logger.LogInformation($"LastQueryDurationInSeconds is: {lastQueryDurationInSeconds}");

        if (lastQuery.Value != null && DateTime.UtcNow <= lastQuery.Value.LastQuery.AddSeconds(lastQueryDurationInSeconds))
        {
            return Ok(lastQuery.Value.Data);
        }

        IEnumerable<dynamic>? result = null;

        bool saved = false;
        while (!saved)
        {
            result = await QueryPets();
            lastQuery.Value = new StateModel(DateTime.UtcNow, result);
            saved = await lastQuery.TrySaveAsync();
        }

        return Ok(result);
    }

    private async Task<int> GetConfiguredLastQueryDurationInSecondsAsync()
    {
        try
        {
            var configuration = await daprClient.GetConfiguration("wisdomconfigstore", new List<string>() { "LastQueryDurationInSeconds" });
            _ = int.TryParse(configuration.Items.First().Value.Value, out int lastQueryDurationInSeconds);
            return lastQueryDurationInSeconds;
        }
        catch (Exception)
        {
            return 30;
        }
    }

    private async Task<IEnumerable<dynamic>> QueryPets()
    {
        IEnumerable<PatientModel> patients = [];

        var pets = await daprClient.InvokeMethodAsync<IEnumerable<PetModel>>(HttpMethod.Get, "pet", "petquery");
        var rescues = await daprClient.InvokeMethodAsync<IEnumerable<RescueModel>>(HttpMethod.Get, "rescuequery", "rescuequery");

        try
        {
            patients = await daprClient.InvokeMethodAsync<IEnumerable<PatientModel>>(HttpMethod.Get, "hospital", "patientquery");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve the patients.");
        }

        var result = from pet in pets
                     join rescue in rescues on pet.Id equals rescue.Id
                     select new
                     {
                         pet.Id,
                         pet.Name,
                         pet.Breed,
                         pet.Sex,
                         pet.Color,
                         pet.DateOfBirth,
                         pet.Species,
                         Hospital = patients.FirstOrDefault(p => p.Id == pet.Id) is var patient 
                                && patient != null ? 
                             new
                             {
                                 patient.BloodType,
                                 patient.Weight,
                                 patient.Status,
                             } : null,
                         Rescue = new
                         {
                             rescue.AdopterId,
                             rescue.AdopterName,
                             rescue.AdoptionStatus
                         }
                     };
 
        return result;
    }
}