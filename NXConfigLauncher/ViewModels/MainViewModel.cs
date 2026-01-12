using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NXConfigLauncher.Helpers;
using NXConfigLauncher.Models;
using NXConfigLauncher.Services;

namespace NXConfigLauncher.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly INxDetectionService _nxDetectionService;
        private readonly IFirewallService _firewallService;
        private readonly IHostsService _hostsService;
        private readonly IProcessMonitorService _processMonitorService;
        private readonly IEnvironmentService _environmentService;
        private readonly IProcessService _processService;
        private readonly IConfigService _configService;
        private readonly INxLauncherService _nxLauncherService;

        #region Properties

        private ObservableCollection<NxVersionInfo> _nxVersions = new();
        public ObservableCollection<NxVersionInfo> NxVersions
        {
            get => _nxVersions;
            set => SetProperty(ref _nxVersions, value);
        }

        private NxVersionInfo? _selectedNxVersion;
        public NxVersionInfo? SelectedNxVersion
        {
            get => _selectedNxVersion;
            set
            {
                if (SetProperty(ref _selectedNxVersion, value))
                {
                    SaveSettings();
                }
            }
        }

        private bool _isNetworkBlocked;
        public bool IsNetworkBlocked
        {
            get => _isNetworkBlocked;
            set
            {
                if (SetProperty(ref _isNetworkBlocked, value))
                {
                    SaveSettings();
                }
            }
        }

        private ObservableCollection<string> _availablePorts = new();
        public ObservableCollection<string> AvailablePorts
        {
            get => _availablePorts;
            set => SetProperty(ref _availablePorts, value);
        }

        private string _selectedPort = "28000";
        public string SelectedPort
        {
            get => _selectedPort;
            set
            {
                if (SetProperty(ref _selectedPort, value))
                {
                    SaveSettings();
                }
            }
        }

        private string _serverAddress = string.Empty;
        public string ServerAddress
        {
            get => _serverAddress;
            set => SetProperty(ref _serverAddress, value);
        }

        private bool _isEnglishSelected = true;
        public bool IsEnglishSelected
        {
            get => _isEnglishSelected;
            set
            {
                if (SetProperty(ref _isEnglishSelected, value) && value)
                {
                    IsKoreanSelected = false;
                    SaveSettings();
                }
            }
        }

        private bool _isKoreanSelected;
        public bool IsKoreanSelected
        {
            get => _isKoreanSelected;
            set
            {
                if (SetProperty(ref _isKoreanSelected, value) && value)
                {
                    IsEnglishSelected = false;
                    SaveSettings();
                }
            }
        }

        private ObservableCollection<ProcessStatus> _processStatuses = new();
        public ObservableCollection<ProcessStatus> ProcessStatuses
        {
            get => _processStatuses;
            set => SetProperty(ref _processStatuses, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private Brush _statusMessageColor = Brushes.Black;
        public Brush StatusMessageColor
        {
            get => _statusMessageColor;
            set => SetProperty(ref _statusMessageColor, value);
        }

        private string _firewallStatus = "확인 중...";
        public string FirewallStatus
        {
            get => _firewallStatus;
            set => SetProperty(ref _firewallStatus, value);
        }

        private Brush _firewallStatusColor = Brushes.Gray;
        public Brush FirewallStatusColor
        {
            get => _firewallStatusColor;
            set => SetProperty(ref _firewallStatusColor, value);
        }

        private bool _showFirewallDetails;
        public bool ShowFirewallDetails
        {
            get => _showFirewallDetails;
            set => SetProperty(ref _showFirewallDetails, value);
        }

        private ObservableCollection<string> _blockedRules = new();
        public ObservableCollection<string> BlockedRules
        {
            get => _blockedRules;
            set => SetProperty(ref _blockedRules, value);
        }

        private string _domainBlockStatus = "확인 중...";
        public string DomainBlockStatus
        {
            get => _domainBlockStatus;
            set => SetProperty(ref _domainBlockStatus, value);
        }

        private Brush _domainBlockStatusColor = Brushes.Gray;
        public Brush DomainBlockStatusColor
        {
            get => _domainBlockStatusColor;
            set => SetProperty(ref _domainBlockStatusColor, value);
        }

        private ObservableCollection<string> _blockedDomains = new();
        public ObservableCollection<string> BlockedDomains
        {
            get => _blockedDomains;
            set => SetProperty(ref _blockedDomains, value);
        }

        private string _processMonitorStatus = "감시 안함";
        public string ProcessMonitorStatus
        {
            get => _processMonitorStatus;
            set => SetProperty(ref _processMonitorStatus, value);
        }

        private Brush _processMonitorStatusColor = Brushes.Gray;
        public Brush ProcessMonitorStatusColor
        {
            get => _processMonitorStatusColor;
            set => SetProperty(ref _processMonitorStatusColor, value);
        }

        private int _monitoredProcessCount;
        public int MonitoredProcessCount
        {
            get => _monitoredProcessCount;
            set => SetProperty(ref _monitoredProcessCount, value);
        }

        #endregion

        #region Commands

        public ICommand LaunchNxCommand { get; }
        public ICommand CleanProcessesCommand { get; }
        public ICommand RefreshProcessStatusCommand { get; }

        #endregion

        /// <summary>
        /// DI 생성자 - 서비스 컨테이너에서 주입
        /// </summary>
        public MainViewModel(
            INxDetectionService nxDetectionService,
            IFirewallService firewallService,
            IHostsService hostsService,
            IProcessMonitorService processMonitorService,
            IEnvironmentService environmentService,
            IProcessService processService,
            IConfigService configService,
            INxLauncherService nxLauncherService)
        {
            _nxDetectionService = nxDetectionService;
            _firewallService = firewallService;
            _hostsService = hostsService;
            _processMonitorService = processMonitorService;
            _environmentService = environmentService;
            _processService = processService;
            _configService = configService;
            _nxLauncherService = nxLauncherService;

            // 프로세스 차단 이벤트 구독
            _processMonitorService.ProcessBlocked += OnProcessBlocked;

            // 커맨드 초기화
            LaunchNxCommand = new RelayCommand(ExecuteLaunchNx, CanExecuteLaunchNx);
            CleanProcessesCommand = new RelayCommand(ExecuteCleanProcesses);
            RefreshProcessStatusCommand = new RelayCommand(ExecuteRefreshProcessStatus);

            // 초기화
            Initialize();
        }

        /// <summary>
        /// 기본 생성자 - ServiceLocator를 통한 서비스 해결 (XAML 디자이너 호환)
        /// </summary>
        public MainViewModel() : this(
            ServiceLocator.GetService<INxDetectionService>(),
            ServiceLocator.GetService<IFirewallService>(),
            ServiceLocator.GetService<IHostsService>(),
            ServiceLocator.GetService<IProcessMonitorService>(),
            ServiceLocator.GetService<IEnvironmentService>(),
            ServiceLocator.GetService<IProcessService>(),
            ServiceLocator.GetService<IConfigService>(),
            ServiceLocator.GetService<INxLauncherService>())
        {
        }

        private void Initialize()
        {
            // NX 버전 감지
            LoadNxVersions();

            // 사용 가능한 포트 목록
            LoadAvailablePorts();

            // 저장된 설정 불러오기
            LoadSettings();

            // 프로세스 상태 조회
            RefreshProcessStatus();

            // 서버 주소 가져오기
            LoadServerAddress();

            // 방화벽 상태 배지만 업데이트 (박스는 숨김 유지)
            UpdateFirewallBadge();

            // 도메인 차단 상태 초기화
            RefreshDomainBlockStatus();

            // 프로세스 감시 상태 초기화
            RefreshProcessMonitorStatus();
        }

        private void LoadNxVersions()
        {
            var versions = _nxDetectionService.DetectInstalledVersions();
            NxVersions.Clear();

            foreach (var version in versions)
            {
                NxVersions.Add(version);
            }

            if (NxVersions.Count == 0)
            {
                SetStatusMessage("NX가 설치되어 있지 않습니다.", false);
            }
        }

        private void LoadAvailablePorts()
        {
            AvailablePorts.Clear();
            foreach (var port in _environmentService.GetAvailablePorts())
            {
                AvailablePorts.Add(port);
            }
        }

        private void LoadServerAddress()
        {
            var (_, server) = _environmentService.GetLicenseServer();
            ServerAddress = server;
        }

        private void LoadSettings()
        {
            var config = _configService.Load();

            // 네트워크 차단 설정
            _isNetworkBlocked = config.IsNetworkBlocked;
            OnPropertyChanged(nameof(IsNetworkBlocked));

            // 라이센스 포트
            if (!string.IsNullOrEmpty(config.LicensePort) && AvailablePorts.Contains(config.LicensePort))
            {
                _selectedPort = config.LicensePort;
                OnPropertyChanged(nameof(SelectedPort));
            }

            // 언어 설정
            if (config.Language.ToLower() == "korean")
            {
                _isKoreanSelected = true;
                _isEnglishSelected = false;
            }
            else
            {
                _isEnglishSelected = true;
                _isKoreanSelected = false;
            }
            OnPropertyChanged(nameof(IsEnglishSelected));
            OnPropertyChanged(nameof(IsKoreanSelected));

            // 선택된 NX 버전
            if (!string.IsNullOrEmpty(config.SelectedNxVersion))
            {
                var version = NxVersions.FirstOrDefault(v =>
                    v.VersionName == config.SelectedNxVersion ||
                    v.InstallPath == config.SelectedNxPath);

                if (version != null)
                {
                    _selectedNxVersion = version;
                    OnPropertyChanged(nameof(SelectedNxVersion));
                }
            }

            // 버전이 선택되지 않았으면 첫 번째 버전 선택
            if (_selectedNxVersion == null && NxVersions.Count > 0)
            {
                _selectedNxVersion = NxVersions[0];
                OnPropertyChanged(nameof(SelectedNxVersion));
            }
        }

        private void SaveSettings()
        {
            var config = new AppConfig
            {
                SelectedNxVersion = SelectedNxVersion?.VersionName ?? string.Empty,
                SelectedNxPath = SelectedNxVersion?.InstallPath ?? string.Empty,
                IsNetworkBlocked = IsNetworkBlocked,
                LicensePort = SelectedPort,
                Language = IsKoreanSelected ? "korean" : "english"
            };

            _configService.Save(config);
        }

        private void RefreshProcessStatus()
        {
            var statuses = _processService.GetAllProcessStatus();
            ProcessStatuses.Clear();

            foreach (var status in statuses)
            {
                ProcessStatuses.Add(status);
            }
        }

        private void UpdateFirewallBadge()
        {
            // 배지만 업데이트, 상세 박스는 건드리지 않음
            var (count, _) = _firewallService.GetBlockStatus();

            if (count > 0)
            {
                FirewallStatus = $"차단 중 ({count}개)";
                FirewallStatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
            else
            {
                FirewallStatus = "차단 없음";
                FirewallStatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }

            // 도메인 차단 상태도 업데이트
            UpdateDomainBlockBadge();
        }

        private void UpdateDomainBlockBadge()
        {
            var (domainCount, _) = _hostsService.GetBlockStatus();

            if (domainCount > 0)
            {
                DomainBlockStatus = $"도메인 차단 ({domainCount}개)";
                DomainBlockStatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
            }
            else
            {
                DomainBlockStatus = "도메인 차단 없음";
                DomainBlockStatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }
        }

        private void RefreshFirewallStatus()
        {
            var (count, rules) = _firewallService.GetBlockStatus();

            BlockedRules.Clear();

            if (count > 0)
            {
                FirewallStatus = $"차단 중 ({count}개)";
                FirewallStatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red
                ShowFirewallDetails = true;

                foreach (var rule in rules)
                {
                    // "NXConfigLauncher_Block_ugraf" -> "ugraf.exe 차단됨"
                    var processName = rule.Replace("NXConfigLauncher_Block_", "") + ".exe";
                    BlockedRules.Add($"• {processName} - 아웃바운드 차단됨");
                }
            }
            else
            {
                FirewallStatus = "차단 없음";
                FirewallStatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
                ShowFirewallDetails = false;
            }

            // 도메인 차단 상태도 새로고침
            RefreshDomainBlockStatus();
        }

        private void RefreshDomainBlockStatus()
        {
            var (domainCount, domains) = _hostsService.GetBlockStatus();

            BlockedDomains.Clear();

            if (domainCount > 0)
            {
                DomainBlockStatus = $"도메인 차단 ({domainCount}개)";
                DomainBlockStatusColor = new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red

                foreach (var domain in domains)
                {
                    BlockedDomains.Add($"• {domain}");
                }
            }
            else
            {
                DomainBlockStatus = "도메인 차단 없음";
                DomainBlockStatusColor = new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green
            }

            // 프로세스 감시 상태도 새로고침
            RefreshProcessMonitorStatus();
        }

        private void RefreshProcessMonitorStatus()
        {
            if (_processMonitorService.IsMonitoring)
            {
                var blockedCount = _processMonitorService.GetBlockedProcesses().Count;
                MonitoredProcessCount = blockedCount;
                ProcessMonitorStatus = $"감시 중 ({blockedCount}개 차단)";
                ProcessMonitorStatusColor = new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue
            }
            else
            {
                MonitoredProcessCount = 0;
                ProcessMonitorStatus = "감시 안함";
                ProcessMonitorStatusColor = Brushes.Gray;
            }
        }

        private void OnProcessBlocked(object? sender, ProcessBlockedEventArgs e)
        {
            // UI 스레드에서 실행
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MonitoredProcessCount = _processMonitorService.GetBlockedProcesses().Count;
                ProcessMonitorStatus = $"감시 중 ({MonitoredProcessCount}개 차단)";
                Logger.Info($"Process blocked by monitor: {e.ProcessName} ({e.ProcessPath})");
            });
        }

        private bool CanExecuteLaunchNx(object? parameter)
        {
            return SelectedNxVersion != null;
        }

        private async void ExecuteLaunchNx(object? parameter)
        {
            if (SelectedNxVersion == null)
            {
                SetStatusMessage("NX 버전을 선택해주세요.", false);
                return;
            }

            var language = IsKoreanSelected ? "korean" : "english";
            var (success, message) = await _nxLauncherService.LaunchNxAsync(
                SelectedNxVersion,
                SelectedPort,
                language,
                IsNetworkBlocked);

            SetStatusMessage(message, success);

            // 프로세스 및 방화벽 상태 새로고침
            RefreshProcessStatus();
            RefreshFirewallStatus();
        }

        private void ExecuteCleanProcesses(object? parameter)
        {
            // 커스텀 확인 대화상자
            var confirmed = Views.ConfirmDialog.Show(
                "프로세스 정리",
                "실행 중인 NX 관련 프로세스를 모두 종료하시겠습니까?",
                "주의! 실행중인 NX도 종료됩니다.\n작업을 저장했는지 확인하세요.",
                Application.Current.MainWindow);

            if (!confirmed)
                return;

            var (killed, failed, failedList) = _processService.KillAllNxProcesses();

            if (failed > 0)
            {
                var failedNames = string.Join(", ", failedList);
                SetStatusMessage($"{killed}개 프로세스 종료, {failed}개 실패: {failedNames}", false);
            }
            else if (killed > 0)
            {
                SetStatusMessage($"{killed}개 프로세스가 종료되었습니다.", true);
            }
            else
            {
                SetStatusMessage("종료할 프로세스가 없습니다.", true);
            }

            // 프로세스 상태 새로고침
            RefreshProcessStatus();
        }

        private void ExecuteRefreshProcessStatus(object? parameter)
        {
            RefreshProcessStatus();
            RefreshFirewallStatus();
            SetStatusMessage("상태가 새로고침되었습니다.", true);
        }

        private void SetStatusMessage(string message, bool isSuccess)
        {
            StatusMessage = message;
            StatusMessageColor = isSuccess
                ? new SolidColorBrush(Color.FromRgb(76, 175, 80))   // Green
                : new SolidColorBrush(Color.FromRgb(244, 67, 54));  // Red
        }

        public void OnWindowClosing()
        {
            // 프로그램 종료 시 방화벽 규칙 정리 (옵션)
            // _nxLauncherService.CleanupOnExit(IsNetworkBlocked);
        }
    }
}
