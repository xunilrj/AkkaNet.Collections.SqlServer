using Akka.Actor;
using Akka.Event;
using MachinaAurum.Collections.SqlServer;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MachinaAurum.AkkaNet.Collections.SqlServer.Actors
{
    public class SqlAckQueueActor : ReceiveActor
    {
        public static Akka.Actor.Props Props(SqlQueueParameters parameters, IActorRef target = null)
        {
            return Akka.Actor.Props.Create<SqlAckQueueActor>(parameters, target);
        }

        private readonly ILoggingAdapter Log = Logging.GetLogger(Context);
        private readonly IActorRef Target;

        ManualResetEvent Sync;

        public SqlAckQueueActor(SqlQueueParameters parameters, IActorRef target = null)
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

            Sync = new ManualResetEvent(false);

            Task.Factory.StartNew(async () =>
            {
                IActorRef myref = null;

                while (true)
                {
                    try
                    {
                        myref = await system.ActorSelection(mypath).ResolveOne(TimeSpan.FromSeconds(1));

                        var dic = new SqlNoMemoryDictionary<Guid, QueuItemEnvelope>();
                        dic.Prepare(parameters.ConnectionString, parameters.BaggageTable.Replace("Baggage", "Status"), "Id", "Status");

                        var queue = new SqlQueue(parameters);
                        queue.CreateObjects();

                        while (true)
                        {
                            Log.Debug("SqlAckQueueActor Waiting Message...");

                            int index = 0;
                            queue.DequeueGroup<Guid>(dic, x => (x as dynamic).Id, x =>
                            {
                                Log.Debug("SqlAckQueueActor sending message {Index}. Waiting ack...", index);

                                Sync.Reset();

                                myref.Tell(x);

                                var result = Sync.WaitOne(60 * 1000);
                                if (result == false)
                                {
                                    Log.Error("SqlAckQueueActor ack timeout. Message {Index}", index);
                                    throw new TimeoutException("SqlAckQueueActor ack timeout.");
                                }
                                else
                                {
                                    Log.Debug("SqlAckQueueActor ack {Index} received", index);
                                }

                                index++;
                            });

                            Log.Debug("SqlAckQueueActor All Messages Sent.");
                        }
                    }
                    catch (SqlException e) when (e.Number == -2)
                    {
                        Log.Debug("SqlAckQueueActor No Message yet. Starting Again");
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "SqlAckQueueActor ({0}) Error", mypath);

                        if (myref != null)
                        {
                            myref.Tell(Kill.Instance);
                        }

                        break;
                    }

                    await Task.Delay(1000);
                }
            });

            Become(WaitMessage);
        }

        private void WaitMessage()
        {
            Log.Debug("SqlAckQueueActor became WaitMessage");
            ReceiveAny(x =>
            {
                Become(WaitAck);
                Target.Tell(x);
            });
        }

        private void WaitAck()
        {
            Log.Debug("SqlAckQueueActor became WaitAck");
            ReceiveAny(x =>
            {
                Become(WaitMessage);
                Sync.Set();
            });
        }
    }
}
