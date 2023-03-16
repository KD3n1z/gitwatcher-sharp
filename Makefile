pArgs=--self-contained -c Release -p:PublishSingleFile=true
zArgs=-9 zip.zip

all: clean build

build:
	dotnet build

clean:
	-rm -rf bin
	-rm -rf dest

publish-win:
	dotnet publish -r win-x64 -o dest/win-x64 $(pArgs)

publish-win-arm64:
	dotnet publish -r win-arm64 -o dest/win-arm64 $(pArgs)

publish-linux:
	dotnet publish -r linux-x64 -o dest/linux-x64 $(pArgs)

publish-linux-arm64:
	dotnet publish -r linux-arm64 -o dest/linux-arm64 $(pArgs)

publish-osx:
	dotnet publish -r osx-x64 -o dest/osx-x64 $(pArgs)

publish-all: clean publish-win publish-win-arm64 publish-linux publish-linux-arm64 publish-osx

zip-all: 
	mkdir dest/zips
	cd dest/osx-x64; zip $(zArgs) gitwatcher; mv zip.zip ../zips/macOS.zip
	cd dest/linux-x64; zip $(zArgs) gitwatcher; mv zip.zip ../zips/linux-x64.zip
	cd dest/linux-arm64; zip $(zArgs) gitwatcher; mv zip.zip ../zips/linux-arm64.zip
	cd dest/win-x64; zip $(zArgs) gitwatcher.exe; mv zip.zip ../zips/windows-x64.zip
	cd dest/win-arm64; zip $(zArgs) gitwatcher.exe; mv zip.zip ../zips/windows-arm64.zip

paz: publish-all zip-all

publish:
	dotnet publish --use-current-runtime -o dest $(pArgs)

install:
	sudo cp dest/gitwatcher /usr/local/bin