using CommandLine;
using MathNet.Numerics.Statistics;
using Pipelines.Sockets.Unofficial;
using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace EchoClient
{

    public class Options
    {
        [Option('h', "help", Required = false, Default = false, HelpText = "print usage info and exit")]
        public bool Help { get; set; }

        [Option('l', "address", Required = false, Default = "127.0.0.1", HelpText = "listening address")]
        public string Address { get; set; }

        [Option('p', "port", Required = false, Default = 10008, HelpText = "listening port")]
        public int Port { get; set; }

        [Option('c', "clients", Required = false, Default = 5000, HelpText = "number of clients")]
        public int Clients { get; set; }

        [Option('r', "round", Required = false, Default = 2, HelpText = "echo round")]
        public int Rounds { get; set; }


        [Option('s', "payload", Required = false, Default = 64, HelpText = "payload size")]
        public int Payload { get; set; }

        public static Options s_Current;
    }

    class Program
    {
        static void Main(string[] args)
        {

            Options options = null;


            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(_options =>
                {
                    options = _options;
                });

            if (options == null)
            {
                return;
            }

            RuntimeTracing.RuntimeEventListener listener = new RuntimeTracing.RuntimeEventListener();

            listener.ThreadPoolWorkerThreadWait += () =>
             {
                 Console.WriteLine("==============> {0} {1} {2} {3} {4} {5} {6}",
                     Interlocked.Read(ref listener.EnqueueCnt),
                     Interlocked.Read(ref listener.DequeueCnt),
                     Interlocked.Read(ref PipeEchoClient.ConnectBeginCnt),
                     Interlocked.Read(ref PipeEchoClient.ConnectFinishCnt),
                     Interlocked.Read(ref PipeEchoClient.WriteBeginCnt),
                     Interlocked.Read(ref PipeEchoClient.WriteBeginCnt),
                     Interlocked.Read(ref PipeEchoClient.ReadFinishCnt)
                     );
             };

            PipeEchoClient[] clients = new PipeEchoClient[options.Clients];
            Task[] echoTasks = new Task[options.Clients];

            Random r = new Random();
            byte[] payload = new byte[options.Payload];
            r.NextBytes(payload);

            IPAddress address = IPAddress.Parse(options.Address);
            EndPoint endpoint = new IPEndPoint(address, options.Port);

            for (int i = 0; i < options.Clients; i++)
            {
                clients[i] = new PipeEchoClient(endpoint, options.Rounds, payload);
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            for (int i = 0; i < options.Clients; i++)
            {
                echoTasks[i] = clients[i].Start();
            }
            Task.WaitAll(echoTasks);
            stopwatch.Stop();


            int errorNum = 0;
            foreach (PipeEchoClient cli in clients)
            {
                if (cli.Error != null)
                {
                    errorNum++;
                }
            }

            Console.WriteLine($"{options.Clients} clients, payload {options.Payload} bytes, {options.Rounds} rounds");
            Console.WriteLine("Total Time Elapsed: {0} Milliseconds", stopwatch.Elapsed.TotalMilliseconds);
            Console.WriteLine("{0} error of {1}", errorNum, options.Clients);

            double[] connect = clients.Where(cli => cli.Error == null).Select(cli => cli.ConnectDuration.TotalMilliseconds).ToArray();

            double[] echo = clients.Where(cli => cli.Error == null).Select(cli => cli.EchoDuration.TotalMilliseconds).ToArray();

            double[] total = clients.Where(cli => cli.Error == null).Select(cli => cli.ConnectDuration.TotalMilliseconds + cli.EchoDuration.TotalMilliseconds).ToArray();


            Console.WriteLine("connect\tp90:{0:N2}ms\tp95:{1:N2}ms\tp99:{2:N2}ms\tp99.9:{3:N2}ms",
                    Percentile(connect, 0.9),
                    Percentile(connect, 0.95),
                    Percentile(connect, 0.99),
                    Percentile(connect, 0.999)
                    );

            Console.WriteLine("echo\tp90:{0:N2}ms\tp95:{1:N2}ms\tp99:{2:N2}ms\tp99.9:{3:N2}ms",
                   Percentile(echo, 0.9),
                   Percentile(echo, 0.95),
                   Percentile(echo, 0.99),
                   Percentile(echo, 0.999)
                   );

            Console.WriteLine("total\tp90:{0:N2}ms\tp95:{1:N2}ms\tp99:{2:N2}ms\tp99.9:{3:N2}ms",
                   Percentile(total, 0.9),
                   Percentile(total, 0.95),
                   Percentile(total, 0.99),
                   Percentile(total, 0.999)
                   );

            Console.WriteLine("==============> {0} {1} {2} {3} {4} {5} {6}",
                Interlocked.Read(ref listener.EnqueueCnt),
                Interlocked.Read(ref listener.DequeueCnt),
                Interlocked.Read(ref PipeEchoClient.ConnectBeginCnt),
                Interlocked.Read(ref PipeEchoClient.ConnectFinishCnt),
                Interlocked.Read(ref PipeEchoClient.WriteBeginCnt),
                Interlocked.Read(ref PipeEchoClient.WriteBeginCnt),
                Interlocked.Read(ref PipeEchoClient.ReadFinishCnt)
    );
        }

        public static double Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            double n = (N - 1) * excelPercentile + 1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d)
                return sequence[0];
            else if (n == N)
                return sequence[N - 1];
            else
            {
                int k = (int)n;
                double d = n - k;
                return sequence[k - 1] + d * (sequence[k] - sequence[k - 1]);
            }
        }


    }
}
