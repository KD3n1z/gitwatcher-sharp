all: clean build

build:
	dotnet build

clean:
	-rm -rf bin