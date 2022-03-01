using MassTransit;

namespace WorkerServiceTest
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IBus _bus;

        public Worker(ILogger<Worker> logger, IBus bus)
        {
            _bus = bus;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var message = "hello";//new string('A', 1024 * 2000);

            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);

                await _bus.Publish(new Message() { Text = $"The time is {DateTimeOffset.Now} message to test: {message}" }, stoppingToken);
                

                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
