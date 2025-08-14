using Dapr.Client;
using Dapr.Workflow;
using System.Diagnostics;
using WisdomPetMedicine.Hospital.Api.Commands;
using WisdomPetMedicine.Hospital.Domain.Entities;
using WisdomPetMedicine.Hospital.Domain.Repositories;
using WisdomPetMedicine.Hospital.Domain.ValueObjects;

namespace WisdomPetMedicine.Hospital.Api.ApplicationServices;

public class HospitalApplicationService(IPatientAggregateStore patientAggregateStore,
                                        DaprClient daprClient,
                                        DaprWorkflowClient daprWorkflowClient,
                                        ILogger<HospitalApplicationService> logger)
{
    public async Task HandleAsync(SetWeightCommand command)
    {
        logger.LogInformation($"Activity Id is: {Activity.Current?.Id}");
        var patient = await patientAggregateStore.LoadAsync(PatientId.Create(command.Id));
        await using (var patientLock = await daprClient.Lock("wisdomlockstore", 
            command.Id.ToString(), Activity.Current?.Id, 60))
        {
            if (!patientLock.Success)
            {
                throw new Exception("Busy");
            }

            logger.LogInformation($"Lock status is: {patientLock.Success}");
            patient.SetWeight(PatientWeight.Create(command.Weight));
            await patientAggregateStore.SaveAsync(patient);

            Thread.Sleep(10000);
        }
    }

    public async Task HandleAsync(SetBloodTypeCommand command)
    {
        var patient = await patientAggregateStore.LoadAsync(PatientId.Create(command.Id));
        patient.SetBloodType(PatientBloodType.Create(command.BloodType));
        await patientAggregateStore.SaveAsync(patient);
    }

    public async Task HandleAsync(AdmitPatientCommand command)
    {
        var instanceId = Activity.Current!.Id!.ToString();

        await daprWorkflowClient.ScheduleNewWorkflowAsync(
            name: nameof(PatientAdmissionWorkflow),
            instanceId: instanceId,
            input: command.Id);

        var workflowState = await daprWorkflowClient.WaitForWorkflowStartAsync(instanceId);

        logger.LogInformation($"""
            The workflow has started. The status is: 
            {Enum.GetName(typeof(WorkflowRuntimeStatus), workflowState.RuntimeStatus)}
            """);

        workflowState = await daprWorkflowClient.WaitForWorkflowCompletionAsync(
            instanceId: instanceId);

        logger.LogInformation($"""
            The workflow has finished. The status is: 
            {Enum.GetName(typeof(WorkflowRuntimeStatus), workflowState.RuntimeStatus)}
            """);

        var workflowOutput = workflowState.ReadOutputAs<PatientAdmissionResult>();

        logger.LogInformation(workflowOutput!.Admitted ?
                "Patient was admitted!" :
                $"Patient was not admitted because: {workflowOutput!.Reason}");

        /*var patient = await patientAggregateStore.LoadAsync(PatientId.Create(command.Id));
        patient.AdmitPatient();
        await patientAggregateStore.SaveAsync(patient);

        var message = $"Patient {patient.Id} admitted";
        await daprClient.InvokeBindingAsync("petoutputbinding", "create", message);*/
    }

    public async Task HandleAsync(DischargePatientCommand command)
    {
        var patient = await patientAggregateStore.LoadAsync(PatientId.Create(command.Id));
        patient.DischargePatient();
        await patientAggregateStore.SaveAsync(patient);
    }

    public async Task HandleAsync(AddProcedureCommand command)
    {
        var patient = await patientAggregateStore.LoadAsync(PatientId.Create(command.Id));
        patient.AddProcedure(Procedure.Create(command.Procedure));
        await patientAggregateStore.SaveAsync(patient);
    }
}

public record PatientAdmissionResult(bool Admitted, string? Reason);
public class PatientAdmissionWorkflow : Workflow<Guid, PatientAdmissionResult>
{
    public override async Task<PatientAdmissionResult> RunAsync(WorkflowContext context, Guid input)
    {
        var isRoomAvailable = await context.CallActivityAsync<bool>(nameof(VerifyRoomAvailabilityActivity), input);

        if (isRoomAvailable)
        {
            return new PatientAdmissionResult(true, null);
        }
        else
        {
            return new PatientAdmissionResult(false, "No room available");
        }
    }
}

public class VerifyRoomAvailabilityActivity(ILogger<VerifyRoomAvailabilityActivity> logger) : WorkflowActivity<Guid, bool>
{
    public override async Task<bool> RunAsync(WorkflowActivityContext context, Guid input)
    {
        logger.LogInformation("Verifying if a room is available...");
        await Task.Delay(3000);
        return Random.Shared.Next(1, 10) <= 7;
    }
}