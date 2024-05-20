APP_NAME = IPP24chat_xjakub41
EXECUTABLE_NAME=ipk24chat-client
OUTPUTPATH = .

.PHONY: build publish clean

all: publish

build_app:
	dotnet build $(APP_NAME).csproj

publish: build_app
	dotnet publish $(APP_NAME).csproj -p:PublishSingleFile=true -c Release -r linux-x64 --self-contained false -p:AssemblyName=$(EXECUTABLE_NAME) -o $(OUTPUTPATH)
	@echo "Published $(EXECUTABLE_NAME)."

publish_mac: build_app
	dotnet publish $(APP_NAME).csproj -p:PublishSingleFile=true -c Release -r osx-x64 --self-contained false -p:AssemblyName=$(EXECUTABLE_NAME) -o $(OUTPUTPATH)
	@echo "Published $(EXECUTABLE_NAME) on MacOS."

clean:
	dotnet clean $(APP_NAME).csproj
	rm -rf $(OUTPUTPATH)/bin $(OUTPUTPATH)/obj
	rm $(OUTPUTPATH)/$(EXECUTABLE_NAME).pdb
	rm $(OUTPUTPATH)/$(EXECUTABLE_NAME)
	rm xjakub41.zip

pack:
	zip xjakub41.zip commands/AuthHandler.cs commands/HelpHandler.cs commands/JoinHandler.cs commands/RenameHandler.cs CHANGELOG.md ChatClient.cs ICommandHandler.cs IChatCommunicator.cs IPP24chat_xjakub41.csproj IPP24chat_xjakub41.sln LICENSE Makefile Message.cs Program.cs README.md TcpChatCommunicator.cs UdpChatCommunicator.cs img/pipedtcp.png img/udppcap.png img/retransmission.png -j