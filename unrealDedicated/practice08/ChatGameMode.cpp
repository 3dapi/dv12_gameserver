// ChatGameMode.cpp
void AChatGameMode::SpawnItem(const FVector& Loc)
{
    if (AItemActor* A = GetWorld()->SpawnActor<AItemActor>(ItemClass, Loc, FRotator::ZeroRotator))
    {
        Items.Add(A);
        NotifyItemCount();                                       // 개수를 모든 접속자에게 Client RPC로 알린다
    }
}
void AChatGameMode::DespawnItem()
{
    if (Items.Num() == 0) return;
    if (AItemActor* A = Items.Pop().Get()) A->Destroy();         // 서버에서 Destroy하면 클라이언트에서도 사라진다
    NotifyItemCount();
}
