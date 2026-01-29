// 引入CommunityToolkit.Mvvm的ObservableObject（实现INotifyPropertyChanged）
using CommunityToolkit.Mvvm.ComponentModel;
// 引入RelayCommand（命令绑定）
using CommunityToolkit.Mvvm.Input;
// 引入核心模型
using MES_WPF.Core.Models;
// 引入权限服务接口
using MES_WPF.Core.Services.SystemManagement;
// 引入对话框服务
using MES_WPF.Services;
// 基础系统类
using System;
// 集合类
using System.Collections.Generic;
// 可观察集合（UI自动更新）
using System.Collections.ObjectModel;
// 组件模型（INotifyPropertyChanged等）
using System.ComponentModel;
// LINQ查询
using System.Linq;
// 异步任务
using System.Threading.Tasks;
// 集合视图（过滤/排序）
using System.Windows.Data;

// 命名空间：系统管理模块的ViewModel
namespace MES_WPF.ViewModels.SystemManagement
{
    /// <summary>
    /// 权限管理视图模型
    /// 负责权限管理页面的逻辑处理、数据绑定、命令响应
    /// </summary>
    public partial class PermissionManagementViewModel : ObservableObject
    {
        #region 依赖注入服务
        // 权限服务（用于权限数据的增删改查）
        private readonly IPermissionService _permissionService;
        // 对话框服务（用于弹窗提示）
        private readonly IDialogService _dialogService;
        #endregion

        #region 视图绑定属性
        /// <summary>
        /// 选中的权限节点（树形结构）
        /// ObservableProperty：自动生成属性变更通知
        /// </summary>
        [ObservableProperty]
        private PermissionNode? _selectedPermission;

        /// <summary>
        /// 搜索关键词（用于过滤权限）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 是否正在刷新数据（加载中状态）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 权限总数
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 选中的权限类型（0:全部, 1:菜单, 2:按钮, 3:数据）
        /// </summary>
        [ObservableProperty]
        private byte _selectedPermissionType = 0;

        /// <summary>
        /// 页面标题
        /// </summary>
        [ObservableProperty]
        private string _title = "权限管理";

        #region 新增/编辑权限弹窗相关属性
        /// <summary>
        /// 是否打开权限编辑对话框
        /// </summary>
        [ObservableProperty]
        private bool _isPermissionDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true:编辑, false:新增）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的权限对象（新增/编辑共用）
        /// </summary>
        [ObservableProperty]
        private Permission _editingPermission = new Permission();

        /// <summary>
        /// 选中的父权限ID（用于设置权限层级）
        /// </summary>
        [ObservableProperty]
        private int? _selectedParentId;

        /// <summary>
        /// 父级权限列表（用于下拉选择）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Permission> _parentPermissions = new ObservableCollection<Permission>();
        #endregion

        /// <summary>
        /// 权限树形结构数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<PermissionNode> _permissionTree = new ObservableCollection<PermissionNode>();

        /// <summary>
        /// 权限平铺列表（用于表格展示）
        /// </summary>
        public ObservableCollection<Permission> Permissions { get; } = new();

        /// <summary>
        /// 权限列表的视图（用于过滤、排序）
        /// </summary>
        public ICollectionView? PermissionsView { get; private set; }
        #endregion

        #region 属性变更回调
        /// <summary>
        /// 搜索关键词变更时的回调方法
        /// 自动生成（ObservableProperty特性），关键词变化时刷新过滤
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            // 刷新视图过滤
            PermissionsView?.Refresh();
        }

