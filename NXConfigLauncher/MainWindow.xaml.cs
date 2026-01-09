using System.ComponentModel;
using System.Reflection;
using System.Windows;
using NXConfigLauncher.ViewModels;

namespace NXConfigLauncher;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        Title = $"NX Configuration Launcher v{GetAppVersion()}";

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // ViewModel 속성 변경 감지하여 윈도우 크기 재계산
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        Closing += MainWindow_Closing;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // ShowFirewallDetails 속성이 변경되면 윈도우 높이 재계산
        if (e.PropertyName == nameof(MainViewModel.ShowFirewallDetails))
        {
            // SizeToContent를 일시적으로 변경하여 레이아웃 재계산 강제
            Dispatcher.BeginInvoke(new System.Action(() =>
            {
                // 현재 SizeToContent 값을 저장
                var currentSizeToContent = SizeToContent;

                // Manual로 변경 후 다시 Height로 설정하여 크기 재계산
                SizeToContent = SizeToContent.Manual;
                SizeToContent = currentSizeToContent;
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static string GetAppVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version?.ToString() ?? "unknown";
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // 이벤트 핸들러 해제
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;

        _viewModel.OnWindowClosing();
    }
}
