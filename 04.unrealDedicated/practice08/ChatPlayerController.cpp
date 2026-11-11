// ChatPlayerController.cpp   (UFUNCTION(Server, Reliable))
void AChatPlayerController::ServerSpawnItem_Implementation()
{
    if (AChatGameMode* GM = GetWorld()->GetAuthGameMode<AChatGameMode>())
        GM->SpawnItem(FVector(FMath::RandRange(-300.f, 300.f), FMath::RandRange(-300.f, 300.f), 100.f));
}
