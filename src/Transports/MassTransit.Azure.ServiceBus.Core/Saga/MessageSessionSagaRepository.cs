﻿namespace MassTransit.Azure.ServiceBus.Core.Saga
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Context;
    using GreenPipes;
    using Logging;
    using MassTransit.Saga;
    using Metadata;
    using Newtonsoft.Json;
    using Serialization;


    public static class MessageSessionSagaRepository
    {
        public static ISagaRepository<T> Create<T>()
            where T : class, ISaga
        {
            var consumeContextFactory = new SagaConsumeContextFactory<MessageSessionContext, T>();

            var repositoryFactory = new MessageSessionSagaRepositoryContextFactory<T>(consumeContextFactory);

            return new SagaRepository<T>(repositoryFactory);
        }
    }


    /// <summary>
    /// A saga repository that uses the message session in Azure Service Bus to store the state
    /// of the saga.
    /// </summary>
    /// <typeparam name="TSaga">The saga state type</typeparam>
    [Obsolete("This should not be used directly, use MessageSessionSagaRepository.Create<T>() to create a saga repository, or use the container integration")]
    public class MessageSessionSagaRepository<TSaga> :
        ISagaRepository<TSaga>
        where TSaga : class, ISaga
    {
        public void Probe(ProbeContext context)
        {
            var scope = context.CreateScope("sagaRepository");
            scope.Set(new { Persistence = "messageSession" });
        }

        async Task ISagaRepository<TSaga>.Send<T>(ConsumeContext<T> context, ISagaPolicy<TSaga, T> policy, IPipe<SagaConsumeContext<TSaga, T>> next)
        {
            if (!context.TryGetPayload(out MessageSessionContext sessionContext))
            {
                throw new SagaException($"The session-based saga repository requires an active message session: {TypeMetadataCache<TSaga>.ShortName}",
                    typeof(TSaga), typeof(T));
            }

            if (Guid.TryParse(sessionContext.SessionId, out var sessionId))
                context = new CorrelationIdConsumeContextProxy<T>(context, sessionId);

            StartedActivity? activity = LogContext.IfEnabled(OperationName.Saga.Send)?.StartSagaActivity<TSaga, T>(context);
            try
            {
                var saga = await ReadSagaState(sessionContext).ConfigureAwait(false);
                if (saga == null)
                {
                    var missingSagaPipe = new MissingPipe<T>(next, WriteSagaState);

                    await policy.Missing(context, missingSagaPipe).ConfigureAwait(false);
                }
                else
                {
                    SagaConsumeContext<TSaga, T> sagaConsumeContext = new MessageSessionSagaConsumeContext<TSaga, T>(context, sessionContext, saga);

                    LogContext.Debug?.Log("SAGA:{SagaType}:{CorrelationId} Used {MessageType}", TypeMetadataCache<TSaga>.ShortName,
                        context.CorrelationId, TypeMetadataCache<T>.ShortName);

                    await policy.Existing(sagaConsumeContext, next).ConfigureAwait(false);

                    if (!sagaConsumeContext.IsCompleted)
                    {
                        await WriteSagaState(sessionContext, saga).ConfigureAwait(false);

                        LogContext.Debug?.Log("SAGA:{SagaType}:{CorrelationId} Updated {MessageType}", TypeMetadataCache<TSaga>.ShortName,
                            context.CorrelationId, TypeMetadataCache<T>.ShortName);
                    }
                }
            }
            finally
            {
                activity?.Stop();
            }
        }

        Task ISagaRepository<TSaga>.SendQuery<T>(ConsumeContext<T> context, ISagaQuery<TSaga> query, ISagaPolicy<TSaga, T> policy,
            IPipe<SagaConsumeContext<TSaga, T>> next)
        {
            throw new NotImplementedException(
                $"Query-based saga correlation is not available when using the MessageSession-based saga repository: {TypeMetadataCache<TSaga>.ShortName}");
        }

        /// <summary>
        /// Writes the saga state to the message session
        /// </summary>
        /// <param name="context">The message session context</param>
        /// <param name="saga">The saga state</param>
        /// <returns>An awaitable task, of course</returns>
        /// <returns>An awaitable task, of course</returns>
        async Task WriteSagaState(MessageSessionContext context, TSaga saga)
        {
            using var serializeStream = new MemoryStream();
            using var writer = new StreamWriter(serializeStream, Encoding.UTF8, 1024, true);
            using var jsonWriter = new JsonTextWriter(writer);


            JsonMessageSerializer.Serializer.Serialize(jsonWriter, saga);


            await jsonWriter.FlushAsync().ConfigureAwait(false);
            await serializeStream.FlushAsync().ConfigureAwait(false);

            await context.SetStateAsync(new BinaryData(serializeStream.ToArray())).ConfigureAwait(false);
        }

        async Task<TSaga> ReadSagaState(MessageSessionContext context)
        {
            var state = await context.GetStateAsync().ConfigureAwait(false);
            if (state == null)
                return default;

            using var stateStream = state.ToStream();
            if (stateStream.Length == 0)
                return default;


            using var reader = new StreamReader(stateStream, Encoding.UTF8, false, 1024, true);
            using var jsonReader = new JsonTextReader(reader);

            return JsonMessageSerializer.Deserializer.Deserialize<TSaga>(jsonReader);
        }


        /// <summary>
        /// Once the message pipe has processed the saga instance, add it to the saga repository
        /// </summary>
        /// <typeparam name="TMessage"></typeparam>
        class MissingPipe<TMessage> :
            IPipe<SagaConsumeContext<TSaga, TMessage>>
            where TMessage : class
        {
            readonly IPipe<SagaConsumeContext<TSaga, TMessage>> _next;
            readonly Func<MessageSessionContext, TSaga, Task> _writeSagaState;

            public MissingPipe(IPipe<SagaConsumeContext<TSaga, TMessage>> next, Func<MessageSessionContext, TSaga, Task> writeSagaState)
            {
                _next = next;
                _writeSagaState = writeSagaState;
            }

            void IProbeSite.Probe(ProbeContext context)
            {
                _next.Probe(context);
            }

            public async Task Send(SagaConsumeContext<TSaga, TMessage> context)
            {
                var sessionContext = context.GetPayload<MessageSessionContext>();

                var proxy = new MessageSessionSagaConsumeContext<TSaga, TMessage>(context, sessionContext, context.Saga);

                LogContext.Debug?.Log("SAGA:{SagaType}:{CorrelationId} Created {MessageType}", TypeMetadataCache<TSaga>.ShortName,
                    context.Saga.CorrelationId, TypeMetadataCache<TMessage>.ShortName);

                try
                {
                    await _next.Send(proxy).ConfigureAwait(false);

                    if (!proxy.IsCompleted)
                    {
                        await _writeSagaState(sessionContext, proxy.Saga).ConfigureAwait(false);

                        LogContext.Debug?.Log("SAGA:{SagaType}:{CorrelationId} Saved {MessageType}", TypeMetadataCache<TSaga>.ShortName,
                            context.Saga.CorrelationId, TypeMetadataCache<TMessage>.ShortName);
                    }
                }
                catch (Exception)
                {
                    LogContext.Debug?.Log("SAGA:{SagaType}:{CorrelationId} Removed(Fault) {MessageType}", TypeMetadataCache<TSaga>.ShortName,
                        context.Saga.CorrelationId, TypeMetadataCache<TMessage>.ShortName);

                    throw;
                }
            }
        }
    }
}
