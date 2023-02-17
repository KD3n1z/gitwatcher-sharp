using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gitwatcher
{
    internal static class Program
    {
        static int interval = 1;
        static bool log = false;
        static void Main(string[] args) {
            for(int i = 0; i < args.Length; i++) {
                if(args[i] == "--interval" || args[i] == "-i") {
                    interval = int.Parse(args[i + 1]);
                }
                if(args[i] == "--log" || args[i] == "-l") {
                    log = true;
                }
                if(args[i] == "--help" || args[i] == "-h") {
                    Console.WriteLine("usage: gitwatcher [--log | --help | --interval <seconds>]");
                    return;
                }
            }

            string? gitVersion = Execute("--version");
            if(gitVersion == null) {
                Console.WriteLine("error: git not found");
                return;
            }

            Console.WriteLine("gitwatcher build 1, " + gitVersion + "\n\tpull interval: " + interval + " (seconds)\n\tlog: " + (log ? "everything" : "important"));

            bool firstLoop = true;

            while(true) {
                string? pullResult = Execute("pull");
                if(pullResult == null || pullResult.StartsWith("fatal")) {
                    Console.WriteLine(pullResult);
                    break;
                }
                if(log) {
                    Console.WriteLine("git pull: '" + pullResult + "'");
                }
                if(pullResult != "Already up to date." || firstLoop) {
                    Console.WriteLine(DateTime.Now.ToShortTimeString() + " - restarting...");

                    
                }
                Thread.Sleep(interval * 1000);
                firstLoop = false;
            }
            Console.WriteLine("bye");
        }

        static string? Execute(string cmd) {
            try {
                Process p = new Process();
                p.StartInfo.Arguments = cmd;
                p.StartInfo.FileName = "git";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.Start();
                p.WaitForExit();
                return (p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd()).Trim();
            }catch {
                return null;
            }
        }
    
        static void Process_Exited(object? sender, System.EventArgs e)
        {
            if(log) {
                Console.WriteLine("process exited");
            }
        }
    }
}