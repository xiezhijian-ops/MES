// 基础系统类：DateTime（系统时间）、Type（视图类型反射）
using System;
// 可观察集合：NavigationItems变更时自动通知UI刷新
using System.Collections.ObjectModel;
// WPF核心类：Window（登录窗口）、Application（应用程序上下文）
using System.Windows;
// WPF命令接口：ICommand（绑定注销等交互）
using System.Windows.Input;
// MVVM命令：RelayCommand（简化ICommand实现）
using CommunityToolkit.Mvvm.Input;
// 核心模型：User（用户实体）
using MES_WPF.Core.Models;
// 服务接口：导航/弹窗/认证（依赖注入）
using MES_WPF.Services;
// 系统管理VM：LoginViewModel（登录视图模型）
using MES_WPF.ViewModels.SystemManagement;
// 视图基类：LoginView（登录视图）
using MES_WPF.Views;
// 系统管理视图：UserManagementView（用户管理视图）
using MES_WPF.Views.SystemManagement;

// 命名空间：MES_WPF的核心视图模型 → 主窗口的VM
namespace MES_WPF.ViewModels
{
    /// <summary>
    /// 主窗口视图模型（MainWindow.xaml的配套VM）
    /// 核心职责：
    /// 1. 全局状态管理：当前用户、系统时间、状态栏消息、当前显示视图
    /// 2. 导航管理：左侧菜单导航、视图切换
    /// 3. 用户交互：注销登录、确认弹窗
    /// 4. 定时任务：实时更新系统时间
    /// 设计原则：
    /// - 依赖注入：通过构造函数接收服务接口，解耦具体实现
    /// - 数据驱动：所有UI状态通过属性绑定，无直接操作UI
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        #region 私有字段（Backing Field）
        /// <summary>
        /// 导航服务（只读）：封装视图切换逻辑，VM不直接依赖View
        /// </summary>
        private readonly INavigationService _navigationService;
        /// <summary>
        /// 弹窗服务（只读）：封装弹窗逻辑（确认框、提示框），解耦WPF弹窗API
        /// </summary>
        private readonly IDialogService _dialogService;
        /// <summary>
        /// 认证服务（只读）：封装用户登录/注销逻辑，管理当前用户状态
        /// </summary>
        private readonly IAuthenticationService _authService;

        /// <summary>
        /// 当前显示的视图实例（如UserManagementView）
        /// </summary>
        private object _currentView;
        /// <summary>
        /// 当前选中的导航菜单项
        /// </summary>
        private NavigationItem _selectedNavigationItem;
        /// <summary>
        /// 当前登录用户名（显示在顶部）
        /// </summary>
        private string _currentUserName = "管理员";
        /// <summary>
        /// 状态栏消息（如“就绪”“导航中”）
        /// </summary>
        private string _statusMessage = "就绪";
        /// <summary>
        /// 当前系统时间（实时更新）
        /// </summary>
        private DateTime _currentDateTime = DateTime.Now;
        /// <summary>
        /// 定时器：每秒更新系统时间
        /// </summary>
        private System.Timers.Timer _timer;
        /// <summary>
        /// 当前登录用户实体（包含用户ID、姓名、权限等）
        /// </summary>
        private User _currentUser;
        #endregion

        #region 公共属性（供UI绑定）
        /// <summary>
        /// 当前显示的视图（绑定到MainWindow的ContentControl.Content）
        /// 类型：object → 兼容所有View类型（UserControl）
        /// SetProperty：赋值时自动通知UI刷新
        /// </summary>
        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// 当前选中的导航菜单项（绑定到左侧菜单的SelectedItem）
        /// 核心逻辑：选中项变更时，自动触发视图导航
        /// </summary>
        public NavigationItem SelectedNavigationItem
        {
            get => _selectedNavigationItem;
            set
            {
                // SetProperty返回true表示值已变更，且选中项不为空 → 触发导航
                if (SetProperty(ref _selectedNavigationItem, value) && value != null)
                {
                    NavigateToView(value.ViewType);
                }
            }
        }

        /// <summary>
        /// 当前用户名（显示在顶部导航栏）
        /// </summary>
        public string CurrentUserName
        {
            get => _currentUserName;
            set => SetProperty(ref _currentUserName, value);
        }

