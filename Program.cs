using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace gitwatcher
{
    internal static class Program
    {
        public const string version = "1.0.2";

            
        static string shell = "sh";
        static string shellArgs = "-c \"%cmd\"";
        static bool replaceQuotes = true;
        static string platformCfgPath = "";
        static string cfgPath = "";
        static Process? appProcess = null;

        static int interval = 60;
        static bool log = false;
        static void Main(string[] args) {
            OSPlatform platform = GetOperatingSystem();

            platformCfgPath = Path.Combine(".gitwatcher", "config-" + platform.ToString().ToLower() + ".json");
            cfgPath = Path.Combine(".gitwatcher", "config.json");

            for(int i = 0; i < args.Length; i++) {
                switch(args[i]) {
                    case "--interval":
                    case "-i":
                        interval = int.Parse(args[++i]);
                        break;
                    case "--log":
                    case "-l":
                        log = true;
                        break;
                    case "--help":
                    case "-h":
                        Log(
                            "gitwatcher v" + version + 
                            "\n\nUsage: gitwatcher [options]\n\nOptions:" + 
                            "\n\t-i --interval <seconds>\tPull interval. (Default value: 60)" + 
                            "\n\t-l --log\t\tLog each action." + 
                            "\n\t-h --help\t\tPrint usage." + 
                            "\n\t-v --version\t\tPrint current version." + 
                            "\n\t-u --checkForUpdates\tCheck for newer versions on github.", true);
                        return;
                    case "--version":
                    case "-v":
                        Log("gitwatcher v" + version, true);
                        return;
                    case "-u":
                    case "--checkForUpdates":
                        CheckForUpdates();
                        return;
                    default:
                        Error(args[i] + ": invalid option");
                        return;
                }
            }

            if(platform == OSPlatform.Windows) {
                shell = "cmd.exe";
                shellArgs = "/C%cmd";
                replaceQuotes = false;
            }

            string? gitVersion = ExecuteGitCommand("--version");
            if(gitVersion == null) {
                Error("git not found");
                return;
            }

            Log("gitwatcher v" + version + ", " + gitVersion +
                "\n\tpull interval: " + interval + " (seconds)" + 
                (log ? "\n\tlog: everything" : "") +
                "\n\tplatform: " + platform + " (" + shell + " " + shellArgs + ")",
            true);

            bool firstLoop = true;

            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs eventArgs) => {
                if(appProcess != null) {
                    Log("killing process " + appProcess.Id + "...");
                    try {
                        appProcess.Kill(true);
                        Log("done");
                    }catch{}
                }
                eventArgs.Cancel = false;
            };

            while(true) {
                string? pullResult = ExecuteGitCommand("pull");
                if(pullResult == null || pullResult.StartsWith("fatal") || pullResult.StartsWith("error")) {
                    Log(pullResult, true);
                    break;
                }
                Log("git pull: '" + pullResult + "'");
                if((pullResult != "Already up to date." && !string.IsNullOrWhiteSpace(pullResult)) || firstLoop) {
                    RestartApp();
                }
                Thread.Sleep(interval * 1000);
                firstLoop = false;
            }
            Log("bye", true);
        }

        static void RestartApp() {
            Log(DateTime.Now.ToShortTimeString() + " - restarting...", true);

            if(appProcess != null) {
                Log("\tkilling process " + appProcess.Id + "...");
                try {
                    appProcess.Kill(true);
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
                        appProcess = new Process();
                        appProcess.StartInfo.FileName = cfg.fileName != null ? cfg.fileName : shell;
                        appProcess.StartInfo.UseShellExecute = true;

                        bool cReplaceQuotes = (cfg.replaceQuotes != null ? cfg.replaceQuotes : replaceQuotes) == true;

                        appProcess.StartInfo.Arguments = (cfg.args != null ? cfg.args : shellArgs)
                            .Replace("%cmd", cReplaceQuotes ? cfg.cmd.Replace("\"", "\\\"") : cfg.cmd);
                        Log("\trunning " + appProcess.StartInfo.FileName + " " + appProcess.StartInfo.Arguments + "... (replaceQuotes=" + cReplaceQuotes + ")");
                        appProcess.Start();
                        Log("\tprocess id: " + appProcess.Id);
                        Log("\tdone", true);
                    }else{
                        Log("\t\terror", true);
                    }
                }catch (Exception e){
                    Log("\t\t" + e.Message, true);
                }
            }else{
                Log("\tconfig.json not found :(", true);
            }
        }

        static void Log(string? text, bool important = false) {
            if(important || log) {
                Console.WriteLine(text);
            }
        }

        static void Error(string? text) {
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("error: ");
            Console.ForegroundColor = fg;
            Console.WriteLine(text);
        }

        static string? ExecuteGitCommand(string cmd) {
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

        static void CheckForUpdates() {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/KD3n1z/gitwatcher/releases/latest");
            request.Headers.Add("User-Agent", "gitwatcher");

            HttpResponseMessage response = new HttpClient().Send(request);
            
            GithubApiResponse? apiResponse = JsonSerializer.Deserialize<GithubApiResponse>(response.Content.ReadAsStringAsync().Result);

            if(apiResponse != null && apiResponse.tag_name != null) {
                Version remoteVersion = new Version(apiResponse.tag_name.Replace("v", ""));
                Version localVersion = new Version(version);

                if(remoteVersion > localVersion) {
                    Log("Newer version available:\n\tlocal: v" + localVersion + "\n\tremote: v" + remoteVersion + "\n\n" + apiResponse.html_url, true);
                }else{
                    Log("No need to update:\n\tlocal: v" + localVersion + "\n\tremote: v" + remoteVersion, true);
                }
            }else{
                Error("request error; status code: " + response.StatusCode);
                return;
            }
        }
    }

    class GithubApiResponse {
        public string? tag_name {
            get; set;
        }
        public string? html_url {
            get; set;
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