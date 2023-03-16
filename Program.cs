using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace gitwatcher
{
    internal static class Program
    {
        public const string version = "1.1.1";

        static OSPlatform platform;
        static string shell = "sh";
        static string shellArgs = "-c \"%cmd\"";
        static bool replaceQuotes = true;
        static string platformCfgPath = "";
        static string cfgPath = "";
        static Process? appProcess = null;

        // webhook mode
        static int? port = null;
        static string? secret = null;

        static int interval = 60;
        static bool log = false;
        
        static void Main(string[] args) {
            platform = GetOperatingSystem();

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
                            "\n\t-i --interval <seconds>\tSpecify pull interval." + 
                            "\n\t-l --log\t\tLog each action." + 
                            "\n\t-h --help\t\tPrint usage." + 
                            "\n\t-v --version\t\tPrint current version." + 
                            "\n\t-u --checkForUpdates\tCheck for newer versions on github" +
                            "\n\t-p --port <port>\tSpecify http server port (webhook mode)." + 
                            "\n\t-s --secret <secret>\tSpecify webhook secret (webhook mode).", true);
                        return;
                    case "--version":
                    case "-v":
                        Log("gitwatcher v" + version, true);
                        return;
                    case "-u":
                    case "--checkForUpdates":
                        CheckForUpdates();
                        return;
                    case "-p":
                    case "--port":
                        port = int.Parse(args[++i]);
                        break;
                    case "-s":
                    case "--secret":
                        secret = args[++i];
                        break;
                    default:
                        LogError(args[i] + ": invalid option");
                        return;
                }
            }

            if(platform == OSPlatform.Windows) {
                shell = "cmd.exe";
                shellArgs = "/C%cmd";
                replaceQuotes = false;
            }else if(platform == OSPlatform.FreeBSD) {
                LogWarning("gitwatcher has not been tested on FreeBSD, use at your own risk");
            }

            if(secret != null && port == null) {
                LogWarning("webhook mode is disabled, specify --port to enable it");
            }

            string? gitVersion = ExecuteGitCommand("--version");
            if(gitVersion == null) {
                LogError("git not found");
                return;
            }

            Log("gitwatcher v" + version + ", " + gitVersion +
                (port == null ? "\n\tpull interval: " + interval + " (seconds)" : "\n\twebhook mode, port: " + port.ToString()) + 
                (log ? "\n\tlog: everything" : "") +
                "\n\tplatform: " + platform + " (" + shell + " " + shellArgs + ")",
            true);

            HttpListener? server = null;

            Console.CancelKeyPress += (object? sender, ConsoleCancelEventArgs eventArgs) => {
                if(appProcess != null) {
                    Log("killing process " + appProcess.Id + "...");
                    try {
                        appProcess.Kill(true);
                        Log("\tdone");
                    }catch{}
                }
                if(server != null) {
                    Log("stopping http server...");
                    try{
                        server.Stop();
                        server.Close();
                        Log("\tdone");
                    }catch{}
                }
                eventArgs.Cancel = false;
            };

            if(port != null) {
                Log("starting http server... ", true);
                server = new HttpListener();
                server.Prefixes.Add("http://*:" + port.ToString() + "/webhook/");
                Log("\tprefixes:");
                foreach(string prefix in server.Prefixes) {
                    Log("\t\t" + prefix);
                }
                server.Start();
            }

            bool firstLoop = true;

            while(true) {
                string? pullResult = ExecuteGitCommand("pull");
                if(pullResult == null || pullResult.ToLower().StartsWith("fatal") || pullResult.ToLower().StartsWith("error")) {
                    LogError("\"" + pullResult + "\"");
                    break;
                }
                Log("git pull: '" + pullResult + "'");
                if((pullResult != "Already up to date." && !string.IsNullOrWhiteSpace(pullResult)) || firstLoop) {
                    RestartApp();
                }
                if(server == null) {
                    Thread.Sleep(interval * 1000);
                }else{
                    bool cycle = true;
                    while(cycle) {
                        HttpListenerContext? context = server.GetContext();
                        Log("request received:");
                        if(context != null) {
                            try{
                                string resp = "error";
                                switch(context.Request.Headers["X-Github-Event"]) {
                                    case "ping":
                                        resp = "pong";
                                        break;
                                    case "push":
                                        Log("\tpush...");
                                        bool needToPull = false;
                                        if(secret != null) {
                                            Log("\t\tveryfing hash...");
                                            string data = "";
                                            using(StreamReader sr = new StreamReader(context.Request.InputStream)) {
                                                data = sr.ReadToEnd();
                                            }
                                            Log("\t\t\tcomputing...");
                                            DateTime startDT = DateTime.Now;
                                            string hashStr = "";
                                            using (HMACSHA256 hash = new HMACSHA256(Encoding.UTF8.GetBytes(secret))) {
                                                StringBuilder sb = new StringBuilder();

                                                foreach(byte b in hash.ComputeHash(Encoding.UTF8.GetBytes(data))) {
                                                    sb.Append(b.ToString("x2"));
                                                }

                                                hashStr = sb.ToString();
                                            }
                                            Log("\t\t\t\tdone in " + DateTime.Now.Subtract(startDT).TotalMilliseconds + "ms");
                                            hashStr = "sha256=" + hashStr;
                                            string? reqHash = context.Request.Headers["X-Hub-Signature-256"];
                                            Log("\t\tactual hash:  " + hashStr + "\n\t\trequest hash: " + reqHash);
                                            if(hashStr == reqHash) {
                                                needToPull = true;
                                                Log("\t\tverified!");
                                            }
                                        }
                                        else {
                                            needToPull = true;
                                        }

                                        if(needToPull) {
                                            resp = "updating...";
                                            Log("\t\tpulling...");
                                            cycle = false;
                                        }else{
                                            resp = "access denied";
                                        }
                                        break;
                                    default: 
                                        break;
                                }
                                byte[] buffer = Encoding.UTF8.GetBytes(resp);
                                context.Response.ContentLength64 = buffer.Length;
                                context.Response.OutputStream.Write(buffer);
                                context.Response.OutputStream.Flush();
                                Log("\twriting \"" + resp + "\"...");
                            }catch(Exception err){
                                LogWarning("http error: " + err.Message);
                            }
                        }
                    }
                    Thread.Sleep(1000);
                }
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
                        if(cfg.interval != null) {
                            if(cfg.interval != interval) {
                                interval = (int)cfg.interval;
                                Log("\tinterval=" + interval + "s");
                            }
                        }

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

        static void LogError(string? text) {
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("error: ");
            Console.ForegroundColor = fg;
            Console.WriteLine(text);
        }

        static void LogWarning(string? text) {
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("warning: ");
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return OSPlatform.Windows;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) {
                return OSPlatform.Linux;
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD)) {
                return OSPlatform.FreeBSD;
            }

            throw new Exception("Cannot determine operating system!");
        }

        static bool CheckForUpdates() {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/KD3n1z/gitwatcher/releases/latest");
            request.Headers.Add("User-Agent", "gitwatcher");

            HttpResponseMessage response = new HttpClient().Send(request);
            
            GithubApiResponse? apiResponse = JsonSerializer.Deserialize<GithubApiResponse>(response.Content.ReadAsStringAsync().Result);

            if(apiResponse != null && apiResponse.tag_name != null) {
                Version remoteVersion = new Version(apiResponse.tag_name.Replace("v", ""));
                Version localVersion = new Version(version);

                if(remoteVersion > localVersion) {
                    string downloadUrl = "?";
                    if(apiResponse.assets != null) {
                        string platformStr = platform.ToString().ToLower();
                        string archStr = RuntimeInformation.ProcessArchitecture.ToString().ToLower();
                        foreach(GithubApiResponse.ReleaseAsset asset in apiResponse.assets) {
                            string compareString = apiResponse.html_url ?? "?";
                            if(asset.name != null) {
                                compareString = asset.name.ToLower().Split('.')[0];
                            }
                            if(compareString == platformStr || compareString == platformStr + "-" + archStr) {
                                if(asset.browser_download_url != null) {
                                    downloadUrl = asset.browser_download_url;
                                }
                                break;
                            }
                        }
                    }
                    Log("Newer version available:\n\tlocal: v" + localVersion + "\n\tremote: v" + remoteVersion + "\n\n" + downloadUrl, true);
                    return true;
                }else{
                    Log("No need to update:\n\tlocal: v" + localVersion + "\n\tremote: v" + remoteVersion, true);
                }
            }else{
                LogError("request error; status code: " + response.StatusCode);
            }
            return false;
        }
    }

    class GithubApiResponse {
        public class ReleaseAsset {
            public string? name {
                get; set;
            }
            public string? browser_download_url {
                get; set;
            }
        }

        public string? tag_name {
            get; set;
        }
        public string? html_url {
            get; set;
        }
        public ReleaseAsset[]? assets {
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
        public int? interval {
            get; set;
        }
    }
}