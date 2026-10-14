using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CarTrackApp;

public class MaintenanceDto
{
    public int id { get; set; }
    public int vehicleId { get; set; }
    public string category { get; set; } = string.Empty;
    public string title { get; set; } = string.Empty;
    public string? notes { get; set; }
    public DateTime date { get; set; }
    public int atKm { get; set; }
    public decimal cost { get; set; }
    public DateTime? dueDate { get; set; }
    public int? dueAtKm { get; set; }
    public bool hasNotes => !string.IsNullOrWhiteSpace(notes);
}

public class FuelRecordDto
{
    public int id { get; set; }
    public int vehicleId { get; set; }
    public DateTime date { get; set; }
    public int atKm { get; set; }
    public decimal liters { get; set; }
    public decimal cost { get; set; }
    public bool isFullTank { get; set; }
}

public class VehicleDocumentDto
{
    public int id { get; set; }
    public int vehicleId { get; set; }
    public string type { get; set; } = string.Empty;
    public DateTime expiryDate { get; set; }
}

public partial class VehicleExpensesViewModel : ObservableObject
{
    private const int DefaultDateReminderYears = 1;
    private const int DefaultMileageReminderIntervalKm = 10_000;

    private static readonly HttpClient Client = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<string> EntryTypes { get; } =
    [
        "Bakım",
        "Onarım",
        "Lastik",
        "Yakıt Alımı",
        "Belge Tarihleri",
        "Diğer"
    ];

    public IReadOnlyList<string> DocumentTypes { get; } = ["Muayene", "Trafik sigortası", "Kasko"];

    private bool _isLoading;
    private string _selectedEntryType = "Bakım";
    private string _selectedDocumentType = "Muayene";
    private string _title = string.Empty;
    private string _cost = string.Empty;
    private string _atKm = string.Empty;
    private string _notes = string.Empty;
    private string _tireWidth = string.Empty;
    private string _tireProfile = string.Empty;
    private string _tireRim = string.Empty;
    private string _tireQuantity = string.Empty;
    private string _selectedTireSeason = "Kış lastiği";
    private DateTime _selectedDate = DateTime.Today;
    private string? _errorMessage;
    private string _selectedHistoryFilter = "Tümü";
    private bool _engineOilChanged;
    private bool _oilFilterChanged;
    private bool _airFilterChanged;
    private bool _cabinFilterChanged;
    private bool _fuelFilterChanged;
    private string _engineOilDetails = string.Empty;
    private bool _hasDateReminder;
    private bool _hasMileageReminder;
    private DateTime _dueDate = DateTime.Today.AddYears(1);
    private string _dueAtKm = string.Empty;
    private bool _isSettingReminderDefaults;
    private bool _hasCustomDueDate;
    private bool _hasCustomDueAtKm;
    private string _fuelLiters = string.Empty;
    private string _fuelCost = string.Empty;
    private string _fuelAtKm = string.Empty;
    private bool _fuelIsFullTank = true;
    private string _documentExpenseCost = string.Empty;
    private DateTime _documentDate = DateTime.Today;
    private string? _fuelErrorMessage;
    private string? _documentErrorMessage;

    public IReadOnlyList<string> TireSeasons { get; } =
    [
        "Kış lastiği",
        "Yaz lastiği",
        "4 mevsim lastiği"
    ];

    public IReadOnlyList<string> TireWidths { get; } =
        Enumerable.Range(0, 24).Select(index => (125 + index * 10).ToString(CultureInfo.InvariantCulture)).ToArray();

    public IReadOnlyList<string> TireProfiles { get; } =
        ["25", "30", "35", "40", "45", "50", "55", "60", "65", "70", "75", "80", "85"];

    public IReadOnlyList<string> TireRims { get; } =
        Enumerable.Range(10, 15).Select(rim => rim.ToString(CultureInfo.InvariantCulture)).ToArray();

    public IReadOnlyList<string> HistoryFilters { get; } =
    [
        "Tümü",
        "Son 3 ay",
        "Son 1 yıl",
        "1 yıldan eski"
    ];

    public VehicleDto Vehicle { get; }
    private string VehicleDisplayName =>
        string.Join(" ", new[] { Vehicle.brand, Vehicle.model }.Where(value => !string.IsNullOrWhiteSpace(value)));
    public ObservableCollection<MaintenanceDto> Maintenances { get; } = new();
    public ObservableCollection<MaintenanceDto> FilteredMaintenances { get; } = new();
    public ObservableCollection<FuelRecordDto> FuelRecords { get; } = new();
    public ObservableCollection<VehicleDocumentDto> VehicleDocuments { get; } = new();
    public IAsyncRelayCommand LoadMaintenancesCommand { get; }
    public IAsyncRelayCommand AddMaintenanceCommand { get; }
    public IAsyncRelayCommand AddFuelRecordCommand { get; }
    public IAsyncRelayCommand SaveVehicleDocumentsCommand { get; }
    public IAsyncRelayCommand<MaintenanceDto?> DeleteMaintenanceCommand { get; }
    public bool IsNotLoading => !IsLoading;
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public int RecordCount => Maintenances.Count + FuelRecords.Count;
    public string TotalCostText => $"{Maintenances.Sum(record => record.cost) + FuelRecords.Sum(record => record.cost):N2} ₺";
    public int FilteredRecordCount => FilteredMaintenances.Count;
    public string MaintenanceEmptyMessage => Maintenances.Count == 0
        ? "Henüz harcama kaydı yok."
        : "Bu tarih aralığında harcama kaydı bulunamadı.";
    public bool HasFuelError => !string.IsNullOrWhiteSpace(FuelErrorMessage);
    public bool HasDocumentError => !string.IsNullOrWhiteSpace(DocumentErrorMessage);
    public bool HasFuelRecords => FuelRecords.Count > 0;
    public bool HasFilteredMaintenances => FilteredMaintenances.Count > 0;
    public bool HasNoFilteredMaintenances => FilteredMaintenances.Count == 0;
    public string FuelConsumptionText => CalculateFuelConsumption() is { } consumption
        ? $"{consumption:N2} L/100 km"
        : "İki tam depo kaydı girince hesaplanır";
    public string FuelSummaryText => $"{FuelRecords.Count} yakıt kaydı";
    public string FuelRecentRecordsText => string.Join(Environment.NewLine,
        FuelRecords.OrderByDescending(record => record.date).Take(3).Select(record =>
            $"{record.date:dd.MM.yyyy} · {record.liters:N2} L · {record.cost:N2} ₺ · {record.atKm:N0} km"));
    public bool HasUpcomingReminders => !string.IsNullOrWhiteSpace(UpcomingRemindersText);
    public string UpcomingRemindersText => BuildUpcomingRemindersText();

