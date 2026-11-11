// ChatPlayerController.h
UFUNCTION(Server, Reliable) void ServerSendChat(const FString& Msg);
UFUNCTION(Client, Reliable) void ClientReceiveChat(const FString& Line);
UFUNCTION(Client, Reliable) void ClientReceiveUsers(const TArray<FString>& Names);
