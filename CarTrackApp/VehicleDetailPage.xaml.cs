namespace CarTrackApp;

public partial class VehicleDetailPage : ContentPage, IQueryAttributable
{
    public VehicleDetailPage()
    {
        InitializeComponent();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("vehicle", out var value) && value is VehicleDto vehicle)
            BindingContext = new VehicleExpensesViewModel(vehicle);
    }

    private async void OnEditCurrentKmTapped(object? sender, EventArgs e)
    {
        if (BindingContext is not VehicleExpensesViewModel viewModel)
            return;

        var mileage = await DisplayPromptAsync(
            "Kilometreyi düzenle",
            "Aracın güncel kilometresini gir.",
            "Kaydet",
            "İptal",
            initialValue: viewModel.Vehicle.currentKm.ToString(),
            keyboard: Keyboard.Numeric);
        if (mileage is null)
            return;

        var errorMessage = await viewModel.UpdateCurrentMileageAsync(mileage);
        if (errorMessage is not null)
            await DisplayAlertAsync("Kilometre güncellenemedi", errorMessage, "Tamam");
    }

    private async void OnMaintenanceTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border { BindingContext: MaintenanceDto record })
            return;

        var details = $"{record.category}\n{record.title}\n\n" +
                      $"Tutar: {record.cost:N2} ₺\n" +
                      $"Tarih: {record.date:dd.MM.yyyy}\n" +
                      $"Kilometre: {record.atKm:N0} km";

        if (record.dueDate is { } dueDate)
            details += $"\nSonraki bakım tarihi: {dueDate:dd.MM.yyyy}";

        if (record.dueAtKm is { } dueAtKm)
            details += $"\nSonraki bakım kilometresi: {dueAtKm:N0} km";

        if (!string.IsNullOrWhiteSpace(record.notes))
            details += $"\n\nNotlar:\n{record.notes}";

        await DisplayAlertAsync("Harcama detayı", details, "Kapat");
    }
}