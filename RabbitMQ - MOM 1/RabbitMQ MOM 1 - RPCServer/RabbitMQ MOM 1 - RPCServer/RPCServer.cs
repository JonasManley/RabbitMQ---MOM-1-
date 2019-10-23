using System;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;


namespace RabbitMQ_MOM_1___RPCServer
{  
    class RPCServer
    {
        public static void Main()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            using (var connection = factory.CreateConnection())
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(queue: "rpc_queue", durable: false,
                  exclusive: false, autoDelete: false, arguments: null);
                channel.BasicQos(0, 1, false);
                var consumer = new EventingBasicConsumer(channel);
                channel.BasicConsume(queue: "rpc_queue",
                  autoAck: false, consumer: consumer);
                Console.WriteLine(" [x] Awaiting RPC requests");

                consumer.Received += (model, ea) =>
                {
                    string response = null;

                    var body = ea.Body;
                    var props = ea.BasicProperties;
                    var replyProps = channel.CreateBasicProperties();
                    replyProps.CorrelationId = props.CorrelationId;

                    try
                    {
                        var message = Encoding.UTF8.GetString(body);
                        string n = message;

                        if(n == "yes")
                        {
                            Console.WriteLine($" [.] Want offer: {message})");
                            response = GetAdvertisement("autumn");
                        }
                        else if(n == "no")
                        {
                            Console.WriteLine($"[.] Want offer: {message})");
                            response = "You didn't take the offer.";
                        }
                        else if (n == "ok")
                        {
                            Console.WriteLine($"[.] Confirmed: {message})");
                            response = $"You took the offer.";
                        }
                        else if (n == "pass")
                        {
                            Console.WriteLine($"[.] Didn't confirm: {message})");
                            response = "You didn't confirm the offer.";
                        }
                        else
                        {
                            Console.WriteLine("ERROR...");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(" [.] " + e.Message);
                        response = "";
                    }
                    finally
                    {
                        var responseBytes = Encoding.UTF8.GetBytes(response);
                        channel.BasicPublish(exchange: "", routingKey: props.ReplyTo,
                          basicProperties: replyProps, body: responseBytes);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag,
                          multiple: false);
                    }
                };

                Console.WriteLine(" Press [enter] to exit.");
                Console.ReadLine();
            }
        }

        private static string GetAdvertisement(string campaign)
        {

            switch (campaign)
            {
                case "winter":
                    {
                        return "Is it to cold, grab a flight ticket to a warm place";
                    }
                case "summer":
                    {
                        return "Want a nice holiday, book your flight now";
                    }
                case "autumn":
                    {
                        return "Dreams come true this autumn";
                    }
                case "spring":
                    {
                        return "Spring is near, book your flight now";
                    }
            }
            return "Couldn't fint any offers";
        }
    }
}
