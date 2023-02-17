all: clean build

build:
	dotnet build

run:
	./bin/Debug/net7.0/gitwatcher

clean:
	-rm -rf bin