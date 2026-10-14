namespace CarTrackApp;

public partial class ExpenseStatisticsPage : ContentPage, IQueryAttributable
{
    private readonly ExpenseStatisticsViewModel _viewModel = new();

    public ExpenseStatisticsPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("vehicles", out var value) && value is IEnumerable<VehicleDto> vehicles)
            _viewModel.SetVehicles(vehicles);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
