using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MassTransit;

namespace WorkerServiceTest
{
    public class MessageConsumer : IConsumer<Message>
    {
        //private readonly ILogger<MessageConsumer> _logger;

        //public MessageConsumer(ILogger<MessageConsumer> logger)
        //{
        //    _logger = logger;
        //}
        public Task Consume(ConsumeContext<Message> context)
        {
            //_logger.LogInformation($"Received Text: {context.Message.Text}");
            //Console.WriteLine($"Received Text: was recevied{context.Message.Text}");
            Console.WriteLine($"Received Text: was recevied");

            throw new InvalidOperationException("We tried but failed!");

            return Task.CompletedTask;
        }
    }
}
