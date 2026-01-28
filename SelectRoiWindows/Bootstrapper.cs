using Caliburn.Micro;
using RoiEditor.ViewModels;
using SelectRoiWindows.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace SelectRoiWindows
{
    public class Bootstrapper : BootstrapperBase
    {
        private SimpleContainer _container;

        public Bootstrapper()
        {
            Initialize();
        }

        protected override void Configure()
        {
            _container = new SimpleContainer();

            // 1. 注册核心服务 (单例)
            _container.Singleton<IWindowManager, WindowManager>();
            _container.Singleton<IEventAggregator, EventAggregator>();

            // 2. 注册主窗口 Shell
            _container.PerRequest<ShellViewModel>();
        }


        protected override IEnumerable<Assembly> SelectAssemblies()
        {
            var assemblies = new List<Assembly>();

            // 1. 添加主程序 (EXE) 的程序集
            assemblies.Add(Assembly.GetEntryAssembly());

            // 2. 添加类库 (DLL) 的程序集
            // 只要引用类库中任意一个类 (比如 EditorViewModel) 即可获取其 Assembly
            assemblies.Add(typeof(RoiMainViewModel).Assembly);

            return assemblies;
        }

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            // 启动主窗口
            DisplayRootViewFor<ShellViewModel>();
        }

        protected override object GetInstance(Type service, string key)
        {
            return _container.GetInstance(service, key);
        }

        protected override IEnumerable<object> GetAllInstances(Type service)
        {
            return _container.GetAllInstances(service);
        }

        protected override void BuildUp(object instance)
        {
            _container.BuildUp(instance);
        }
    }
}
