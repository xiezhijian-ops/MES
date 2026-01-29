// 引入MaterialDesign控件（本类未直接使用，预留扩展：如替换为MD风格弹窗）
using MaterialDesignThemes.Wpf;
// 引入Task/TaskCompletionSource：实现异步弹窗逻辑（避免阻塞UI）
using System.Threading.Tasks;
// 引入WPF核心窗口/控件：Window（自定义输入框）、MessageBox（系统弹窗）
using System.Windows;
// 引入WPF布局控件：StackPanel/TextBlock/TextBox/Button等（构建自定义输入对话框）
using System.Windows.Controls;

// 命名空间：MES_WPF的服务层 → 封装通用弹窗逻辑，供ViewModel调用
// 设计原则：服务层专注于通用功能封装，ViewModel通过接口调用，解耦UI细节
namespace MES_WPF.Services
{
    /// <summary>
    /// 弹窗服务实现类（实现IDialogService接口）
    /// 核心职责：
    /// 1. 封装系统弹窗：确认框、信息框、错误框（基于MessageBox）
    /// 2. 自定义弹窗：输入对话框（动态构建WPF控件）
    /// 3. 异步接口：所有方法返回Task，适配ViewModel的异步逻辑
    /// 设计亮点：
    /// - 接口实现：依赖抽象（IDialogService）而非具体实现，便于后续替换弹窗样式
    /// - 异步兼容：同步弹窗包装为Task，统一接口风格
    /// - 自定义控件：动态构建输入框，无需额外XAML文件
    /// </summary>
    public class DialogService : IDialogService
    {
        /// <summary>
        /// 显示确认弹窗（异步包装）
        /// 场景：注销登录、删除数据等需要用户确认的操作
        /// </summary>
        /// <param name="title">弹窗标题（如“注销”）</param>
        /// <param name="message">弹窗内容（如“确定要注销当前用户吗？”）</param>
        /// <returns>用户选择（true=确认/Yes，false=取消/No）</returns>
        /// <remarks>
        /// 底层调用MessageBox（同步），但返回Task<bool>适配异步调用场景
        /// MessageBoxButton.YesNo：显示“是/否”按钮
        /// MessageBoxImage.Question：显示问号图标，符合确认操作的视觉提示
        /// </remarks>
        public async Task<bool> ShowConfirmAsync(string title, string message)
        {
            // 调用系统MessageBox显示确认框：
            // - message：弹窗正文
            // - title：弹窗标题栏文本
            // - MessageBoxButton.YesNo：按钮组（是/否）
            // - MessageBoxImage.Question：图标（问号）
            var result = MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);

            // 返回用户选择：点击“是”返回true，否则返回false
            // async标记方法，实际无异步操作，编译器自动包装为CompletedTask
            return result == MessageBoxResult.Yes;
        }

        /// <summary>
        /// 显示信息弹窗（异步包装）
        /// 场景：操作成功提示（如“保存成功”）、系统通知等
        /// </summary>
        /// <param name="title">弹窗标题（如“提示”）</param>
        /// <param name="message">弹窗内容（如“生产计划已保存”）</param>
        /// <returns>已完成的Task（无返回值）</returns>
        /// <remarks>
        /// MessageBoxImage.Information：显示信息图标（i），符合成功/提示场景
        /// Task.CompletedTask：返回已完成任务，适配异步调用规范
        /// </remarks>
        public Task ShowInfoAsync(string title, string message)
        {
            // 调用系统MessageBox显示信息框：
            // - MessageBoxButton.OK：仅显示“确定”按钮
            // - MessageBoxImage.Information：信息图标
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

            // 返回已完成的Task：无异步操作，统一接口返回类型
            return Task.CompletedTask;
        }

        /// <summary>
        /// 显示错误弹窗（异步包装）
        /// 场景：操作失败提示（如“数据库连接失败”）、异常提示等
        /// </summary>
        /// <param name="title">弹窗标题（如“错误”）</param>
        /// <param name="message">弹窗内容（如“质检数据提交失败，请重试”）</param>
        /// <returns>已完成的Task（无返回值）</returns>
        /// <remarks>
        /// MessageBoxImage.Error：显示错误图标（×），符合失败场景的视觉提示
        /// </remarks>
        public Task ShowErrorAsync(string title, string message)
        {
            // 调用系统MessageBox显示错误框：
            // - MessageBoxImage.Error：错误图标
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

            // 返回已完成的Task：统一接口风格
            return Task.CompletedTask;
        }

