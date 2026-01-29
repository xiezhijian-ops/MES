// 引入CommunityToolkit.Mvvm核心特性：
// - ObservableObject：实现INotifyPropertyChanged，支持数据变更通知UI
// - SetProperty：简化属性赋值+通知逻辑
using CommunityToolkit.Mvvm.ComponentModel;
// 引入RelayCommand：轻量级命令实现，无需手动实现ICommand接口
using CommunityToolkit.Mvvm.Input;
// MaterialDesign图标枚举：PackIconKind定义了所有内置图标类型（如ViewDashboard、Factory）
using MaterialDesignThemes.Wpf;
// 引入菜单实体模型：MenuItemModel是菜单项的数据载体
using MES_WPF.Models;
// 引入导航服务接口：INavigationService封装页面跳转逻辑（解耦View和ViewModel）
using MES_WPF.Services;
// 基础系统类：DateTime、ArgumentNullException等
using System;
// 可观察集合：MenuItems变更时自动通知UI刷新（如新增/删除菜单项）
using System.Collections.ObjectModel;
// LINQ扩展：FirstOrDefault等方法用于查找父菜单
using System.Linq;
// WPF命令接口：ICommand用于绑定UI交互（如菜单点击）
using System.Windows.Input;

// 命名空间：MES_WPF的视图模型层 → 专注于菜单的业务逻辑和数据管理
namespace MES_WPF.ViewModels
{
    /// <summary>
    /// 菜单视图模型（左侧导航菜单的核心VM）
    /// 核心职责：
    /// 1. 管理菜单数据：初始化所有一级/二级菜单项，维护选中/展开状态
    /// 2. 处理菜单交互：展开/折叠、选中、标题更新
    /// 3. 触发页面导航：通过INavigationService跳转到对应View
    /// 设计原则：
    /// - 依赖注入：通过构造函数接收INavigationService，解耦导航实现
    /// - 命令驱动：所有交互（点击、展开）通过ICommand实现，符合MVVM
    /// - 数据驱动UI：菜单状态（选中/展开）变更时自动通知UI刷新
    /// </summary>
    public partial class MenuViewModel : ObservableObject
    {
        /// <summary>
        /// 导航服务（只读私有字段）
        /// 作用：封装页面跳转逻辑，VM不直接引用View，仅通过接口调用
        /// 生命周期：与MenuViewModel一致（由DI容器注入）
        /// </summary>
        private readonly INavigationService _navigationService;

        /// <summary>
        /// 菜单项集合（绑定到UI的TreeView.ItemsSource）
        /// 类型：ObservableCollection → 集合变更（增删菜单项）时UI自动刷新
        /// 初始化：直接new，避免空引用，且为只读属性（仅内部可修改集合元素）
        /// </summary>
        public ObservableCollection<MenuItemModel> MenuItems { get; } = new ObservableCollection<MenuItemModel>();

        /// <summary>
        /// 私有字段：存储当前选中的菜单项
        /// </summary>
        private MenuItemModel _selectedMenuItem;
        /// <summary>
        /// 当前选中的菜单项（绑定到TreeView.SelectedItem）
        /// SetProperty：赋值时自动触发PropertyChanged，UI更新选中样式
        /// </summary>
        public MenuItemModel SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetProperty(ref _selectedMenuItem, value);
        }

        /// <summary>
        /// 私有字段：存储当前一级菜单标题（面包屑/顶部标题栏显示）
        /// </summary>
        private string _currentMenuTitle = "首页";
        /// <summary>
        /// 当前菜单标题（一级菜单）
        /// 用途：显示在UI顶部的面包屑/标题区域，如“生产管理”“系统管理”
        /// SetProperty：标题变更时UI自动刷新
        /// </summary>
        public string CurrentMenuTitle
        {
            get => _currentMenuTitle;
            set => SetProperty(ref _currentMenuTitle, value);
        }

        /// <summary>
        /// 私有字段：存储当前子菜单标题（面包屑显示）
        /// </summary>
        private string _currentSubMenuTitle = "系统首页";
        /// <summary>
        /// 当前子菜单标题（二级菜单）
        /// 用途：面包屑二级标题，如“生产计划”“用户管理”，无二级菜单时为空
        /// </summary>
        public string CurrentSubMenuTitle
        {
            get => _currentSubMenuTitle;
            set => SetProperty(ref _currentSubMenuTitle, value);
        }

        /// <summary>
        /// 私有字段：存储主内容区显示的View实例
        /// </summary>
        private object _mainContent;
        /// <summary>
        /// 主内容区（绑定到MainWindow的ContentControl.Content）
        /// 用途：菜单选中后，显示对应的View（如DashboardView、ProductionPlanView）
        /// 类型：object → 兼容所有View类型（UserControl）
        /// </summary>
        public object MainContent
        {
            get => _mainContent;
            set => SetProperty(ref _mainContent, value);
        }

        /// <summary>
        /// 菜单项选择命令（绑定到TreeViewItem的Click/Selected事件）
        /// 类型：ICommand → 符合MVVM，VM不直接操作UI控件，通过命令响应交互
        /// </summary>
        public ICommand SelectMenuItemCommand { get; }

