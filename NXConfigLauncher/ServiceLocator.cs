using Microsoft.Extensions.DependencyInjection;
using NXConfigLauncher.Services;
using NXConfigLauncher.ViewModels;

namespace NXConfigLauncher
{
    /// <summary>
    /// 전역 서비스 로케이터 - DI 컨테이너를 통한 서비스 해결
    /// </summary>
    public static class ServiceLocator
    {
        private static IServiceProvider? _serviceProvider;

        public static IServiceProvider ServiceProvider =>
            _serviceProvider ?? throw new InvalidOperationException("ServiceProvider가 초기화되지 않았습니다.");

        /// <summary>
        /// DI 컨테이너 초기화 및 서비스 등록
        /// </summary>
        public static void Initialize()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// 서비스 등록 설정
        /// </summary>
        private static void ConfigureServices(IServiceCollection services)
        {
            // 핵심 서비스 (Singleton)
            services.AddSingleton<IRegistryPathService, RegistryPathService>();
            services.AddSingleton<IConfigService, ConfigService>();

            // 비즈니스 서비스 (Singleton - 상태를 공유해야 함)
            services.AddSingleton<IEnvironmentService, EnvironmentService>();
            services.AddSingleton<IProcessService, ProcessService>();
            services.AddSingleton<IHostsService, HostsService>();

            // 의존성이 있는 서비스
            services.AddSingleton<IFirewallService>(sp =>
                new FirewallService(sp.GetRequiredService<IRegistryPathService>()));

            services.AddSingleton<INxDetectionService>(sp =>
                new NxDetectionService(sp.GetRequiredService<IRegistryPathService>()));

            services.AddSingleton<IProcessMonitorService>(sp =>
                new ProcessMonitorService(sp.GetRequiredService<IFirewallService>()));

            services.AddSingleton<INxLauncherService>(sp =>
                new NxLauncherService(
                    sp.GetRequiredService<IEnvironmentService>(),
                    sp.GetRequiredService<IFirewallService>(),
                    sp.GetRequiredService<IHostsService>(),
                    sp.GetRequiredService<IProcessMonitorService>(),
                    sp.GetRequiredService<IRegistryPathService>()));

            // ViewModel (Transient - 필요할 때마다 새로 생성)
            services.AddTransient<MainViewModel>();
        }

        /// <summary>
        /// 서비스 해결
        /// </summary>
        public static T GetService<T>() where T : notnull
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        /// <summary>
        /// 서비스 해결 (null 허용)
        /// </summary>
        public static T? GetServiceOrDefault<T>() where T : class
        {
            return ServiceProvider.GetService<T>();
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public static void Dispose()
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }
            _serviceProvider = null;
        }
    }
}
