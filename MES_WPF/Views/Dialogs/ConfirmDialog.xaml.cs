using System.Windows;
using System.Windows.Controls;

namespace MES_WPF.Views.Dialogs
{
    /// <summary>
    /// 通用确认对话框控件（WPF UserControl）
    /// 核心用途：系统中所有需要用户确认的操作（如删除、提交、状态变更）统一使用此控件
    /// 设计优势：通过依赖属性支持数据绑定，可自定义标题和提示消息，样式统一易维护
    /// </summary>
    public partial class ConfirmDialog : UserControl
    {
        #region 依赖属性定义（支持WPF数据绑定）
        /// <summary>
        /// 对话框标题依赖属性（默认值："确认"）
        /// 作用：允许通过XAML绑定或代码设置对话框标题，支持MVVM模式下的动态标题
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),                  // 依赖属性对应的CLR属性名
                typeof(string),                 // 依赖属性类型
                typeof(ConfirmDialog),          // 所属控件类型
                new PropertyMetadata("确认")    // 默认值："确认"
            );

        /// <summary>
        /// 对话框提示消息依赖属性（默认值："您确定要执行此操作吗？"）
        /// 作用：允许自定义确认提示内容（如"确定删除该BOM吗？"），支持动态文本
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(ConfirmDialog),
                new PropertyMetadata("您确定要执行此操作吗？")
            );
        #endregion

        #region CLR属性（依赖属性的包装器，便于代码访问）
        /// <summary>
        /// 对话框标题（CLR属性）
        /// 说明：通过GetValue/SetValue访问依赖属性，保持依赖属性的绑定特性
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// 对话框提示消息（CLR属性）
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }
        #endregion

        #region 构造函数与初始化
        /// <summary>
        /// 构造函数：初始化控件UI（加载XAML布局）
        /// </summary>
        public ConfirmDialog()
        {
            InitializeComponent(); // 必须调用：加载ConfirmDialog.xaml中定义的UI元素

            // 注册Loaded事件：控件加载完成后执行初始化逻辑
            Loaded += ConfirmDialog_Loaded;
        }

        /// <summary>
        /// 控件加载完成事件处理：将依赖属性值绑定到UI元素
        /// 说明：确保Title和Message的最新值同步到界面的TextBlock
        /// </summary>
        private void ConfirmDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // TitleTextBlock：XAML中定义的标题文本控件
            TitleTextBlock.Text = Title;
            // MessageTextBlock：XAML中定义的提示消息文本控件
            MessageTextBlock.Text = Message;
        }
        #endregion
    }
}