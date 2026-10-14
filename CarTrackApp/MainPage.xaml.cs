namespace CarTrackApp;

public partial class MainPage : ContentPage
{
    private const string AiButtonOnLeftKey = "ai-mechanic.button.on-left";
    private const string AiButtonTranslationYKey = "ai-mechanic.button.translation-y";
    private const double AiButtonSize = 76;
    private const double AiButtonLeftInset = 16;
    private const double AiButtonRightInset = 20;
    private const double AiButtonBottomInset = 24;
    private const double AiButtonTopInset = 48;

    private bool _hasLoadedInitialData;
    private bool _hasRestoredAiButtonPosition;
    private bool _aiButtonOnLeft;
    private double _aiButtonStartCenterX;
    private double _aiButtonStartTranslationY;

    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
        SizeChanged += OnPageSizeChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        RestoreAiMechanicButtonPosition();
        if (_hasLoadedInitialData || BindingContext is not MainViewModel viewModel)
            return;

        _hasLoadedInitialData = true;
        Shell.SetNavBarIsVisible(this, false);

        var minimumSplashDuration = Task.Delay(TimeSpan.FromSeconds(1.8));

        await viewModel.LoadVehiclesCommand.ExecuteAsync(null);
        await viewModel.LoadMonthlyExpensesCommand.ExecuteAsync(null);
        await minimumSplashDuration;

        await GarageDoorImage.TranslateToAsync(0, -150, 900, Easing.CubicInOut);
        await GarageLoadingOverlay.FadeToAsync(0, 400, Easing.CubicOut);
        GarageLoadingOverlay.IsVisible = false;
        Shell.SetNavBarIsVisible(this, true);

