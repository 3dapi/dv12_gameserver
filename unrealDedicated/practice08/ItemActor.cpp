// ItemActor.cpp
AItemActor::AItemActor()
{
    bReplicates = true;                                          // 서버에서 생성하면 클라이언트에 복제된다
    SetReplicateMovement(true);
}
