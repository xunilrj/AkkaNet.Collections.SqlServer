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

            var queue = new SqlQueue(parameters);
            queue.CreateObjects();

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
}
