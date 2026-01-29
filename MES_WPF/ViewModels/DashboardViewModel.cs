// 引入CommunityToolkit.Mvvm的核心特性（ObservableObject、SetProperty等）
using CommunityToolkit.Mvvm.ComponentModel;
// LiveCharts图表库核心类型（ChartValues用于存储图表数据）
using LiveCharts;
// LiveCharts WPF端控件（SeriesCollection、PieSeries等）
using LiveCharts.Wpf;
// 引入项目实体模型（TaskItem、NotificationItem）
using MES_WPF.Models;
// 基础系统类（DateTime、Random等）
using System;
// 可观察集合（数据变更自动通知UI刷新）
using System.Collections.ObjectModel;
// WPF媒体类（颜色相关，此处未直接使用但预留）
using System.Windows.Media;

// 命名空间：MES_WPF的视图模型层（MVVM的核心逻辑层）
namespace MES_WPF.ViewModels
{
    /// <summary>
    /// 仪表盘视图模型（DashboardView的配套VM）
    /// 核心职责：
    /// 1. 管理仪表盘所有展示数据（生产趋势、产品占比、任务、通知）
    /// 2. 提供数据变更的通知能力（继承ObservableObject）
    /// 3. 生成模拟数据供UI展示
    /// </summary>
    public partial class DashboardViewModel : ObservableObject
    {
        // ==================== 生产趋势数据（折线图） ====================
        // 私有字段：存储生产趋势数值（LiveCharts专用类型）
        private ChartValues<double> _productionTrendData;
        /// <summary>
        /// 生产趋势数据（绑定到折线图的Series属性）
        /// 特性：SetProperty自动触发INotifyPropertyChanged，UI实时刷新
        /// </summary>
        public ChartValues<double> ProductionTrendData
        {
            get => _productionTrendData; // 读取私有字段值
            set => SetProperty(ref _productionTrendData, value); // 设置值并通知UI
        }

        // 私有字段：存储生产趋势X轴日期标签
        private ObservableCollection<string> _productionTrendLabels;
        /// <summary>
        /// 生产趋势标签（绑定到折线图的AxisX.Labels属性）
        /// 类型：ObservableCollection → 增删改查自动刷新UI
        /// </summary>
        public ObservableCollection<string> ProductionTrendLabels
        {
            get => _productionTrendLabels;
            set => SetProperty(ref _productionTrendLabels, value);
        }

        // ==================== 产品类型数据（饼图） ====================
        // 私有字段：存储产品占比饼图数据
        private SeriesCollection _productTypeData;
        /// <summary>
        /// 产品类型数据（绑定到饼图的SeriesCollection属性）
        /// SeriesCollection：LiveCharts饼图的核心数据结构，包含多个PieSeries
        /// </summary>
        public SeriesCollection ProductTypeData
        {
            get => _productTypeData;
            set => SetProperty(ref _productTypeData, value);
        }

        // ==================== 任务列表数据 ====================
        // 私有字段：存储待办任务列表
        private ObservableCollection<TaskItem> _tasks;
        /// <summary>
        /// 任务列表（绑定到UI的ListBox/DataGrid）
        /// 类型：ObservableCollection<TaskItem> → 任务增删时UI自动更新
        /// </summary>
        public ObservableCollection<TaskItem> Tasks
        {
            get => _tasks;
            set => SetProperty(ref _tasks, value);
        }

        // ==================== 通知列表数据 ====================
        // 私有字段：存储系统通知列表
        private ObservableCollection<NotificationItem> _notifications;
        /// <summary>
        /// 通知列表（绑定到UI的ListBox/DataGrid）
        /// 类型：ObservableCollection<NotificationItem> → 通知增删时UI自动更新
        /// </summary>
        public ObservableCollection<NotificationItem> Notifications
        {
            get => _notifications;
            set => SetProperty(ref _notifications, value);
        }

        /// <summary>
        /// 构造函数（VM初始化入口）
        /// 执行逻辑：
        /// 1. 初始化所有集合属性（避免空引用）
        /// 2. 调用模拟数据生成方法
        /// </summary>
        public DashboardViewModel()
        {
            // 初始化集合：提前实例化避免UI绑定时空指针
            ProductionTrendData = new ChartValues<double>(); // 折线图数值集合
            ProductionTrendLabels = new ObservableCollection<string>(); // 折线图标签集合
            ProductTypeData = new SeriesCollection(); // 饼图系列集合
            Tasks = new ObservableCollection<TaskItem>(); // 任务列表
            Notifications = new ObservableCollection<NotificationItem>(); // 通知列表

            // 生成各类模拟数据（按业务逻辑拆分方法，便于维护）
            GenerateProductionTrendData(); // 生产趋势（折线图）
            GenerateProductTypeData(); // 产品占比（饼图）
            GenerateTaskData(); // 待办任务
            GenerateNotificationData(); // 系统通知
        }

        /// <summary>
        /// 生成生产趋势模拟数据（近7天产量）
        /// 逻辑：
        /// 1. 生成近7天的日期标签（MM-dd格式）
        /// 2. 生成随机产量数据（150-250之间）
        /// </summary>
        private void GenerateProductionTrendData()
        {
            // 获取当前时间（用于计算近7天日期）
            DateTime now = DateTime.Now;
            // 循环生成近7天的日期标签（从6天前到今天）
            for (int i = 6; i >= 0; i--)
            {
                // 计算第i天前的日期
                DateTime date = now.AddDays(-i);
                // 添加格式化后的日期标签（如：05-20）
                ProductionTrendLabels.Add(date.ToString("MM-dd"));
            }

            // 初始化随机数生成器（用于生成模拟产量）
            Random random = new Random();
            // 为7天分别生成随机产量（150-250之间）
            for (int i = 0; i < 7; i++)
            {
                // Next(150,250)：生成150（包含）到250（不包含）的随机整数
                ProductionTrendData.Add(random.Next(150, 250));
            }
        }

