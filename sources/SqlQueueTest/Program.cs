using Akka.Actor;
using MachinaAurum.AkkaNet.Collections.SqlServer.Actors;
using MachinaAurum.Collections.SqlServer;
using System;

namespace SqlQueueTest
{
    class Program
    {
        static void Main(string[] args)
        {
            var system = ActorSystem.Create("QueueSystem");

            var consoleActor = system.ActorOf<ConsoleActor>();

            var parameters = new SqlQueueParameters("data source=.;initial catalog=KeyValueDB;user id=sa;password=12345678a", "SERVICEORIGIN", "SERVICEDESTINATION", "CONTRACT", "MESSAGETYPE", "QUEUEORIGIN", "QUEUEDESTINATION", "QUEUEBAGGAGE");
            var queueActor = system.ActorOf(SqlQueueActor.Props(parameters, consoleActor));

            system.WhenTerminated.Wait();
        }
    }

    public class ConsoleActor : ReceiveActor
    {
        public ConsoleActor()
        {
            ReceiveAny(x =>
            {
                Console.WriteLine(x.ToString());
            });
        }
    }


    [Serializable]
    public class ItemDto
    {
        public int Int { get; set; }
        public long Long { get; set; }
        public double Double { get; set; }
        public float Float { get; set; }
        public string Text { get; set; }

        public Guid UniqueID { get; set; }

        public DateTime DateTime { get; set; }

        public ChildDto Child { get; set; }

        public ENUM[] Options { get; set; }
        public string[] Strings { get; set; }

        public ItemDto(int id)
        {
            Int = id;
            Long = 5;
            Float = 3.1f;
            Double = 4.2;
            Text = "TEXT";

            UniqueID = Guid.Parse("c060ee98-2527-4a47-88cb-e65263ed4277");
            DateTime = DateTime.UtcNow;

            Options = new[] { ENUM.A, ENUM.B };
            Strings = new[] { "abc", "def" };

            Child = new ChildDto(99);
        }
    }

    public enum ENUM
    {
        A, B, C
    }

    public class ChildDto
    {
        public int Int { get; set; }

        public Child2Dto Child2 { get; set; }

        public ChildDto(int id)
        {
            Int = id;
            Child2 = new Child2Dto("CHILD2");
        }
    }

    public class Child2Dto
    {
        public string Text { get; set; }
        public Child2Dto(string text)
        {
            Text = text;
        }
    }
}
