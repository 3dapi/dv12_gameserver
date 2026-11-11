// 공통 코드에서 구분한다
if (IsNetMode(NM_ListenServer) && IsLocalController())
{
    // 호스트 플레이어: 서버이면서 화면이 있다. 위젯을 만들고, 이 컨트롤러의 Server RPC는 네트워크를 거치지 않고 로컬에서 바로 실행된다.
}
else if (!HasAuthority() && IsLocalController())
{
    // 원격 클라이언트
}

// 연결 실패와 호스트 종료 처리 (GameInstance의 Init에서 등록)
GEngine->OnNetworkFailure().AddUObject(this, &UChatSessionSubsystem::OnNetFail);
void UChatSessionSubsystem::OnNetFail(UWorld*, UNetDriver*, ENetworkFailure::Type T, const FString& Msg)
{
    OnStatus.Broadcast(FString::Printf(TEXT("연결 끊김: %s"), *Msg));
    UGameplayStatics::OpenLevel(this, TEXT("MenuMap"));              // 메뉴로 복귀
}
