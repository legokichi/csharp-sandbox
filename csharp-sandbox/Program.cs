﻿using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

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
        var msgQue = new ConcurrentQueue<bool>();
        var retQue = new ConcurrentQueue<byte[]>();
        var reader = new Task(() => {
          var proc = new Process();
          proc.StartInfo.FileName = @"node";
          proc.StartInfo.Arguments = @"-e 'i=0;setInterval(function(){process.stdout.write((i++)+"":""+Date.now()+""\\n"")},10);'";
          proc.StartInfo.UseShellExecute = false;
          proc.StartInfo.RedirectStandardInput = true;
          proc.StartInfo.RedirectStandardOutput = true;
          proc.StartInfo.RedirectStandardError = true;
          proc.Start();
          var que = new Queue<Byte>();
          using(var br = new BinaryReader(proc.StandardOutput.BaseStream)) {
            while(!ct.IsCancellationRequested) {
              que.Enqueue(br.ReadByte());
              bool flag;
              while(msgQue.Count > 0 && msgQue.TryDequeue(out flag)) {
                retQue.Enqueue(que.ToArray());
                que.Clear();
              }
            }
          }
          Console.WriteLine("cancelled.");
          proc.Close();
          retQue.Enqueue(que.ToArray()); // last
          que.Clear();
          ct.Token.ThrowIfCancellationRequested();
        });
        byte[] readBuf;
        try {
          reader.Start();
          for(var i = 0; i < 10; i++) {
            await Task.Delay(1 * 1000);
            Console.WriteLine("try read({0})", i);
            msgQue.Enqueue(true);

            while(retQue.Count > 0 && retQue.TryDequeue(out readBuf)) {
              var str = System.Text.Encoding.ASCII.GetString(readBuf);
              Console.WriteLine(str);
            }
          }
          ct.Cancel();
          reader.Wait();
        } catch(AggregateException ae) {
          foreach(var err in ae.InnerExceptions) {
            if(err is OperationCanceledException) { continue; }
            Console.WriteLine("{0}: {1}: {2}", ae.Message, err.GetType().Name, err.Message);
            Console.WriteLine(err.StackTrace);
          }
        }
        Console.WriteLine("last read");
        while(retQue.Count > 0 && retQue.TryDequeue(out readBuf)) { // last
          var str = System.Text.Encoding.ASCII.GetString(readBuf);
          Console.WriteLine(str);
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
