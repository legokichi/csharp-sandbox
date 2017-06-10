using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;


namespace csharpsandbox{
    static class MainClass{
        public static void Main(string[] args)
        {
            var sw = new Stopwatch();
            sw.Start();
            AsyncMain().Wait();
            sw.Stop();
            Console.WriteLine(String.Format("{0}ms", sw.ElapsedMilliseconds));
            Console.WriteLine("finish");
        }
		static async Task AsyncMain()
		{
            Func<Task> mkfifo = async () =>
            {
                var proc = new Process();
                proc.StartInfo.FileName = @"mkfifo";
                proc.StartInfo.Arguments = @"/Users/u01749/date_pipe";
                proc.Start();
                await proc.WaitForExitAsync();
            };

			var ct = new CancellationTokenSource();
			Func<Task> reader = async () =>
			{
				Console.WriteLine("StreamReader");
				using (var r = new StreamReader(@"/Users/u01749/date_pipe"))
				{
                    string line;
					while ((line = await r.ReadLineAsync()) != null)
					{
						if (ct.IsCancellationRequested)
						{
							Console.WriteLine("reader cancelled");
							ct.Token.ThrowIfCancellationRequested();
						}
						Console.WriteLine(line);
					}
				}
                Console.WriteLine("fin StreamReader");
			};

            Func<Task> writer = async () =>
            {
                var proc = new Process();
                proc.StartInfo.FileName = @"node";
                proc.StartInfo.Arguments =
                        @"-e 'a=fs.createWriteStream(""/Users/u01749/date_pipe"",{flags:""w"",defaultEncoding:""utf8""});setInterval(()=>a.write(""""+Date.now()+""\\n""),1000);'";
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardInput = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.Start();
                await Task.Delay(10 * 1000);
                proc.Close();
            };

            Console.WriteLine("start mkfifo");
            await mkfifo();
            Console.WriteLine("end mkfifo");
			var reading = reader();
			Console.WriteLine("start reader");

            Console.WriteLine("waiting writer stop");
            await writer();
            Console.WriteLine("writer stopped");
            ct.Cancel();


            Console.WriteLine("Promise.all test");
            await Task.WhenAll(
				((Func<Task>)(async () =>{
					await Task.Delay(100);
                    Console.WriteLine("100");
				}))(),
                ((Func<Task>)(async () =>{
					await Task.Delay(200);
                    Console.WriteLine("200");
				}))()
            );

			Console.WriteLine("Done!");
            return;
		}
		public static Task WaitForExitAsync(this Process proc, CancellationToken ct = default(CancellationToken)){
			var tcs = new TaskCompletionSource<object>();
			proc.EnableRaisingEvents = true;
            proc.Exited += (sender, args) =>{
                tcs.TrySetResult(null);
            };
            if (ct != default(CancellationToken)){
                ct.Register(tcs.SetCanceled);
            }
			return tcs.Task;
		}
	}
}