        /// <summary>
        /// 构造函数（MenuViewModel初始化入口）
        /// 依赖注入：接收INavigationService（由DI容器自动注入，解耦导航实现）
        /// 执行逻辑：初始化命令 → 初始化菜单数据 → 设置默认选中项
        /// </summary>
        /// <param name="navigationService">导航服务（不能为空）</param>
        public MenuViewModel(INavigationService navigationService)
        {
            // 空值校验：导航服务不能为空，否则抛异常（防止空指针）
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // 初始化菜单项选择命令：
            // RelayCommand<MenuItemModel>：泛型命令，参数为选中的MenuItemModel
            // OnSelectMenuItem：命令执行的回调方法（处理菜单选中逻辑）
            SelectMenuItemCommand = new RelayCommand<MenuItemModel>(OnSelectMenuItem);

            // 初始化所有菜单项数据（一级+二级菜单）
            InitializeMenuItems();
        }

        /// <summary>
        /// 初始化菜单项数据（核心方法）
        /// 业务逻辑：构建MES系统的所有导航菜单，包含一级菜单和二级子菜单
        /// 设计：每个菜单项设置标题、图标、目标View名称、展开命令
        /// </summary>
        private void InitializeMenuItems()
        {
            // 1. 系统首页（一级菜单，无二级子菜单）
            var dashboardItem = new MenuItemModel
            {
                Title = "系统首页",          // 菜单显示文本
                Icon = PackIconKind.ViewDashboard, // MaterialDesign图标（仪表盘）
                ViewName = "DashboardView",  // 目标View名称（导航时使用）
                IsSelected = true            // 默认选中（打开系统时显示首页）
            };
            // 绑定展开命令：点击展开/折叠按钮时执行ToggleMenuExpand
            // RelayCommand<object>：参数为object，此处用_忽略参数
            dashboardItem.ExpandCommand = new RelayCommand<object>(_ => ToggleMenuExpand(dashboardItem));
            // 将系统首页添加到菜单集合
            MenuItems.Add(dashboardItem);

            // 2. 基础信息（一级菜单，包含6个二级子菜单）
            var BasicItem = new MenuItemModel
            {
                Title = "基础信息",          // 一级菜单标题
                Icon = PackIconKind.Factory, // 图标（工厂）
                ViewName = "ProductionView"  // 一级菜单默认View（点击一级菜单时导航到此处）
            };
            // 绑定展开命令
            BasicItem.ExpandCommand = new RelayCommand<object>(_ => ToggleMenuExpand(BasicItem));
            // 添加二级子菜单（BOM/设备/操作/工序/产品/资源管理）
            BasicItem.SubItems.Add(new MenuItemModel { Title = "BOM管理", ViewName = "BOMView" });
            BasicItem.SubItems.Add(new MenuItemModel { Title = "设备管理", ViewName = "EquipmentView" });
            BasicItem.SubItems.Add(new MenuItemModel { Title = "操作管理", ViewName = "OperationView" });
            BasicItem.SubItems.Add(new MenuItemModel { Title = "工序管理", ViewName = "ProcessRouteView" });
            BasicItem.SubItems.Add(new MenuItemModel { Title = "产品管理", ViewName = "ProductView" });
            BasicItem.SubItems.Add(new MenuItemModel { Title = "资源信息管理", ViewName = "ResourceView" });
            // 添加到菜单集合
            MenuItems.Add(BasicItem);

            

            // 4. 设备管理（一级菜单，包含6个二级子菜单）
            var equipmentItem = new MenuItemModel
            {
                Title = "设备管理",
                Icon = PackIconKind.Tools,   // 图标（工具）
                ViewName = "EquipmentView"   // 一级菜单默认View
            };
            equipmentItem.ExpandCommand = new RelayCommand<object>(_ => ToggleMenuExpand(equipmentItem));
            // 二级子菜单：维护执行/项目/订单/计划/参数日志/备件仓
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "维护执行", ViewName = "MaintenanceExecutionView" });
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "维护项目", ViewName = "MaintenanceItemView" });
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "维护订单", ViewName = "MaintenanceOrderView" });
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "设备维护计划", ViewName = "MaintenancePlanView" });
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "参数日志", ViewName = "MaintenanceItemView" });
            equipmentItem.SubItems.Add(new MenuItemModel { Title = "备件仓", ViewName = "SpareView" });
            MenuItems.Add(equipmentItem);

            

            

            // 7. 系统管理（一级菜单，包含8个二级子菜单）
            var systemItem = new MenuItemModel
            {
                Title = "系统管理",
                Icon = PackIconKind.Cog,      // 图标（齿轮）
                ViewName = "SystemView"       // 一级菜单默认View
            };
            systemItem.ExpandCommand = new RelayCommand<object>(_ => ToggleMenuExpand(systemItem));
            // 二级子菜单：用户/角色/员工/部门/权限/系统设置/数据字典/操作日志
            systemItem.SubItems.Add(new MenuItemModel { Title = "用户管理", ViewName = "UserManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "角色管理", ViewName = "RoleManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "员工管理", ViewName = "EmployeeManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "部门管理", ViewName = "DepartmentManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "权限设置", ViewName = "PermissionManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "系统设置", ViewName = "SystemConfigManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "数据字典", ViewName = "DictionaryManagementView" });
            systemItem.SubItems.Add(new MenuItemModel { Title = "操作日志", ViewName = "OperationLogManagementView" });
            MenuItems.Add(systemItem);

            // 设置默认选中项：系统首页（打开系统时默认选中）
            SelectedMenuItem = dashboardItem;
        }

        /// <summary>
        /// 切换菜单展开/折叠状态（核心交互逻辑）
        /// 规则：
        /// 1. 点击菜单展开按钮，切换当前菜单的展开状态
        /// 2. 若当前菜单展开，则折叠其他所有展开的菜单（保证同一时间仅一个菜单展开）
        /// 3. 手动触发MenuItems的属性变更通知，确保UI刷新
        /// </summary>
        /// <param name="menuItem">要切换状态的菜单项（不能为空）</param>
        private void ToggleMenuExpand(MenuItemModel menuItem)
        {
            // 空值校验：菜单项为空则直接返回
            if (menuItem == null) return;

            // 切换当前菜单的展开状态（true→false / false→true）
            menuItem.IsExpanded = !menuItem.IsExpanded;

            // 排他展开规则：展开当前菜单时，折叠其他所有展开的菜单
            if (menuItem.IsExpanded)
            {
                // 遍历所有一级菜单
                foreach (var item in MenuItems)
                {
                    // 排除当前菜单，且其他菜单处于展开状态 → 折叠
                    if (item != menuItem && item.IsExpanded)
                    {
                        item.IsExpanded = false;
                    }
                }
            }

            // 手动触发属性变更通知：
            // MenuItems是ObservableCollection，但IsExpanded是MenuItemModel的属性
            // 需手动通知UI刷新菜单的展开状态
            OnPropertyChanged(nameof(MenuItems));
        }

        /// <summary>
        /// 菜单项选择处理（SelectMenuItemCommand的回调方法）
        /// 核心逻辑：
        /// 1. 更新所有菜单项的选中状态（仅当前项选中）
        /// 2. 更新面包屑标题（一级/二级菜单标题）
        /// 3. 触发页面导航（跳转到对应View）
        /// 4. 更新SelectedMenuItem属性
        /// </summary>
        /// <param name="menuItem">选中的菜单项（不能为空）</param>
        private void OnSelectMenuItem(MenuItemModel menuItem)
        {
            // 空值校验：菜单项为空则直接返回
            if (menuItem == null) return;

            // 第一步：更新所有菜单项的选中状态（排他选中）
            foreach (var item in MenuItems)
            {
                // 一级菜单：仅当前选中项设为true，其余为false
                item.IsSelected = (item == menuItem);

                // 遍历当前一级菜单的所有二级子菜单
                foreach (var subItem in item.SubItems)
                {
                    if (subItem == menuItem)
                    {
                        // 二级子菜单是当前选中项 → 设为选中，且展开父菜单
                        subItem.IsSelected = true;
                        item.IsExpanded = true; // 确保父菜单展开，用户能看到选中的子菜单
                    }
                    else
                    {
                        // 其他二级子菜单 → 取消选中
                        subItem.IsSelected = false;
                    }
                }
            }

            // 第二步：更新面包屑标题（CurrentMenuTitle/CurrentSubMenuTitle）
            if (menuItem.SubItems.Count == 0)
            {
                // 情况1：选中的是二级子菜单（无自己的子菜单）
                // 查找父菜单（当前子菜单所属的一级菜单）
                var parentItem = MenuItems.FirstOrDefault(item => item.SubItems.Contains(menuItem));
                if (parentItem != null)
                {
                    // 一级菜单标题 = 父菜单标题，二级标题 = 子菜单标题
                    CurrentMenuTitle = parentItem.Title;
                    CurrentSubMenuTitle = menuItem.Title;
                }
                else
                {
                    // 特殊情况：无父菜单（如系统首页）→ 一级标题=当前标题，二级标题为空
                    CurrentMenuTitle = menuItem.Title;
                    CurrentSubMenuTitle = string.Empty;
                }
            }
            else
            {
                // 情况2：选中的是一级菜单（有子菜单）→ 一级标题=当前标题，二级标题为空
                CurrentMenuTitle = menuItem.Title;
                CurrentSubMenuTitle = string.Empty;
            }

            // 第三步：触发页面导航（核心）
            // 仅当ViewName不为空时，调用导航服务跳转到对应View
            if (!string.IsNullOrEmpty(menuItem.ViewName))
            {
                // 导航服务根据ViewName创建View实例，并显示在主内容区
                _navigationService.NavigateTo(menuItem.ViewName);
            }

            // 第四步：更新SelectedMenuItem属性（触发UI刷新选中样式）
            SelectedMenuItem = menuItem;
        }
    }
}