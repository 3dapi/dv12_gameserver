// ChatGameMode.cpp
void AChatGameMode::EnterChannel(AChatPlayerController* PC, const FString& Ch, bool bCreate)
{
    if (!Channels.Contains(Ch)) { if (!bCreate) return; Channels.Add(Ch); }
    LeaveChannel(PC);
    Channels[Ch].AddUnique(PC);
    ChannelOf.Add(PC, Ch);
    BroadcastChannels();
}

void AChatGameMode::LeaveChannel(AChatPlayerController* PC)
{
    if (!PC) return;
    if (FString* Ch = ChannelOf.Find(PC))
    {
        const FString Name = *Ch;
        Channels[Name].Remove(PC);
        if (Channels[Name].Num() == 0) Channels.Remove(Name);
        ChannelOf.Remove(PC);
    }
    BroadcastChannels();
}

void AChatGameMode::ChannelChat(AChatPlayerController* From, const FString& Msg)
{
    FString* Ch = ChannelOf.Find(From);
    if (!Ch) return;
    const FString Line = FString::Printf(TEXT("[%s] %s"), *From->PlayerState->GetPlayerName(), *Msg);
    for (auto& W : Channels[*Ch])
        if (W.IsValid()) W->ClientReceiveChannelChat(Line);   // 같은 채널에만 전달
}
