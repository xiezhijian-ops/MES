// 引入CommunityToolkit.Mvvm的ObservableObject基类（实现INotifyPropertyChanged，支持属性通知）
using CommunityToolkit.Mvvm.ComponentModel;
// 引入RelayCommand（MVVM命令实现，简化命令绑定）
using CommunityToolkit.Mvvm.Input;
// 引入MES系统核心模型（Role、Permission等实体类）
using MES_WPF.Core.Models;
// 引入系统管理核心服务接口（角色、权限相关）
using MES_WPF.Core.Services.SystemManagement;
// 引入通用服务（对话框服务等）
using MES_WPF.Services;
// 引入系统核心库（基础类型、异常处理等）
using System;
// 引入泛型集合（列表操作）
using System.Collections.Generic;
// 引入可观察集合（支持UI自动刷新的集合类型）
using System.Collections.ObjectModel;
// 引入组件模型（ICollectionView、INotifyPropertyChanged等）
using System.ComponentModel;
// 引入LINQ（集合查询、筛选）
using System.Linq;
// 引入异步编程（Task、async/await）
using System.Threading.Tasks;
// 引入WPF数据绑定（CollectionViewSource）
using System.Windows.Data;

// 命名空间：MES_WPF系统的"系统管理"模块ViewModel层
namespace MES_WPF.ViewModels.SystemManagement
{
    /// <summary>
    /// 角色管理ViewModel
    /// 职责：处理角色管理页面的业务逻辑、数据绑定、命令响应
    /// 基类：ObservableObject（CommunityToolkit.Mvvm提供，简化属性通知）
    /// </summary>
    public partial class RoleManagementViewModel : ObservableObject
    {
        #region 依赖注入服务
        // 角色服务接口（封装角色的增删改查、权限分配等业务逻辑）
        private readonly IRoleService _roleService;
        // 权限服务接口（封装权限的查询等业务逻辑）
        private readonly IPermissionService _permissionService;
        // 对话框服务接口（封装弹窗提示、确认框等UI交互）
        private readonly IDialogService _dialogService;
        #endregion

        #region 视图绑定属性（ObservableProperty自动生成属性通知）
        /// <summary>
        /// 当前选中的角色（用于编辑、删除、分配权限等操作）
        /// ObservableProperty特性：自动生成SelectedRole属性及PropertyChanged通知
        /// </summary>
        [ObservableProperty]
        private Role? _selectedRole;

        /// <summary>
        /// 搜索关键词（用于模糊查询角色名称/编码/备注）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 数据刷新状态（控制加载中动画显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 角色总数（分页控件显示总条数）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 选中的状态筛选条件（0:全部, 1:启用, 2:禁用）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 0;

        /// <summary>
        /// 选中的角色类型筛选条件（0:全部, 1:系统角色, 2:业务角色）
        /// </summary>
        [ObservableProperty]
        private byte _selectedRoleType = 0;

        /// <summary>
        /// 当前页码（分页控件当前页）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页条数（分页控件每页显示数量）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题（UI显示的标题文本）
        /// </summary>
        [ObservableProperty]
        private string _title = "角色管理";

        #region 新增/编辑角色相关属性
        /// <summary>
        /// 角色新增/编辑对话框是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isRoleDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true:编辑现有角色, false:新增角色）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的角色对象（新增时为新对象，编辑时为副本）
        /// </summary>
        [ObservableProperty]
        private Role _editingRole = new Role();
        #endregion

        #region 角色权限分配相关属性
        /// <summary>
        /// 权限分配对话框是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isPermissionDialogOpen;

        /// <summary>
        /// 当前正在分配权限的角色
        /// </summary>
        [ObservableProperty]
        private Role? _currentRole;

        /// <summary>
        /// 所有权限列表（权限分配对话框的可选权限）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Permission> _permissions = new ObservableCollection<Permission>();

        /// <summary>
        /// 已选中的权限列表（角色已分配的权限）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Permission> _selectedPermissions = new ObservableCollection<Permission>();
        #endregion
        #endregion

        #region 公共可观察集合
        /// <summary>
        /// 角色列表（绑定到UI的DataGrid/ListView，自动通知UI刷新）
        /// </summary>
        public ObservableCollection<Role> Roles { get; } = new();