        /// <summary>
        /// 状态栏消息（显示在窗口底部）
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 当前系统时间（实时更新，显示在顶部）
        /// </summary>
        public DateTime CurrentDateTime
        {
            get => _currentDateTime;
            set => SetProperty(ref _currentDateTime, value);
        }

        /// <summary>
        /// 当前登录用户实体（核心状态）
        /// 扩展逻辑：用户变更时，自动更新显示的用户名
        /// </summary>
        public User CurrentUser
        {
            get => _currentUser;
            set
            {
                // SetProperty返回true表示用户已变更
                if (SetProperty(ref _currentUser, value))
                {
                    // 更新显示名：有用户则显示真实姓名，无则显示“未登录”
                    CurrentUserName = value?.RealName ?? "未登录";
                }
            }
        }

        /// <summary>
        /// 导航菜单项集合（绑定到左侧菜单的ItemsSource）
        /// 类型：ObservableCollection → 增删菜单项时UI自动刷新
        /// 初始化：直接new，避免空引用
        /// </summary>
        public ObservableCollection<NavigationItem> NavigationItems { get; } = new ObservableCollection<NavigationItem>();

        /// <summary>
        /// 注销命令（绑定到顶部“注销”按钮的Command）
        /// 类型：ICommand → 符合MVVM，无直接UI操作
        /// </summary>
        public ICommand LogoutCommand { get; }
        #endregion

        #region 构造函数（初始化核心逻辑）
        /// <summary>
        /// 构造函数（依赖注入入口）
        /// 接收三大核心服务：导航、弹窗、认证
        /// </summary>
        /// <param name="navigationService">导航服务</param>
        /// <param name="dialogService">弹窗服务</param>
        /// <param name="authService">认证服务</param>
        public MainViewModel(
            INavigationService navigationService,
            IDialogService dialogService,
            IAuthenticationService authService)
        {
            // 赋值服务字段（DI容器自动注入，无需手动new）
            _navigationService = navigationService;
            _dialogService = dialogService;
            _authService = authService;

            // 初始化注销命令：RelayCommand绑定LogoutAsync方法（异步注销）
            LogoutCommand = new RelayCommand(LogoutAsync);

            // 初始化左侧导航菜单（添加所有菜单项）
            InitializeNavigation();

            // 初始化定时器（每秒更新系统时间）
            InitializeTimer();

            // 获取当前登录用户（从认证服务读取，无需手动传参）
            CurrentUser = _authService.CurrentUser;
        }
        #endregion

        #region 私有方法（核心业务逻辑）
        /// <summary>
        /// 初始化导航菜单（构建左侧所有菜单项）
        /// 业务逻辑：添加MES系统核心模块的导航项，包含标题、图标、目标视图
        /// </summary>
        private void InitializeNavigation()
        {
            // 添加导航项：标题、图标（MaterialDesign）、目标视图类型
            // 系统管理 → 绑定UserManagementView（用户管理视图）
            NavigationItems.Add(new NavigationItem("系统管理", "Cog", typeof(UserManagementView)));
            // 生产计划 → 暂未绑定视图（null，后续扩展）
            NavigationItems.Add(new NavigationItem("生产计划", "Calendar", null));
            // 生产执行 → 暂未绑定视图
            NavigationItems.Add(new NavigationItem("生产执行", "Play", null));
            // 物料管理 → 暂未绑定视图
            NavigationItems.Add(new NavigationItem("物料管理", "Package", null));
            // 质量管理 → 暂未绑定视图
            NavigationItems.Add(new NavigationItem("质量管理", "CheckCircle", null));
            // 设备管理 → 暂未绑定视图
            NavigationItems.Add(new NavigationItem("设备管理", "Cog", null));
            // 绩效分析 → 暂未绑定视图
            NavigationItems.Add(new NavigationItem("绩效分析", "ChartBar", null));

            // 默认选择第一个菜单项（系统管理）：打开主窗口时默认显示用户管理视图
            if (NavigationItems.Count > 0)
            {
                SelectedNavigationItem = NavigationItems[0];
            }
        }

