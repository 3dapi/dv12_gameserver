// ChatWidget.h
UCLASS()
class UChatWidget : public UUserWidget
{
    GENERATED_BODY()
public:
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* AddressBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* PortBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* NameBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* CodeBox;
    UPROPERTY(meta = (BindWidget)) UEditableTextBox* ChatBox;
    UPROPERTY(meta = (BindWidget)) UButton* ConnectBtn;
    UPROPERTY(meta = (BindWidget)) UButton* DisconnectBtn;
    UPROPERTY(meta = (BindWidget)) UButton* SendBtn;
    UPROPERTY(meta = (BindWidget)) UTextBlock* StatusText;
    UPROPERTY(meta = (BindWidget)) UTextBlock* LogText;
    UPROPERTY(meta = (BindWidget)) UTextBlock* UserListText;

    void AddLog(const FString& Line);
    void SetUsers(const TArray<FString>& Names);
    void SetStatus(const FString& S) { StatusText->SetText(FText::FromString(S)); }

    virtual void NativeConstruct() override;
};
