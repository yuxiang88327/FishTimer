using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Forms;
using ContextMenu = System.Windows.Forms.ContextMenu;
using Application = System.Windows.Application;
using Label = System.Windows.Controls.Label;
using TextBox = System.Windows.Controls.TextBox;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using System.Windows.Media.Animation;

namespace WpfApp1
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        // Win32 API声明
        [DllImport("user32.dll")]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("dwmapi.dll", CharSet = CharSet.Auto)]
        static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int X,
        int Y,
        int Width,
        int Height,
        uint uFlags);

        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_SHOWWINDOW = 0x0040;

        // 新增Win32 API声明
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private System.Windows.Forms.NotifyIcon trayIcon;
        private ContextMenu trayMenu;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; public int Top; public int Right; public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS
        {
            public int cxLeftWidth;
            public int cxRightWidth;
            public int cyTopHeight;
            public int cyBottomHeight;
        }

        private const int TASKBAR_HEIGHT = 48; // Win11默认高度
        private DispatcherTimer positionTimer;
        private DispatcherTimer textUpdateTimer;
        private DispatcherTimer layerCheckTimer;

        private string _displayText;
        public string DisplayText
        {
            get => _displayText;
            set
            {
                if (_displayText != value)
                {
                    _displayText = value;
                    OnPropertyChanged(nameof(DisplayText));
                }
            }
        }

        public DateTime targetTime = DateTime.Today.AddHours(18); // 今天18:00

        public MainWindow()
        {
            InitializeComponent();
            InitializeTrayIcon();
            DataContext = this;
            InitializeWindow();
            StartPositionMonitoring();
            StartTextUpdateTimer();

            // 设置定时器每500毫秒检查一次窗口层级
            layerCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            layerCheckTimer.Tick += EnsureWindowOnTop;
            layerCheckTimer.Start();
        }


        private void EnsureWindowOnTop(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;

            // 获取当前活动窗口句柄
            IntPtr foregroundWindow = GetForegroundWindow();

            // 如果当前活动窗口是任务栏或菜单窗口，不设置最上层
            if (foregroundWindow == hwnd || !IsWindowVisible(foregroundWindow))
            {
                // 窗口已在最上层，无需处理
                return;
            }

            // 将窗口设置为最上层
            SetWindowPos(hwnd, HWND_TOPMOST, (int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        }

        private void InitializeWindow()
        {
            // 窗口样式设置
            WindowStyle = WindowStyle.None;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            AllowsTransparency = true;
            //Background = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)); // 半透明背景

            // 设置扩展窗口样式（关键修改）
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);


            // 点击穿透设置
            var margins = new MARGINS { cxLeftWidth = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
        }

        private void StartPositionMonitoring()
        {
            positionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            positionTimer.Tick += UpdatePosition;
            positionTimer.Start();
            UpdatePosition(null, null);
        }

        private void StartTextUpdateTimer()
        {
            textUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // 每秒更新一次
            };
            textUpdateTimer.Tick += UpdateText;
            textUpdateTimer.Start();
        }

        private void UpdatePosition(object sender, EventArgs e)
        {
            // 强制保持置顶状态
            if (!Topmost) Topmost = true;

            var taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero) return;

            GetWindowRect(taskbarHandle, out RECT taskbarRect);

            // 计算目标区域（假设任务栏在底部）
            int startButtonWidth = 10;   // 开始按钮区域宽度
            int widgetWidth = 60;        // 小组件按钮宽度

            this.Width = 200;
            this.Height = TASKBAR_HEIGHT;

            // 确保窗口左侧与任务栏的左侧对齐
            this.Left = widgetWidth;
            this.Top = taskbarRect.Top;


        }
        private void UpdateText(object sender, EventArgs e)
        {
            // 获取当前时间
            DateTime currentTime = DateTime.Now;

            // 如果当前时间已经过了18:00，则将目标时间设为明天的18:00
            if (currentTime > targetTime)
            {
                targetTime = targetTime.AddDays(1); // 设置为明天的18:00
            }

            // 计算剩余时间（倒计时）
            TimeSpan timeRemaining = targetTime - currentTime;

            // 更新显示文本为倒计时格式
            DisplayText = $"🐟: {timeRemaining.Hours:D2}:{timeRemaining.Minutes:D2}:{timeRemaining.Seconds:D2}";

            // 动态改变TextBlock的颜色
            if (timeRemaining.TotalMinutes < 30) // 如果倒计时不到半小时
            {
                // 启用闪烁效果，改变TextBlock的颜色
                StartFlashingColor(Colors.Red);
            }
            else if (timeRemaining.TotalMinutes < 60) // 如果倒计时不到1小时
            {
                // 设置文本颜色为黄色
                myTextBlock.Foreground = new SolidColorBrush(Colors.Yellow);
            }
            else
            {
                // 设置文本颜色为默认的白色
                myTextBlock.Foreground = new SolidColorBrush(Colors.White);
            }
        }

        private void StartFlashingColor(Color flashColor)
        {
            // 如果已经有动画在运行，先停止它
            if (myTextBlock.Foreground is SolidColorBrush solidColorBrush &&
                solidColorBrush.Color == flashColor)
            {
                return;
            }

            // 创建一个Storyboard，设置闪烁效果
            var storyboard = new Storyboard();
            var colorAnimation = new ColorAnimation
            {
                From = flashColor,   // 初始颜色
                To = Colors.Yellow, // 目标颜色，透明
                AutoReverse = true,   // 自动反转
                RepeatBehavior = RepeatBehavior.Forever, // 永远重复
                Duration = new Duration(TimeSpan.FromSeconds(0.5)) // 每次闪烁的时长
            };

            // 设置动画的目标为TextBlock的颜色
            Storyboard.SetTarget(colorAnimation, myTextBlock);
            Storyboard.SetTargetProperty(colorAnimation, new PropertyPath("(TextBlock.Foreground).(SolidColorBrush.Color)"));

            // 开始动画
            storyboard.Children.Add(colorAnimation);
            storyboard.Begin();
        }



        // 实现 INotifyPropertyChanged 接口
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        private void InitializeTrayIcon()
        {
            // 创建托盘图标菜单
            trayMenu = new ContextMenu();
            //trayMenu.MenuItems.Add("设置倒计时", OnSetCountdownTime); // 添加设置倒计时菜单项
            trayMenu.MenuItems.Add("退出", OnExit);

            // 创建托盘图标
            trayIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("icon.ico"), // 设置托盘图标，替换为实际图标路径
                ContextMenu = trayMenu,
                Visible = true // 显示托盘图标
            };

            // 设置托盘图标的双击事件，双击时恢复窗口
            trayIcon.DoubleClick += TrayIcon_DoubleClick;
        }

        private void OnSetCountdownTime(object sender, EventArgs e)
        {
            // 弹出一个输入框让用户设置倒计时结束时间
            var inputDialog = new InputDialog("请输入倒计时结束时间（格式：HH:mm）", "倒计时设置");
            if (inputDialog.ShowDialog() == true)
            {
                string inputTime = inputDialog.InputText;

                // 尝试解析用户输入的时间
                if (DateTime.TryParseExact(inputTime, "HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime countdownEndTime))
                {
                    // 设置新的倒计时结束时间
                    SetCountdownEndTime(countdownEndTime);
                }
                else
                {
                    MessageBox.Show("无效的时间格式，请使用HH:mm格式。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SetCountdownEndTime(DateTime newEndTime)
        {
            // 更新倒计时结束时间（这里假设您有一个字段或属性来存储它）
            targetTime = newEndTime;

            // 根据新时间更新倒计时
            UpdateText(null, null); // 调用更新文本的方法，刷新倒计时显示
        }




        // 双击托盘图标时恢复窗口
        private void TrayIcon_DoubleClick(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal; // 恢复窗口状态
            this.Activate(); // 激活窗口
        }

        // 点击退出菜单项时关闭程序
        private void OnExit(object sender, EventArgs e)
        {
            trayIcon.Dispose(); // 释放托盘图标资源
            Application.Current.Shutdown(); // 退出程序
        }

        // 重写窗口关闭事件，避免直接关闭窗口
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            // 隐藏窗口而不是关闭窗口
            this.Hide();
            e.Cancel = true;
        }
    }

    public class InputDialog : Window
    {
        public string InputText { get; private set; }

        private TextBox inputTextBox;

        public InputDialog(string prompt, string title)
        {
            this.Title = title;
            this.Width = 300;
            this.Height = 150;

            var stackPanel = new StackPanel();
            var label = new Label { Content = prompt };
            inputTextBox = new TextBox { Margin = new Thickness(10), Width = 200 };
            var okButton = new Button { Content = "OK", Width = 100, Margin = new Thickness(10) };

            okButton.Click += (sender, e) => { InputText = inputTextBox.Text; this.DialogResult = true; };
            stackPanel.Children.Add(label);
            stackPanel.Children.Add(inputTextBox);
            stackPanel.Children.Add(okButton);

            this.Content = stackPanel;
        }
    }
}
