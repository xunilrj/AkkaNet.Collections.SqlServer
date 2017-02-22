using Akka.Actor;
using Akka.TestKit.Xunit2;
using MachinaAurum.AkkaNet.Collections.SqlServer.Actors;
using MachinaAurum.Collections.SqlServer;
using Xunit;

namespace MachinaAurum.AkkaNet.Collections.SqlServer.Tests
{
    public class SqlQueueActorTests : TestKit
    {
        [Fact]
        public void MustFowardMessageToItsTarget()
        {
            var testActor = CreateTestProbe("test");
            var parameters = new SqlQueueParameters("data source=.;initial catalog=KeyValueDB;user id=sa;password=12345678a", "SERVICEORIGIN", "SERVICEDESTINATION", "CONTRACT", "MESSAGETYPE", "QUEUEORIGIN", "QUEUEDESTINATION", "QUEUEBAGGAGE");
            var queueActor = Sys.ActorOf(SqlQueueActor.Props(parameters, testActor));

            testActor.Send(queueActor, 12);

            var result = testActor.ExpectMsg<int>();
            Assert.Equal(12, result);
        }

        [Fact]
        public void MustFowardToItsParentIfTargetIsNull()
        {
            var testActor = CreateTestProbe("test");
            var props = Props.Create<ParentActor>(testActor);
            var parent = Sys.ActorOf(props);

            testActor.Send(parent, 12);

            var result = testActor.ExpectMsg<int>();
            Assert.Equal(12, result);
        }
    }

    public class ParentActor : ReceiveActor
    {
        IActorRef Target;

        public ParentActor(IActorRef target)
        {
            Target = target;

            var parameters = new SqlQueueParameters("data source=.;initial catalog=KeyValueDB;user id=sa;password=12345678a", "SERVICEORIGIN", "SERVICEDESTINATION", "CONTRACT", "MESSAGETYPE", "QUEUEORIGIN", "QUEUEDESTINATION", "QUEUEBAGGAGE");
            var queue = Context.ActorOf(SqlQueueActor.Props(parameters));

            ReceiveAny(x =>
            {
                if (Sender == queue)
                {
                    Target.Forward(x);
                }
                else
                {
                    queue.Forward(x);
                }
            });
        }
    }
}