    public string SelectedHistoryFilter
    {
        get => _selectedHistoryFilter;
        set
        {
            if (SetProperty(ref _selectedHistoryFilter, value))
                ApplyHistoryFilter();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(IsNotLoading));
                AddMaintenanceCommand?.NotifyCanExecuteChanged();
                AddFuelRecordCommand?.NotifyCanExecuteChanged();
                SaveVehicleDocumentsCommand?.NotifyCanExecuteChanged();
                DeleteMaintenanceCommand?.NotifyCanExecuteChanged();
            }
        }
    }

    public string SelectedEntryType
    {
        get => _selectedEntryType;
        set
        {
            if (SetProperty(ref _selectedEntryType, value))
            {
                OnPropertyChanged(nameof(IsMaintenanceEntry));
                OnPropertyChanged(nameof(IsFuelEntry));
                OnPropertyChanged(nameof(IsDocumentEntry));
                OnPropertyChanged(nameof(IsTireCategory));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
                OnPropertyChanged(nameof(SelectedCategory));
                OnPropertyChanged(nameof(MaintenanceTitle));

                if (!IsMaintenanceCategory)
                {
                    HasDateReminder = false;
                    HasMileageReminder = false;
                    DueAtKm = string.Empty;
                }
            }
        }
    }

    public string SelectedDocumentType
    {
        get => _selectedDocumentType;
        set
        {
            if (!SetProperty(ref _selectedDocumentType, value))
                return;

            DocumentDate = TryGetDocumentDate(value, out var savedExpiryDate)
                ? savedExpiryDate.AddYears(-GetDocumentRenewalIntervalYears(value))
                : DateTime.Today;
            OnPropertyChanged(nameof(DocumentExpiryDate));
            OnPropertyChanged(nameof(DocumentExpirySummary));
        }
    }

    public string DocumentExpirySummary =>
        $"{SelectedDocumentType} {DocumentExpiryDate:dd.MM.yyyy} tarihinde bitecek " +
        $"({FormatDateStatus(DocumentExpiryDate)}). Bitişten 30 gün önce hatırlatılır.";

    public bool IsMaintenanceEntry => SelectedEntryType is
        "Bakım" or "Lastik" or "Onarım" or "Diğer";
    public bool IsFuelEntry => SelectedEntryType == "Yakıt Alımı";
    public bool IsDocumentEntry => SelectedEntryType == "Belge Tarihleri";
    public bool IsTireCategory => SelectedEntryType == "Lastik";
    public bool IsMaintenanceCategory => SelectedEntryType == "Bakım";
    public bool IsEngineOilChanged => EngineOilChanged;
    public string SelectedCategory => SelectedEntryType switch
    {
        "Lastik" => "Lastik",
        "Onarım" => "Onarım",
        "Diğer" => "Diğer",
        _ => "Bakım"
    };

    public string MaintenanceTitle
    {
        get
        {
            if (!IsMaintenanceCategory)
                return SelectedEntryType;

            var selectedItems = GetSelectedMaintenanceItems();
            return selectedItems.Count == 0 ? "Bakım" : string.Join(", ", selectedItems);
        }
    }

    public bool EngineOilChanged
    {
        get => _engineOilChanged;
        set
        {
            if (SetProperty(ref _engineOilChanged, value))
            {
                OnPropertyChanged(nameof(IsEngineOilChanged));
                OnPropertyChanged(nameof(MaintenanceTitle));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
            }
        }
    }

    public bool OilFilterChanged
    {
        get => _oilFilterChanged;
        set
        {
            if (SetProperty(ref _oilFilterChanged, value))
            {
                OnPropertyChanged(nameof(MaintenanceTitle));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
            }
        }
    }

    public bool AirFilterChanged
    {
        get => _airFilterChanged;
        set
        {
            if (SetProperty(ref _airFilterChanged, value))
            {
                OnPropertyChanged(nameof(MaintenanceTitle));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
            }
        }
    }

    public bool CabinFilterChanged
    {
        get => _cabinFilterChanged;
        set
        {
            if (SetProperty(ref _cabinFilterChanged, value))
            {
                OnPropertyChanged(nameof(MaintenanceTitle));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
            }
        }
    }

    public bool FuelFilterChanged
    {
        get => _fuelFilterChanged;
        set
        {
            if (SetProperty(ref _fuelFilterChanged, value))
            {
                OnPropertyChanged(nameof(MaintenanceTitle));
                OnPropertyChanged(nameof(IsMaintenanceCategory));
            }
        }
    }

    public bool HasDateReminder
    {
        get => _hasDateReminder;
        set
        {
            if (!SetProperty(ref _hasDateReminder, value))
                return;

            if (value)
                SetDefaultDueDate();
            else
                _hasCustomDueDate = false;
        }
    }

    public bool HasMileageReminder
    {
        get => _hasMileageReminder;
        set
        {
            if (!SetProperty(ref _hasMileageReminder, value))
                return;

            if (value)
                SetDefaultDueAtKm();
            else
                _hasCustomDueAtKm = false;
        }
    }

    public DateTime DueDate
    {
        get => _dueDate;
        set
        {
            if (SetProperty(ref _dueDate, value) && !_isSettingReminderDefaults && HasDateReminder)
                _hasCustomDueDate = true;
        }
    }

    public string DueAtKm
    {
        get => _dueAtKm;
        set
        {
            if (SetProperty(ref _dueAtKm, value) && !_isSettingReminderDefaults && HasMileageReminder)
                _hasCustomDueAtKm = true;
        }
    }

    public string FuelLiters
    {
        get => _fuelLiters;
        set => SetProperty(ref _fuelLiters, value);
    }

    public string FuelCost
    {
        get => _fuelCost;
        set => SetProperty(ref _fuelCost, value);
    }

    public string FuelAtKm
    {
        get => _fuelAtKm;
        set => SetProperty(ref _fuelAtKm, value);
    }

    public bool FuelIsFullTank
    {
        get => _fuelIsFullTank;
        set => SetProperty(ref _fuelIsFullTank, value);
    }

    public DateTime DocumentDate
    {
        get => _documentDate;
        set
        {
            if (!SetProperty(ref _documentDate, value))
                return;

            OnPropertyChanged(nameof(DocumentExpiryDate));
            OnPropertyChanged(nameof(DocumentExpirySummary));
        }
    }

    public DateTime DocumentExpiryDate =>
        DocumentDate.AddYears(GetDocumentRenewalIntervalYears(SelectedDocumentType));

    public string DocumentExpenseCost
    {
        get => _documentExpenseCost;
        set => SetProperty(ref _documentExpenseCost, value);
    }

    public string? FuelErrorMessage
    {
        get => _fuelErrorMessage;
        private set
        {
            if (SetProperty(ref _fuelErrorMessage, value))
                OnPropertyChanged(nameof(HasFuelError));
        }
    }

    public string? DocumentErrorMessage
    {
        get => _documentErrorMessage;
        private set
        {
            if (SetProperty(ref _documentErrorMessage, value))
                OnPropertyChanged(nameof(HasDocumentError));
        }
    }

    public string EngineOilDetails
    {
        get => _engineOilDetails;
        set => SetProperty(ref _engineOilDetails, value);
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Cost
    {
        get => _cost;
        set
        {
            if (SetProperty(ref _cost, value))
                OnPropertyChanged(nameof(TireUnitPriceText));
        }
    }

    public string TireQuantity
    {
        get => _tireQuantity;
        set
        {
            if (SetProperty(ref _tireQuantity, value))
                OnPropertyChanged(nameof(TireUnitPriceText));
        }
    }

    public string TireUnitPriceText
    {
        get
        {
            if (!int.TryParse(TireQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var quantity)
                || quantity <= 0
                || !TryParseAmount(Cost, out var totalCost)
                || totalCost <= 0)
                return "Toplam tutar ve adet girildiğinde hesaplanır.";

            return $"Tek lastik: {totalCost / quantity:N2} ₺";
        }
    }

    public string AtKm
    {
        get => _atKm;
        set
        {
            if (SetProperty(ref _atKm, value) && HasMileageReminder && !_hasCustomDueAtKm)
                SetDefaultDueAtKm();
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string TireWidth
    {
        get => _tireWidth;
        set => SetProperty(ref _tireWidth, value);
    }

    public string TireProfile
    {
        get => _tireProfile;
        set => SetProperty(ref _tireProfile, value);
    }

    public string TireRim
    {
        get => _tireRim;
        set => SetProperty(ref _tireRim, value);
    }

    public string SelectedTireSeason
    {
        get => _selectedTireSeason;
        set => SetProperty(ref _selectedTireSeason, value);
    }

    public DateTime SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value) && HasDateReminder && !_hasCustomDueDate)
                SetDefaultDueDate();
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public VehicleExpensesViewModel(VehicleDto selectedVehicle)
    {
        Vehicle = selectedVehicle;
        AtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
        FuelAtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
        LoadMaintenancesCommand = new AsyncRelayCommand(LoadMaintenancesAsync);
        AddMaintenanceCommand = new AsyncRelayCommand(AddMaintenanceAsync, () => !IsLoading);
        AddFuelRecordCommand = new AsyncRelayCommand(AddFuelRecordAsync, () => !IsLoading);
        SaveVehicleDocumentsCommand = new AsyncRelayCommand(SaveVehicleDocumentsAsync, () => !IsLoading);
        DeleteMaintenanceCommand = new AsyncRelayCommand<MaintenanceDto?>(
            DeleteMaintenanceAsync,
            _ => !IsLoading);
        _ = LoadMaintenancesCommand.ExecuteAsync(null);
    }

    public async Task<string?> UpdateCurrentMileageAsync(string mileageText)
    {
        if (!int.TryParse(mileageText, NumberStyles.None, CultureInfo.InvariantCulture, out var mileage)
            || mileage < 0)
            return "Kilometreyi sıfır veya daha büyük bir tam sayı olarak gir.";

        var errorMessage = await PersistVehicleMileageAsync(mileage);
        if (errorMessage is not null)
            return errorMessage;

        await NotifyMileageRemindersAsync();
        return null;
    }

    private async Task LoadMaintenancesAsync()
    {
        if (IsLoading)
            return;

        IsLoading = true;
        AddMaintenanceCommand.NotifyCanExecuteChanged();
        DeleteMaintenanceCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;

        try
        {
            await RefreshVehicleAsync();

            using var response = await Client.GetAsync(
                $"{ApiEndpoints.Maintenances}/vehicle/{Vehicle.id}");
            response.EnsureSuccessStatusCode();

            var records = await response.Content.ReadFromJsonAsync<List<MaintenanceDto>>(JsonOptions)
                ?? throw new JsonException("The maintenance API returned an empty response.");

            ReplaceMaintenances(records);
            await Task.WhenAll(RefreshFuelRecordsAsync(), RefreshVehicleDocumentsAsync());
            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
            await ScheduleDateRemindersAsync();
            await NotifyMileageRemindersAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Maintenance records could not be loaded: {ex}");
            ErrorMessage = GetErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
            AddMaintenanceCommand.NotifyCanExecuteChanged();
            DeleteMaintenanceCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task AddMaintenanceAsync()
    {
        ErrorMessage = null;

        if (IsMaintenanceCategory
            && !EngineOilChanged && !OilFilterChanged && !AirFilterChanged
            && !CabinFilterChanged && !FuelFilterChanged)
        {
            ErrorMessage = "Yapılan bakım işlemlerinden en az birini işaretle.";
            return;
        }

        string? tireDetails = null;
        if (IsTireCategory && !TryBuildTireSearchQuery(out tireDetails, out var tireValidationError))
        {
            ErrorMessage = tireValidationError;
            return;
        }

        if (!decimal.TryParse(Cost, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedCost)
            && !decimal.TryParse(Cost, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedCost))
        {
            ErrorMessage = "Geçerli bir tutar gir.";
            return;
        }

        if (parsedCost <= 0)
        {
            ErrorMessage = "Tutar sıfırdan büyük olmalı.";
            return;
        }

        if (IsTireCategory)
        {
            if (!int.TryParse(TireQuantity, NumberStyles.None, CultureInfo.InvariantCulture, out var tireQuantity)
                || tireQuantity <= 0)
            {
                ErrorMessage = "Lastik adedini en az 1 olacak şekilde gir.";
                return;
            }

            tireDetails = $"{tireDetails}; Adet: {tireQuantity}; Tek lastik: {parsedCost / tireQuantity:N2} ₺";
        }

        if (!int.TryParse(AtKm, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedKm)
            || parsedKm < 0)
        {
            ErrorMessage = "Kilometreyi sıfır veya daha büyük bir tam sayı olarak gir.";
            return;
        }

        var hasDateReminder = IsMaintenanceCategory && HasDateReminder;
        var hasMileageReminder = IsMaintenanceCategory && HasMileageReminder;
        int? parsedDueAtKm = null;
        if (hasMileageReminder)
        {
            if (!int.TryParse(DueAtKm, NumberStyles.None, CultureInfo.InvariantCulture, out var dueAtKm)
                || dueAtKm <= parsedKm)
            {
                ErrorMessage = "Hatırlatma kilometresini işlem kilometresinden büyük gir.";
                return;
            }

            parsedDueAtKm = dueAtKm;
        }

        if (hasDateReminder && DueDate.Date <= SelectedDate.Date)
        {
            ErrorMessage = "Hatırlatma tarihi işlem tarihinden sonra olmalı.";
            return;
        }

        IsLoading = true;
        AddMaintenanceCommand.NotifyCanExecuteChanged();
        DeleteMaintenanceCommand.NotifyCanExecuteChanged();

        try
        {
            var record = new MaintenanceDto
            {
                vehicleId = Vehicle.id,
                category = SelectedCategory,
                title = string.IsNullOrWhiteSpace(Title)
                    ? MaintenanceTitle
                    : Title.Trim(),
                notes = BuildNotes(tireDetails),
                date = DateTime.SpecifyKind(SelectedDate.Date, DateTimeKind.Utc),
                atKm = parsedKm,
                cost = parsedCost,
                dueDate = hasDateReminder
                    ? DateTime.SpecifyKind(DueDate.Date, DateTimeKind.Utc)
                    : null,
                dueAtKm = parsedDueAtKm
            };

            using var response = await Client.PostAsJsonAsync(ApiEndpoints.Maintenances, record, JsonOptions);
            response.EnsureSuccessStatusCode();
            var savedRecord = await response.Content.ReadFromJsonAsync<MaintenanceDto>(JsonOptions)
                ?? throw new JsonException("The maintenance API returned an empty response.");
            if (parsedKm > Vehicle.currentKm)
            {
                var mileageError = await PersistVehicleMileageAsync(parsedKm);
                if (mileageError is not null)
                    ErrorMessage = $"Kayıt eklendi ancak araç kilometresi veritabanına güncellenemedi: {mileageError}";
            }

            Title = string.Empty;
            Cost = string.Empty;
            AtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
            Notes = string.Empty;
            TireWidth = string.Empty;
            TireProfile = string.Empty;
            TireRim = string.Empty;
            TireQuantity = string.Empty;
            EngineOilChanged = false;
            OilFilterChanged = false;
            AirFilterChanged = false;
            CabinFilterChanged = false;
            FuelFilterChanged = false;
            EngineOilDetails = string.Empty;
            HasDateReminder = false;
            HasMileageReminder = false;
            DueDate = DateTime.Today.AddYears(1);
            DueAtKm = string.Empty;
            SelectedDate = DateTime.Today;
            SelectedEntryType = "Bakım";

            var notificationPermissionGranted = true;
            if (savedRecord.dueDate is { } dueDate)
            {
                notificationPermissionGranted &= await ReminderNotificationService.ScheduleAsync(
                    100000 + savedRecord.id,
                    $"{VehicleDisplayName} bakım hatırlatması",
                    $"{savedRecord.title} için bakım zamanı yaklaşıyor.",
                    dueDate);
            }

            if (savedRecord.dueAtKm is { } dueAtKm && Vehicle.currentKm >= dueAtKm)
                notificationPermissionGranted &= await NotifyMileageReminderAsync(savedRecord);

            try
            {
                await RefreshMaintenancesAsync();
                OnPropertyChanged(nameof(UpcomingRemindersText));
                OnPropertyChanged(nameof(HasUpcomingReminders));
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
            {
                Debug.WriteLine($"Maintenance was saved, but the list could not be refreshed: {ex}");
                ErrorMessage = "Kayıt eklendi ancak liste yenilenemedi. Yeniden yüklemeyi deneyebilirsin.";
            }

            if (!notificationPermissionGranted)
                ErrorMessage = "Kayıt eklendi; ancak bildirim izni kapalı olduğu için telefon bildirimi ayarlanamadı.";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Maintenance record could not be saved: {ex}");
            ErrorMessage = GetErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
            AddMaintenanceCommand.NotifyCanExecuteChanged();
            DeleteMaintenanceCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task AddFuelRecordAsync()
    {
        FuelErrorMessage = null;
        if (!decimal.TryParse(FuelLiters, NumberStyles.Number, CultureInfo.CurrentCulture, out var liters)
            && !decimal.TryParse(FuelLiters, NumberStyles.Number, CultureInfo.InvariantCulture, out liters))
        {
            FuelErrorMessage = "Geçerli bir litre miktarı gir.";
            return;
        }

        if (liters <= 0)
        {
            FuelErrorMessage = "Litre miktarı sıfırdan büyük olmalı.";
            return;
        }

        if (!decimal.TryParse(FuelCost, NumberStyles.Number, CultureInfo.CurrentCulture, out var cost)
            && !decimal.TryParse(FuelCost, NumberStyles.Number, CultureInfo.InvariantCulture, out cost))
        {
            FuelErrorMessage = "Geçerli bir yakıt tutarı gir.";
            return;
        }

        if (cost <= 0)
        {
            FuelErrorMessage = "Yakıt tutarı sıfırdan büyük olmalı.";
            return;
        }

        if (!int.TryParse(FuelAtKm, NumberStyles.None, CultureInfo.InvariantCulture, out var atKm)
            || atKm < Vehicle.currentKm)
        {
            FuelErrorMessage = $"Kilometre, aracın mevcut değerinden ({Vehicle.currentKm:N0} km) düşük olamaz.";
            return;
        }

        IsLoading = true;
        try
        {
            var record = new FuelRecordDto
            {
                vehicleId = Vehicle.id,
                date = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Utc),
                atKm = atKm,
                liters = liters,
                cost = cost,
                isFullTank = FuelIsFullTank
            };

            using var response = await Client.PostAsJsonAsync(ApiEndpoints.FuelRecords, record, JsonOptions);
            response.EnsureSuccessStatusCode();
            await RefreshFuelRecordsAsync();

            if (atKm > Vehicle.currentKm)
            {
                var mileageError = await PersistVehicleMileageAsync(atKm);
                if (mileageError is not null)
                    FuelErrorMessage = $"Yakıt kaydı eklendi ancak araç kilometresi veritabanına güncellenemedi: {mileageError}";
            }
            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
            FuelLiters = string.Empty;
            FuelCost = string.Empty;
            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
            await NotifyMileageRemindersAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Fuel record could not be saved: {ex}");
            FuelErrorMessage = GetErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SaveVehicleDocumentsAsync()
    {
        DocumentErrorMessage = null;
        decimal? documentExpenseCost = null;
        if (!string.IsNullOrWhiteSpace(DocumentExpenseCost))
        {
            var hasValidCost =
                decimal.TryParse(DocumentExpenseCost, NumberStyles.Number, CultureInfo.CurrentCulture, out var parsedCost)
                || decimal.TryParse(DocumentExpenseCost, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedCost);
            if (!hasValidCost || parsedCost <= 0)
            {
                DocumentErrorMessage = "Belge harcaması için sıfırdan büyük geçerli bir tutar gir.";
                return;
            }

            documentExpenseCost = parsedCost;
        }

        var documentToSave = CreateDocument(SelectedDocumentType, DocumentExpiryDate);

        IsLoading = true;
        try
        {
            var endpoint = $"{ApiEndpoints.VehicleDocuments}/vehicle/{Vehicle.id}";
            using var response = await Client.PutAsJsonAsync(endpoint, documentToSave, JsonOptions);
            response.EnsureSuccessStatusCode();
            await RefreshVehicleDocumentsAsync();

            if (documentExpenseCost is { } expenseCost)
            {
                var expense = new MaintenanceDto
                {
                    vehicleId = Vehicle.id,
                    category = "Belge",
                    title = $"{SelectedDocumentType} gideri",
                    notes = $"{SelectedDocumentType} belge yenileme gideri.",
                    date = DateTime.SpecifyKind(DocumentDate.Date, DateTimeKind.Utc),
                    atKm = Vehicle.currentKm,
                    cost = expenseCost
                };

                try
                {
                    using var expenseResponse = await Client.PostAsJsonAsync(
                        ApiEndpoints.Maintenances,
                        expense,
                        JsonOptions);
                    expenseResponse.EnsureSuccessStatusCode();
                    DocumentExpenseCost = string.Empty;
                }
                catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
                {
                    Debug.WriteLine($"Document dates were saved, but the related expense could not be saved: {ex}");
                    DocumentErrorMessage = $"Belge tarihi kaydedildi ancak harcama kaydedilemedi: {GetErrorMessage(ex)}";
                }

                if (string.IsNullOrWhiteSpace(DocumentExpenseCost))
                {
                    try
                    {
                        await RefreshMaintenancesAsync();
                    }
                    catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
                    {
                        Debug.WriteLine($"Document expense was saved, but maintenance history could not be refreshed: {ex}");
                        DocumentErrorMessage = "Belge harcaması kaydedildi ancak harcama geçmişi yenilenemedi.";
                    }
                }
            }

            var permissionGranted = true;
            foreach (var document in VehicleDocuments)
            {
                permissionGranted &= await ReminderNotificationService.ScheduleAsync(
                    GetDocumentNotificationId(document.type),
                    $"{VehicleDisplayName} - {document.type}",
                    $"{document.type} bitiş tarihi yaklaşıyor: {document.expiryDate:dd.MM.yyyy}.",
                    document.expiryDate);
            }

            foreach (var type in new[] { "Muayene", "Trafik sigortası", "Kasko" })
            {
                if (!VehicleDocuments.Any(document => document.type == type))
                    ReminderNotificationService.Cancel(GetDocumentNotificationId(type));
            }

            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
            if (!permissionGranted && string.IsNullOrWhiteSpace(DocumentErrorMessage))
                DocumentErrorMessage = "Belgeler kaydedildi; ancak bildirim izni kapalı olduğu için telefon bildirimi ayarlanamadı.";
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Vehicle documents could not be saved: {ex}");
            DocumentErrorMessage = GetErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DeleteMaintenanceAsync(MaintenanceDto? record)
    {
        if (record is null || IsLoading)
            return;

        var page = Application.Current?.Windows.FirstOrDefault()?.Page;
        if (page is null)
        {
            ErrorMessage = "Silme onayı gösterilemedi.";
            return;
        }

        var confirmed = await page.DisplayAlertAsync(
            "Kaydı sil",
            $"“{record.title}” harcama kaydı kalıcı olarak silinsin mi?",
            "Sil",
            "Vazgeç");

        if (!confirmed)
            return;

        IsLoading = true;
        AddMaintenanceCommand.NotifyCanExecuteChanged();
        DeleteMaintenanceCommand.NotifyCanExecuteChanged();
        ErrorMessage = null;

        try
        {
            using var response = await Client.DeleteAsync(
                $"{ApiEndpoints.Maintenances}/{record.id}");
            response.EnsureSuccessStatusCode();

            ReminderNotificationService.Cancel(100000 + record.id);
            ReminderNotificationService.Cancel(300000 + record.id);
            Maintenances.Remove(record);
            ApplyHistoryFilter();
            OnPropertyChanged(nameof(RecordCount));
            OnPropertyChanged(nameof(TotalCostText));
            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Maintenance record {record.id} could not be deleted: {ex}");
            ErrorMessage = GetErrorMessage(ex);
        }
        finally
        {
            IsLoading = false;
            AddMaintenanceCommand.NotifyCanExecuteChanged();
            DeleteMaintenanceCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private async Task SearchTiresAsync()
    {
        ErrorMessage = null;

        if (!TryBuildTireSearchQuery(out var searchQuery, out var validationError))
        {
            ErrorMessage = validationError;
            return;
        }

        try
        {
            var searchUri = new Uri(
                $"https://www.google.com/search?q={Uri.EscapeDataString(searchQuery)}");
            await Launcher.Default.OpenAsync(searchUri);
        }
        catch (Exception ex) when (
            ex is FeatureNotSupportedException or UriFormatException or InvalidOperationException)
        {
            Debug.WriteLine($"Could not open tire search: {ex}");
            ErrorMessage = "Google araması açılamadı. Cihazda bir tarayıcı bulunduğunu kontrol et.";
        }
    }

    private bool TryBuildTireSearchQuery(out string query, out string error)
    {
        query = string.Empty;
        error = string.Empty;

        if (!int.TryParse(TireWidth, NumberStyles.None, CultureInfo.InvariantCulture, out var width)
            || width is < 125 or > 355)
        {
            error = "Lastik genişliğini 125 ile 355 arasında gir (ör. 205).";
            return false;
        }

        if (!int.TryParse(TireProfile, NumberStyles.None, CultureInfo.InvariantCulture, out var profile)
            || profile is < 25 or > 85)
        {
            error = "Yanak oranını 25 ile 85 arasında gir (ör. 55).";
            return false;
        }

        if (!int.TryParse(TireRim, NumberStyles.None, CultureInfo.InvariantCulture, out var rim)
            || rim is < 10 or > 24)
        {
            error = "Jant çapını 10 ile 24 arasında gir (ör. 16).";
            return false;
        }

        if (!TireSeasons.Contains(SelectedTireSeason))
        {
            error = "Lastik mevsimini seç.";
            return false;
        }

        query = $"{width}/{profile} R{rim} {SelectedTireSeason}";
        return true;
    }

    private static bool TryParseAmount(string value, out decimal amount) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
        || decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);

    private async Task RefreshFuelRecordsAsync()
    {
        using var response = await Client.GetAsync(
            $"{ApiEndpoints.FuelRecords}/vehicle/{Vehicle.id}");
        response.EnsureSuccessStatusCode();

        var records = await response.Content.ReadFromJsonAsync<List<FuelRecordDto>>(JsonOptions)
            ?? throw new JsonException("The fuel records API returned an empty response.");

        FuelRecords.Clear();
        foreach (var record in records.OrderByDescending(record => record.date))
            FuelRecords.Add(record);

        OnPropertyChanged(nameof(FuelConsumptionText));
        OnPropertyChanged(nameof(FuelSummaryText));
        OnPropertyChanged(nameof(FuelRecentRecordsText));
        OnPropertyChanged(nameof(HasFuelRecords));
        OnPropertyChanged(nameof(RecordCount));
        OnPropertyChanged(nameof(TotalCostText));
    }

    private async Task RefreshVehicleDocumentsAsync()
    {
        using var response = await Client.GetAsync(
            $"{ApiEndpoints.VehicleDocuments}/vehicle/{Vehicle.id}");
        response.EnsureSuccessStatusCode();

        var documents = await response.Content.ReadFromJsonAsync<List<VehicleDocumentDto>>(JsonOptions)
            ?? throw new JsonException("The vehicle documents API returned an empty response.");

        VehicleDocuments.Clear();
        foreach (var document in documents)
            VehicleDocuments.Add(document);

        if (TryGetDocumentDate(SelectedDocumentType, out var savedDate))
            DocumentDate = savedDate.AddYears(-GetDocumentRenewalIntervalYears(SelectedDocumentType));
    }

    private async Task ScheduleDateRemindersAsync()
    {
        var permissionGranted = true;
        foreach (var maintenance in Maintenances.Where(record => record.dueDate is not null))
        {
            var dueDate = maintenance.dueDate!.Value;
            if (dueDate.Date < DateTime.Today)
            {
                ReminderNotificationService.Cancel(100000 + maintenance.id);
                continue;
            }

            permissionGranted &= await ReminderNotificationService.ScheduleAsync(
                100000 + maintenance.id,
                $"{VehicleDisplayName} bakım hatırlatması",
                $"{maintenance.title} için bakım zamanı yaklaşıyor.",
                dueDate);
        }

        foreach (var document in VehicleDocuments)
        {
            if (document.expiryDate.Date < DateTime.Today)
            {
                ReminderNotificationService.Cancel(GetDocumentNotificationId(document.type));
                continue;
            }

            permissionGranted &= await ReminderNotificationService.ScheduleAsync(
                GetDocumentNotificationId(document.type),
                $"{VehicleDisplayName} - {document.type}",
                $"{document.type} bitiş tarihi yaklaşıyor: {document.expiryDate:dd.MM.yyyy}.",
                document.expiryDate);
        }

        if (!permissionGranted)
            Debug.WriteLine("Notification permission is disabled; in-app reminders remain available.");
    }

    private async Task NotifyMileageRemindersAsync()
    {
        foreach (var maintenance in Maintenances.Where(record =>
                     record.dueAtKm is not null && Vehicle.currentKm >= record.dueAtKm))
        {
            await NotifyMileageReminderAsync(maintenance);
        }
    }

    private async Task<bool> NotifyMileageReminderAsync(MaintenanceDto maintenance)
    {
        var dueAtKm = maintenance.dueAtKm!.Value;
        var preferenceKey = $"reminder.mileage.{maintenance.id}.{dueAtKm}";
        if (Preferences.Default.Get(preferenceKey, false))
            return true;

        var permissionGranted = await ReminderNotificationService.ShowNowAsync(
            300000 + maintenance.id,
            $"{VehicleDisplayName} bakım kilometresi geldi",
            $"{maintenance.title} için belirlenen {dueAtKm:N0} km sınırına ulaşıldı.");
        if (permissionGranted)
            Preferences.Default.Set(preferenceKey, true);

        return permissionGranted;
    }

    private bool TryGetDocumentDate(string type, out DateTime date)
    {
        var document = VehicleDocuments.FirstOrDefault(item => item.type == type);
        date = document?.expiryDate.Date ?? default;
        return document is not null;
    }

    private static int GetDocumentRenewalIntervalYears(string type) =>
        type == "Muayene" ? 2 : 1;

    private static VehicleDocumentDto CreateDocument(string type, DateTime expiryDate) => new()
    {
        type = type,
        expiryDate = DateTime.SpecifyKind(expiryDate.Date, DateTimeKind.Utc)
    };

    private static int GetDocumentNotificationId(string type) => type switch
    {
        "Muayene" => 200000,
        "Trafik sigortası" => 200001,
        "Kasko" => 200002,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown vehicle document type.")
    };

    private decimal? CalculateFuelConsumption()
    {
        decimal litersBetweenFullTanks = 0;
        decimal totalLiters = 0;
        int? previousFullTankKm = null;
        var totalDistance = 0;

        foreach (var record in FuelRecords.OrderBy(record => record.atKm).ThenBy(record => record.date))
        {
            if (previousFullTankKm is not null)
                litersBetweenFullTanks += record.liters;

            if (!record.isFullTank)
                continue;

            if (previousFullTankKm is { } previousKm && record.atKm > previousKm)
            {
                totalLiters += litersBetweenFullTanks;
                totalDistance += record.atKm - previousKm;
            }

            previousFullTankKm = record.atKm;
            litersBetweenFullTanks = 0;
        }

        return totalDistance > 0 ? totalLiters * 100 / totalDistance : null;
    }

    private string BuildUpcomingRemindersText()
    {
        var reminders = new List<(DateTime? Date, string Text)>();

        foreach (var maintenance in Maintenances.Where(record =>
                     record.dueDate is not null || record.dueAtKm is not null))
        {
            var statuses = new List<string>();
            if (maintenance.dueDate is { } dueDate)
                statuses.Add(FormatDateStatus(dueDate));
            if (maintenance.dueAtKm is { } dueAtKm)
                statuses.Add(dueAtKm <= Vehicle.currentKm
                    ? "Kilometre zamanı geldi"
                    : $"{dueAtKm - Vehicle.currentKm:N0} km kaldı");

            reminders.Add((maintenance.dueDate, $"Bakım: {string.Join(" · ", statuses)}"));
        }

        foreach (var document in VehicleDocuments)
            reminders.Add((document.expiryDate,
                $"{document.type}: {document.expiryDate:dd.MM.yyyy} · {FormatDateStatus(document.expiryDate)}"));

        return string.Join(Environment.NewLine,
            reminders.OrderBy(item => item.Date ?? DateTime.MaxValue)
                .Select(item => item.Text));
    }

    private static string FormatDateStatus(DateTime date)
    {
        var daysRemaining = (date.Date - DateTime.Today).Days;
        return daysRemaining switch
        {
            < 0 => $"{Math.Abs(daysRemaining)} gün gecikti",
            0 => "Bugün",
            1 => "1 gün kaldı",
            _ => $"{daysRemaining} gün kaldı"
        };
    }

    private string? BuildNotes(string? tireDetails)
    {
        var noteSections = new List<string>();

        if (IsMaintenanceCategory)
        {
            var selectedItems = GetSelectedMaintenanceItems();
            if (selectedItems.Count > 0)
                noteSections.Add($"Bakımda değiştirilenler: {string.Join(", ", selectedItems)}");

            var oilDetails = EngineOilDetails.Trim();
            if (IsEngineOilChanged && !string.IsNullOrEmpty(oilDetails))
                noteSections.Add($"Kullanılan motor yağı: {oilDetails}");
        }

        if (!IsMaintenanceCategory)
            noteSections.Add($"Yapılan işlem: {SelectedEntryType}");

        if (tireDetails is not null)
            noteSections.Add($"Lastik ölçüsü ve mevsimi: {tireDetails}");

        var notes = Notes.Trim();
        if (!string.IsNullOrEmpty(notes))
            noteSections.Add(notes);

        return noteSections.Count == 0 ? null : string.Join("\n", noteSections);
    }

    private List<string> GetSelectedMaintenanceItems()
    {
        var items = new List<string>();
        if (EngineOilChanged)
            items.Add("Motor yağı");
        if (OilFilterChanged)
            items.Add("Yağ filtresi");
        if (AirFilterChanged)
            items.Add("Hava filtresi");
        if (CabinFilterChanged)
            items.Add("Polen filtresi");
        if (FuelFilterChanged)
            items.Add("Yakıt filtresi");
        return items;
    }

    private async Task RefreshMaintenancesAsync()
    {
        using var response = await Client.GetAsync(
            $"{ApiEndpoints.Maintenances}/vehicle/{Vehicle.id}");
        response.EnsureSuccessStatusCode();

        var records = await response.Content.ReadFromJsonAsync<List<MaintenanceDto>>(JsonOptions)
            ?? throw new JsonException("The maintenance API returned an empty response.");

        ReplaceMaintenances(records);
    }

    private async Task RefreshVehicleAsync()
    {
        using var response = await Client.GetAsync($"{ApiEndpoints.Vehicles}/{Vehicle.id}");
        response.EnsureSuccessStatusCode();

        var latestVehicle = await response.Content.ReadFromJsonAsync<VehicleDto>(JsonOptions)
            ?? throw new JsonException("The vehicle API returned an empty response.");

        Vehicle.currentKm = latestVehicle.currentKm;
        AtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
        FuelAtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
        if (HasMileageReminder && !_hasCustomDueAtKm)
            SetDefaultDueAtKm();
    }

    private async Task<string?> PersistVehicleMileageAsync(int mileage)
    {
        try
        {
            using var response = await Client.PutAsJsonAsync(
                $"{ApiEndpoints.Vehicles}/{Vehicle.id}/current-km",
                mileage,
                JsonOptions);
            response.EnsureSuccessStatusCode();

            var updatedVehicle = await response.Content.ReadFromJsonAsync<VehicleDto>(JsonOptions)
                ?? throw new JsonException("The vehicle API returned an empty mileage update response.");
            if (updatedVehicle.currentKm != mileage)
                throw new JsonException("The vehicle API did not confirm the requested mileage.");

            Vehicle.currentKm = updatedVehicle.currentKm;
            AtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
            FuelAtKm = Vehicle.currentKm.ToString(CultureInfo.InvariantCulture);
            if (HasMileageReminder && !_hasCustomDueAtKm)
                SetDefaultDueAtKm();
            OnPropertyChanged(nameof(UpcomingRemindersText));
            OnPropertyChanged(nameof(HasUpcomingReminders));
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            Debug.WriteLine($"Vehicle mileage could not be persisted: {ex}");
            return GetErrorMessage(ex);
        }
    }

    private void SetDefaultDueDate()
    {
        _isSettingReminderDefaults = true;
        try
        {
            DueDate = SelectedDate.Date.AddYears(DefaultDateReminderYears);
        }
        finally
        {
            _isSettingReminderDefaults = false;
        }
    }

    private void SetDefaultDueAtKm()
    {
        var currentMileage = Vehicle.currentKm;
        if (int.TryParse(AtKm, NumberStyles.None, CultureInfo.InvariantCulture, out var enteredMileage))
            currentMileage = Math.Max(currentMileage, enteredMileage);

        var dueMileage = (long)currentMileage + DefaultMileageReminderIntervalKm;
        _isSettingReminderDefaults = true;
        try
        {
            DueAtKm = dueMileage <= int.MaxValue
                ? dueMileage.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
        }
        finally
        {
            _isSettingReminderDefaults = false;
        }
    }

    private void ReplaceMaintenances(IEnumerable<MaintenanceDto> records)
    {
        Maintenances.Clear();
        foreach (var record in records)
            Maintenances.Add(record);

        ApplyHistoryFilter();
        OnPropertyChanged(nameof(RecordCount));
        OnPropertyChanged(nameof(TotalCostText));
    }

    private void ApplyHistoryFilter()
    {
        var today = DateTime.Today;
        var threeMonthsAgo = today.AddMonths(-3);
        var oneYearAgo = today.AddYears(-1);

        var filteredRecords = SelectedHistoryFilter switch
        {
            "Son 3 ay" => Maintenances.Where(record =>
                record.date >= threeMonthsAgo && record.date < today.AddDays(1)),
            "Son 1 yıl" => Maintenances.Where(record =>
                record.date >= oneYearAgo && record.date < today.AddDays(1)),
            "1 yıldan eski" => Maintenances.Where(record => record.date < oneYearAgo),
            _ => Maintenances.AsEnumerable()
        };

        FilteredMaintenances.Clear();
        foreach (var record in filteredRecords
                     .OrderByDescending(record => record.date)
                     .ThenByDescending(record => record.id))
            FilteredMaintenances.Add(record);

        OnPropertyChanged(nameof(FilteredRecordCount));
        OnPropertyChanged(nameof(HasFilteredMaintenances));
        OnPropertyChanged(nameof(HasNoFilteredMaintenances));
        OnPropertyChanged(nameof(MaintenanceEmptyMessage));
    }

    private static string GetErrorMessage(Exception exception)
    {
        if (exception is TaskCanceledException)
            return "Sunucu zamanında yanıt vermedi. Tekrar deneyebilirsin.";

        if (exception is HttpRequestException { StatusCode: not null } httpException)
        {
            if (httpException.StatusCode == System.Net.HttpStatusCode.NotFound)
                return "Sunucuda istenen kayıt veya API rotası bulunamadı (HTTP 404). Backend'in güncel sürümde çalıştığını kontrol et.";

            return $"Sunucu isteği kabul etmedi (HTTP {(int)httpException.StatusCode}).";
        }

        if (exception is JsonException)
            return "Sunucudan gelen bakım kayıtları okunamadı.";

        return "API'ye bağlanılamadı. Sunucunun çalıştığını kontrol et.";
    }
}