        await viewModel.LoadWeatherCommand.ExecuteAsync(null);
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (_hasRestoredAiButtonPosition)
            ConstrainAiMechanicButtonPosition();
        else
            RestoreAiMechanicButtonPosition();
    }

    private void RestoreAiMechanicButtonPosition()
    {
        if (_hasRestoredAiButtonPosition || Width <= 0 || Height <= 0)
            return;

        _aiButtonOnLeft = Preferences.Default.Get(AiButtonOnLeftKey, false);
        SetAiButtonSide(_aiButtonOnLeft);
        AiMechanicFloatingButton.TranslationX = 0;
        AiMechanicFloatingButton.TranslationY =
            Preferences.Default.Get(AiButtonTranslationYKey, 0d);
        _hasRestoredAiButtonPosition = true;
        ConstrainAiMechanicButtonPosition();
    }

    private async void OnAiMechanicPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _aiButtonStartCenterX = GetAiButtonBaseX() +
                    AiMechanicFloatingButton.TranslationX + AiButtonSize / 2;
                _aiButtonStartTranslationY = AiMechanicFloatingButton.TranslationY;
                break;
            case GestureStatus.Running:
                _aiButtonOnLeft = _aiButtonStartCenterX + e.TotalX < Width / 2;
                SetAiButtonSide(_aiButtonOnLeft);
                var centerX = Math.Clamp(
                    _aiButtonStartCenterX + e.TotalX,
                    AiButtonLeftInset + AiButtonSize / 2,
                    Width - AiButtonRightInset - AiButtonSize / 2);
                var translationX = centerX - GetAiButtonBaseX() - AiButtonSize / 2;
                AiMechanicFloatingButton.TranslationX = translationX;
                AiMechanicFloatingButton.TranslationY =
                    _aiButtonStartTranslationY + e.TotalY;
                ConstrainAiMechanicButtonPosition();
                break;
            case GestureStatus.Completed:
                ConstrainAiMechanicButtonPosition();
                await AnimateAiMechanicButtonToEdgeAsync();
                Preferences.Default.Set(AiButtonOnLeftKey, _aiButtonOnLeft);
                Preferences.Default.Set(
                    AiButtonTranslationYKey,
                    AiMechanicFloatingButton.TranslationY);
                break;
            case GestureStatus.Canceled:
                ConstrainAiMechanicButtonPosition();
                await AnimateAiMechanicButtonToEdgeAsync();
                Preferences.Default.Set(
                    AiButtonTranslationYKey,
                    AiMechanicFloatingButton.TranslationY);
                break;
        }
    }

    private void ConstrainAiMechanicButtonPosition()
    {
        var baseY = Height - AiButtonBottomInset - AiButtonSize;
        var minY = AiButtonTopInset - baseY;
        var baseX = GetAiButtonBaseX();
        var minX = AiButtonLeftInset - baseX;
        var maxX = Width - AiButtonRightInset - AiButtonSize - baseX;
        var translationX = Math.Clamp(AiMechanicFloatingButton.TranslationX, minX, maxX);
        AiMechanicFloatingButton.TranslationX = translationX;
        AiMechanicFloatingButton.TranslationY =
            Math.Clamp(AiMechanicFloatingButton.TranslationY, minY, 0);
    }

    private async Task AnimateAiMechanicButtonToEdgeAsync()
    {
        await AiMechanicFloatingButton.TranslateToAsync(
            0,
            AiMechanicFloatingButton.TranslationY,
            260,
            Easing.CubicOut);
    }

    private double GetAiButtonBaseX() => _aiButtonOnLeft
        ? AiButtonLeftInset
        : Width - AiButtonRightInset - AiButtonSize;

    private void SetAiButtonSide(bool onLeft)
    {
        var side = onLeft ? LayoutOptions.Start : LayoutOptions.End;
        AiMechanicFloatingButton.HorizontalOptions = side;
    }

    private void OnRestoreAiMechanicButtonTapped(object? sender, TappedEventArgs e)
    {
        _aiButtonOnLeft = AiMechanicRestoreButton.X + AiMechanicRestoreButton.Width / 2 < Width / 2;
        SetAiButtonSide(_aiButtonOnLeft);
        AiMechanicFloatingButton.TranslationX =
            AiMechanicRestoreButton.X + AiMechanicRestoreButton.Width / 2
            - GetAiButtonBaseX() - AiButtonSize / 2;
        AiMechanicFloatingButton.TranslationY =
            AiMechanicRestoreButton.Y + AiMechanicRestoreButton.Height / 2
            - (Height - AiButtonBottomInset - AiButtonSize)
            - AiButtonSize / 2;
        ConstrainAiMechanicButtonPosition();
        Preferences.Default.Set(AiButtonOnLeftKey, _aiButtonOnLeft);
        Preferences.Default.Set(AiButtonTranslationYKey, AiMechanicFloatingButton.TranslationY);

        if (BindingContext is MainViewModel viewModel)
            viewModel.RestoreAiMechanicButtonCommand.Execute(null);
    }

    private async void OnBrandSelected(object? sender, EventArgs e)
    {
        await Task.Yield();

        if (BindingContext is MainViewModel viewModel && sender is Picker picker)
            viewModel.SelectBrand(picker.SelectedItem as string);
    }

    private async void OnModelSelected(object? sender, EventArgs e)
    {
        await Task.Yield();

        if (BindingContext is MainViewModel viewModel && sender is Picker picker)
            viewModel.SelectModel(picker.SelectedItem as string);
    }

    private void OnVehicleDragStarting(object? sender, DragStartingEventArgs e)
    {
        if (sender is not Element { BindingContext: VehicleDto vehicle })
            return;

        e.Data.Properties["CarTrack.VehicleId"] = vehicle.id;
    }

    private void OnVehicleDrop(object? sender, DropEventArgs e)
    {
        if (BindingContext is not MainViewModel viewModel ||
            sender is not Element { BindingContext: VehicleDto targetVehicle } ||
            !e.Data.Properties.TryGetValue("CarTrack.VehicleId", out var draggedVehicleId) ||
            draggedVehicleId is not int vehicleId)
            return;

        viewModel.ReorderVehicle(vehicleId, targetVehicle.id);
    }

}