using System.Windows;
using NXConfigLauncher.Helpers;

namespace NXConfigLauncher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // DI 컨테이너 초기화
        ServiceLocator.Initialize();

        var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        Logger.Info($"App start. Version={version} BaseDir={AppDomain.CurrentDomain.BaseDirectory}");

        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error($"DispatcherUnhandledException: {args.Exception}");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error($"UnhandledException: {args.ExceptionObject}");
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // DI 컨테이너 정리
        ServiceLocator.Dispose();
        base.OnExit(e);
    }
}
