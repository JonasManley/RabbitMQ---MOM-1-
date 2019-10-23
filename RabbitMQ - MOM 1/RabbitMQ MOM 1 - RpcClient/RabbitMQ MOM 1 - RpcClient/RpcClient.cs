using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQ_MOM_1___RpcClient
{
    public class RpcClient
    {
        private const string QUEUE_NAME = "rpc_queue";

        private readonly IConnection connection;
        private readonly IModel channel;
        private readonly string replyQueueName;
        private readonly EventingBasicConsumer consumer;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<string>> callbackMapper =
                    new ConcurrentDictionary<string, TaskCompletionSource<string>>();

        public RpcClient()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };

            connection = factory.CreateConnection();
            channel = connection.CreateModel();
            replyQueueName = channel.QueueDeclare().QueueName;
            consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                if (!callbackMapper.TryRemove(ea.BasicProperties.CorrelationId, out TaskCompletionSource<string> tcs))
                    return;
                var body = ea.Body;
                var response = Encoding.UTF8.GetString(body);
                tcs.TrySetResult(response);
            };
        }

        public Task<string> CallAsync(string message, CancellationToken cancellationToken = default(CancellationToken))
        {
            IBasicProperties props = channel.CreateBasicProperties();
            var correlationId = Guid.NewGuid().ToString();
            props.CorrelationId = correlationId;
            props.ReplyTo = replyQueueName;
            var messageBytes = Encoding.UTF8.GetBytes(message);
            var tcs = new TaskCompletionSource<string>();
            callbackMapper.TryAdd(correlationId, tcs);

            channel.BasicPublish(
                exchange: "",
                routingKey: QUEUE_NAME,
                basicProperties: props,
                body: messageBytes);

            channel.BasicConsume(
                consumer: consumer,
                queue: replyQueueName,
                autoAck: true);

            cancellationToken.Register(() => callbackMapper.TryRemove(correlationId, out var tmp));
            return tcs.Task;
        }

        public void Close()
        {
            connection.Close();
        }
    }

    public class Rpc
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("RPC Client has connected to Agency");
            Console.WriteLine("Would you like to revice the latest offer?");
            string yesOrNo = Console.ReadLine();
            
            if(yesOrNo == "yes")
            {
                Task t = InvokeAsync(yesOrNo);
                t.Wait();
            }
            else if(yesOrNo == "no")
            {
                Task t = InvokeAsync(yesOrNo);
                t.Wait();
            }
            else
            {
                Console.WriteLine("ERROR");
            }
            Console.ReadLine();
        }

        private static async Task InvokeAsync(string n)
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            var rpcClient = new RpcClient();

            Console.WriteLine($" [x] Requesting offer: {n})");

            var response = await rpcClient.CallAsync(n.ToString());
            Console.WriteLine($" [.] Got '{response}'");

            if (response == "You didn't take the offer.")
            {
                Console.WriteLine("you didn' want any offers, so client is shutting down...");
                rpcClient.Close();
            }
            else if (response == "Dreams come true this autumn")
            {
                Console.WriteLine("Do you wanna book your tickets for this offer?");
                Console.WriteLine("Type ok to confirm or pass for no");

                string yesOrNo = Console.ReadLine();
                var response2 = await rpcClient.CallAsync(yesOrNo);
                if (response2 == "You took the offer.")
                {
                    Console.WriteLine("Tickets are now booked");
                }
                else if (response2 == "You didn't confirm the offer.")
                {
                    Console.WriteLine("You said no to the offer, and therefore will the program now shutdown, thanks...");
                }
                else
                {
                    Console.WriteLine("ERROR: Couldn't understand what you typed... shutting down...");
                }
            }
            rpcClient.Close();
        }
    }
}
