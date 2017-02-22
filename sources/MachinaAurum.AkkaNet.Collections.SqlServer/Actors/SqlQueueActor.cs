using Akka.Actor;
using MachinaAurum.Collections.SqlServer;
using System;
using System.Threading.Tasks;

namespace MachinaAurum.AkkaNet.Collections.SqlServer.Actors
{
    public class SqlQueueActor : ReceiveActor
    {
        public static Akka.Actor.Props Props(SqlQueueParameters parameters, IActorRef target = null)
        {
            return Akka.Actor.Props.Create<SqlQueueActor>(parameters, target);
        }

        IActorRef Target;

        public SqlQueueActor(SqlQueueParameters parameters, IActorRef target = null)
        {
            if (target != null)
            {
                Target = target;
            }
            else
            {
                Target = Context.Parent;
            }

            var system = Context.System;
            var mypath = Context.Self.Path;

            var queue = new SqlQueue(parameters.ConnectionString, parameters.ServiceOrigin, parameters.ServiceDestination, parameters.Contract, parameters.MessageType, parameters.QueueDestination);
            queue.CreateObjects(parameters.QueueOrigin);

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    var myref = await system.ActorSelection(mypath).ResolveOne(TimeSpan.FromSeconds(1));

                    while(true)
                    {
                        var items = queue.DequeueGroup();
                        foreach (var item in items)
                        {
                            myref.Tell(item);
                        }
                    }
                }
                catch
                {

                }
            });

            ReceiveAny(x =>
            {
                Target.Tell(x);
            });
        }
    }

    public class SqlQueueParameters
    {
        public string ConnectionString { get; set; }
        public string ServiceOrigin { get; set; }
        public string ServiceDestination { get; set; }
        public string Contract { get; set; }
        public string MessageType { get; set; }
        public string QueueOrigin { get; set; }
        public string QueueDestination { get; set; }

        public SqlQueueParameters(string connectionString, string serviceOrigin, string serviceDestination, string contract, string messageType, string queueOrigin, string queueDestination)
        {
            ConnectionString = connectionString;
            ServiceOrigin = serviceOrigin;
            ServiceDestination = serviceDestination;
            Contract = contract;
            MessageType = messageType;
            QueueOrigin = queueOrigin;
            QueueDestination = queueDestination;
        }
    }
}
