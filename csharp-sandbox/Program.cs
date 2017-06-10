using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace csharpsandbox{
    class MainClass{
        public static void Main(string[] args){
			var sw = new Stopwatch();
            var proc = new Process();
            proc.StartInfo.FileName = @"node";
            proc.StartInfo.Arguments = 
                @"-e 'a=fs.createWriteStream(""~/date_pipe"",{flags:""w"",defaultEncoding:""utf8""});setInterval(()=>a.write(""""+Date.now()+""\\n""),1000);'";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.RedirectStandardInput = true;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
            sw.Start();
            using (var r = new StreamReader(@"~/date_pipe")){
				string line;
                while (sw.ElapsedMilliseconds < 10000 && (line = r.ReadLine()) != null){
					Console.WriteLine(line);
				}
            }
            proc.Close();
            Console.WriteLine("Hello World!");
         }
    }
}