        /// <summary>
        /// 生成产品类型饼图模拟数据（各产品占比）
        /// 逻辑：
        /// 1. 为每个产品类型创建PieSeries
        /// 2. 设置占比、数据标签、标签格式
        /// </summary>
        private void GenerateProductTypeData()
        {
            // 添加A型产品：占比35%，红色系（默认，可自定义Fill）
            ProductTypeData.Add(new PieSeries
            {
                Title = "A型产品", // 产品名称（图例显示）
                Values = new ChartValues<double> { 35 }, // 占比数值
                DataLabels = true, // 显示数据标签（占比数值）
                LabelPoint = point => $"A型: {point.Y}%" // 自定义标签格式（如：A型: 35%）
            });

            // 添加B型产品：占比25%
            ProductTypeData.Add(new PieSeries
            {
                Title = "B型产品",
                Values = new ChartValues<double> { 25 },
                DataLabels = true,
                LabelPoint = point => $"B型: {point.Y}%"
            });

            // 添加C型产品：占比20%
            ProductTypeData.Add(new PieSeries
            {
                Title = "C型产品",
                Values = new ChartValues<double> { 20 },
                DataLabels = true,
                LabelPoint = point => $"C型: {point.Y}%"
            });

            // 添加D型产品：占比15%
            ProductTypeData.Add(new PieSeries
            {
                Title = "D型产品",
                Values = new ChartValues<double> { 15 },
                DataLabels = true,
                LabelPoint = point => $"D型: {point.Y}%"
            });

            // 添加其他产品：占比5%
            ProductTypeData.Add(new PieSeries
            {
                Title = "其他",
                Values = new ChartValues<double> { 5 },
                DataLabels = true,
                LabelPoint = point => $"其他: {point.Y}%"
            });
        }

        /// <summary>
        /// 生成任务列表模拟数据（待办生产/质检/设备/采购任务）
        /// 逻辑：
        /// 1. 构建TaskItem实例（包含名称、日期、优先级、颜色）
        /// 2. 添加到Tasks集合（UI自动刷新）
        /// </summary>
        private void GenerateTaskData()
        {
            // 任务1：A型产品生产计划审批（高优先级，红色）
            Tasks.Add(new TaskItem
            {
                TaskName = "A型产品生产计划审批", // 任务名称
                PlanDate = DateTime.Now.ToString("yyyy-MM-dd"), // 计划日期（今天）
                Priority = "高", // 优先级
                PriorityColor = "#F44336" // 优先级颜色（MaterialDesign红色）
            });

            // 任务2：B型产品质检报告确认（中优先级，橙色）
            Tasks.Add(new TaskItem
            {
                TaskName = "B型产品质检报告确认",
                PlanDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"), // 计划日期（明天）
                Priority = "中",
                PriorityColor = "#FF9800" // MaterialDesign橙色
            });

            // 任务3：设备维护计划制定（低优先级，绿色）
            Tasks.Add(new TaskItem
            {
                TaskName = "设备维护计划制定",
                PlanDate = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd"), // 计划日期（后天）
                Priority = "低",
                PriorityColor = "#4CAF50" // MaterialDesign绿色
            });

            // 任务4：原材料采购申请审批（高优先级，红色）
            Tasks.Add(new TaskItem
            {
                TaskName = "原材料采购申请审批",
                PlanDate = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd"), // 计划日期（明天）
                Priority = "高",
                PriorityColor = "#F44336"
            });
        }

        /// <summary>
        /// 生成通知列表模拟数据（系统更新、设备提醒、生产完成、质检异常）
        /// 逻辑：
        /// 1. 构建NotificationItem实例（标题、时间、图标、背景色）
        /// 2. 添加到Notifications集合（UI自动刷新）
        /// </summary>
        private void GenerateNotificationData()
        {
            // 通知1：系统更新通知（1小时前，蓝色图标）
            Notifications.Add(new NotificationItem
            {
                Title = "系统更新通知", // 通知标题
                Time = DateTime.Now.AddHours(-1).ToString("yyyy-MM-dd HH:mm"), // 通知时间（1小时前）
                IconKind = "Bell", // 图标类型（MaterialDesign PackIcon的Kind）
                IconBackground = "#2196F3" // 图标背景色（MaterialDesign蓝色）
            });

            // 通知2：设备维护提醒（3小时前，橙色图标）
            Notifications.Add(new NotificationItem
            {
                Title = "设备维护提醒",
                Time = DateTime.Now.AddHours(-3).ToString("yyyy-MM-dd HH:mm"), // 3小时前
                IconKind = "Tools", // 工具图标
                IconBackground = "#FF9800" // 橙色
            });

            // 通知3：生产计划已完成（5小时前，绿色图标）
            Notifications.Add(new NotificationItem
            {
                Title = "生产计划已完成",
                Time = DateTime.Now.AddHours(-5).ToString("yyyy-MM-dd HH:mm"), // 5小时前
                IconKind = "CheckCircle", // 完成图标
                IconBackground = "#4CAF50" // 绿色
            });

            // 通知4：质检异常警报（1天前，红色图标）
            Notifications.Add(new NotificationItem
            {
                Title = "质检异常警报",
                Time = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd HH:mm"), // 1天前
                IconKind = "Alert", // 警告图标
                IconBackground = "#F44336" // 红色
            });
        }
    }
}