        /// <summary>
        /// 角色列表的视图（用于筛选、排序，绑定到UI的筛选后数据）
        /// </summary>
        public ICollectionView? RolesView { get; private set; }
        #endregion

        #region 属性变更回调（partial方法，由ObservableProperty自动生成）
        /// <summary>
        /// SearchKeyword属性变更时的回调
        /// 作用：搜索关键词变化时，刷新筛选视图
        /// </summary>
        /// <param name="value">变更后的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            // 刷新筛选视图，应用新的搜索关键词
            RolesView?.Refresh();
        }

        /// <summary>
        /// SelectedStatus属性变更时的回调
        /// 作用：状态筛选条件变化时，刷新筛选视图
        /// </summary>
        /// <param name="value">变更后的状态筛选值</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            RolesView?.Refresh();
        }

        /// <summary>
        /// SelectedRoleType属性变更时的回调
        /// 作用：角色类型筛选条件变化时，刷新筛选视图
        /// </summary>
        /// <param name="value">变更后的角色类型筛选值</param>
        partial void OnSelectedRoleTypeChanged(byte value)
        {
            RolesView?.Refresh();
        }
        #endregion

        #region 构造函数（依赖注入+初始化）
        /// <summary>
        /// 构造函数（通过依赖注入获取服务实例）
        /// </summary>
        /// <param name="roleService">角色服务实例</param>
        /// <param name="permissionService">权限服务实例</param>
        /// <param name="dialogService">对话框服务实例</param>
        /// <exception cref="ArgumentNullException">服务实例为空时抛出</exception>
        public RoleManagementViewModel(
            IRoleService roleService,
            IPermissionService permissionService,
            IDialogService dialogService)
        {
            // 校验服务实例非空，避免空引用异常
            _roleService = roleService ?? throw new ArgumentNullException(nameof(roleService));
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化筛选器（设置RolesView的过滤规则）
            SetupFilter();

            // 异步加载角色数据（不阻塞构造函数执行）
            _ = LoadRolesAsync();
        }
        #endregion

        #region 私有方法（筛选、数据加载）
        /// <summary>
        /// 设置角色列表的筛选器
        /// 作用：初始化RolesView并绑定过滤规则
        /// </summary>
        private void SetupFilter()
        {
            // 获取Roles集合的默认视图（用于筛选、排序）
            RolesView = CollectionViewSource.GetDefaultView(Roles);
            if (RolesView != null)
            {
                // 绑定过滤方法：RoleFilter返回true则显示该角色
                RolesView.Filter = RoleFilter;
            }
        }

        /// <summary>
        /// 角色筛选逻辑（核心过滤方法）
        /// </summary>
        /// <param name="obj">待筛选的角色对象</param>
        /// <returns>true:显示该角色, false:隐藏该角色</returns>
        private bool RoleFilter(object obj)
        {
            // 无筛选条件时，显示所有角色
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedStatus == 0 && SelectedRoleType == 0)
            {
                return true;
            }

            // 校验对象类型为Role，避免类型转换异常
            if (obj is Role role)
            {
                #region 关键词筛选（模糊匹配角色名称/编码/备注，忽略大小写）
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (role.RoleName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (role.RoleCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (role.Remark?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);
                #endregion

                #region 状态筛选（0:全部, 1:启用, 2:禁用）
                bool matchesStatus = SelectedStatus == 0 || role.Status == SelectedStatus;
                #endregion

                #region 角色类型筛选（0:全部, 1:系统角色, 2:业务角色）
                bool matchesRoleType = SelectedRoleType == 0 || role.RoleType == SelectedRoleType;
                #endregion

                // 所有筛选条件都满足时，显示该角色
                return matchesKeyword && matchesStatus && matchesRoleType;
            }

            // 非Role类型对象，直接隐藏
            return false;
        }

        /// <summary>
        /// 异步加载角色数据（核心数据加载方法）
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadRolesAsync()
        {
            try
            {
                // 设置刷新状态为true，显示加载中动画
                IsRefreshing = true;

                // 清空现有数据，避免重复加载
                Roles.Clear();

                // 调用角色服务，异步获取所有角色数据
                var roles = await _roleService.GetAllAsync();

                // 将获取到的角色数据添加到可观察集合（UI自动刷新）
                foreach (var role in roles)
                {
                    Roles.Add(role);
                }

                // 更新角色总数（用于分页控件显示）
                TotalCount = Roles.Count;

                // 刷新筛选视图，应用当前筛选条件
                RolesView?.Refresh();
            }
            catch (Exception ex)
            {
                // 捕获异常，通过对话框服务显示错误提示
                await _dialogService.ShowErrorAsync("错误", $"加载角色数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都设置刷新状态为false，隐藏加载中动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 异步加载权限数据（用于角色权限分配）
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <returns>异步任务</returns>
        private async Task LoadPermissionsAsync(int roleId)
        {
            try
            {
                // 清空现有权限数据，避免重复加载
                Permissions.Clear();
                SelectedPermissions.Clear();

                // 调用权限服务，异步获取所有权限数据
                var allPermissions = await _permissionService.GetAllAsync();

                // 调用角色服务，异步获取该角色已分配的权限
                var rolePermissions = await _roleService.GetRolePermissionsAsync(roleId);
                // 提取已分配权限的ID列表，用于后续匹配
                var rolePermissionIds = rolePermissions.Select(p => p.Id).ToList();

                // 遍历所有权限，添加到权限列表
                foreach (var permission in allPermissions)
                {
                    Permissions.Add(permission);

                    // 如果该权限是角色已分配的，添加到已选权限列表
                    if (rolePermissionIds.Contains(permission.Id))
                    {
                        SelectedPermissions.Add(permission);
                    }
                }
            }
            catch (Exception ex)
            {
                // 捕获异常，显示错误提示
                await _dialogService.ShowErrorAsync("错误", $"加载权限数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令方法（RelayCommand自动生成ICommand属性）
        /// <summary>
        /// 刷新角色列表命令（绑定到UI的刷新按钮）
        /// RelayCommand特性：自动生成RefreshRolesCommand属性，支持异步执行
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task RefreshRoles()
        {
            // 调用加载角色数据方法
            await LoadRolesAsync();
        }

        /// <summary>
        /// 搜索角色命令（绑定到UI的搜索按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SearchRoles()
        {
            try
            {
                IsRefreshing = true;

                // 刷新筛选视图，应用当前搜索/筛选条件
                RolesView?.Refresh();

                // 搜索后重置到第一页
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索失败: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件命令（绑定到UI的重置按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空搜索关键词
            SearchKeyword = string.Empty;
            // 重置状态筛选为"全部"
            SelectedStatus = 0;
            // 重置角色类型筛选为"全部"
            SelectedRoleType = 0;

            // 调用搜索命令，刷新视图
            await SearchRoles();
        }

        /// <summary>
        /// 批量删除角色命令（绑定到UI的批量删除按钮）
        /// 注：当前实现仅删除选中的单个角色，可扩展为批量
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的角色（当前仅支持单个选中，可扩展为多选）
            var selectedRoles = Roles.Where(r => r == SelectedRole).ToList();

            // 未选中角色时，提示用户
            if (selectedRoles.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的角色");
                return;
            }

            // 显示确认对话框，确认是否删除
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedRoles.Count} 个角色吗？此操作不可撤销。");

            // 用户确认删除
            if (result)
            {
                try
                {
                    // 遍历选中的角色，逐个删除
                    foreach (var role in selectedRoles)
                    {
                        // 调用角色服务，异步删除角色
                        await _roleService.DeleteAsync(role);
                        // 从角色列表中移除该角色（UI自动刷新）
                        Roles.Remove(role);
                    }

                    // 更新角色总数
                    TotalCount = Roles.Count;
                    // 提示删除成功
                    await _dialogService.ShowInfoAsync("成功", "角色已删除");
                }
                catch (Exception ex)
                {
                    // 捕获删除异常，显示错误提示
                    await _dialogService.ShowErrorAsync("错误", $"删除角色失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出角色命令（绑定到UI的导出按钮，暂未实现）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ExportRoles()
        {
            // 提示功能未实现
            await _dialogService.ShowInfoAsync("导出", "角色导出功能尚未实现");
        }

        /// <summary>
        /// 分页跳转命令（绑定到分页控件的页码按钮）
        /// </summary>
        /// <param name="page">目标页码</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 校验页码有效性（不能小于1，不能大于总页数）
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return;
            }

            // 更新当前页码（UI分页控件自动刷新）
            CurrentPage = page;
        }

        /// <summary>
        /// 新增角色命令（绑定到UI的新增按钮）
        /// </summary>
        [RelayCommand]
        private void AddRole()
        {
            // 设置为新增模式（非编辑模式）
            IsEditMode = false;
            // 初始化新角色对象，设置默认值
            EditingRole = new Role
            {
                Status = 1, // 默认启用状态
                RoleType = 2, // 默认业务角色
                CreateTime = DateTime.Now, // 创建时间为当前时间
                CreateBy = 1, // 假设当前操作用户ID为1（实际应从登录信息获取）
                SortOrder = 1, // 默认排序值
                RoleCode = string.Empty, // 清空角色编码
                RoleName = string.Empty, // 清空角色名称
                Remark = string.Empty // 清空备注
            };

            // 打开角色新增/编辑对话框
            IsRoleDialogOpen = true;
        }

        /// <summary>
        /// 编辑角色命令（绑定到UI的编辑按钮）
        /// </summary>
        /// <param name="role">待编辑的角色对象</param>
        [RelayCommand]
        private void EditRole(Role? role)
        {
            // 角色为空时，直接返回
            if (role == null) return;

            // 设置为编辑模式
            IsEditMode = true;

            // 创建角色对象的副本（避免直接修改原数据，防止未保存时UI提前刷新）
            EditingRole = new Role
            {
                Id = role.Id,
                RoleCode = role.RoleCode,
                RoleName = role.RoleName,
                RoleType = role.RoleType,
                Status = role.Status,
                SortOrder = role.SortOrder,
                CreateBy = role.CreateBy,
                CreateTime = role.CreateTime,
                UpdateTime = role.UpdateTime,
                Remark = role.Remark
            };

            // 打开角色编辑对话框
            IsRoleDialogOpen = true;
        }

        /// <summary>
        /// 删除角色命令（绑定到UI的删除按钮）
        /// </summary>
        /// <param name="role">待删除的角色对象</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task DeleteRole(Role? role)
        {
            // 角色为空时，直接返回
            if (role == null) return;

            // 显示确认删除对话框
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除角色\"{role.RoleName}\"吗？此操作不可撤销。");

            // 用户确认删除
            if (result)
            {
                try
                {
                    // 调用角色服务，异步删除角色
                    await _roleService.DeleteAsync(role);
                    // 从角色列表中移除该角色
                    Roles.Remove(role);
                    // 更新角色总数
                    TotalCount = Roles.Count;
                    // 提示删除成功
                    await _dialogService.ShowInfoAsync("成功", "角色已删除");
                }
                catch (Exception ex)
                {
                    // 捕获删除异常，显示错误提示
                    await _dialogService.ShowErrorAsync("错误", $"删除角色失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消编辑角色命令（绑定到对话框的取消按钮）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭角色新增/编辑对话框
            IsRoleDialogOpen = false;
        }

        /// <summary>
        /// 取消权限分配命令（绑定到权限对话框的取消按钮）
        /// </summary>
        [RelayCommand]
        private void CancelPermissionEdit()
        {
            // 关闭权限分配对话框
            IsPermissionDialogOpen = false;
        }

        /// <summary>
        /// 保存角色命令（绑定到对话框的保存按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SaveRole()
        {
            #region 表单验证（必填字段校验）
            // 角色名称为空时，提示错误
            if (string.IsNullOrWhiteSpace(EditingRole.RoleName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入角色名称");
                return;
            }

            // 角色编码为空时，提示错误
            if (string.IsNullOrWhiteSpace(EditingRole.RoleCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入角色编码");
                return;
            }
            #endregion

            try
            {
                if (IsEditMode)
                {
                    #region 编辑模式：更新现有角色
                    // 设置更新时间为当前时间
                    EditingRole.UpdateTime = DateTime.Now;
                    // 调用角色服务，异步更新角色
                    await _roleService.UpdateAsync(EditingRole);

                    // 查找列表中对应的原角色对象
                    var existingRole = Roles.FirstOrDefault(r => r.Id == EditingRole.Id);
                    if (existingRole != null)
                    {
                        // 获取原角色在列表中的索引
                        int index = Roles.IndexOf(existingRole);
                        // 替换为编辑后的角色对象（UI自动刷新）
                        Roles[index] = EditingRole;
                    }

                    // 提示更新成功
                    await _dialogService.ShowInfoAsync("成功", "角色信息已更新");
                    #endregion
                }
                else
                {
                    #region 新增模式：创建新角色
                    // 调用角色服务，异步添加新角色
                    var newRole = await _roleService.AddAsync(EditingRole);

                    // 将新角色添加到角色列表
                    Roles.Add(newRole);
                    // 更新角色总数
                    TotalCount = Roles.Count;

                    // 提示创建成功
                    await _dialogService.ShowInfoAsync("成功", "角色已创建");
                    #endregion
                }

                // 关闭角色对话框
                IsRoleDialogOpen = false;

                // 刷新筛选视图，应用最新数据
                RolesView?.Refresh();
            }
            catch (Exception ex)
            {
                // 捕获保存异常，显示错误提示
                await _dialogService.ShowErrorAsync("错误", $"保存角色失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 保存权限分配命令（绑定到权限对话框的保存按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SavePermissions()
        {
            // 当前分配权限的角色为空时，直接返回
            if (CurrentRole == null) return;

            try
            {
                // 提取已选中权限的ID列表
                var selectedPermissionIds = SelectedPermissions.Select(p => p.Id).ToList();

                // 调用角色服务，异步为角色分配权限（1为操作人ID，实际应从登录信息获取）
                await _roleService.AssignPermissionsAsync(CurrentRole.Id, selectedPermissionIds, 1);

                // 关闭权限分配对话框
                IsPermissionDialogOpen = false;

                // 提示权限分配成功
                await _dialogService.ShowInfoAsync("成功", "角色权限已更新");
            }
            catch (Exception ex)
            {
                // 捕获保存异常，显示错误提示
                await _dialogService.ShowErrorAsync("错误", $"保存权限失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分配权限命令（绑定到UI的分配权限按钮）
        /// </summary>
        /// <param name="role">待分配权限的角色</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task AssignPermissions(Role? role)
        {
            // 角色为空时，直接返回
            if (role == null) return;

            // 设置当前分配权限的角色
            CurrentRole = role;

            // 异步加载该角色的权限数据
            await LoadPermissionsAsync(role.Id);

            // 打开权限分配对话框
            IsPermissionDialogOpen = true;
        }

        /// <summary>
        /// 禁用角色命令（绑定到UI的禁用按钮）
        /// </summary>
        /// <param name="role">待禁用的角色</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task DisableRole(Role? role)
        {
            // 角色为空时，直接返回
            if (role == null) return;

            // 显示确认禁用对话框
            var result = await _dialogService.ShowConfirmAsync("禁用角色", $"确定要禁用角色\"{role.RoleName}\"吗？");

            // 用户确认禁用
            if (result)
            {
                try
                {
                    // 设置角色状态为禁用（2:禁用）
                    role.Status = 2;
                    // 调用角色服务，异步更新角色状态
                    await _roleService.UpdateAsync(role);
                    // 刷新筛选视图，应用状态变更
                    RolesView?.Refresh();
                    // 提示禁用成功
                    await _dialogService.ShowInfoAsync("成功", "角色已禁用");
                }
                catch (Exception ex)
                {
                    // 捕获禁用异常，显示错误提示
                    await _dialogService.ShowErrorAsync("错误", $"禁用角色失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 启用角色命令（绑定到UI的启用按钮）
        /// </summary>
        /// <param name="role">待启用的角色</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task EnableRole(Role? role)
        {
            // 角色为空时，直接返回
            if (role == null) return;

            // 显示确认启用对话框
            var result = await _dialogService.ShowConfirmAsync("启用角色", $"确定要启用角色\"{role.RoleName}\"吗？");

            // 用户确认启用
            if (result)
            {
                try
                {
                    // 设置角色状态为启用（1:启用）
                    role.Status = 1;
                    // 调用角色服务，异步更新角色状态
                    await _roleService.UpdateAsync(role);
                    // 刷新筛选视图，应用状态变更
                    RolesView?.Refresh();
                    // 提示启用成功
                    await _dialogService.ShowInfoAsync("成功", "角色已启用");
                }
                catch (Exception ex)
                {
                    // 捕获启用异常，显示错误提示
                    await _dialogService.ShowErrorAsync("错误", $"启用角色失败: {ex.Message}");
                }
            }
        }
        #endregion
    }
}