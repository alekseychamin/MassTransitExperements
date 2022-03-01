// See https://aka.ms/new-console-template for more information

using Azure.Messaging.ServiceBus.Administration;


var client = new ServiceBusAdministrationClient("Endpoint=sb://sb-workerservicetest.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=sd//hhh//ptOEL+hT780dshy7K9josnsnyKyA2eQfkY=");
//QueueProperties queue = await client.GetQueueAsync("queuename");
//queue.MaxSizeInMegabytes = 2048;
//queue.MaxMessageSizeInKilobytes = 256;
//QueueProperties updatedQueue = await client.UpdateQueueAsync(queue);

var options = new CreateQueueOptions("queuename")
{
    AutoDeleteOnIdle = TimeSpan.FromDays(7),
    DefaultMessageTimeToLive = TimeSpan.FromDays(2),
    DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(1),
    EnableBatchedOperations = true,
    DeadLetteringOnMessageExpiration = true,
    EnablePartitioning = false,
    ForwardDeadLetteredMessagesTo = null,
    ForwardTo = null,
    LockDuration = TimeSpan.FromSeconds(45),
    MaxDeliveryCount = 8,
    MaxSizeInMegabytes = 2048,
    MaxMessageSizeInKilobytes = 128,
    RequiresDuplicateDetection = true,
    RequiresSession = true,
    UserMetadata = "some metadata"
};

options.AuthorizationRules.Add(new SharedAccessAuthorizationRule(
    "allClaims",
    new[] { AccessRights.Manage, AccessRights.Send, AccessRights.Listen }));

//QueueProperties createdQueue = await client.CreateQueueAsync(options);
var queueProperties = await CreateQueueAsync(options).ConfigureAwait(false);


Task<QueueProperties> CreateQueueAsync(CreateQueueOptions createQueueOptions)
{
    return RunOperation(async () => (await client.CreateQueueAsync(createQueueOptions)).Value);
}

async Task<T> RunOperation<T>(Func<Task<T>> operation)
{
    T result = default;
    result = await operation().ConfigureAwait(false);
    return result;
}
