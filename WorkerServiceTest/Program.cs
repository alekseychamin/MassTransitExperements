using WorkerServiceTest;
using MassTransit;

Microsoft.Extensions.Hosting.IHost host = Host.CreateDefaultBuilder(args)
	 .ConfigureServices((context, services) =>
	 {
         services.AddMassTransit(x =>
             {
                 //x.AddConsumer<MessageConsumer>();
                 
                 x.UsingAzureServiceBus((con, cfg) =>
                 {
                     
                     //cfg.MaxSizeInMegabytes = 5120;
                     //cfg.MaxMessageSizeInKilobytes = 1024*3;

                     cfg.Host(context.Configuration.GetConnectionString("ServiceBus"));
                     
                     cfg.ReceiveEndpoint("WorkerService", endPoint =>
                     {
                         
                         
                         //endPoint.MaxSizeInMegabytes = 5120;
                         //endPoint.MaxMessageSizeInKilobytes = 1024*3;
                         //endPoint.ConfigureError();
                         //endPoint.ConfigureReceive();
                         endPoint.Consumer<MessageConsumer>();
                     });

                 });
             });

         services.AddMassTransitHostedService()
                 .AddHostedService<Worker>();
	 })
	 .Build();

await host.RunAsync();
