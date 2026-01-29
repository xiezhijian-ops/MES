using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Controls;

namespace MES_WPF.Views.Dialogs
{
    /// <summary>
    /// 通用输入对话框控件（基于 MaterialDesignThemes 框架）
    /// 核心用途：系统中需要用户输入文本的场景（如批量操作备注、参数配置、名称修改等）
    /// 设计优势：支持自定义标题/提示消息、自动聚焦输入框、与 MaterialDesign DialogHost 无缝集成
    /// 继承 UserControl：便于嵌入弹窗容器，支持样式统一和复用
    /// </summary>
    public partial class InputDialog : UserControl
    {
        #region 依赖属性定义（支持WPF数据绑定与MVVM模式）
        /// <summary>
        /// 对话框标题依赖属性（默认值："输入"）
        /// 作用：允许通过XAML绑定或代码动态设置弹窗标题（如"输入BOM版本号"）
        /// 依赖属性特性：支持样式绑定、动画、属性变更通知
        /// </summary>
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),                  // 对应CLR属性名
                typeof(string),                 // 属性类型
                typeof(InputDialog),            // 所属控件类型
                new PropertyMetadata("输入")    // 默认值
            );

        /// <summary>
        /// 输入提示消息依赖属性（默认值："请输入："）
        /// 作用：告知用户需输入的内容（如"请输入新的产品编码"）
        /// </summary>
        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(InputDialog),
                new PropertyMetadata("请输入：")
            );

        /// <summary>
        /// 输入内容依赖属性（默认值：空字符串）
        /// 作用：双向绑定输入框内容，支持初始化赋值（如编辑场景回显原有值）和获取用户输入值
        /// </summary>
        public static readonly DependencyProperty InputProperty =
            DependencyProperty.Register(
                nameof(Input),
                typeof(string),
                typeof(InputDialog),
                new PropertyMetadata(string.Empty)
            );
        #endregion

        #region CLR属性包装器（简化依赖属性访问）
        /// <summary>
        /// 对话框标题（CLR属性）
        /// 说明：通过GetValue/SetValue间接操作依赖属性，保持依赖属性的绑定特性
        /// 外部代码可直接通过 dialog.Title 访问，无需关注依赖属性底层实现
        /// </summary>
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        /// <summary>
        /// 输入提示消息（CLR属性）
        /// </summary>
        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        /// <summary>
        /// 输入内容（CLR属性）
        /// 双向绑定场景：初始化时设置默认值，用户输入后通过该属性获取结果
        /// </summary>
        public string Input
        {
            get => (string)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }
        #endregion

        #region 构造函数与控件初始化
        /// <summary>
        /// 构造函数：初始化控件UI布局
        /// </summary>
        public InputDialog()
        {
            InitializeComponent(); // 加载XAML中定义的UI元素（标题、提示、输入框、按钮）

            // 注册Loaded事件：控件完全加载后执行初始化逻辑
            Loaded += InputDialog_Loaded;
        }

        /// <summary>
        /// 控件加载完成事件处理：初始化UI显示与交互状态
        /// 触发时机：控件及其子元素（TextBlock、TextBox、Button）全部渲染完成后
        /// </summary>
        private void InputDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. 将依赖属性值同步到UI元素（确保标题、提示消息、默认输入值正确显示）
            TitleTextBlock.Text = Title;       // 标题文本控件赋值
            MessageTextBlock.Text = Message;   // 提示消息文本控件赋值
            InputTextBox.Text = Input;         // 输入框赋值（支持初始化默认值）

            // 2. 自动聚焦输入框（用户体验优化：无需手动点击输入框，直接键盘输入）
            InputTextBox.Focus();
        }
        #endregion

        #region 确认按钮点击事件（核心交互逻辑）
        /// <summary>
        /// 确认按钮点击事件：关闭对话框并返回用户输入值
        /// 依赖 MaterialDesignThemes 的 DialogHost 组件实现弹窗关闭
        /// </summary>
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            // DialogHost.CloseDialogCommand：MaterialDesign框架提供的弹窗关闭命令
            // 第一个参数：返回给调用方的数据（用户输入的文本）
            // 第二个参数：命令目标（null表示默认目标，即当前DialogHost）
            DialogHost.CloseDialogCommand.Execute(InputTextBox.Text, null);
        }
        #endregion
    }
}