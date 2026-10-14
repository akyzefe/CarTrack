using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTrackApp;

public sealed class MonthlyExpenseBar
{
    public string MonthLabel { get; init; } = string.Empty;
    public string AmountLabel { get; init; } = string.Empty;
    public double Height { get; init; }
    public string MonthTextColor { get; init; } = "#526275";
    public bool IsCurrentMonth { get; init; }
}

public sealed class ExpenseBreakdownItem
{
    public string Name { get; init; } = string.Empty;
    public string AmountLabel { get; init; } = string.Empty;
    public string DetailsLabel { get; init; } = string.Empty;
    public double Share { get; init; }
}

public sealed class RecentExpenseItem
{
    public string DetailLabel { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string CategoryLabel { get; init; } = string.Empty;
    public string AmountLabel { get; init; } = string.Empty;
}

public sealed class MonthlyExpenseChartDrawable(IReadOnlyList<MonthlyExpenseBar> months) : IDrawable
{
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (months.Count == 0 || dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
            return;

        const float left = 12;
        const float right = 12;
        const float top = 20;
        const float bottom = 29;
        var plotWidth = dirtyRect.Width - left - right;
        var plotHeight = dirtyRect.Height - top - bottom;
        if (plotWidth <= 0 || plotHeight <= 0)
            return;

        canvas.StrokeSize = 1;
        canvas.StrokeColor = Color.FromArgb("#E1E7EF");
        for (var row = 0; row < 4; row++)
        {
            var y = top + plotHeight * row / 3;
            canvas.DrawLine(left, y, dirtyRect.Width - right, y);
        }

        var maxHeight = Math.Max(1, months.Max(month => month.Height));
        var slotWidth = plotWidth / months.Count;
        var points = months.Select((month, index) =>
        {
            var x = left + slotWidth * (index + 0.5f);
            var normalized = (float)(month.Height / maxHeight);
            var y = top + plotHeight * (1 - normalized);
            return (Month: month, X: x, Y: y);
        }).ToList();

        foreach (var point in points)
        {
            var barTop = Math.Min(point.Y + 7, top + plotHeight);
            var barHeight = Math.Max(0, top + plotHeight - barTop);
            if (barHeight <= 0)
                continue;

            canvas.FillColor = Color.FromArgb(point.Month.IsCurrentMonth ? "#D6E0F0" : "#E8EEF6");
            canvas.FillRoundedRectangle(point.X - 3, barTop, 6, barHeight, 3);
        }

        if (points.Count > 1)
        {
            var line = new PathF();
            line.MoveTo(points[0].X, points[0].Y);
            foreach (var point in points.Skip(1))
                line.LineTo(point.X, point.Y);

            canvas.StrokeColor = Color.FromArgb("#25436B");
            canvas.StrokeSize = 3;
            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            canvas.DrawPath(line);
        }

        canvas.FontSize = 10;
        foreach (var point in points)
        {
            var isCurrentMonth = point.Month.IsCurrentMonth;
            var radius = isCurrentMonth ? 6 : 4.5f;
            canvas.FillColor = Colors.White;
            canvas.FillCircle(point.X, point.Y, radius + 2);
            canvas.FillColor = Color.FromArgb(isCurrentMonth ? "#25436B" : "#7893B3");
            canvas.FillCircle(point.X, point.Y, radius);

            canvas.FontColor = Color.FromArgb(point.Month.MonthTextColor);
            canvas.DrawString(
                point.Month.MonthLabel,
                point.X - slotWidth / 2,
                dirtyRect.Height - bottom + 7,
                slotWidth,
                18,
                HorizontalAlignment.Center,
                VerticalAlignment.Top);
        }
    }
}

public partial class ExpenseStatisticsViewModel : ObservableObject
{
    private static readonly HttpClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<ExpenseEntry> _allExpenses = [];

    public IReadOnlyList<string> PeriodOptions { get; } = ["Bu ay", "Son 6 ay", "Son 12 ay", "Tümü"];
    public ObservableCollection<MonthlyExpenseBar> MonthlyBars { get; } = [];
    public ObservableCollection<ExpenseBreakdownItem> CategoryBreakdown { get; } = [];
    public ObservableCollection<ExpenseBreakdownItem> VehicleBreakdown { get; } = [];
    public ObservableCollection<RecentExpenseItem> RecentExpenses { get; } = [];
    public ObservableCollection<VehicleDto> Vehicles { get; } = [];

