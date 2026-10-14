using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace CarTrackApp;

public sealed class CarTrackCheckBoxHandler : CheckBoxHandler
{
    protected override MauiCheckBox CreatePlatformView() =>
        new CarTrackMauiCheckBox(VirtualView.AutomationId == "FuelFullTankCheckBox");
}

internal sealed class CarTrackMauiCheckBox : MauiCheckBox
{
    private readonly bool _useFilledCircle;

    public CarTrackMauiCheckBox(bool useFilledCircle)
    {
        _useFilledCircle = useFilledCircle;
    }

    protected override UIImage GetCheckBoxImage()
    {
        if (!IsChecked)
            return base.GetCheckBoxImage();

        var symbolName = _useFilledCircle ? "circle.fill" : "wrench.fill";
        return UIImage.GetSystemImage(symbolName)?
                   .ImageWithRenderingMode(UIImageRenderingMode.AlwaysTemplate)
               ?? base.GetCheckBoxImage();
    }
}
