using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace MES_WPF.Views.Dialogs
{
    /// <summary>
    /// 通用消息对话框控件（基于 MaterialDesignThemes 框架）
    /// 核心用途：系统中展示各类消息通知（成功、错误、警告、信息等）
    /// 设计优势：支持自定义标题、消息内容、图标样式，与 MaterialDesign 风格统一，适配 MVVM 模式
    /// 继承 UserControl：便于嵌入 DialogHost 容器，支持全系统复用和样式统一
    /// </summary>
    public partial class MessageDialog : UserControl
    {
        #region 依赖属性定义（支持WPF数据绑定与动态配置）
        /// <summary>
        /// 对话框标题依赖属性（默认值："消息"）
        /// 作用：根据消息类型自定义标题（如"成功"、"错误"、"警告"）
        /// 依赖属性特性：支持 XAML 绑定、样式设置、属性变更通知
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),                  // 对应 CLR 属性名
                typeof(string),                 // 属性类型
                typeof(MessageDialog),          // 所属控件类型
                new PropertyMetadata("消息")    // 默认值
            );

        /// <summary>
        /// 消息内容依赖属性（默认值："操作已完成。"）
        /// 作用：展示具体消息文本（如"BOM添加成功"、"加载数据失败，请重试"）
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(MessageDialog),
                new PropertyMetadata("操作已完成。")
            );

        /// <summary>
        /// 消息图标依赖属性（默认值：PackIconKind.Information 信息图标）
        /// 作用：通过图标直观区分消息类型（成功/错误/警告/信息）
        /// 类型说明：PackIconKind 是 MaterialDesignThemes 框架提供的图标枚举，包含数百种 Material 风格图标
        /// </summary>
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(PackIconKind),
                typeof(MessageDialog),
                new PropertyMetadata(PackIconKind.Information)
            );
        #endregion

        #region CLR 属性包装器（简化依赖属性访问）
        /// <summary>
        /// 对话框标题（CLR 属性）
        /// 说明：通过 GetValue/SetValue 间接操作依赖属性，保持绑定特性
        /// 外部代码可直接通过 dialog.Title 访问，无需关注依赖属性底层实现
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// 消息内容（CLR 属性）
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// 消息图标（CLR 属性）
        /// 常用值：
        /// - PackIconKind.Information：信息图标（默认）
        /// - PackIconKind.CheckCircle：成功图标
        /// - PackIconKind.Error：错误图标
        /// - PackIconKind.Warning：警告图标
        /// </summary>
        public PackIconKind Icon
        {
            get => (PackIconKind)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }
        #endregion

        #region 构造函数与控件初始化
        /// <summary>
        /// 构造函数：初始化控件 UI 布局
        /// </summary>
        public MessageDialog()
        {
            InitializeComponent(); // 加载 XAML 中定义的 UI 元素（标题、图标、消息、确认按钮）

            // 注册 Loaded 事件：控件完全加载后执行初始化逻辑
            Loaded += MessageDialog_Loaded;
        }

        /// <summary>
        /// 控件加载完成事件处理：同步依赖属性值到 UI 元素
        /// 触发时机：控件及其子元素（TextBlock、PackIcon、Button）全部渲染完成后
        /// 核心作用：确保标题、消息、图标按配置正确显示
        /// </summary>
        private void MessageDialog_Loaded(object sender, RoutedEventArgs e)
        {
            TitleTextBlock.Text = Title;       // 标题文本控件赋值
            MessageTextBlock.Text = Message;   // 消息文本控件赋值
            IconControl.Kind = Icon;           // 图标控件赋值（IconControl 是 XAML 中定义的 PackIcon 控件）
        }
        #endregion

        #region 确认按钮点击事件（关闭弹窗）
        /// <summary>
        /// 确认按钮点击事件：关闭消息对话框
        /// 依赖 MaterialDesignThemes 的 DialogHost 组件实现弹窗关闭
        /// 消息对话框无需返回数据，仅用于通知，因此执行无参关闭命令
        /// </summary>
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // DialogHost.CloseDialogCommand：MaterialDesign 框架提供的弹窗关闭命令
            // 第一个参数：返回给调用方的数据（消息对话框无需返回，传 null 即可）
            // 第二个参数：命令目标（null 表示默认目标，即当前 DialogHost）
            DialogHost.CloseDialogCommand.Execute(null, null);
        }
        #endregion
    }
}