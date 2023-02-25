using System.Diagnostics;
using System.Runtime.InteropServices;
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

            OSPlatform platform = GetOperatingSystem();

            string platformCfgPath = Path.Combine(".gitwatcher", "config-" + platform.ToString().ToLower() + ".json");
            string cfgPath = Path.Combine(".gitwatcher", "config.json");
            
            string shell = "bash";
            string shellArgs = "-c \"%cmd\"";
            bool replaceQuotes = true;

            if(platform == OSPlatform.Windows) {
                shell = "cmd.exe";
                shellArgs = "/C%cmd";
                replaceQuotes = false;
            }

            string? gitVersion = Execute("--version");
            if(gitVersion == null) {
                Log("error: git not found", true);
                return;
            }

            Log("gitwatcher build 2, " + gitVersion +
                "\n\tpull interval: " + interval + " (seconds)" + 
                "\n\tlog: " + (log ? "everything" : "important") +
                "\n\tplatform: " + platform + " (" + shell + " " + shellArgs + ")",
            true);

            bool firstLoop = true;

            Process? p = null;

            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs eventArgs) => {
                if(p != null) {
                    Log("killing process " + p.Id + "...");
                    try {
                        p.Kill(true);
                        Log("done");
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
                if((pullResult != "Already up to date." && !string.IsNullOrWhiteSpace(pullResult)) || firstLoop) {
                    Log(DateTime.Now.ToShortTimeString() + " - restarting...", true);

                    if(p != null) {
                        Log("\tkilling process " + p.Id + "...");
                        try {
                            p.Kill(true);
                            Log("\t\tdone");
                        }catch (Exception e){
                            Log("\t\t" + e.Message);
                        }
                    }

                    string cCfgPath = File.Exists(platformCfgPath) ? platformCfgPath : cfgPath;

                    if(File.Exists(cCfgPath)) {
                        Log("\treading " + cCfgPath + "...");

                        Config? cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(cCfgPath));

                        try{
                            if(cfg != null && cfg.cmd != null) {
                                p = new Process();
                                p.StartInfo.FileName = cfg.fileName != null ? cfg.fileName : shell;
                                p.StartInfo.UseShellExecute = true;

                                bool cReplaceQuotes = (cfg.replaceQuotes != null ? cfg.replaceQuotes : replaceQuotes) == true;

                                p.StartInfo.Arguments = (cfg.args != null ? cfg.args : shellArgs)
                                    .Replace("%cmd", cReplaceQuotes ? cfg.cmd.Replace("\"", "\\\"") : cfg.cmd);
                                Log("\trunning " + p.StartInfo.FileName + " " + p.StartInfo.Arguments + "... (replaceQuotes=" + cReplaceQuotes + ")");
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
                        Log("\tconfig.json not found :(", true);
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

        static OSPlatform GetOperatingSystem() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) {
                return OSPlatform.OSX;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return OSPlatform.Linux;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return OSPlatform.Windows;
            }

            throw new Exception("Cannot determine operating system!");
        }
    }

    class Config {
        public string? cmd {
            get; set;
        }
        public string? fileName {
            get; set;
        }
        public string? args {
            get; set;
        }
        public bool? replaceQuotes {
            get; set;
        }
    }
}