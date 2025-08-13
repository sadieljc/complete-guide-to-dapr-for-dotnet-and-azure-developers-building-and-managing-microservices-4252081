using Dapper;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace WisdomPetMedicine.RescueQuery.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class RescueQueryController(IConfiguration configuration, 
                                   DaprClient daprClient) : ControllerBase
{
    [HttpGet("adopters")]
    public async Task<IActionResult> GetAdopters()
    {
        var sql = "SELECT * FROM Adopters";
        using var connection = new SqlConnection(configuration.GetValue<string>("Rescue"));
        var adopters = (await connection.QueryAsync(sql)).ToList();
        foreach (IDictionary<string, object> item in adopters)
        {
            var encryptedName = item["Name_Value"].ToString();
            var nameBytes = Convert.FromBase64String(encryptedName);
            var decryptedNameBytes = await daprClient.DecryptAsync("wisdomazurekeyvault",
                nameBytes, "wpmkey");
            var decryptedName = System.Text.Encoding.UTF8.GetString(decryptedNameBytes.ToArray());
            item["Name_Value"] = decryptedName;
        };
        
        return Ok(adopters);
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        Thread.Sleep(8000);

        string sql = @"SELECT 
                        ram.Id, 
                        ram.Name,
                        ram.Breed,
                        ram.Color,
                        Sex = 
                        CASE ram.Sex
	                        WHEN 0 THEN 'Male'
	                        WHEN 1 THEN 'Female'
                        END,
                        ra.AdopterId_Value AS AdopterId, 
                        AdoptionStatus =
                        CASE ra.AdoptionStatus
	                        WHEN 0 THEN 'None'
	                        WHEN 1 THEN 'Pending review'
	                        WHEN 2 THEN 'Accepted'
	                        WHEN 3 THEN 'Rejected'
                        END,
                        a.Name_Value as AdopterName, 
                        a.Questionnaire_DoYouRent,
                        a.Questionnaire_HasChildren,
                        a.Questionnaire_HasFencedYard,
                        a.Questionnaire_IsActivePerson,
                        a.Address_Street,
                        a.Address_Number,
                        a.Address_City,
                        a.Address_PostalCode,
                        a.Address_Country,
                        a.PhoneNumber_Value
                        FROM RescuedAnimalsMetadata ram
                        JOIN RescuedAnimals ra ON ram.Id = ra.Id
                        LEFT JOIN Adopters a ON ra.AdopterId_Value = a.Id";
        using var connection = new SqlConnection(configuration.GetValue<string>("Rescue"));
        var orderDetail = (await connection.QueryAsync(sql)).ToList();
        return Ok(orderDetail);
    }
}