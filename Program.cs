using System.Diagnostics;
using System.Text.Json;

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
                    Log("usage: gitwatcher [--log | --help | --interval <seconds>]", true);
                    return;
                }
            }

            string? gitVersion = Execute("--version");
            if(gitVersion == null) {
                Log("error: git not found", true);
                return;
            }

            Log("gitwatcher build 1, " +
                gitVersion +
                "\n\tpull interval: " +
                interval +
                " (seconds)\n\tlog: " +
                (log ? "everything" : "important"),
            true);

            bool firstLoop = true;

            Process? p = null;

            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs eventArgs) => {
                if(p != null) {
                    Log("\tkilling process " + p.Id + "...");
                    try {
                        p.Kill();
                    }catch{}
                }
                eventArgs.Cancel = false;
            };

            while(true) {
                string? pullResult = Execute("pull");
                if(pullResult == null || pullResult.StartsWith("fatal")) {
                    Log(pullResult, true);
                    break;
                }
                Log("git pull: '" + pullResult + "'");
                if(pullResult != "Already up to date." || firstLoop) {
                    Log(DateTime.Now.ToShortTimeString() + " - restarting...", true);

                    if(p != null) {
                        Log("\tkilling process " + p.Id + "...");
                        try {
                            p.Kill();
                            p.WaitForExit();
                            Log("\t\tdone");
                        }catch (Exception e){
                            Log("\t\t" + e.Message);
                        }
                    }

                    // wait for process to exit completely
                    Thread.Sleep(100);

                    Log("\treading .gitwatcher/config.json...");

                    if(File.Exists(".gitwatcher/config.json")) {
                        try{
                            Config? cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(".gitwatcher/config.json"));

                            if(cfg != null && cfg.FileName != null) {
                                Log("\tstarting " + cfg.FileName + "...");
                                p = new Process();
                                p.StartInfo.FileName = cfg.FileName;
                                p.StartInfo.UseShellExecute = true;
                                if(cfg.Args != null) {
                                    p.StartInfo.Arguments = cfg.Args;
                                }
                                p.Start();
                                Log("\tprocess id: " + p.Id);
                                Log("\tdone", true);
                            }else{
                                Log("\t\trerror", true);
                            }
                        }catch (Exception e){
                            Log("\t\t" + e.Message, true);
                        }
                    }else{     
                        Log("\t\tconfig.json not found", true);
                    }
                }
                Thread.Sleep(interval * 1000);
                firstLoop = false;
            }
            Log("bye", true);
        }

        static void Log(string? text, bool important = false) {
            if(important || log) {
                Console.WriteLine(text);
            }
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
    }

    class Config {
        public string? FileName {
            get; set;
        }
        public string? Args {
            get; set;
        }
    }
}