    [ObservableProperty]
    private string selectedPeriod = "Bu ay";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool hasError;

    [ObservableProperty]
    private string? errorMessage;

    [ObservableProperty]
    private string totalCost = "₺0,00";

    [ObservableProperty]
    private string recordCount = "0 kayıt";

    [ObservableProperty]
    private string monthlyAverage = "₺0,00";

    [ObservableProperty]
    private string topCategory = "Henüz harcama yok";

    [ObservableProperty]
    private string chartDescription = string.Empty;

    [ObservableProperty]
    private string peakMonthSummary = "En yüksek ay: —";

    [ObservableProperty]
    private IDrawable chartDrawable = new MonthlyExpenseChartDrawable([]);

    [ObservableProperty]
    private bool hasNoExpenses;

    public void SetVehicles(IEnumerable<VehicleDto> vehicles)
    {
        Vehicles.Clear();
        foreach (var vehicle in vehicles)
            Vehicles.Add(vehicle);
    }

    partial void OnSelectedPeriodChanged(string value) => Recalculate();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        try
        {
            var data = await Task.WhenAll(Vehicles.Select(LoadVehicleExpensesAsync));
            _allExpenses.Clear();
            _allExpenses.AddRange(data.SelectMany(items => items));
            Recalculate();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Expense statistics could not be loaded: {ex}");
            HasError = true;
            ErrorMessage = "Harcama istatistikleri yüklenemedi. Bağlantını kontrol edip tekrar dene.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<List<ExpenseEntry>> LoadVehicleExpensesAsync(VehicleDto vehicle)
    {
        using var maintenanceResponse = await Client.GetAsync(
            $"{ApiEndpoints.Maintenances}/vehicle/{vehicle.id}");
        maintenanceResponse.EnsureSuccessStatusCode();
        var maintenance = await maintenanceResponse.Content.ReadFromJsonAsync<List<MaintenanceDto>>(JsonOptions)
            ?? throw new JsonException($"Maintenance response for vehicle {vehicle.id} was empty.");

        using var fuelResponse = await Client.GetAsync(
            $"{ApiEndpoints.FuelRecords}/vehicle/{vehicle.id}");
        fuelResponse.EnsureSuccessStatusCode();
        var fuel = await fuelResponse.Content.ReadFromJsonAsync<List<FuelRecordDto>>(JsonOptions)
            ?? throw new JsonException($"Fuel response for vehicle {vehicle.id} was empty.");

        var vehicleName = string.Join(" ", new[] { vehicle.brand, vehicle.model }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        return maintenance.Select(item => new ExpenseEntry(
                item.date.Date,
                item.cost,
                item.category,
                item.title,
                vehicleName))
            .Concat(fuel.Select(item => new ExpenseEntry(
                item.date.Date,
                item.cost,
                "Yakıt",
                item.liters > 0 ? $"{item.liters:N1} L yakıt" : "Yakıt alımı",
                vehicleName)))
            .ToList();
    }

    private void Recalculate()
    {
        var today = DateTime.Today;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var start = SelectedPeriod switch
        {
            "Son 6 ay" => currentMonth.AddMonths(-5),
            "Son 12 ay" => currentMonth.AddMonths(-11),
            "Tümü" => _allExpenses.Count == 0
                ? currentMonth
                : new DateTime(_allExpenses.Min(item => item.Date).Year, _allExpenses.Min(item => item.Date).Month, 1),
            _ => currentMonth
        };
        var end = currentMonth.AddMonths(1);
        var filtered = _allExpenses
            .Where(item => item.Date >= start && item.Date < end)
            .ToList();

        var total = filtered.Sum(item => item.Cost);
        var monthCount = Math.Max(1, ((currentMonth.Year - start.Year) * 12) + currentMonth.Month - start.Month + 1);
        TotalCost = FormatMoney(total);
        RecordCount = $"{filtered.Count} kayıt";
        MonthlyAverage = FormatMoney(total / monthCount);
        HasNoExpenses = filtered.Count == 0;

        var categories = filtered
            .GroupBy(item => DisplayCategory(item.Category))
            .Select(group => new { Name = group.Key, Amount = group.Sum(item => item.Cost), Count = group.Count() })
            .OrderByDescending(group => group.Amount)
            .ToList();
        TopCategory = categories.Count == 0 ? "Henüz harcama yok" : categories[0].Name;
        ReplaceCollection(CategoryBreakdown, categories.Select(group => new ExpenseBreakdownItem
        {
            Name = group.Name,
            AmountLabel = FormatMoney(group.Amount),
            DetailsLabel = $"{group.Count} kayıt · {(total == 0 ? 0 : (double)(group.Amount / total) * 100):N0}%",
            Share = total == 0 ? 0 : (double)(group.Amount / total)
        }));

        var vehicles = filtered
            .GroupBy(item => string.IsNullOrWhiteSpace(item.VehicleName) ? "Araç" : item.VehicleName)
            .Select(group => new { Name = group.Key, Amount = group.Sum(item => item.Cost), Count = group.Count() })
            .OrderByDescending(group => group.Amount)
            .ToList();
        ReplaceCollection(VehicleBreakdown, vehicles.Select(group => new ExpenseBreakdownItem
        {
            Name = group.Name,
            AmountLabel = FormatMoney(group.Amount),
            DetailsLabel = $"{group.Count} kayıt · {(total == 0 ? 0 : (double)(group.Amount / total) * 100):N0}%",
            Share = total == 0 ? 0 : (double)(group.Amount / total)
        }));

        ReplaceCollection(RecentExpenses, filtered
            .OrderByDescending(item => item.Date)
            .Take(10)
            .Select(item => new RecentExpenseItem
            {
                DetailLabel = $"{item.Date.ToString("dd MMM yyyy", CultureInfo.GetCultureInfo("tr-TR"))} · {item.VehicleName}",
                Description = item.Title,
                CategoryLabel = DisplayCategory(item.Category),
                AmountLabel = FormatMoney(item.Cost)
            }));

        var chartStart = SelectedPeriod == "Tümü" && monthCount > 12
            ? currentMonth.AddMonths(-11)
            : start;
        var chartMonths = Enumerable.Range(0, Math.Max(1, ((currentMonth.Year - chartStart.Year) * 12)
            + currentMonth.Month - chartStart.Month + 1))
            .Select(index => chartStart.AddMonths(index))
            .ToList();
        var monthlyTotals = filtered
            .GroupBy(item => new DateTime(item.Date.Year, item.Date.Month, 1))
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Cost));
        var maxMonthlyTotal = chartMonths.Select(month => monthlyTotals.GetValueOrDefault(month)).DefaultIfEmpty().Max();
        var peakMonth = monthlyTotals
            .Where(item => item.Value > 0)
            .OrderByDescending(item => item.Value)
            .FirstOrDefault();
        PeakMonthSummary = peakMonth.Value > 0
            ? $"En yüksek ay · {peakMonth.Key.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"))} · {FormatMoney(peakMonth.Value)}"
            : "Henüz aylık harcama yok";
        ReplaceCollection(MonthlyBars, chartMonths.Select(month =>
        {
            var amount = monthlyTotals.GetValueOrDefault(month);
            return new MonthlyExpenseBar
            {
                MonthLabel = month.ToString("MMM", CultureInfo.GetCultureInfo("tr-TR")),
                AmountLabel = amount == 0 ? "—" : amount >= 10000 ? $"{amount / 1000:N0}b" : $"{amount:N0}",
                Height = maxMonthlyTotal == 0 ? 0 : Math.Max(5, (double)(amount / maxMonthlyTotal) * 112),
                MonthTextColor = month == currentMonth ? "#25436B" : "#687386",
                IsCurrentMonth = month == currentMonth
            };
        }));
        ChartDrawable = new MonthlyExpenseChartDrawable(MonthlyBars.ToList());
        ChartDescription = SelectedPeriod == "Tümü" && monthCount > 12
            ? "Son 12 ay gösteriliyor · toplam ve özet seçili dönemin tamamını kapsar"
            : SelectedPeriod;
    }

    private static string DisplayCategory(string category) =>
        string.Equals(category, "Belge", StringComparison.OrdinalIgnoreCase)
            ? "Belge gideri"
            : string.Equals(category, "Yakıt", StringComparison.OrdinalIgnoreCase)
                ? "Yakıt"
                : string.IsNullOrWhiteSpace(category) ? "Diğer" : category;

    private static string FormatMoney(decimal amount) =>
        $"{amount.ToString("N2", CultureInfo.GetCultureInfo("tr-TR"))} ₺";

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    private sealed record ExpenseEntry(
        DateTime Date,
        decimal Cost,
        string Category,
        string Title,
        string VehicleName);
}
