// ChatGameMode.h  (서버에만 존재하므로 클라이언트에서는 접근할 수 없다)
TMap<FString, TArray<TWeakObjectPtr<AChatPlayerController>>> Channels;
TMap<TWeakObjectPtr<AChatPlayerController>, FString> ChannelOf;
