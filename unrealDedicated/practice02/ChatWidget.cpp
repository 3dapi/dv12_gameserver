// ChatWidget.cpp
void UChatWidget::OnConnect()
{
    const FString Url = FString::Printf(TEXT("%s:%s?Name=%s?Code=%s"),
        *AddressBox->GetText().ToString(), *PortBox->GetText().ToString(),
        *NameBox->GetText().ToString(), *CodeBox->GetText().ToString());
    GetOwningPlayer()->ClientTravel(Url, TRAVEL_Absolute);
}
