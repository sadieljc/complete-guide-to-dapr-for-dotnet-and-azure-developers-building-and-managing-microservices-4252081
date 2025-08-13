using Dapr.AI.Conversation.Extensions;
using Dapr.Client;
using Dapr.Extensions.Configuration;
using Dapr.Jobs;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using WisdomPetMedicine.Rescue.Api.ApplicationServices;
using WisdomPetMedicine.Rescue.Api.Extensions;
using WisdomPetMedicine.Rescue.Api.Infrastructure;
using WisdomPetMedicine.Rescue.Domain.Repositories;

var builder = WebApplication.CreateBuilder(args);
var daprClient = new DaprClientBuilder().Build();
builder.Configuration.AddDaprSecretStore("wisdomsecretstore", daprClient);

// Add services to the container.
builder.Services.AddRescueDb(builder.Configuration);
builder.Services.AddScoped<AdopterApplicationService>();
builder.Services.AddScoped<EmailApplicationService>();
builder.Services.AddScoped<IRescueRepository, RescueRepository>();
builder.Services.AddScoped<IEmailService, DigestEmailService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddControllers()
                .AddDapr();
builder.Services.AddDaprJobsClient();
builder.Services.AddDaprConversationClient();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.EnsureRescueDbIsCreated();
app.UseHttpsRedirection();
app.UseAuthorization();
app.UseCloudEvents();
app.MapSubscribeHandler();
app.MapControllers();

await using var scope = app.Services.CreateAsyncScope();
var daprJobsClient = scope.ServiceProvider.GetRequiredService<DaprJobsClient>();

var schedule = DaprJobSchedule.FromExpression("@every 15s");
await daprJobsClient.ScheduleJobAsync("digest", schedule);

app.MapDaprScheduledJobHandler(async (string jobName,
                    ReadOnlyMemory<byte> jobPayload,
                    EmailApplicationService emailApplicationService) => {
                        switch (jobName)
                        {
                            case "digest":
                                await emailApplicationService.SendDigest();
                                break;
                        }
                    });
app.Run();