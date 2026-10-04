using System.Threading;
using System.Windows;

namespace MyTranslator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private Mutex? _singleInstanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局快捷键按进程注册，第二个实例会注册失败并陷入
            // “没有快捷键、退出后原实例还在”的混乱状态，直接禁止多开
            _singleInstanceMutex = new Mutex(true, @"Local\MyTranslator.SingleInstance", out var createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show("MyTranslator 已在运行，请先从托盘退出已有实例。", "MyTranslator");
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex is not null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // 进程即将退出，所有权异常无需处理
                }
                _singleInstanceMutex.Dispose();
            }

            base.OnExit(e);
        }
    }
}
