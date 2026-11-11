using System;
using Unity.Collections;
using Unity.Netcode;

// 접속자 한 명의 정보. 서버가 NetworkList에 담아 모든 접속자에게 복제한다.
public struct UserState : INetworkSerializable, IEquatable<UserState>
{
    public ulong Id;                      // ClientId
    public FixedString64Bytes Name;       // 접속 승인 때 확인한 이름
    public FixedString64Bytes Channel;    // 들어가 있는 채널 이름. 비어 있으면 로비(어느 채널에도 없음)
    public int Hp;                        // 체력 (실습 11)
    public bool Ready;                    // 준비 여부 (실습 11)
    public int Gold;                      // 받은 보너스 (실습 12)

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Channel);
        serializer.SerializeValue(ref Hp);
        serializer.SerializeValue(ref Ready);
        serializer.SerializeValue(ref Gold);
    }

    public bool Equals(UserState other)
    {
        return Id == other.Id && Name.Equals(other.Name) && Channel.Equals(other.Channel)
            && Hp == other.Hp && Ready == other.Ready && Gold == other.Gold;
    }
}
