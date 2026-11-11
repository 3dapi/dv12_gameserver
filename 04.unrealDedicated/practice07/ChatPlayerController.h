// ChatPlayerController.h
UFUNCTION(Server, Reliable) void ServerClaimBonus();
UFUNCTION(Client, Reliable) void ClientBonusResult(bool bOk, int32 Amount);
