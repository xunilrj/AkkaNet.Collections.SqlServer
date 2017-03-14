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
    public class SqlQueueAck
    {

    }

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
                        var dic = new SqlNoMemoryDictionary<Guid, QueuItemEnvelope>();
                        dic.Prepare(parameters.ConnectionString, parameters.BaggageTable.Replace("Baggage", "Status"), "Id", "Status");

                        var queue = new SqlQueue(parameters);
                        queue.CreateObjects();

                        while (true)
                        {
                            Log.Debug("SqlAckQueueActor Resolving {0}", mypath);
                            myref = await system.ActorSelection(mypath).ResolveOne(TimeSpan.FromSeconds(1));
                            Log.Debug("SqlAckQueueActor found {0}", myref);

                            Log.Debug("SqlAckQueueActor Waiting Message...");

                            int index = 0;
                            queue.DequeueGroup<Guid>(dic, x => (x as dynamic).Id, x =>
                            {
                                Log.Debug("SqlAckQueueActor sending message {0}. Waiting ack...", index);

                                Sync.Reset();

                                myref.Tell(x);

                                var result = Sync.WaitOne(60 * 1000);
                                if (result == false)
                                {
                                    Log.Error("SqlAckQueueActor ack timeout. Message {0}", index);
                                    throw new TimeoutException("SqlAckQueueActor ack timeout.");
                                }
                                else
                                {
                                    Log.Debug("SqlAckQueueActor ack {0} received", index);
                                }

                                index++;
                            });

                            Log.Debug("SqlAckQueueActor All Messages Sent.");
                        }
                    }
                    catch (SqlException e) when (e.Number == -2)
                    {
                        Log.Debug("SqlAckQueueActor No Message yet. Starting Again...");
                    }
                    catch (SqlException e) when (e.Number == 9617)
                    {
                        Log.Warning("SqlAckQueueActor Queue disabled. Will wait some time and start again.");
                        await Task.Delay(60 * 1000);
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

            Receive<SqlQueueAck>(x =>
            {
                Log.Debug("SqlAckQueueActor ack received");
                Sync.Set();
            });
            ReceiveAny(x =>
            {
                Log.Debug("SqlAckQueueActor telling {0}", Target.Path);
                Target.Tell(x);
            });
        }
    }
}
