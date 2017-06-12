using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace csharpsandbox {
  static class MainClass {
    
    public static void Main(string[] args) {
      var sw = new Stopwatch();
      sw.Start();
      //AsyncFIFOSandbox().Wait();
      //ParalellSandbox().Wait();
      //AsyncSandbox().Wait();
      AsyncPipeSandbox().Wait();
			sw.Stop();
      Console.WriteLine(String.Format("{0}ms", sw.ElapsedMilliseconds));
      Console.WriteLine("finish");
    }

    static async Task AsyncPipeSandbox() {
      using(var ct = new CancellationTokenSource()){
				var reader = new Task(() => {
					Console.WriteLine("starting task");
					var proc = new Process();
					proc.StartInfo.FileName = @"node";
					proc.StartInfo.Arguments = @"-e 'setInterval(()=>process.stdout.write(""""+Date.now()+""\n""),1000);'";
					proc.StartInfo.UseShellExecute = false;
					proc.StartInfo.RedirectStandardInput = true;
					proc.StartInfo.RedirectStandardOutput = true;
					proc.Start();
					Console.WriteLine("proc started");
					using(var br = new BinaryReader(proc.StandardOutput.BaseStream)) {
						try {
							while(!ct.IsCancellationRequested) {
								var bytes = br.ReadBytes(1);
								var str = System.Text.Encoding.ASCII.GetString(bytes);
								Console.Write(str);
							}
							proc.Close();
							ct.Token.ThrowIfCancellationRequested();
						} catch(EndOfStreamException err) {
							Console.WriteLine("Error writing the data.\n{0}", err.GetType().Name);
							proc.Close();
						}
					}
				});
				reader.Start();
				Console.WriteLine("task started");
				await Task.Delay(10 * 1000);
				try {
					ct.Cancel();
					reader.Wait();
				} catch(AggregateException ae) {
					foreach(var err in ae.InnerExceptions) {
						Console.WriteLine(ae.Message + ": " + err.Message);
					}
				}
      }
			Console.WriteLine("Done!");
			return;
    }

    static async Task AsyncFIFOSandbox() {
      Func<Task> mkfifo = async () => {
        var proc = new Process();
        proc.StartInfo.FileName = @"mkfifo";
        proc.StartInfo.Arguments = @"./date_pipe";
        proc.Start();
        await proc.WaitForExitAsync();
      };

      var ct = new CancellationTokenSource();
      Func<Task> reader = async () => {
        Console.WriteLine("StreamReader");
        using(var r = new StreamReader(@"./date_pipe")) {
          string line;
          while((line = await r.ReadLineAsync()) != null) {
            if(ct.IsCancellationRequested) {
              Console.WriteLine("reader cancelled");
              ct.Token.ThrowIfCancellationRequested();
            }
            Console.WriteLine(line);
          }
        }
        Console.WriteLine("fin StreamReader");
      };

      Func<Task> writer = async () => {
        var proc = new Process();
        proc.StartInfo.FileName = @"node";
        proc.StartInfo.Arguments =
                @"-e 'a=fs.createWriteStream(""./date_pipe"",{flags:""w"",defaultEncoding:""utf8""});setInterval(()=>a.write(""""+Date.now()+""\\n""),1000);'";
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
      await writer(); // wait 10 sec...
      Console.WriteLine("writer stopped");
      ct.Cancel(); // stopping reader

      Console.WriteLine("Done!");
      return;
    }

    static async Task AsyncSandbox(){
			Console.WriteLine("Promise.all test");
			await Task.WhenAll(
			  ((Func<Task>)(async () => {
				  await Task.Delay(100);
				  Console.WriteLine("100");
			  }))(),
			  ((Func<Task>)(async () => {
				  await Task.Delay(200);
				  Console.WriteLine("200");
			  }))()
			);
      return;
    }

    static void ParalellSandbox(){
      // for(int i=0; i<100; i++)
      Parallel.For(0, 100, (i) => {
        Console.WriteLine(String.Format("{0}", i));
      });

      var arr = new string[] { "test1", "test2", "test3" };
      Parallel.ForEach(arr, (line)=>{
        Console.WriteLine(line);
      });

      var lst = new List<string>(arr);

			Parallel.ForEach(lst, (line) => {
				Console.WriteLine(line);
			});
    }

    static Task WaitForExitAsync(this Process proc, CancellationToken ct = default(CancellationToken)) {
      var tcs = new TaskCompletionSource<object>();
      proc.EnableRaisingEvents = true;
      proc.Exited += (sender, args) => {
        tcs.TrySetResult(null);
      };
      if(ct != default(CancellationToken)) {
        ct.Register(tcs.SetCanceled);
      }
      return tcs.Task;
    }
  }
}
