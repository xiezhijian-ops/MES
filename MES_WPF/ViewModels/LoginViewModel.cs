using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MES_WPF.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace MES_WPF.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        //依赖注入
        private readonly IAuthenticationService _authenticationService;

        [ObservableProperty]
        //[ObservableProperty]：自动生成公共属性（如public string Username { get; set; }），
        //并自动触发PropertyChanged事件；
        //对比原生 MVVM：无需手动写OnPropertyChanged(nameof(Username))，大幅简化代码；
        private string _username = "";

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private bool _isLoading = false;// 加载状态：控制登录按钮的加载动画/禁用状态

        /// <summary>
        /// 登录完成事件
        /// </summary>
        public event EventHandler<bool> LoginCompleted;//泛型委托，bool参数表示登录是否成功

        // 构造函数注入：DI容器（App.xaml.cs）创建ViewModel时，自动传入IAuthenticationService实现
        public LoginViewModel(IAuthenticationService authenticationService)
        {
            // 空值保护：确保认证服务必传，抛出明确的参数异常（便于调试）
            _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));
        }

        // [RelayCommand]：自动生成public ICommand LoginCommand，绑定到登录按钮的Command属性
        [RelayCommand]
        private async Task Login() // 异步方法：避免阻塞UI线程
        {
            // 第一步：客户端基础校验（前置过滤，减少无效服务调用）
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "用户名和密码不能为空"; // 触发PropertyChanged，UI显示错误
                return; // 校验失败，直接返回
            }

            // 第二步：设置加载状态，清空历史错误
            IsLoading = true; // 按钮进入加载状态（禁用/显示进度条）
            ErrorMessage = ""; // 清空之前的错误提示

            try
            {
                // 第三步：调用认证服务的异步登录方法（核心：业务逻辑委托给服务层）
                bool success = await _authenticationService.LoginAsync(Username, Password);

                if (success)
                {
                    // 登录成功：触发事件，通知上层跳转主窗口
                    LoginCompleted?.Invoke(this, true);
                }
                else
                {
                    // 登录失败：更新错误提示，触发事件
                    ErrorMessage = "用户名或密码错误";
                    LoginCompleted?.Invoke(this, false);
                }
            }
            catch (Exception ex)
            {
                // 异常处理：捕获所有异常（如数据库连接失败、网络异常），转为用户可读提示
                ErrorMessage = $"登录时发生错误: {ex.Message}";
                LoginCompleted?.Invoke(this, false);
            }
            finally
            {
                // 第四步：无论成功/失败/异常，重置加载状态（避免按钮一直显示加载）
                IsLoading = false;
            }
        }
    }
} 