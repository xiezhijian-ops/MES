using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MES_WPF.Core.Models;
using MES_WPF.Core.Services.SystemManagement;
using MES_WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MES_WPF.ViewModels.SystemManagement
{
    /// <summary>
    /// 用户管理视图模型
    /// 核心职责：封装用户管理页面的所有业务逻辑（增删改查、筛选、分页、角色分配、密码重置、状态管理等）
    /// 基于MVVM架构，使用CommunityToolkit.Mvvm实现属性通知和命令绑定，解耦UI与业务逻辑
    /// </summary>
    public partial class UserManagementViewModel : ObservableObject
    {
        #region 依赖注入服务（核心依赖）
        // 用户业务服务：封装用户数据的CRUD及专属业务逻辑（如密码重置、角色分配）
        private readonly IUserService _userService;
        // 弹窗交互服务：统一管理系统弹窗（错误、确认、信息提示、输入框），避免直接耦合WPF弹窗API
        private readonly IDialogService _dialogService;
        // 导航服务：用于页面跳转（当前未实际使用，预留扩展）
        private readonly INavigationService _navigationService;
        #endregion

        #region 视图绑定属性（UI双向绑定的核心属性）
        /// <summary>
        /// 当前选中的用户（绑定到DataGrid的SelectedItem）
        /// 用于删除、编辑、重置密码、锁定/解锁等操作的数据源
        /// </summary>
        [ObservableProperty] // CommunityToolkit特性：自动生成属性变更通知（INotifyPropertyChanged）
        private User? _selectedUser;

        /// <summary>
        /// 搜索关键词（绑定到搜索输入框）
        /// 支持用户名、真实姓名、邮箱、手机号的模糊匹配（不区分大小写）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty; // 初始化为空字符串，避免null

        /// <summary>
        /// 数据加载状态标识（绑定到加载动画的Visibility）
        /// true=加载中（显示动画），false=加载完成（隐藏动画）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 用户总数（绑定到分页控件的总数显示）
        /// 用于计算总页数，仅做展示，未实际参与分页逻辑（当前分页仅前端假分页）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 最后登录时间筛选-开始日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内登录过的用户
        /// </summary>
        [ObservableProperty]
        private DateTime? _startDate;

        /// <summary>
        /// 最后登录时间筛选-结束日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内登录过的用户
        /// </summary>
        [ObservableProperty]
        private DateTime? _endDate;

        /// <summary>
        /// 创建时间筛选-开始日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内创建的用户
        /// </summary>
        [ObservableProperty]
        private DateTime? _createTimeStart;

        /// <summary>
        /// 创建时间筛选-结束日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内创建的用户
        /// </summary>
        [ObservableProperty]
        private DateTime? _createTimeEnd;

        /// <summary>
        /// 选中的状态筛选条件（绑定到状态筛选下拉框）
        /// 枚举约定：0-全部，1-正常，2-锁定，3-禁用（与User实体的Status字段一致）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 0; // 初始值为0（全部）

        /// <summary>
        /// 当前页码（绑定到分页控件的当前页输入框/按钮）
        /// 初始值为1（默认第一页）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示数量（绑定到分页控件的页大小选择框）
        /// 固定为10，可扩展为可配置项（如10/20/50）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题（绑定到窗口/页面的Title属性）
        /// 可动态修改（如编辑时改为"编辑用户"）
        /// </summary>
        [ObservableProperty]
        private string _title = "用户管理";

        #region 新增/编辑用户弹窗相关属性（弹窗专属绑定属性）
        /// <summary>
        /// 用户编辑弹窗是否打开（绑定到弹窗的IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isUserDialogOpen;

        /// <summary>
        /// 是否为编辑模式（区分新增/编辑逻辑）
        /// true=编辑已有用户，false=新增用户
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的用户对象（绑定到弹窗的输入控件）
        /// 新增时：初始化新对象；编辑时：创建原对象副本（避免直接修改列表数据）
        /// </summary>
        [ObservableProperty]
        private User _editingUser = new User(); // 初始化为空对象

        /// <summary>
        /// 密码（绑定到新增用户弹窗的密码输入框）
        /// 仅新增时使用，编辑时隐藏该字段
        /// </summary>
        [ObservableProperty]
        private string _password = string.Empty;

        /// <summary>
        /// 确认密码（绑定到新增用户弹窗的确认密码输入框）
        /// 用于校验两次密码输入是否一致
        /// </summary>
        [ObservableProperty]
        private string _confirmPassword = string.Empty;

        /// <summary>
        /// 选中的角色ID（绑定到角色下拉框的SelectedValue）
        /// 用于为用户分配角色
        /// </summary>
        [ObservableProperty]
        private int? _selectedRoleId;

        /// <summary>
        /// 角色列表（绑定到角色下拉框的ItemsSource）
        /// 供新增/编辑用户时选择角色
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Role> _roles = new ObservableCollection<Role>();
        #endregion

        /// <summary>
        /// 用户列表（核心数据源，绑定到DataGrid的ItemsSource）
        /// ObservableCollection自动触发UI更新（集合变更通知INotifyCollectionChanged）
        /// </summary>
        public ObservableCollection<User> Users { get; } = new();

        /// <summary>
        /// 用户列表视图（用于筛选/排序的包装层）
        /// ICollectionView提供内置的筛选、排序、分组能力，不修改原集合
        /// </summary>
        public ICollectionView? UsersView { get; private set; }
        #endregion

        #region 属性变更通知处理（CommunityToolkit特性）
        /// <summary>
        /// SearchKeyword属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：关键词变化时立即刷新筛选视图，实现实时搜索
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }

        /// <summary>
        /// SelectedStatus属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：状态筛选条件变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的状态值</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }

        /// <summary>
        /// StartDate属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：最后登录开始日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的开始日期</param>
        partial void OnStartDateChanged(DateTime? value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }

        /// <summary>
        /// EndDate属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：最后登录结束日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的结束日期</param>
        partial void OnEndDateChanged(DateTime? value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }

        /// <summary>
        /// CreateTimeStart属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：创建开始日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的创建开始日期</param>
        partial void OnCreateTimeStartChanged(DateTime? value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }

        /// <summary>
        /// CreateTimeEnd属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：创建结束日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的创建结束日期</param>
        partial void OnCreateTimeEndChanged(DateTime? value)
        {
            UsersView?.Refresh(); // 刷新筛选：重新执行UserFilter逻辑
        }
        #endregion

        #region 构造函数（初始化核心逻辑）
        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// 注：需通过DI容器注入服务，避免手动new导致耦合
        /// </summary>
        /// <param name="userService">用户服务（必传，空值抛异常）</param>
        /// <param name="dialogService">弹窗服务（必传，空值抛异常）</param>
        /// <param name="navigationService">导航服务（必传，空值抛异常）</param>
        /// <exception cref="ArgumentNullException">服务实例为空时抛出</exception>
        public UserManagementViewModel(
            IUserService userService,
            IDialogService dialogService,
            INavigationService navigationService)
        {
            // 空值校验：防止空服务导致后续NPE（空指针异常）
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // 初始化筛选器：绑定自定义筛选逻辑到UsersView
            SetupFilter();

            // 异步加载用户数据（_= 忽略返回值，避免警告；不阻塞构造函数执行）
            _ = LoadUsersAsync();

            // 异步加载角色数据（供新增/编辑用户时选择角色）
            _ = LoadRolesAsync();
        }
        #endregion

        #region 私有核心方法（内部业务逻辑封装）
        /// <summary>
        /// 设置用户列表筛选规则（初始化UsersView）
        /// 核心：将ICollectionView与Users集合绑定，并注册自定义筛选逻辑
        /// </summary>
        private void SetupFilter()
        {
            // 获取Users集合的默认视图（WPF内置的集合视图包装器）
            UsersView = CollectionViewSource.GetDefaultView(Users);
            if (UsersView != null)
            {
                // 绑定自定义筛选逻辑：UserFilter方法作为筛选器
                // 每次Refresh()时会执行该方法过滤数据
                UsersView.Filter = UserFilter;
            }
        }

        /// <summary>
        /// 用户筛选核心逻辑（关键词+状态+创建时间+最后登录时间四重过滤）
        /// 由UsersView.Filter调用，返回true表示保留该数据，false表示过滤掉
        /// </summary>
        /// <param name="obj">待筛选的用户对象（ICollectionView传入的集合项）</param>
        /// <returns>是否符合筛选条件</returns>
        private bool UserFilter(object obj)
        {
            // 无筛选条件时（关键词为空+状态为全部），全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedStatus == 0)
            {
                return true;
            }

            // 类型校验：确保obj是User类型（避免类型转换异常）
            if (obj is User user)
            {
                // 1. 关键词匹配逻辑：支持用户名/真实姓名/邮箱/手机号，不区分大小写
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (user.Username?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 用户名匹配（null容错）
                                     (user.RealName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 真实姓名匹配（null容错）
                                     (user.Email?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||    // 邮箱匹配（null容错）
                                     (user.Mobile?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);     // 手机号匹配（null容错）

                // 2. 状态匹配逻辑：0=全部，1=正常，2=锁定，3=禁用
                bool matchesStatus = SelectedStatus == 0 || user.Status == SelectedStatus;

                // 3. 创建时间匹配逻辑：筛选指定时间段内创建的用户
                bool matchesCreateTime = true;
                if (CreateTimeStart.HasValue && user.CreateTime < CreateTimeStart.Value)
                {
                    matchesCreateTime = false; // 创建时间早于开始时间，过滤掉
                }
                if (CreateTimeEnd.HasValue && user.CreateTime > CreateTimeEnd.Value.AddDays(1).AddSeconds(-1))
                {
                    // 创建时间晚于结束时间的23:59:59，过滤掉（实现"包含结束日期全天"的筛选）
                    matchesCreateTime = false;
                }

                // 4. 最后登录时间匹配逻辑：筛选指定时间段内登录过的用户
                bool matchesDate = true;
                if (StartDate.HasValue && user.LastLoginTime.HasValue && user.LastLoginTime < StartDate.Value)
                {
                    matchesDate = false; // 登录时间早于开始时间，过滤掉
                }
                if (EndDate.HasValue && user.LastLoginTime.HasValue && user.LastLoginTime > EndDate.Value.AddDays(1).AddSeconds(-1))
                {
                    // 登录时间晚于结束时间的23:59:59，过滤掉（实现"包含结束日期全天"的筛选）
                    matchesDate = false;
                }

                // 四重条件满足：关键词 AND 状态 AND 创建时间 AND 登录时间
                return matchesKeyword && matchesStatus && matchesCreateTime && matchesDate;
            }

            // 非User类型，直接过滤掉
            return false;
        }

        /// <summary>
        /// 加载所有用户数据（初始化/刷新时调用）
        /// 核心逻辑：从服务层获取数据，更新UI集合，处理异常
        /// </summary>
        private async Task LoadUsersAsync()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 清空现有数据：避免重复加载导致数据重复
                Users.Clear();

                // 异步获取所有用户（服务层封装数据库查询逻辑）
                var users = await _userService.GetAllAsync();

                // 遍历添加到ObservableCollection：触发UI自动更新
                foreach (var user in users)
                {
                    Users.Add(user);
                }

                // 更新总数：用于分页计算
                TotalCount = Users.Count;

                // 刷新筛选视图：确保新数据应用筛选规则
                UsersView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示错误信息（用户友好）
                await _dialogService.ShowErrorAsync("错误", $"加载用户数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都标记加载完成：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 加载角色数据（供新增/编辑用户时选择角色）
        /// 核心逻辑：从服务层获取角色，加载失败时添加默认角色（保证UI可用）
        /// </summary>
        private async Task LoadRolesAsync()
        {
            try
            {
                // 清空现有数据：避免重复加载
                Roles.Clear();

                // 从DI容器获取角色服务并加载所有角色（未通过构造函数注入，灵活获取）
                var roles = await App.GetService<IRoleService>().GetAllAsync();

                // 将角色添加到列表（供下拉选择）
                foreach (var role in roles)
                {
                    Roles.Add(role);
                }

                // 无角色数据时添加示例角色（避免下拉框为空）
                if (Roles.Count == 0)
                {
                    Roles.Add(new Role { Id = 1, RoleName = "管理员", RoleCode = "admin" });
                    Roles.Add(new Role { Id = 2, RoleName = "操作员", RoleCode = "operator" });
                    Roles.Add(new Role { Id = 3, RoleName = "访客", RoleCode = "guest" });
                }
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示加载失败
                await _dialogService.ShowErrorAsync("错误", $"加载角色数据失败: {ex.Message}");

                // 加载失败时添加默认角色（保证UI可用）
                Roles.Clear();
                Roles.Add(new Role { Id = 1, RoleName = "管理员", RoleCode = "admin" });
                Roles.Add(new Role { Id = 2, RoleName = "操作员", RoleCode = "operator" });
                Roles.Add(new Role { Id = 3, RoleName = "访客", RoleCode = "guest" });
            }
        }
        #endregion

        #region 命令方法（RelayCommand：绑定到UI按钮的执行逻辑）
        /// <summary>
        /// 刷新用户列表（绑定到刷新按钮）
        /// 核心逻辑：重新调用LoadUsersAsync加载最新数据
        /// </summary>
        [RelayCommand] // CommunityToolkit特性：自动生成ICommand属性（RefreshUsersCommand）
        private async Task RefreshUsers()
        {
            await LoadUsersAsync();
        }

        /// <summary>
        /// 执行搜索（绑定到搜索按钮）
        /// 核心逻辑：刷新筛选视图 + 重置页码到第一页
        /// </summary>
        [RelayCommand]
        private async Task SearchUsers()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 刷新筛选视图：应用最新的筛选规则（关键词/状态/时间）
                UsersView?.Refresh();

                // 搜索后重置页码：默认显示第一页结果
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示搜索失败
                await _dialogService.ShowErrorAsync("错误", $"搜索失败: {ex.Message}");
            }
            finally
            {
                // 标记加载完成：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件（绑定到重置按钮）
        /// 核心逻辑：清空所有筛选条件 + 重新搜索
        /// </summary>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空所有筛选条件
            SearchKeyword = string.Empty;
            SelectedStatus = 0;
            StartDate = null;
            EndDate = null;
            CreateTimeStart = null;
            CreateTimeEnd = null;

            // 重新执行搜索：应用重置后的条件
            await SearchUsers();
        }

        /// <summary>
        /// 批量删除用户（绑定到批量删除按钮，当前仅支持单个删除）
        /// 核心逻辑：校验选中项 + 二次确认 + 执行删除
        /// </summary>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的用户（当前逻辑：仅支持单个选中，可扩展为多选）
            var selectedUsers = Users.Where(u => u == SelectedUser).ToList();

            // 无选中项提示：用户友好
            if (selectedUsers.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的用户");
                return;
            }

            // 二次确认：防止误操作（删除不可逆）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedUsers.Count} 个用户吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    foreach (var user in selectedUsers)
                    {
                        // 执行删除：调用服务层删除方法
                        await _userService.DeleteAsync(user);
                        // 从UI列表移除：触发UI更新
                        Users.Remove(user);
                    }

                    // 更新总数：同步删除后的数据量
                    TotalCount = Users.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "用户已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除用户失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出用户数据（绑定到导出按钮，预留功能）
        /// </summary>
        [RelayCommand]
        private async Task ExportUsers()
        {
            // 临时提示：功能未实现
            await _dialogService.ShowInfoAsync("导出", "用户导出功能尚未实现");
        }

        /// <summary>
        /// 查看密码（绑定到DataGrid的查看密码按钮，仅演示）
        /// 注：实际项目中禁止明文显示密码，此处仅为示例
        /// </summary>
        /// <param name="user">要查看密码的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task ViewPassword(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 演示提示：实际项目中应调用API获取加密密码或提供重置功能
            await _dialogService.ShowInfoAsync("查看密码", $"用户 {user.Username} 的密码为: ******");
        }

        /// <summary>
        /// 分页跳转（绑定到分页控件的页码按钮/输入框）
        /// 核心逻辑：校验页码合法性，更新当前页码（未实现实际分页加载）
        /// </summary>
        /// <param name="page">目标页码（由UI传递）</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 页码合法性校验：1 ≤ page ≤ 总页数（总页数=向上取整(总数/页大小)）
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return; // 非法页码，直接返回
            }

            // 更新当前页码：UI自动刷新分页显示
            CurrentPage = page;

            // 备注：实际应用中应调用服务层的分页查询方法加载对应页数据
            // 此处仅做前端页码更新，无实际数据加载逻辑
        }

        /// <summary>
        /// 新增用户（绑定到新增按钮）
        /// 核心逻辑：初始化新增对象 + 设置默认角色 + 打开弹窗
        /// </summary>
        [RelayCommand]
        private void AddUser()
        {
            // 标记为新增模式：区分保存逻辑
            IsEditMode = false;

            // 初始化新用户对象（设置默认值）
            EditingUser = new User
            {
                Status = 1,          // 默认状态：正常（1）
                CreateTime = DateTime.Now, // 创建时间：当前时间
                Username = string.Empty,   // 用户名：空（需用户输入）
                RealName = string.Empty,   // 真实姓名：空（需用户输入）
                Email = string.Empty,      // 邮箱：空（可选）
                Mobile = string.Empty,     // 手机号：空（可选）
                Password = string.Empty    // 密码：空（需用户输入）
            };

            // 清空密码和确认密码输入框
            Password = string.Empty;
            ConfirmPassword = string.Empty;

            // 默认选中第一个角色（避免下拉框为空）
            SelectedRoleId = Roles.FirstOrDefault()?.Id;

            // 打开编辑弹窗：UI显示弹窗
            IsUserDialogOpen = true;
        }

        /// <summary>
        /// 编辑用户（绑定到DataGrid的编辑按钮）
        /// 核心逻辑：创建原对象副本 + 打开弹窗（角色分配待完善）
        /// </summary>
        /// <param name="user">要编辑的用户（由UI传递选中项）</param>
        [RelayCommand]
        private void EditUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 标记为编辑模式：区分保存逻辑
            IsEditMode = true;

            // 创建用户副本（深拷贝）：避免直接修改原列表数据（MVVM最佳实践）
            EditingUser = new User
            {
                Id = user.Id,                     // 用户ID（主键，不可改）
                Username = user.Username,         // 用户名
                RealName = user.RealName,         // 真实姓名
                Email = user.Email,               // 邮箱
                Mobile = user.Mobile,             // 手机号
                Status = user.Status,             // 状态
                Remark = user.Remark,             // 备注
                CreateTime = user.CreateTime,     // 创建时间（不可改）
                LastLoginTime = user.LastLoginTime, // 最后登录时间（不可改）
                LastLoginIp = user.LastLoginIp    // 最后登录IP（不可改）
            };

            // 备注：实际项目中应通过用户ID获取已分配的角色并赋值给SelectedRoleId
            SelectedRoleId = null;

            // 打开编辑弹窗：UI显示弹窗
            IsUserDialogOpen = true;
        }

        /// <summary>
        /// 单个删除用户（绑定到DataGrid的删除按钮）
        /// 核心逻辑：二次确认 + 执行删除
        /// </summary>
        /// <param name="user">要删除的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task DeleteUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作（删除不可逆）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除用户\"{user.RealName}\"吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    // 执行删除：调用服务层删除方法
                    await _userService.DeleteAsync(user);
                    // 从UI列表移除：触发UI更新
                    Users.Remove(user);
                    // 更新总数：同步删除后的数据量
                    TotalCount = Users.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "用户已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除用户失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消编辑（绑定到弹窗的取消按钮）
        /// 核心逻辑：关闭弹窗（不保存任何修改）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭编辑弹窗：UI隐藏弹窗
            IsUserDialogOpen = false;
        }

        /// <summary>
        /// 保存用户（绑定到弹窗的保存按钮，支持新增/编辑）
        /// 核心逻辑：数据校验 + 密码校验（新增） + 保存数据 + 角色分配 + 刷新UI
        /// </summary>
        [RelayCommand]
        private async Task SaveUser()
        {
            // 数据校验：必填项非空校验
            if (string.IsNullOrWhiteSpace(EditingUser.Username))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入用户名");
                return; // 终止保存流程
            }

            if (string.IsNullOrWhiteSpace(EditingUser.RealName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入真实姓名");
                return; // 终止保存流程
            }

            // 新增模式专属校验：密码和确认密码
            if (!IsEditMode)
            {
                if (string.IsNullOrWhiteSpace(Password))
                {
                    await _dialogService.ShowErrorAsync("错误", "请输入密码");
                    return; // 终止保存流程
                }

                if (Password != ConfirmPassword)
                {
                    await _dialogService.ShowErrorAsync("错误", "两次输入的密码不一致");
                    return; // 终止保存流程
                }
            }

            try
            {
                if (IsEditMode) // 编辑模式
                {
                    // 执行更新：调用服务层更新方法
                    await _userService.UpdateAsync(EditingUser);

                    // 更新列表中的数据：替换原对象（触发UI更新）
                    var existingUser = Users.FirstOrDefault(u => u.Id == EditingUser.Id);
                    if (existingUser != null)
                    {
                        int index = Users.IndexOf(existingUser);
                        Users[index] = EditingUser;
                    }

                    // 角色分配：如果选择了角色，为用户分配该角色
                    if (SelectedRoleId.HasValue)
                    {
                        // 第三个参数1为操作人ID，实际应从当前登录用户获取
                        await _userService.AssignRolesAsync(EditingUser.Id, new List<int> { SelectedRoleId.Value }, 1);
                    }

                    // 成功提示：编辑成功
                    await _dialogService.ShowInfoAsync("成功", "用户信息已更新");
                }
                else // 新增模式
                {
                    // 设置密码：备注-实际项目中应在服务层加密密码（避免明文传输/存储）
                    EditingUser.Password = Password;
                    // 执行新增：调用服务层新增方法，返回新创建的用户（含自增ID）
                    var newUser = await _userService.AddAsync(EditingUser);

                    // 添加到UI列表：触发UI更新
                    Users.Add(newUser);
                    // 更新总数：同步新增后的数据量
                    TotalCount = Users.Count;

                    // 角色分配：如果选择了角色，为新用户分配该角色
                    if (SelectedRoleId.HasValue)
                    {
                        // 第三个参数1为操作人ID，实际应从当前登录用户获取
                        await _userService.AssignRolesAsync(newUser.Id, new List<int> { SelectedRoleId.Value }, 1);
                    }

                    // 成功提示：新增成功
                    await _dialogService.ShowInfoAsync("成功", "用户已创建");
                }

                // 关闭弹窗：保存后隐藏弹窗
                IsUserDialogOpen = false;

                // 刷新视图：确保新数据应用筛选规则
                UsersView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示保存失败
                await _dialogService.ShowErrorAsync("错误", $"保存用户失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 重置密码（绑定到DataGrid的重置密码按钮）
        /// 核心逻辑：二次确认 + 输入新密码 + 执行重置
        /// </summary>
        /// <param name="user">要重置密码的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task ResetPassword(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("重置密码", $"确定要重置用户\"{user.RealName}\"的密码吗？");

            if (result) // 用户确认重置
            {
                // 弹出输入框：让用户输入新密码（默认值为123456）
                var newPassword = await _dialogService.ShowInputAsync("新密码", "请输入新密码:", "123456");

                // 非空校验：用户未输入/取消则终止
                if (!string.IsNullOrEmpty(newPassword))
                {
                    try
                    {
                        // 执行密码重置：第三个参数1为操作人ID，实际应从当前登录用户获取
                        await _userService.ResetPasswordAsync(user.Id, newPassword, 1);
                        // 成功提示：用户友好
                        await _dialogService.ShowInfoAsync("成功", "密码已重置");
                    }
                    catch (Exception ex)
                    {
                        // 异常处理：弹窗提示重置失败
                        await _dialogService.ShowErrorAsync("错误", $"重置密码失败: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 锁定用户（绑定到DataGrid的锁定按钮）
        /// 核心逻辑：修改状态为锁定 + 保存更新
        /// </summary>
        /// <param name="user">要锁定的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task LockUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("锁定用户", $"确定要锁定用户\"{user.RealName}\"吗？");

            if (result) // 用户确认锁定
            {
                try
                {
                    user.Status = 2; // 修改状态为锁定（2=锁定）
                    await _userService.UpdateAsync(user); // 保存更新
                    UsersView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "用户已锁定"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示锁定失败
                    await _dialogService.ShowErrorAsync("错误", $"锁定用户失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 解锁用户（绑定到DataGrid的解锁按钮）
        /// 核心逻辑：修改状态为正常 + 保存更新
        /// </summary>
        /// <param name="user">要解锁的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task UnlockUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("解锁用户", $"确定要解锁用户\"{user.RealName}\"吗？");

            if (result) // 用户确认解锁
            {
                try
                {
                    user.Status = 1; // 修改状态为正常（1=正常）
                    await _userService.UpdateAsync(user); // 保存更新
                    UsersView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "用户已解锁"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示解锁失败
                    await _dialogService.ShowErrorAsync("错误", $"解锁用户失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 禁用用户（绑定到DataGrid的禁用按钮）
        /// 核心逻辑：修改状态为禁用 + 保存更新
        /// </summary>
        /// <param name="user">要禁用的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task DisableUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("禁用用户", $"确定要禁用用户\"{user.RealName}\"吗？");

            if (result) // 用户确认禁用
            {
                try
                {
                    user.Status = 3; // 修改状态为禁用（3=禁用）
                    await _userService.UpdateAsync(user); // 保存更新
                    UsersView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "用户已禁用"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示禁用失败
                    await _dialogService.ShowErrorAsync("错误", $"禁用用户失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 启用用户（绑定到DataGrid的启用按钮）
        /// 核心逻辑：修改状态为正常 + 保存更新
        /// </summary>
        /// <param name="user">要启用的用户（由UI传递选中项）</param>
        [RelayCommand]
        private async Task EnableUser(User? user)
        {
            // 空值校验：避免传入null导致异常
            if (user == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("启用用户", $"确定要启用用户\"{user.RealName}\"吗？");

            if (result) // 用户确认启用
            {
                try
                {
                    user.Status = 1; // 修改状态为正常（1=正常）
                    await _userService.UpdateAsync(user); // 保存更新
                    UsersView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "用户已启用"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示启用失败
                    await _dialogService.ShowErrorAsync("错误", $"启用用户失败: {ex.Message}");
                }
            }
        }
        #endregion
    }

    #region 辅助模型类（临时定义，实际应放在Core.Models中）
    /// <summary>
    /// 角色模型（临时定义，用于角色下拉框绑定）
    /// 实际项目中应使用Core.Models中的Role实体类
    /// </summary>
    public class RoleModel
    {
        /// <summary>
        /// 角色ID（主键）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 角色名称
        /// </summary>
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// 用户模型（临时定义，用于视图绑定）
    /// 实际项目中应使用Core.Models中的User实体类
    /// </summary>
    public class UserModel
    {
        /// <summary>
        /// 用户ID（主键）
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 用户名（登录账号）
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// 真实姓名
        /// </summary>
        public string RealName { get; set; } = string.Empty;

        /// <summary>
        /// 邮箱
        /// </summary>
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// 手机号
        /// </summary>
        public string Mobile { get; set; } = string.Empty;

        /// <summary>
        /// 状态（1=正常，2=锁定，3=禁用）
        /// </summary>
        public byte Status { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; } = string.Empty;

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 最后登录时间
        /// </summary>
        public DateTime? LastLoginTime { get; set; }

        /// <summary>
        /// 最后登录IP
        /// </summary>
        public string LastLoginIp { get; set; } = string.Empty;

        /// <summary>
        /// 用户关联的角色列表
        /// </summary>
        public List<RoleModel> Roles { get; set; } = new List<RoleModel>();
    }
    #endregion
}