using System.Windows;
using Unity;
using Unity.Lifetime;
using UnrealEditorBridge.Adapter;
using UnrealEditorBridge.Wpf.Services;
using UnrealEditorBridge.Wpf.ViewModels;
using UnrealEditorBridge.Wpf.Views;

namespace UnrealEditorBridge.Wpf
{

    /// <summary>
    /// 애플리케이션 진입점.
    /// UnityContainer를 직접 사용하여 DI를 구성한다.
    /// </summary>
    public partial class App : Application
    {
        #region Internal Fields

        private IUnityContainer? _container;

        #endregion

        #region Event Handlers

        /// <inheritdoc/>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _container = new UnityContainer();
            RegisterTypes(_container);

            var mainWindow = _container.Resolve<MainWindow>();
            mainWindow.DataContext = _container.Resolve<MainViewModel>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        /// <inheritdoc/>
        protected override void OnExit(ExitEventArgs e)
        {
            _container?.Dispose();
            base.OnExit(e);
        }

        #endregion

        #region Functions

        private static void RegisterTypes(IUnityContainer container)
        {
            // Adapter 등록
            container.RegisterFactory<IBridgeClient>(
                _ => BridgeClientFactory.Create(),
                new SingletonLifetimeManager());
            container.RegisterSingleton<EditorInstanceDiscovery>();

            // Services 등록
            container.RegisterSingleton<IBridgeService, BridgeService>();

            // ViewModels 등록
            container.RegisterSingleton<MainViewModel>();
            container.RegisterType<ConnectionViewModel>();
            container.RegisterType<AssetListViewModel>();
            container.RegisterType<AssetDetailViewModel>();
            container.RegisterType<EventLogViewModel>();

            // Views 등록
            container.RegisterType<MainWindow>();
        }

        #endregion
    }
}
