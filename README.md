# gitwatcher
gitwatcher will be looking for changes in git, immediately pull them, and then restart your program (you can specify start command in .gitwatcher/config.json)

here's a [demo](https://github.com/KD3n1z/gitwatcherDemo)

## usage
<code>gitwatcher [--interval / -i \<seconds\>] [--log / -l] [--help / -h]</code>

## installing
1. download executable for your platform from [/releases](https://github.com/KD3n1z/gitwatcher/releases) or build it by yourself
2. move it to your bin directory:<br>
    typically <code>/usr/local/bin</code> on macOS/linux<br>
    typically <code>C:\Windows</code> on Windows

## building
requirements
- [dotnet 6.0](https://dotnet.microsoft.com/en-us/download/dotnet/6.0) or higher

run this command:<br>
- <code>dotnet publish --self-contained -c Release -p:PublishSingleFile=true -o dest</code><br>

or, if you have make installed:<br>
- <code>make publish</code>

made with ❤️ and C#