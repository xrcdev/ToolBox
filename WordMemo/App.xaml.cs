using System;
using System.Threading;
using System.Windows;

namespace WordMemo
{
    public partial class App : Application
    {
        private Mutex _singleInstanceMutex;

        public App()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            // 全局热键未使用，但多开容易造成数据文件并发写入，禁止多实例
            _singleInstanceMutex = new Mutex(true, @"Local\WordMemo.SingleInstance", out bool createdNew);
            if (!createdNew)
            {
                MessageBox.Show("WordMemo 已在运行中。", "WordMemo", MessageBoxButton.OK, MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);
        }

        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show("发生未处理的错误：" + e.Exception.Message, "WordMemo",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_singleInstanceMutex != null)
            {
                try
                {
                    _singleInstanceMutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                }
                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
            }

            base.OnExit(e);
        }
    }
}