        /// <summary>
        /// 初始化定时器（实时更新系统时间）
        /// 逻辑：
        /// 1. 创建1秒定时器（Interval=1000ms）
        /// 2. 绑定Elapsed事件：每秒更新CurrentDateTime
        /// 3. 启动定时器
        /// </summary>
        private void InitializeTimer()
        {
            // 创建定时器：间隔1000毫秒（1秒）
            _timer = new System.Timers.Timer(1000);

            // 绑定Elapsed事件：定时器触发时更新系统时间
            // 匿名方法：sender=定时器实例，e=事件参数（此处未使用）
            _timer.Elapsed += (sender, e) =>
            {
                // 更新CurrentDateTime：SetProperty自动通知UI刷新
                CurrentDateTime = DateTime.Now;
            };

            // 启动定时器：开始实时更新时间
            _timer.Start();
        }

        /// <summary>
        /// 导航到指定视图（核心导航逻辑）
        /// </summary>
        /// <param name="viewType">目标视图类型（如typeof(UserManagementView)）</param>
        /// <remarks>viewType=null时不导航（如未实现的菜单）</remarks>
        private void NavigateToView(Type viewType)
        {
            // 空值校验：视图类型为空则跳过（避免空指针）
            if (viewType != null)
            {
                // 调用导航服务切换视图：导航服务内部创建View实例并赋值给CurrentView
                _navigationService.NavigateTo(viewType);
            }
        }

        /// <summary>
        /// 注销登录（异步方法：弹窗确认→注销→切换到登录窗口）
        /// async/await：避免弹窗阻塞UI线程
        /// </summary>
        private async void LogoutAsync()
        {
            // 显示确认弹窗：标题“注销”，内容“确定要注销当前用户吗？”
            // ShowConfirmAsync：异步弹窗，返回用户选择（true=确认，false=取消）
            var result = await _dialogService.ShowConfirmAsync("注销", "确定要注销当前用户吗？");

            // 用户确认注销
            if (result)
            {
                // 执行注销操作：调用认证服务的LogoutAsync（异步，如清除Token、用户状态）
                await _authService.LogoutAsync();

                // 创建登录窗口（WPF Window）
                var loginWindow = new Window
                {
                    Title = "登录 - MES制造执行系统", // 窗口标题
                    Content = App.GetService<LoginView>(), // 窗口内容：登录视图（从DI容器获取）
                    Width = 450, // 窗口宽度
                    Height = 600, // 窗口高度
                    WindowStartupLocation = WindowStartupLocation.CenterScreen, // 居中显示
                    ResizeMode = ResizeMode.NoResize // 禁止调整大小（登录窗口固定尺寸）
                };

                // 绑定登录视图模型：从DI容器获取LoginViewModel，保证依赖注入生效
                loginWindow.DataContext = App.GetService<LoginViewModel>();

                // 关闭当前主窗口
                Application.Current.MainWindow.Close();

                // 设置新的主窗口为登录窗口
                Application.Current.MainWindow = loginWindow;

                // 显示登录窗口
                loginWindow.Show();
            }
        }
        #endregion
    }

    /// <summary>
    /// 导航项实体（左侧菜单的数据载体）
    /// 纯POCO类：仅包含属性，无业务逻辑，用于绑定菜单显示和导航目标
    /// </summary>
    public class NavigationItem
    {
        /// <summary>
        /// 菜单标题（如“系统管理”“生产计划”）
        /// </summary>
        public string Title { get; }
        /// <summary>
        /// 菜单图标（MaterialDesign PackIcon的Kind字符串，如“Cog”“Calendar”）
        /// </summary>
        public string Icon { get; }
        /// <summary>
        /// 目标视图类型（如typeof(UserManagementView)）
        /// null表示该菜单暂未实现视图
        /// </summary>
        public Type ViewType { get; }

        /// <summary>
        /// 构造函数（初始化所有属性）
        /// 所有属性为只读（get;），仅通过构造函数赋值，保证数据不可变
        /// </summary>
        /// <param name="title">菜单标题</param>
        /// <param name="icon">图标名称</param>
        /// <param name="viewType">目标视图类型</param>
        public NavigationItem(string title, string icon, Type viewType)
        {
            Title = title;
            Icon = icon;
            ViewType = viewType;
        }
    }
}