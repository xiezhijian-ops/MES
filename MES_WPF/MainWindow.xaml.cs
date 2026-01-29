// 引入社区MVVM工具包（提供INotifyPropertyChanged、ICommand等基础能力）
using CommunityToolkit.Mvvm.ComponentModel;
// LiveCharts图表库核心（用于生产趋势/产品类型图表展示）
using LiveCharts;
using LiveCharts.Wpf;
// MaterialDesign控件库（提供现代化UI组件：PackIcon、Card等）
using MaterialDesignThemes.Wpf;
// 项目内部工具类（未展示，通常包含转换器、扩展方法等）
using MES_WPF.Helpers;
// 项目实体模型（菜单、任务、通知等数据结构）
using MES_WPF.Models;
// 项目服务层（导航服务、业务服务等）
using MES_WPF.Services;
// 项目视图模型（MVVM的核心，处理业务逻辑和数据状态）
using MES_WPF.ViewModels;
// 项目视图（UI页面）
using MES_WPF.Views;
// 系统管理模块视图（用户管理、角色管理等页面）
using MES_WPF.Views.SystemManagement;
// 基础系统类
using System;
// 泛型集合（存储模拟数据）
using System.Collections.Generic;
// 可观察集合（数据变更自动通知UI刷新）
using System.Collections.ObjectModel;
// 组件模型（INotifyPropertyChanged等接口）
using System.ComponentModel;
// LINQ查询（集合操作）
using System.Linq;
// WPF核心UI类
using System.Windows;
// WPF控件（ContentControl、TreeView等）
using System.Windows.Controls;
// WPF控件基类（ToggleButton等）
using System.Windows.Controls.Primitives;
// WPF输入事件（鼠标、键盘）
using System.Windows.Input;
// WPF媒体（颜色、画刷等）
using System.Windows.Media;

