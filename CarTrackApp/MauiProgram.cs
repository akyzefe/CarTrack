using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
#if IOS
using Microsoft.Maui.Handlers;
using UIKit;
#endif

namespace CarTrackApp;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if IOS
		builder.ConfigureMauiHandlers(handlers =>
			handlers.AddHandler<CheckBox, CarTrackCheckBoxHandler>());

		PickerHandler.Mapper.AppendToMapping("CarTrackPickerDoneSymbol", (handler, _) =>
		{
			if (handler.PlatformView.InputAccessoryView is UIToolbar toolbar &&
				toolbar.Items?.LastOrDefault() is UIBarButtonItem doneButton)
			{
				doneButton.Title = "🔧";
				doneButton.AccessibilityLabel = "Seçimi tamamla";
			}
		});
#endif

#if IOS || ANDROID
		builder.UseLocalNotification();
#endif

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