        /// <summary>
        /// 选中权限类型变更时的回调方法
        /// 自动生成，类型变化时刷新过滤
        /// </summary>
        /// <param name="value">新的权限类型值</param>
        partial void OnSelectedPermissionTypeChanged(byte value)
        {
            // 刷新视图过滤
            PermissionsView?.Refresh();
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数（依赖注入）
        /// </summary>
        /// <param name="permissionService">权限服务</param>
        /// <param name="dialogService">对话框服务</param>
        public PermissionManagementViewModel(
            IPermissionService permissionService,
            IDialogService dialogService)
        {
            // 校验依赖注入服务是否为空
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化过滤规则
            SetupFilter();

            // 异步加载权限数据（不阻塞UI）
            _ = LoadPermissionsAsync();
        }
        #endregion

        #region 过滤逻辑
        /// <summary>
        /// 设置权限列表的过滤规则
        /// </summary>
        private void SetupFilter()
        {
            // 获取Permissions集合的默认视图（用于过滤/排序）
            PermissionsView = CollectionViewSource.GetDefaultView(Permissions);
            if (PermissionsView != null)
            {
                // 设置过滤方法
                PermissionsView.Filter = PermissionFilter;
            }
        }

        /// <summary>
        /// 权限过滤方法（核心过滤逻辑）
        /// </summary>
        /// <param name="obj">待过滤的权限对象</param>
        /// <returns>是否符合过滤条件</returns>
        private bool PermissionFilter(object obj)
        {
            // 如果没有搜索关键词且选择的是全部类型，直接返回true（显示所有）
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedPermissionType == 0)
            {
                return true;
            }

            // 确保对象是Permission类型
            if (obj is Permission permission)
            {
                // 关键词匹配：权限名称/编码/路径/组件包含关键词（忽略大小写）
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (permission.PermissionName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (permission.PermissionCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (permission.Path?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (permission.Component?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 类型匹配：选择全部类型 或 权限类型匹配
                bool matchesType = SelectedPermissionType == 0 || permission.PermissionType == SelectedPermissionType;

                // 同时满足关键词和类型匹配才显示
                return matchesKeyword && matchesType;
            }

            // 非Permission类型返回false（不显示）
            return false;
        }
        #endregion

        #region 数据加载
        /// <summary>
        /// 异步加载所有权限数据
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadPermissionsAsync()
        {
            try
            {
                // 设置加载中状态（UI显示加载动画）
                IsRefreshing = true;

                // 清空现有数据（避免重复）
                Permissions.Clear();
                PermissionTree.Clear();

                // 从服务端获取所有权限数据
                var permissions = await _permissionService.GetAllAsync();

                // 将权限数据添加到可观察集合（UI自动更新）
                foreach (var permission in permissions)
                {
                    Permissions.Add(permission);
                }

                // 更新权限总数
                TotalCount = Permissions.Count;

                // 构建权限树形结构
                BuildPermissionTree();

                // 刷新视图过滤
                PermissionsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载权限数据失败: {ex.Message}");
            }
            finally
            {
                // 无论成功失败，都取消加载中状态
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 构建权限树形结构（顶级节点）
        /// </summary>
        private void BuildPermissionTree()
        {
            // 获取顶级权限（父ID为空或0），按排序号排序
            var topLevelPermissions = Permissions.Where(p => p.ParentId == null || p.ParentId == 0).OrderBy(p => p.SortOrder);

            // 遍历顶级权限，构建树形节点
            foreach (var permission in topLevelPermissions)
            {
                var node = new PermissionNode(permission);
                // 递归构建子节点
                BuildPermissionNode(node);
                // 添加到树形集合
                PermissionTree.Add(node);
            }
        }

        /// <summary>
        /// 递归构建权限节点的子节点
        /// </summary>
        /// <param name="parentNode">父节点</param>
        private void BuildPermissionNode(PermissionNode parentNode)
        {
            // 获取当前父节点的子权限，按排序号排序
            var children = Permissions.Where(p => p.ParentId == parentNode.Permission.Id).OrderBy(p => p.SortOrder);

            // 遍历子权限，构建子节点
            foreach (var childPermission in children)
            {
                var childNode = new PermissionNode(childPermission);
                // 添加到父节点的子节点集合
                parentNode.Children.Add(childNode);
                // 递归构建子节点的子节点
                BuildPermissionNode(childNode);
            }
        }

        /// <summary>
        /// 加载父级权限列表（用于新增/编辑时选择父菜单）
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadParentPermissionsAsync()
        {
            try
            {
                // 清空现有数据
                ParentPermissions.Clear();

                // 添加顶级权限选项（父ID为0）
                ParentPermissions.Add(new Permission { Id = 0, PermissionName = "顶级菜单", ParentId = null });

                // 过滤出所有菜单类型权限（仅菜单可作为父级）
                var menuPermissions = Permissions.Where(p => p.PermissionType == 1);

                if (IsEditMode)
                {
                    // 编辑模式：过滤当前权限及其子权限，避免循环引用（自己不能作为自己的父级）
                    var childPermissionIds = GetChildPermissionIds(EditingPermission.Id);
                    menuPermissions = menuPermissions.Where(p => p.Id != EditingPermission.Id && !childPermissionIds.Contains(p.Id));
                }

                // 将父级菜单添加到集合（按排序号排序）
                foreach (var permission in menuPermissions.OrderBy(p => p.SortOrder))
                {
                    ParentPermissions.Add(permission);
                }
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载父级菜单数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 递归获取指定权限的所有子权限ID（用于编辑时过滤）
        /// </summary>
        /// <param name="permissionId">当前权限ID</param>
        /// <returns>子权限ID列表</returns>
        private List<int> GetChildPermissionIds(int permissionId)
        {
            var result = new List<int>();

            // 获取直接子权限
            var children = Permissions.Where(p => p.ParentId == permissionId);

            // 遍历子权限，递归获取所有层级的子权限ID
            foreach (var child in children)
            {
                result.Add(child.Id);
                // 递归获取子权限的子权限ID
                result.AddRange(GetChildPermissionIds(child.Id));
            }

            return result;
        }
        #endregion

        #region 命令：刷新/搜索/重置
        /// <summary>
        /// 刷新权限数据命令（RelayCommand：绑定到UI按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task RefreshPermissions()
        {
            await LoadPermissionsAsync();
        }

        /// <summary>
        /// 搜索权限命令（触发过滤）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SearchPermissions()
        {
            try
            {
                IsRefreshing = true;
                // 刷新视图过滤（应用搜索条件）
                PermissionsView?.Refresh();
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
        /// 重置搜索条件命令
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空搜索关键词
            SearchKeyword = string.Empty;
            // 重置权限类型为全部
            SelectedPermissionType = 0;

            // 重新触发搜索
            await SearchPermissions();
        }
        #endregion

        #region 命令：批量删除
        /// <summary>
        /// 批量删除权限命令（删除选中节点及其子节点）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 校验是否选中权限
            if (SelectedPermission == null)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的权限");
                return;
            }

            // 确认删除（弹窗确认）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的权限及其子权限吗？此操作不可撤销。");

            if (result)
            {
                try
                {
                    // 获取所有子权限ID
                    var childIds = GetChildPermissionIds(SelectedPermission.Permission.Id);

                    // 先删除子权限
                    foreach (var id in childIds)
                    {
                        var childPermission = Permissions.FirstOrDefault(p => p.Id == id);
                        if (childPermission != null)
                        {
                            await _permissionService.DeleteAsync(childPermission);
                            Permissions.Remove(childPermission);
                        }
                    }

                    // 删除当前选中的权限
                    await _permissionService.DeleteAsync(SelectedPermission.Permission);
                    Permissions.Remove(SelectedPermission.Permission);

                    // 更新总数
                    TotalCount = Permissions.Count;

                    // 重建树形结构
                    PermissionTree.Clear();
                    BuildPermissionTree();

                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "权限已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理
                    await _dialogService.ShowErrorAsync("错误", $"删除权限失败: {ex.Message}");
                }
            }
        }
        #endregion

        #region 命令：新增/编辑权限
        /// <summary>
        /// 新增权限命令
        /// </summary>
        /// <param name="parentPermission">父权限（可选，用于新增子权限）</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task AddPermission(Permission? parentPermission = null)
        {
            // 重置为新增模式
            IsEditMode = false;
            // 初始化新权限对象（设置默认值）
            EditingPermission = new Permission
            {
                Status = 1, // 1=启用状态
                IsVisible = true, // 默认可见
                PermissionType = 1, // 默认菜单类型
                CreateTime = DateTime.Now, // 创建时间
                SortOrder = 1, // 默认排序号
                PermissionCode = string.Empty,
                PermissionName = string.Empty,
                Path = string.Empty,
                Component = string.Empty,
                Icon = string.Empty
            };

            // 如果指定了父权限，设置父ID
            if (parentPermission != null)
            {
                EditingPermission.ParentId = parentPermission.Id;
            }

            // 加载父级权限列表（用于下拉选择）
            await LoadParentPermissionsAsync();

            // 设置选中的父权限ID
            SelectedParentId = EditingPermission.ParentId;

            // 打开编辑对话框
            IsPermissionDialogOpen = true;
        }

        /// <summary>
        /// 编辑权限命令
        /// </summary>
        /// <param name="permission">要编辑的权限</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task EditPermission(Permission? permission)
        {
            // 校验权限是否为空
            if (permission == null) return;

            // 设置为编辑模式
            IsEditMode = true;

            // 复制权限数据（避免直接修改原对象）
            EditingPermission = new Permission
            {
                Id = permission.Id,
                PermissionCode = permission.PermissionCode,
                PermissionName = permission.PermissionName,
                PermissionType = permission.PermissionType,
                ParentId = permission.ParentId,
                Path = permission.Path,
                Component = permission.Component,
                Icon = permission.Icon,
                SortOrder = permission.SortOrder,
                IsVisible = permission.IsVisible,
                Status = permission.Status,
                CreateTime = permission.CreateTime,
                UpdateTime = permission.UpdateTime,
                Remark = permission.Remark
            };

            // 加载父级权限列表（过滤循环引用）
            await LoadParentPermissionsAsync();

            // 设置选中的父权限ID
            SelectedParentId = EditingPermission.ParentId;

            // 打开编辑对话框
            IsPermissionDialogOpen = true;
        }
        #endregion

        #region 命令：删除单个权限
        /// <summary>
        /// 删除单个权限命令
        /// </summary>
        /// <param name="permission">要删除的权限</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task DeletePermission(Permission? permission)
        {
            // 校验权限是否为空
            if (permission == null) return;

            // 获取子权限ID列表
            var childIds = GetChildPermissionIds(permission.Id);
            if (childIds.Any())
            {
                // 有子权限时的确认提示
                var result = await _dialogService.ShowConfirmAsync("确认删除", $"该权限存在 {childIds.Count} 个子权限，删除该权限将同时删除所有子权限。是否继续？");
                if (!result) return;
            }
            else
            {
                // 无子权限时的确认提示
                var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除权限\"{permission.PermissionName}\"吗？此操作不可撤销。");
                if (!result) return;
            }

            try
            {
                // 先删除子权限
                foreach (var id in childIds)
                {
                    var childPermission = Permissions.FirstOrDefault(p => p.Id == id);
                    if (childPermission != null)
                    {
                        await _permissionService.DeleteAsync(childPermission);
                        Permissions.Remove(childPermission);
                    }
                }

                // 删除当前权限
                await _permissionService.DeleteAsync(permission);
                Permissions.Remove(permission);

                // 更新总数
                TotalCount = Permissions.Count;

                // 重建树形结构
                PermissionTree.Clear();
                BuildPermissionTree();

                // 提示成功
                await _dialogService.ShowInfoAsync("成功", "权限已删除");
            }
            catch (Exception ex)
            {
                // 异常处理
                await _dialogService.ShowErrorAsync("错误", $"删除权限失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令：取消/保存编辑
        /// <summary>
        /// 取消编辑命令（关闭对话框）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭编辑对话框
            IsPermissionDialogOpen = false;
        }

        /// <summary>
        /// 保存权限命令（新增/编辑共用）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SavePermission()
        {
            // 校验必填字段：权限名称
            if (string.IsNullOrWhiteSpace(EditingPermission.PermissionName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入权限名称");
                return;
            }

            // 校验必填字段：权限编码
            if (string.IsNullOrWhiteSpace(EditingPermission.PermissionCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入权限编码");
                return;
            }

            try
            {
                // 处理父权限ID：选中0则设为null（顶级权限）
                EditingPermission.ParentId = SelectedParentId == 0 ? null : SelectedParentId;

                if (IsEditMode)
                {
                    // 编辑模式：更新权限
                    EditingPermission.UpdateTime = DateTime.Now; // 更新修改时间
                    await _permissionService.UpdateAsync(EditingPermission);

                    // 更新本地集合中的数据
                    var existingPermission = Permissions.FirstOrDefault(p => p.Id == EditingPermission.Id);
                    if (existingPermission != null)
                    {
                        int index = Permissions.IndexOf(existingPermission);
                        Permissions[index] = EditingPermission;
                    }

                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "权限信息已更新");
                }
                else
                {
                    // 新增模式：创建权限
                    var newPermission = await _permissionService.AddAsync(EditingPermission);

                    // 添加到本地集合
                    Permissions.Add(newPermission);
                    TotalCount = Permissions.Count;

                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "权限已创建");
                }

                // 关闭编辑对话框
                IsPermissionDialogOpen = false;

                // 重建树形结构
                PermissionTree.Clear();
                BuildPermissionTree();

                // 刷新视图
                PermissionsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理
                await _dialogService.ShowErrorAsync("错误", $"保存权限失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令：启用/禁用权限
        /// <summary>
        /// 禁用权限命令
        /// </summary>
        /// <param name="permission">要禁用的权限</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task DisablePermission(Permission? permission)
        {
            if (permission == null) return;

            // 确认禁用
            var result = await _dialogService.ShowConfirmAsync("禁用权限", $"确定要禁用权限\"{permission.PermissionName}\"吗？");

            if (result)
            {
                try
                {
                    permission.Status = 2; // 2=禁用状态
                    await _permissionService.UpdateAsync(permission);
                    // 刷新视图
                    PermissionsView?.Refresh();
                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "权限已禁用");
                }
                catch (Exception ex)
                {
                    // 异常处理
                    await _dialogService.ShowErrorAsync("错误", $"禁用权限失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 启用权限命令
        /// </summary>
        /// <param name="permission">要启用的权限</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task EnablePermission(Permission? permission)
        {
            if (permission == null) return;

            // 确认启用
            var result = await _dialogService.ShowConfirmAsync("启用权限", $"确定要启用权限\"{permission.PermissionName}\"吗？");

            if (result)
            {
                try
                {
                    permission.Status = 1; // 1=启用状态
                    await _permissionService.UpdateAsync(permission);
                    // 刷新视图
                    PermissionsView?.Refresh();
                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "权限已启用");
                }
                catch (Exception ex)
                {
                    // 异常处理
                    await _dialogService.ShowErrorAsync("错误", $"启用权限失败: {ex.Message}");
                }
            }
        }
        #endregion

        #region 命令：添加子权限
        /// <summary>
        /// 给指定权限添加子权限命令
        /// </summary>
        /// <param name="permission">父权限</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task AddChildPermission(Permission? permission)
        {
            if (permission == null) return;
            // 调用新增权限方法，指定父权限
            await AddPermission(permission);
        }
        #endregion
    }

    #region 权限树节点模型
    /// <summary>
    /// 权限树形节点模型（用于树形控件绑定）
    /// </summary>
    public partial class PermissionNode : ObservableObject
    {
        /// <summary>
        /// 节点对应的权限对象
        /// </summary>
        public Permission Permission { get; }

        /// <summary>
        /// 子节点集合
        /// </summary>
        public ObservableCollection<PermissionNode> Children { get; } = new ObservableCollection<PermissionNode>();

        /// <summary>
        /// 节点是否展开
        /// </summary>
        [ObservableProperty]
        private bool _isExpanded = true;

        /// <summary>
        /// 节点是否选中
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="permission">权限对象</param>
        public PermissionNode(Permission permission)
        {
            Permission = permission;
        }
    }
    #endregion
}