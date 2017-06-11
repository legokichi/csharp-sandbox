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
      //AsyncMain().Wait();
      ParalellSandbox();
      sw.Stop();
      Console.WriteLine(String.Format("{0}ms", sw.ElapsedMilliseconds));
      Console.WriteLine("finish");
    }

    static async Task AsyncMain() {
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
        Console.Write(String.Format("{0}", i));
      });

      var arr = new string[] { "test1", "test2", "test3" };
      Parallel.ForEach(arr, (line)=>{
        Console.Write(line);
      });

      var lst = new List<string>(arr);

			Parallel.ForEach(lst, (line) => {
				Console.Write(line);
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
