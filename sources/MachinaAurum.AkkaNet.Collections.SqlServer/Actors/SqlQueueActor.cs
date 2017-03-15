﻿using Akka.Actor;
using Akka.Event;
using MachinaAurum.Collections.SqlServer;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace MachinaAurum.AkkaNet.Collections.SqlServer.Actors
{
    public class SqlQueueActor : ReceiveActor
    {
        public static Akka.Actor.Props Props(SqlQueueParameters parameters, IActorRef target = null)
        {
            return Akka.Actor.Props.Create<SqlQueueActor>(parameters, target);
        }

        private readonly ILoggingAdapter Log = Logging.GetLogger(Context);
        private readonly IActorRef Target;

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

            Task.Factory.StartNew(async () =>
            {
                IActorRef myref = null;

                while (true)
                {
                    try
                    {
                        myref = await system.ActorSelection(mypath).ResolveOne(TimeSpan.FromSeconds(1));

                        var queue = new SqlQueue(parameters);
                        queue.CreateObjects();

                        while (true)
                        {
                            Log.Debug("SqlQueueActor Waiting Message...");

                            var items = queue.DequeueGroup().ToArray();

                            Log.Debug("SqlQueueActor Received {0}.", items.Length);

                            myref.Tell(items);

                            Log.Debug("SqlQueueActor All Messages Sent.");
                        }
                    }
                    catch (SqlException e) when (e.Number == -2)
                    {
                        Log.Debug("SqlQueueActor No Message yet. Starting Again");
                    }
                    catch (SqlException e) when (e.Number == 9617)
                    {
                        Log.Warning("SqlQueueActor Queue disabled. Will wait some time and start again.");
                        await Task.Delay(60 * 1000);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e, "SqlQueueActor ({0}) Error", mypath);

                        if (myref != null)
                        {
                            myref.Tell(Kill.Instance);
                        }

                        break;
                    }

                    await Task.Delay(1000);
                }
            });

            ReceiveAny(x =>
            {
                Target.Tell(x);
            });
        }
    }
}
