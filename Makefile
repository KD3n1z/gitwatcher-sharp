pArgs=--self-contained -c Release -p:PublishSingleFile=true

all: clean build

build:
	dotnet build

clean:
	-rm -rf bin
	-rm -rf dest

publish-win:
	dotnet publish -r win-x64 -o dest/win-x64 $(pArgs)

publish-linux:
	dotnet publish -r linux-x64 -o dest/linux-x64 $(pArgs)

publish-osx:
	dotnet publish -r osx-x64 -o dest/osx-x64 $(pArgs)

publish-all: clean publish-win publish-linux publish-osx
	mkdir dest/zips
	cd dest/osx-x64; zip zip.zip gitwatcher; mv zip.zip ../zips/macOS.zip
	cd dest/linux-x64; zip zip.zip gitwatcher; mv zip.zip ../zips/linux.zip
	cd dest/win-x64; zip zip.zip gitwatcher.exe; mv zip.zip ../zips/windows.zip

publish:
	dotnet publish -o dest $(pArgs)

install:
	sudo cp dest/gitwatcher /usr/local/bin