        /// <summary>
        /// 显示自定义输入对话框（异步返回输入结果）
        /// 场景：需要用户输入文本的操作（如“输入物料编码”“修改用户名”）
        /// </summary>
        /// <param name="title">弹窗标题（如“输入物料编码”）</param>
        /// <param name="message">提示文本（如“请输入需要查询的物料编码：”）</param>
        /// <param name="defaultValue">输入框默认值（可选，默认空字符串）</param>
        /// <returns>用户输入的文本（取消则返回默认值）</returns>
        /// <remarks>
        /// 动态构建WPF控件：无需编写XAML文件，纯代码构建弹窗，灵活性高
        /// TaskCompletionSource：将同步的ShowDialog转换为异步Task，避免阻塞UI
        /// </remarks>
        public Task<string> ShowInputAsync(string title, string message, string defaultValue = "")
        {
            // 1. 创建TaskCompletionSource：桥接同步弹窗和异步返回
            //    - 作用：将ShowDialog（同步）的结果转换为Task<string>（异步）
            //    - 泛型<string>：匹配返回值类型（用户输入的文本）
            var taskCompletionSource = new TaskCompletionSource<string>();

            // 2. 构建自定义输入对话框窗口
            var inputDialog = new Window
            {
                Title = title, // 窗口标题（如“输入物料编码”）
                Width = 400, // 窗口宽度（固定）
                Height = 200, // 窗口高度（固定）
                // 窗口显示位置：相对于主窗口居中（用户体验更佳）
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                // 窗口所有者：绑定到应用程序主窗口（弹窗始终在主窗口上层）
                Owner = Application.Current.MainWindow,
                // 禁止调整窗口大小（输入框无需缩放）
                ResizeMode = ResizeMode.NoResize,
                // 不在任务栏显示（避免多窗口干扰）
                ShowInTaskbar = false,
                // 工具窗口样式（无最大化/最小化按钮，简洁）
                WindowStyle = WindowStyle.ToolWindow
            };

            // 3. 构建窗口内容布局（StackPanel垂直布局）
            //    Margin=20：内边距，避免控件紧贴窗口边缘
            var panel = new StackPanel { Margin = new Thickness(20) };

            // 3.1 添加提示文本（TextBlock）
            panel.Children.Add(new TextBlock
            {
                Text = message, // 提示内容（如“请输入物料编码”）
                TextWrapping = TextWrapping.Wrap, // 文本过长时自动换行
                Margin = new Thickness(0, 0, 0, 10) // 底部间距10，与输入框分隔
            });

            // 3.2 添加输入框（TextBox）
            var textBox = new TextBox
            {
                Text = defaultValue, // 默认值（如空字符串或预设值）
                Margin = new Thickness(0, 0, 0, 20) // 底部间距20，与按钮分隔
            };
            panel.Children.Add(textBox);

            // 3.3 构建按钮面板（水平布局，靠右对齐）
            var buttonsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal, // 水平排列按钮
                HorizontalAlignment = HorizontalAlignment.Right // 按钮组靠右
            };

            // 3.3.1 确定按钮
            var okButton = new Button
            {
                Content = "确定", // 按钮文本
                Width = 80, // 固定宽度
                Margin = new Thickness(0, 0, 10, 0) // 右侧间距10，与取消按钮分隔
            };
            // 确定按钮点击事件：
            okButton.Click += (s, e) =>
            {
                inputDialog.DialogResult = true; // 设置对话框结果为true（确认）
                taskCompletionSource.SetResult(textBox.Text); // 传递用户输入的文本
                inputDialog.Close(); // 关闭弹窗
            };

            // 3.3.2 取消按钮
            var cancelButton = new Button
            {
                Content = "取消", // 按钮文本
                Width = 80 // 固定宽度
            };
            // 取消按钮点击事件：
            cancelButton.Click += (s, e) =>
            {
                inputDialog.DialogResult = false; // 设置对话框结果为false（取消）
                taskCompletionSource.SetResult(defaultValue); // 传递默认值（取消时返回初始值）
                inputDialog.Close(); // 关闭弹窗
            };

            // 3.3.3 将按钮添加到按钮面板
            buttonsPanel.Children.Add(okButton);
            buttonsPanel.Children.Add(cancelButton);

            // 3.4 将按钮面板添加到主布局
            panel.Children.Add(buttonsPanel);

            // 4. 将主布局设置为窗口内容
            inputDialog.Content = panel;

            // 5. 显示模态弹窗（ShowDialog：阻塞当前线程，直到弹窗关闭）
            //    模态弹窗：用户必须关闭此弹窗才能操作主窗口（符合输入框交互逻辑）
            inputDialog.ShowDialog();

            // 6. 返回异步任务：TaskCompletionSource的Task，包含用户输入结果
            return taskCompletionSource.Task;
        }
    }
}