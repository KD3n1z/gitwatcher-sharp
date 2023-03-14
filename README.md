# gitwatcher
gitwatcher will be looking for changes in git, immediately pull them, and then restart your program (you can specify start command in .gitwatcher/config.json)

Here's a [demo](https://github.com/KD3n1z/gitwatcher-demo)

## Usage
gitwatcher [options]

Options:
* <code>-i --interval \<seconds\></code> - Specify pull interval.
* <code>-l --log</code> - Log each action.
* <code>-h --help</code> - Print usage.
* <code>-v --version</code> - Print current version.
* <code>-u --checkForUpdates</code> - Check for newer versions on github.
* <code>-p --port \<port\></code> - Specify http server port (webhook mode).
* <code>-s --secret \<secret\></code> - Specify webhook secret (webhook mode).


### Webhook mode
You can use webhook mode + [github webhooks](https://docs.github.com/en/webhooks-and-events/webhooks/about-webhooks) to make gitwatcher more effective:
- Create new webhook in settings of your github repo:
    - Payload URL: <code>http://[ip]:[port]/webhook</code>
    - Content type: <code>application/json</code>
    - Secret: <code>[secret]</code>

    <img src="https://raw.githubusercontent.com/KD3n1z/kd3n1z-com/main/webhook-github.png" width="50%">
- Start gitwatcher with <code>gitwatcher --port [port] --secret [secret]</code>

## Installing
1. Download archive for your platform from [/releases](https://github.com/KD3n1z/gitwatcher/releases)
2. Unzip it
3. Move it to your bin directory:<br>
    - typically <code>/usr/local/bin</code> on macOS/linux<br>
    - typically <code>C:\Windows</code> on Windows

## Building
Requirements:
- [dotnet 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) or higher

Run this command:<br>
- <code>dotnet publish --self-contained -c Release -p:PublishSingleFile=true -o dest</code><br>

or, if you have make installed:<br>
- <code>make publish</code>

made with ❤️ and C#
