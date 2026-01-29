using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MES_WPF.Services;
using MES_WPF.ViewModels.SystemManagement;

namespace MES_WPF.Views.SystemManagement
{
    /// <summary>
    /// UserManagementView.xaml 的交互逻辑
    /// 作用：用户管理页面的视图实现，负责UI展示和与ViewModel的交互
    /// 说明：遵循MVVM架构，视图仅负责UI渲染和事件转发，业务逻辑在ViewModel中
    /// </summary>
    public partial class UserManagementView : UserControl
    {
        /// <summary>
        /// 构造函数：初始化用户管理视图
        /// 依赖注入：通过构造函数接收ViewModel实例，解耦视图与ViewModel的创建逻辑
        /// </summary>
        /// <param name="viewModel">用户管理ViewModel实例（由DI容器注入）</param>
        public UserManagementView(UserManagementViewModel viewModel)
        {
            // 初始化WPF控件（加载XAML中定义的UI元素）
            InitializeComponent();

            // 设置数据上下文：将ViewModel绑定到视图，实现MVVM的数据绑定和命令绑定
            DataContext = viewModel;

            // 注册视图加载完成事件：确保UI加载完毕后再执行数据刷新，避免UI未就绪导致的异常
            Loaded += (s, e) =>
            {
                // 安全转换：验证DataContext是否为目标ViewModel类型
                if (DataContext is UserManagementViewModel vm)
                {
                    // 异步执行刷新用户数据命令：
                    // 1. 使用_ = 忽略返回的Task，避免异步方法未等待的警告（此处无需等待UI响应）
                    // 2. ExecuteAsync(null)：执行刷新用户列表的异步命令，null表示无参数
                    // 3. 异步执行避免阻塞UI线程，保证页面加载流畅
                    _ = vm.RefreshUsersCommand.ExecuteAsync(null);
                }
            };
        }
    }
}