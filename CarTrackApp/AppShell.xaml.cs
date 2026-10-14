namespace CarTrackApp;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(VehicleDetailPage), typeof(VehicleDetailPage));
		Routing.RegisterRoute(nameof(ExpenseStatisticsPage), typeof(ExpenseStatisticsPage));
	}
}
