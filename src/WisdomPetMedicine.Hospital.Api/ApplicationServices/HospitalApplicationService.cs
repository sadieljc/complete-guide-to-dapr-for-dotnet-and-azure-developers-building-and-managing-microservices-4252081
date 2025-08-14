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

    public async Task HandleAsync(ApprovePatientAdmissionCommand command)
    {
        await daprWorkflowClient.RaiseEventAsync(
            command.WorkflowInstanceId,
            "approved",
            new PatientAdmissionApprovalResult(command.Approved, command.DoctorName));
    }
}

public record PatientAdmissionResult(bool Admitted, string? Reason);
public record PatientAdmissionApprovalResult(bool Approved, string DoctorName);
public class PatientAdmissionWorkflow : Workflow<Guid, PatientAdmissionResult>
{
    public override async Task<PatientAdmissionResult> RunAsync(WorkflowContext context, Guid input)
    {
        var isRoomAvailable = await context.CallActivityAsync<bool>(nameof(VerifyRoomAvailabilityActivity), input);

        if (isRoomAvailable)
        {
            var logger = context.CreateReplaySafeLogger<PatientAdmissionWorkflow>();
            logger.LogInformation("Waiting for approval...");

            var approvedResult = await context.WaitForExternalEventAsync<PatientAdmissionApprovalResult>("approved",
                TimeSpan.FromMinutes(5));

            logger.LogInformation($"Approval received: {approvedResult.Approved} from: {approvedResult.DoctorName}");

            if (!approvedResult.Approved)
            {
                return new PatientAdmissionResult(false, $"Rejected by: {approvedResult.DoctorName}");
            }

            var admitted = await context.CallActivityAsync<PatientAdmissionResult>(nameof(AdmitPatientActivity), input);
            return admitted;
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

public class AdmitPatientActivity(IPatientAggregateStore patientAggregateStore,
                                  DaprClient daprClient,
                                  ILogger<AdmitPatientActivity> logger) : WorkflowActivity<Guid, PatientAdmissionResult>
{
    public override async Task<PatientAdmissionResult> RunAsync(WorkflowActivityContext context, Guid input)
    {
        try
        {
            logger.LogInformation("Admitting the patient...");

            var patient = await patientAggregateStore.LoadAsync(PatientId.Create(input));
            patient.AdmitPatient(); //Still uses DDD's invariants
            await patientAggregateStore.SaveAsync(patient);

            var message = $"Patient {patient.Id} admitted";
            await daprClient.InvokeBindingAsync("petoutputbinding", "create", message);
            
            return new PatientAdmissionResult(true, null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            return new PatientAdmissionResult(false, ex.Message);
        }
    }
}