// 命名空间：MES_WPF主项目
namespace MES_WPF
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// 核心职责：
    /// 1. 初始化依赖注入服务（导航、菜单VM）
    /// 2. 绑定MVVM数据上下文
    /// 3. 处理UI交互（菜单折叠、选中、右键）
    /// 4. 初始化仪表盘模拟数据
    /// 5. 关联导航服务与内容容器
    /// </summary>
    public partial class MainWindow : Window
    {
        // 导航服务：抽象页面切换逻辑，解耦View与导航规则
        private readonly INavigationService _navigationService;
        // 菜单视图模型：管理菜单数据、选中状态、内容区展示
        private readonly MenuViewModel _menuViewModel;

        // 左侧菜单展开状态标记：true=展开（220px），false=折叠（60px）
        private bool _isMenuExpanded = true;

        /// <summary>
        /// 构造函数：依赖注入初始化 + UI绑定 + 数据初始化
        /// </summary>
        /// <param name="navigationService">导航服务（DI注入）</param>
        public MainWindow(INavigationService navigationService)
        {
            // 空值校验：导航服务不能为空，否则抛出异常（保证程序健壮性）
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
            // 从DI容器获取菜单ViewModel（而非new，符合依赖倒置原则）
            _menuViewModel = App.GetService<MenuViewModel>();

            // 初始化WPF UI组件（必须调用，绑定XAML控件）
            InitializeComponent();
            // 设置数据上下文：UI ↔ ViewModel双向绑定的核心
            DataContext = _menuViewModel;

            // 类型转换：判断导航服务是否为具体实现类（NavigationService）
            if (_navigationService is NavigationService navService)
            {
                // Loaded事件：UI加载完成后执行（避免控件未初始化导致的空引用）
                Loaded += (s, e) =>
                {
                    // 查找ContentControl控件（内容展示容器，用于加载子页面）
                    if (FindName("ContentControl") is ContentControl contentControl)
                    {
                        // 关联导航服务与内容容器：告诉导航服务“往哪个控件加载页面”
                        navService.SetContentControl(contentControl);

                        // 初始化默认页面：仪表盘（Dashboard）
                        // 从DI容器获取DashboardView（而非new，保证依赖注入生效）
                        var dashboardView = App.GetService<DashboardView>();
                        // 为DashboardView绑定ViewModel（DI获取，保证数据逻辑解耦）
                        dashboardView.DataContext = App.GetService<DashboardViewModel>();
                        // 将DashboardView赋值给MenuViewModel的MainContent（UI自动刷新）
                        _menuViewModel.MainContent = dashboardView;
                    }
                };
            }

            // 初始化仪表盘模拟数据（生产趋势、产品类型、任务、通知）
            InitializeData();
        }

        /// <summary>
        /// 初始化仪表盘模拟数据
        /// 职责：
        /// 1. 构造四类模拟数据（趋势、产品、任务、通知）
        /// 2. 将数据传递给DashboardViewModel（MVVM原则：数据在VM中）
        /// </summary>
        private void InitializeData()
        {
            // 1. 初始化生产趋势数据（折线图）：7天的产量数据
            var productionTrendData = new ChartValues<double> { 110, 120, 105, 130, 115, 200, 205 };
            // 生产趋势X轴标签：日期
            var productionTrendLabels = new List<string> { "5-14", "5-15", "5-16", "5-17", "5-18", "5-19", "5-20" };

            // 2. 初始化产品类型数据（饼图）：各产品占比
            var productTypeData = new SeriesCollection
            {
                // A型产品：占比25%，深蓝色，显示数据标签
                new PieSeries
                {
                    Title = "A型产品",
                    Values = new ChartValues<double> { 25 },
                    DataLabels = true, // 显示占比数值
                    Fill = new SolidColorBrush(Color.FromRgb(63, 81, 181)) // 画刷颜色
                },
                // B型产品：占比20%，浅绿色
                new PieSeries
                {
                    Title = "B型产品",
                    Values = new ChartValues<double> { 20 },
                    DataLabels = true,
                    Fill = new SolidColorBrush(Color.FromRgb(156, 204, 101))
                },
                // C型产品：占比30%，黄色
                new PieSeries
                {
                    Title = "C型产品",
                    Values = new ChartValues<double> { 30 },
                    DataLabels = true,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 205, 86))
                },
                // D型产品：占比15%，红色
                new PieSeries
                {
                    Title = "D型产品",
                    Values = new ChartValues<double> { 15 },
                    DataLabels = true,
                    Fill = new SolidColorBrush(Color.FromRgb(239, 83, 80))
                },
                // 其他：占比10%，浅蓝色
                new PieSeries
                {
                    Title = "其他",
                    Values = new ChartValues<double> { 10 },
                    DataLabels = true,
                    Fill = new SolidColorBrush(Color.FromRgb(77, 208, 225))
                }
            };

            // 3. 初始化任务列表：待办生产/设备/质检任务
            var tasks = new List<TaskItem>();
            // 生产任务：高优先级（红色）
            tasks.Add(new TaskItem
            {
                TaskName = "生产计划P10023的生产任务",
                PlanDate = "2024-05-20",
                Priority = "高",
                PriorityColor = "#F44336" // 红色（MaterialDesign警告色）
            });
            // 设备维护：中优先级（橙色）
            tasks.Add(new TaskItem
            {
                TaskName = "设备A102维护保养",
                PlanDate = "2024-05-20",
                Priority = "中",
                PriorityColor = "#FF9800" // 橙色
            });
            // 原料检验：高优先级（红色）
            tasks.Add(new TaskItem
            {
                TaskName = "原料RH403入库检验",
                PlanDate = "2024-05-20",
                Priority = "高",
                PriorityColor = "#F44336"
            });

            // 4. 初始化通知列表：系统告警/完成/分配通知
            var notifications = new List<NotificationItem>();
            // 设备异常通知：红色警告图标
            notifications.Add(new NotificationItem
            {
                IconKind = "Alert", // MaterialDesign图标类型
                IconBackground = "#F44336", // 图标背景色
                Title = "设备D15检测到异常，请及时处理",
                Time = "10分钟前"
            });
            // 生产完成通知：绿色成功图标
            notifications.Add(new NotificationItem
            {
                IconKind = "Check",
                IconBackground = "#4CAF50", // 绿色（成功色）
                Title = "生产计划P10023已完成",
                Time = "20分钟前"
            });
            // 任务分配通知：蓝色信息图标
            notifications.Add(new NotificationItem
            {
                IconKind = "Information",
                IconBackground = "#2196F3", // 蓝色（信息色）
                Title = "新的生产任务已分配",
                Time = "1小时前"
            });
            // 质量异常通知：红色警告图标
            notifications.Add(new NotificationItem
            {
                IconKind = "Alert",
                IconBackground = "#F44336",
                Title = "质量检测发现不合格品",
                Time = "2小时前"
            });

            // 数据传递：将模拟数据赋值给DashboardViewModel
            // 类型校验：MainContent是否为DashboardView（避免类型错误）
            if (_menuViewModel.MainContent is DashboardView dashboardView)
            {
                // 类型校验：DashboardView的DataContext是否为DashboardViewModel
                if (dashboardView.DataContext is DashboardViewModel dashboardViewModel)
                {
                    // 生产趋势数据：ChartValues（LiveCharts专用类型）
                    dashboardViewModel.ProductionTrendData = new ChartValues<double>(productionTrendData);
                    // 生产趋势标签：ObservableCollection（数据变更自动通知UI）
                    dashboardViewModel.ProductionTrendLabels = new ObservableCollection<string>(productionTrendLabels);
                    // 产品类型数据：饼图系列集合
                    dashboardViewModel.ProductTypeData = productTypeData;
                    // 任务列表：ObservableCollection（增删改查自动刷新UI）
                    dashboardViewModel.Tasks = new ObservableCollection<TaskItem>(tasks);
                    // 通知列表：ObservableCollection
                    dashboardViewModel.Notifications = new ObservableCollection<NotificationItem>(notifications);
                }
            }
        }

        /// <summary>
        /// 菜单折叠/展开按钮点击事件
        /// 职责：切换左侧菜单宽度（220px展开 / 60px折叠）
        /// </summary>
        /// <param name="sender">触发控件（MenuToggleButton）</param>
        /// <param name="e">路由事件参数</param>
        private void MenuToggleButton_Click(object sender, RoutedEventArgs e)
        {
            // 切换展开状态（取反）
            _isMenuExpanded = !_isMenuExpanded;

            // 获取左侧菜单所在的Grid列定义（Grid.Column="0"）
            var columnDefinition = (ColumnDefinition)((Grid)Content).ColumnDefinitions[0];

            // 根据状态设置列宽度
            if (_isMenuExpanded)
            {
                // 展开：220px（完整菜单）
                columnDefinition.Width = new GridLength(220);
            }
            else
            {
                // 折叠：60px（仅显示图标）
                columnDefinition.Width = new GridLength(60);
            }
        }

        /// <summary>
        /// TreeView选中项变更事件（空实现）
        /// 注释：MVVM模式下，选中逻辑已迁移到MenuViewModel，此处仅占位
        /// </summary>
        /// <param name="sender">TreeView控件</param>
        /// <param name="e">选中项变更参数</param>
        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // 使用MVVM模式，不再需要在代码后台处理选择事件
            // 选择事件由MenuViewModel处理
        }

        /// <summary>
        /// TreeViewItem选中事件
        /// 职责：转发选中事件到MenuViewModel的SelectMenuItemCommand
        /// 核心：UI层仅转发事件，业务逻辑由VM处理（MVVM原则）
        /// </summary>
        /// <param name="sender">选中的TreeViewItem</param>
        /// <param name="e">路由事件参数</param>
        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            // 类型校验：sender是否为TreeViewItem，且数据上下文为MenuItemModel
            if (sender is TreeViewItem treeViewItem && treeViewItem.DataContext is MenuItemModel menuItem)
            {
                // 阻止事件冒泡：避免父控件重复处理该事件（防止多次触发导航）
                e.Handled = true;

                // 命令校验：SelectMenuItemCommand是否可执行
                if (_menuViewModel.SelectMenuItemCommand.CanExecute(menuItem))
                {
                    // 执行命令：传递选中的菜单项（由VM处理导航逻辑）
                    _menuViewModel.SelectMenuItemCommand.Execute(menuItem);
                }

                // 强制更新TreeView布局：确保UI状态（选中/展开）及时刷新
                if (FindName("MenuTreeView") is TreeView menuTreeView)
                {
                    menuTreeView.UpdateLayout();
                }
            }
        }

        /// <summary>
        /// TreeViewItem右键点击事件
        /// 职责：
        /// 1. 选中右键点击的菜单项
        /// 2. 有子菜单时切换展开状态
        /// </summary>
        /// <param name="sender">右键点击的TreeViewItem</param>
        /// <param name="e">鼠标按钮事件参数</param>
        private void TreeViewItem_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 类型校验：sender是否为TreeViewItem，且数据上下文为MenuItemModel
            if (sender is TreeViewItem treeViewItem && treeViewItem.DataContext is MenuItemModel menuItem)
            {
                // 阻止事件冒泡：避免重复处理
                e.Handled = true;

                // 选中当前项：右键点击时先选中（符合用户操作习惯）
                treeViewItem.IsSelected = true;

                // 条件校验：有子菜单 且 展开命令可执行
                if (menuItem.HasSubItems && menuItem.ExpandCommand.CanExecute(null))
                {
                    // 执行展开命令：切换子菜单显示/隐藏
                    menuItem.ExpandCommand.Execute(null);
                }
            }
        }
    }

    /// <summary>
    /// 任务项实体（POCO类）
    /// 用途：仪表盘待办任务列表的数据载体
    /// 特点：仅包含属性，无业务逻辑（DTO）
    /// </summary>
    public class TaskItem
    {
        // 任务名称
        public string TaskName { get; set; }
        // 计划日期
        public string PlanDate { get; set; }
        // 优先级（高/中/低）
        public string Priority { get; set; }
        // 优先级颜色（十六进制色值，直接绑定UI）
        public string PriorityColor { get; set; }
    }

    /// <summary>
    /// 通知项实体（POCO类）
    /// 用途：仪表盘通知列表的数据载体
    /// </summary>
    public class NotificationItem
    {
        // 图标类型（MaterialDesign PackIcon Kind）
        public string IconKind { get; set; }
        // 图标背景色（十六进制色值）
        public string IconBackground { get; set; }
        // 通知标题
        public string Title { get; set; }
        // 通知时间（相对时间：10分钟前、1小时前）
        public string Time { get; set; }
    }
}