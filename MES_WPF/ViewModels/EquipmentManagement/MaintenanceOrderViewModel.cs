using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MES_WPF.Core.Models;
using MES_WPF.Core.Services.BasicInformation;
using MES_WPF.Core.Services.EquipmentManagement;
using MES_WPF.Core.Services.SystemManagement;
using MES_WPF.Model.BasicInformation;
using MES_WPF.Model.EquipmentManagement;
using MES_WPF.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MES_WPF.ViewModels.EquipmentManagement
{
    /// <summary>
    /// 设备维护工单管理视图模型
    /// 职责：封装维护工单的查询、新增、编辑、删除、状态更新等核心业务逻辑
    /// 基于 MVVM 架构 + CommunityToolkit.Mvvm 实现（ObservableObject/RelayCommand）
    /// </summary>
    public partial class MaintenanceOrderViewModel : ObservableObject
    {
        #region 依赖服务注入
        // 维护工单业务服务（核心数据操作）
        private readonly IMaintenanceOrderService _orderService;
        // 弹窗交互服务（提示/确认/错误弹窗）
        private readonly IDialogService _dialogService;
        // 导航服务（页面跳转，当前代码暂未使用）
        private readonly INavigationService _navigationService;
        #endregion

        #region 核心绑定属性（UI 双向绑定 + 自动通知更新）
        /// <summary>
        /// 当前选中的工单（用于编辑/删除/状态更新等操作）
        /// </summary>
        [ObservableProperty]
        private MaintenanceOrder? _selectedOrder;

        /// <summary>
        /// 搜索关键词（支持工单编码/故障描述模糊查询）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 数据加载状态标识（控制加载动画显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 工单总数（分页控件显示总条数）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 筛选开始日期（按计划开始时间筛选）
        /// </summary>
        [ObservableProperty]
        private DateTime? _startDate;

        /// <summary>
        /// 筛选结束日期（按计划开始时间筛选）
        /// </summary>
        [ObservableProperty]
        private DateTime? _endDate;

        /// <summary>
        /// 选中的工单类型（0:全部, 1:计划维护, 2:故障维修, 3:紧急维修）
        /// </summary>
        [ObservableProperty]
        private byte _selectedOrderType = 0;

        /// <summary>
        /// 选中的工单状态（0:全部, 1:待处理, 2:已分配, 3:处理中, 4:已完成, 5:已取消）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 0;

        /// <summary>
        /// 当前页码（分页控件绑定）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示条数（分页控件绑定）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题（动态修改标题时使用）
        /// </summary>
        [ObservableProperty]
        private string _title = "维护工单管理";

        #region 新增/编辑工单弹窗相关属性
        /// <summary>
        /// 新增/编辑工单弹窗是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isOrderDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true:编辑 existing 工单, false:新增工单）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的工单对象（弹窗表单绑定）
        /// </summary>
        [ObservableProperty]
        private MaintenanceOrder _editingOrder = new MaintenanceOrder();

        /// <summary>
        /// 设备列表（弹窗选择设备下拉框数据源）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Equipment> _equipments = new ObservableCollection<Equipment>();

        /// <summary>
        /// 用户列表（弹窗选择维护人员下拉框数据源）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<User> _users = new ObservableCollection<User>();

        /// <summary>
        /// 维护计划列表（弹窗选择维护计划下拉框数据源）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<EquipmentMaintenancePlan> _maintenancePlans = new ObservableCollection<EquipmentMaintenancePlan>();

        /// <summary>
        /// 选中的设备ID（弹窗设备下拉框绑定）
        /// </summary>
        [ObservableProperty]
        private int? _selectedEquipmentId;

        /// <summary>
        /// 选中的维护计划ID（弹窗维护计划下拉框绑定）
        /// </summary>
        [ObservableProperty]
        private int? _selectedMaintenancePlanId;

        /// <summary>
        /// 选中的维护人员ID（弹窗分配人员下拉框绑定）
        /// </summary>
        [ObservableProperty]
        private int? _selectedAssignedToId;
        #endregion
        #endregion

        #region 集合属性（UI 列表绑定）
        /// <summary>
        /// 工单列表数据源（原始数据，未过滤）
        /// </summary>
        public ObservableCollection<MaintenanceOrder> Orders { get; } = new();

        /// <summary>
        /// 工单列表视图（带过滤/排序的视图，绑定到UI列表控件）
        /// </summary>
        public ICollectionView? OrdersView { get; private set; }
        #endregion

        #region 属性变更回调（自动触发过滤刷新）
        /// <summary>
        /// 搜索关键词变更时自动刷新过滤
        /// </summary>
        /// <param name="value">新的关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            OrdersView?.Refresh();
        }

        /// <summary>
        /// 工单类型筛选条件变更时自动刷新过滤
        /// </summary>
        /// <param name="value">新的工单类型</param>
        partial void OnSelectedOrderTypeChanged(byte value)
        {
            OrdersView?.Refresh();
        }

        /// <summary>
        /// 工单状态筛选条件变更时自动刷新过滤
        /// </summary>
        /// <param name="value">新的工单状态</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            OrdersView?.Refresh();
        }

        /// <summary>
        /// 开始日期筛选条件变更时自动刷新过滤
        /// </summary>
        /// <param name="value">新的开始日期</param>
        partial void OnStartDateChanged(DateTime? value)
        {
            OrdersView?.Refresh();
        }

        /// <summary>
        /// 结束日期筛选条件变更时自动刷新过滤
        /// </summary>
        /// <param name="value">新的结束日期</param>
        partial void OnEndDateChanged(DateTime? value)
        {
            OrdersView?.Refresh();
        }
        #endregion

        #region 构造函数（初始化逻辑）
        /// <summary>
        /// 构造函数（依赖注入 + 初始化）
        /// </summary>
        /// <param name="orderService">维护工单服务</param>
        /// <param name="dialogService">弹窗服务</param>
        /// <param name="navigationService">导航服务</param>
        public MaintenanceOrderViewModel(
            IMaintenanceOrderService orderService,
            IDialogService dialogService,
            INavigationService navigationService)
        {
            // 校验依赖服务非空（避免空引用）
            _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // 初始化列表过滤规则
            SetupFilter();

            // 异步加载工单数据（不阻塞UI）
            _ = LoadOrdersAsync();

            // 异步加载关联数据（设备/用户/维护计划）
            _ = LoadEquipmentsAsync();
            _ = LoadUsersAsync();
            _ = LoadMaintenancePlansAsync();
        }
        #endregion

        #region 核心私有方法（初始化/数据加载/过滤）
        /// <summary>
        /// 初始化列表过滤规则
        /// </summary>
        private void SetupFilter()
        {
            // 获取Orders集合的默认视图（用于过滤/排序）
            OrdersView = CollectionViewSource.GetDefaultView(Orders);
            if (OrdersView != null)
            {
                // 绑定过滤方法（每次Refresh时执行OrderFilter）
                OrdersView.Filter = OrderFilter;
            }
        }

        /// <summary>
        /// 工单列表过滤逻辑（多条件组合过滤）
        /// </summary>
        /// <param name="obj">待过滤的工单对象</param>
        /// <returns>true:保留该工单, false:过滤掉该工单</returns>
        private bool OrderFilter(object obj)
        {
            // 无过滤条件时，全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedOrderType == 0 && SelectedStatus == 0 && !StartDate.HasValue && !EndDate.HasValue)
            {
                return true;
            }

            // 仅处理MaintenanceOrder类型的对象
            if (obj is MaintenanceOrder order)
            {
                // 1. 关键词过滤（工单编码/故障描述模糊匹配）
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (order.OrderCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (order.FaultDescription?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 2. 工单类型过滤
                bool matchesType = SelectedOrderType == 0 || order.OrderType == SelectedOrderType;

                // 3. 工单状态过滤
                bool matchesStatus = SelectedStatus == 0 || order.Status == SelectedStatus;

                // 4. 日期范围过滤（计划开始时间）
                bool matchesDate = true;
                if (StartDate.HasValue && order.PlanStartTime < StartDate.Value)
                {
                    matchesDate = false;
                }
                if (EndDate.HasValue && order.PlanStartTime > EndDate.Value.AddDays(1).AddSeconds(-1))
                {
                    matchesDate = false;
                }

                // 所有条件都满足时，保留该工单
                return matchesKeyword && matchesType && matchesStatus && matchesDate;
            }

            // 非MaintenanceOrder类型，过滤掉
            return false;
        }

        /// <summary>
        /// 加载工单列表数据（从服务端获取）
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadOrdersAsync()
        {
            try
            {
                // 显示加载状态
                IsRefreshing = true;

                // 清空现有数据（避免重复）
                Orders.Clear();

                // 从服务获取所有工单（实际项目中建议分页查询，此处简化为全量获取）
                var orders = await _orderService.GetAllAsync();

                // 将数据添加到ObservableCollection（自动通知UI更新）
                foreach (var order in orders)
                {
                    Orders.Add(order);
                }

                // 更新总条数
                TotalCount = Orders.Count;

                // 刷新过滤视图
                OrdersView?.Refresh();
            }
            catch (Exception ex)
            {
                // 加载失败时显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载维护工单数据失败: {ex.Message}");
            }
            finally
            {
                // 隐藏加载状态（无论成功/失败都执行）
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 加载设备列表数据（弹窗下拉框数据源）
        /// 加载失败时生成模拟数据，避免UI空值
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadEquipmentsAsync()
        {
            try
            {
                // 清空现有数据
                Equipments.Clear();

                // 从服务获取设备数据（通过App全局服务容器获取IEquipmentService）
                var equipments = await App.GetService<IEquipmentService>().GetAllAsync();

                // 添加到ObservableCollection
                foreach (var equipment in equipments)
                {
                    Equipments.Add(equipment);
                }
            }
            catch (Exception ex)
            {
                // 加载失败时显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载设备数据失败: {ex.Message}");

                // 生成模拟数据（避免UI下拉框为空）
                Equipments.Clear();
                var mockEquipment1 = new Equipment { Id = 1 };
                mockEquipment1.Resource = new Model.BasicInformation.Resource { ResourceName = "设备1", ResourceCode = "EQ001" };
                Equipments.Add(mockEquipment1);

                var mockEquipment2 = new Equipment { Id = 2 };
                mockEquipment2.Resource = new Model.BasicInformation.Resource { ResourceName = "设备2", ResourceCode = "EQ002" };
                Equipments.Add(mockEquipment2);

                var mockEquipment3 = new Equipment { Id = 3 };
                mockEquipment3.Resource = new Model.BasicInformation.Resource { ResourceName = "设备3", ResourceCode = "EQ003" };
                Equipments.Add(mockEquipment3);
            }
        }

        /// <summary>
        /// 加载用户列表数据（弹窗分配人员下拉框数据源）
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadUsersAsync()
        {
            try
            {
                // 清空现有数据
                Users.Clear();

                // 从服务获取用户数据
                var users = await App.GetService<IUserService>().GetAllAsync();

                // 添加到ObservableCollection
                foreach (var user in users)
                {
                    Users.Add(user);
                }
            }
            catch (Exception ex)
            {
                // 加载失败时显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载用户数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载维护计划列表数据（弹窗维护计划下拉框数据源）
        /// 加载失败时生成模拟数据
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadMaintenancePlansAsync()
        {
            try
            {
                // 清空现有数据
                MaintenancePlans.Clear();

                // 从服务获取维护计划数据
                var plans = await App.GetService<IEquipmentMaintenancePlanService>().GetAllAsync();

                // 添加到ObservableCollection
                foreach (var plan in plans)
                {
                    MaintenancePlans.Add(plan);
                }
            }
            catch (Exception ex)
            {
                // 加载失败时显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载维护计划数据失败: {ex.Message}");

                // 生成模拟数据
                MaintenancePlans.Clear();
                MaintenancePlans.Add(new EquipmentMaintenancePlan { Id = 1, PlanName = "日常巡检", PlanCode = "MP001" });
                MaintenancePlans.Add(new EquipmentMaintenancePlan { Id = 2, PlanName = "设备保养", PlanCode = "MP002" });
                MaintenancePlans.Add(new EquipmentMaintenancePlan { Id = 3, PlanName = "设备维修", PlanCode = "MP003" });
            }
        }
        #endregion

        #region 命令方法（UI 按钮绑定）
        /// <summary>
        /// 刷新工单列表（重新加载数据）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task RefreshOrders()
        {
            await LoadOrdersAsync();
        }

        /// <summary>
        /// 执行搜索（刷新过滤视图 + 重置页码）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SearchOrders()
        {
            try
            {
                IsRefreshing = true;

                // 刷新过滤视图（应用最新过滤条件）
                OrdersView?.Refresh();

                // 重置到第一页
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
        /// 重置搜索条件（清空所有过滤条件 + 重新搜索）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空所有过滤条件
            SearchKeyword = string.Empty;
            SelectedOrderType = 0;
            SelectedStatus = 0;
            StartDate = null;
            EndDate = null;

            // 重新执行搜索
            await SearchOrders();
        }

        /// <summary>
        /// 批量删除工单（仅删除当前选中的工单）
        /// 注意：当前实现仅支持单条删除，批量需优化SelectedOrder为多选集合
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的工单（当前仅支持单选，需优化为多选）
            var selectedOrders = Orders.Where(o => o == SelectedOrder).ToList();

            // 未选中时提示
            if (selectedOrders.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的维护工单");
                return;
            }

            // 确认删除（二次确认，防止误操作）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedOrders.Count} 个维护工单吗？此操作不可撤销。");

            if (result)
            {
                try
                {
                    // 遍历删除选中的工单
                    foreach (var order in selectedOrders)
                    {
                        await _orderService.DeleteAsync(order);
                        Orders.Remove(order);
                    }

                    // 更新总条数
                    TotalCount = Orders.Count;
                    await _dialogService.ShowInfoAsync("成功", "维护工单已删除");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除维护工单失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出工单数据（占位方法，未实现具体逻辑）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ExportOrders()
        {
            await _dialogService.ShowInfoAsync("导出", "维护工单导出功能尚未实现");
        }

        /// <summary>
        /// 跳转到指定页码（分页控件页码点击事件）
        /// </summary>
        /// <param name="page">目标页码</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 校验页码合法性（不能小于1，不能大于总页数）
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return;
            }

            // 更新当前页码
            CurrentPage = page;
        }

        /// <summary>
        /// 新增工单（初始化新增状态 + 打开弹窗）
        /// </summary>
        [RelayCommand]
        private void AddOrder()
        {
            // 标记为新增模式
            IsEditMode = false;

            // 初始化新工单默认值
            EditingOrder = new MaintenanceOrder
            {
                Status = 1, // 默认待处理
                CreateTime = DateTime.Now,
                OrderType = 1, // 默认计划维护
                Priority = 5, // 默认中等优先级
                PlanStartTime = DateTime.Now.AddDays(1), // 默认明天开始
                PlanEndTime = DateTime.Now.AddDays(1).AddHours(2), // 默认持续2小时
                OrderCode = GenerateNewOrderCode(), // 自动生成工单编码
                ReportBy = 1 // 临时：当前登录用户ID（实际需替换为真实用户ID）
            };

            // 默认选中第一个设备（如果有）
            SelectedEquipmentId = Equipments.FirstOrDefault()?.Id;
            SelectedMaintenancePlanId = null;
            SelectedAssignedToId = null;

            // 打开新增/编辑弹窗
            IsOrderDialogOpen = true;
        }

        /// <summary>
        /// 生成新工单编码（格式：MO + 年月日 + 4位序号）
        /// 示例：MO202512230001
        /// </summary>
        /// <returns>新工单编码</returns>
        private string GenerateNewOrderCode()
        {
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            int count = Orders.Count + 1;
            return $"MO{dateStr}{count:D4}"; // D4：不足4位补0
        }

        /// <summary>
        /// 编辑工单（初始化编辑状态 + 打开弹窗）
        /// </summary>
        /// <param name="order">待编辑的工单</param>
        [RelayCommand]
        private void EditOrder(MaintenanceOrder? order)
        {
            // 空值校验
            if (order == null) return;

            // 标记为编辑模式
            IsEditMode = true;

            // 复制工单数据（避免直接修改原始数据，防止未保存时UI提前更新）
            EditingOrder = new MaintenanceOrder
            {
                Id = order.Id,
                OrderCode = order.OrderCode,
                OrderType = order.OrderType,
                EquipmentId = order.EquipmentId,
                MaintenancePlanId = order.MaintenancePlanId,
                FaultDescription = order.FaultDescription,
                FaultCode = order.FaultCode,
                FaultLevel = order.FaultLevel,
                Priority = order.Priority,
                Status = order.Status,
                PlanStartTime = order.PlanStartTime,
                PlanEndTime = order.PlanEndTime,
                ActualStartTime = order.ActualStartTime,
                ActualEndTime = order.ActualEndTime,
                ReportBy = order.ReportBy,
                AssignedTo = order.AssignedTo,
                CreateTime = order.CreateTime,
                UpdateTime = order.UpdateTime,
                Remark = order.Remark
            };

            // 初始化弹窗下拉框选中值
            SelectedEquipmentId = order.EquipmentId;
            SelectedMaintenancePlanId = order.MaintenancePlanId;
            SelectedAssignedToId = order.AssignedTo;

            // 打开弹窗
            IsOrderDialogOpen = true;
        }

        /// <summary>
        /// 单条删除工单（确认后删除）
        /// </summary>
        /// <param name="order">待删除的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task DeleteOrder(MaintenanceOrder? order)
        {
            // 空值校验
            if (order == null) return;

            // 二次确认
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除维护工单\"{order.OrderCode}\"吗？此操作不可撤销。");

            if (result)
            {
                try
                {
                    // 调用服务删除
                    await _orderService.DeleteAsync(order);
                    // 从列表移除
                    Orders.Remove(order);
                    // 更新总条数
                    TotalCount = Orders.Count;
                    // 提示成功
                    await _dialogService.ShowInfoAsync("成功", "维护工单已删除");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除维护工单失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消新增/编辑（关闭弹窗）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsOrderDialogOpen = false;
        }

        /// <summary>
        /// 保存工单（新增/编辑统一处理）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task SaveOrder()
        {
            #region 表单验证
            // 校验工单编码
            if (string.IsNullOrWhiteSpace(EditingOrder.OrderCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入工单编码");
                return;
            }

            // 校验设备选择
            if (SelectedEquipmentId == null)
            {
                await _dialogService.ShowErrorAsync("错误", "请选择设备");
                return;
            }

            // 关联选中的ID到编辑对象
            EditingOrder.EquipmentId = SelectedEquipmentId.Value;
            EditingOrder.MaintenancePlanId = SelectedMaintenancePlanId;
            EditingOrder.AssignedTo = SelectedAssignedToId;

            // 校验时间逻辑（结束时间 > 开始时间）
            if (EditingOrder.PlanEndTime <= EditingOrder.PlanStartTime)
            {
                await _dialogService.ShowErrorAsync("错误", "计划结束时间必须晚于计划开始时间");
                return;
            }
            #endregion

            try
            {
                if (IsEditMode)
                {
                    // 编辑模式：更新现有工单
                    EditingOrder.UpdateTime = DateTime.Now;
                    await _orderService.UpdateAsync(EditingOrder);

                    // 更新列表中的数据（替换原对象）
                    var existingOrder = Orders.FirstOrDefault(o => o.Id == EditingOrder.Id);
                    if (existingOrder != null)
                    {
                        int index = Orders.IndexOf(existingOrder);
                        Orders[index] = EditingOrder;
                    }

                    await _dialogService.ShowInfoAsync("成功", "维护工单信息已更新");
                }
                else
                {
                    // 新增模式：创建新工单
                    EditingOrder.CreateTime = DateTime.Now;
                    var newOrder = await _orderService.AddAsync(EditingOrder);

                    // 添加到列表
                    Orders.Add(newOrder);
                    TotalCount = Orders.Count;

                    await _dialogService.ShowInfoAsync("成功", "维护工单已创建");
                }

                // 关闭弹窗
                IsOrderDialogOpen = false;

                // 刷新视图
                OrdersView?.Refresh();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存维护工单失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 分配工单（选择维护人员并更新工单的AssignedTo字段）
        /// </summary>
        /// <param name="order">待分配的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task AssignOrder(MaintenanceOrder? order)
        {
            if (order == null) return;

            // 打开用户选择弹窗（占位实现，实际需替换为真实的选择逻辑）
            var userId = await ShowUserSelectorAsync("分配工单", "请选择维护人员:");

            if (userId.HasValue)
            {
                try
                {
                    // 调用服务分配工单
                    var updatedOrder = await _orderService.AssignOrderAsync(order.Id, userId.Value);

                    // 更新列表中的数据
                    var index = Orders.IndexOf(order);
                    if (index >= 0)
                    {
                        Orders[index] = updatedOrder;
                    }

                    // 刷新视图
                    OrdersView?.Refresh();

                    await _dialogService.ShowInfoAsync("成功", "工单已分配给维护人员");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"分配工单失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 弹出用户选择器（占位方法，实际需实现弹窗选择逻辑）
        /// </summary>
        /// <param name="title">弹窗标题</param>
        /// <param name="message">弹窗提示语</param>
        /// <returns>选中的用户ID（null:未选择）</returns>
        private async Task<int?> ShowUserSelectorAsync(string title, string message)
        {
            // 示例逻辑（实际需替换为真实的弹窗选择）
            //var userNames = Users.Select(u => u.RealName).ToList();
            //var selectedName = await _dialogService.ShowListAsync(title, message, userNames);

            //if (!string.IsNullOrEmpty(selectedName))
            //{
            //    var user = Users.FirstOrDefault(u => u.RealName == selectedName);
            //    return user?.Id;
            //}

            return null;
        }

        /// <summary>
        /// 更新工单状态（占位方法，注释中包含完整逻辑）
        /// </summary>
        /// <param name="order">待更新状态的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task UpdateOrderStatus(MaintenanceOrder? order)
        {
            
            //if (order == null) return;

            //// 状态选项列表
            //var statusOptions = new List<string> { "待处理", "已分配", "处理中", "已完成", "已取消" };
            //var selectedStatus = await _dialogService.ShowListAsync("更新状态", "请选择新状态:", statusOptions);

            //if (!string.IsNullOrEmpty(selectedStatus))
            //{
            //    try
            //    {
            //        // 状态值映射（字符串 → 字节）
            //        byte newStatus = 1;
            //        switch (selectedStatus)
            //        {
            //            case "待处理": newStatus = 1; break;
            //            case "已分配": newStatus = 2; break;
            //            case "处理中": newStatus = 3; break;
            //            case "已完成": newStatus = 4; break;
            //            case "已取消": newStatus = 5; break;
            //        }

            //        // 更新工单状态
            //        var updatedOrder = await _orderService.UpdateStatusAsync(order.Id, newStatus);

            //        // 已完成状态：自动填充实际结束时间
            //        if (newStatus == 4 && !updatedOrder.ActualEndTime.HasValue)
            //        {
            //            updatedOrder.ActualEndTime = DateTime.Now;
            //            await _orderService.UpdateAsync(updatedOrder);
            //        }

            //        // 更新列表数据
            //        var index = Orders.IndexOf(order);
            //        if (index >= 0)
            //        {
            //            Orders[index] = updatedOrder;
            //        }

            //        OrdersView?.Refresh();
            //        await _dialogService.ShowInfoAsync("成功", "工单状态已更新");
            //    }
            //    catch (Exception ex)
            //    {
            //        await _dialogService.ShowErrorAsync("错误", $"更新工单状态失败: {ex.Message}");
            //    }
            //}
        }

        /// <summary>
        /// 开始执行工单（仅已分配状态可执行，更新为处理中 + 记录实际开始时间）
        /// </summary>
        /// <param name="order">待开始的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task StartOrder(MaintenanceOrder? order)
        {
            if (order == null) return;

            // 状态校验：仅已分配（2）的工单可开始
            if (order.Status != 2)
            {
                await _dialogService.ShowInfoAsync("提示", "只有已分配的工单才能开始");
                return;
            }

            // 确认开始
            var result = await _dialogService.ShowConfirmAsync("开始执行", $"确定要开始执行工单\"{order.OrderCode}\"吗？");

            if (result)
            {
                try
                {
                    // 更新状态为处理中（3）
                    var updatedOrder = await _orderService.UpdateStatusAsync(order.Id, 3);

                    // 记录实际开始时间
                    updatedOrder.ActualStartTime = DateTime.Now;
                    await _orderService.UpdateAsync(updatedOrder);

                    // 更新列表数据
                    var index = Orders.IndexOf(order);
                    if (index >= 0)
                    {
                        Orders[index] = updatedOrder;
                    }

                    OrdersView?.Refresh();
                    await _dialogService.ShowInfoAsync("成功", "工单已开始执行");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"开始执行工单失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 完成工单（仅处理中状态可执行，更新为已完成 + 记录实际结束时间）
        /// </summary>
        /// <param name="order">待完成的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task CompleteOrder(MaintenanceOrder? order)
        {
            if (order == null) return;

            // 状态校验：仅处理中（3）的工单可完成
            if (order.Status != 3)
            {
                await _dialogService.ShowInfoAsync("提示", "只有处理中的工单才能完成");
                return;
            }

            // 确认完成
            var result = await _dialogService.ShowConfirmAsync("完成工单", $"确定要完成工单\"{order.OrderCode}\"吗？");

            if (result)
            {
                try
                {
                    // 更新状态为已完成（4）
                    var updatedOrder = await _orderService.UpdateStatusAsync(order.Id, 4);

                    // 记录实际结束时间
                    updatedOrder.ActualEndTime = DateTime.Now;
                    await _orderService.UpdateAsync(updatedOrder);

                    // 更新列表数据
                    var index = Orders.IndexOf(order);
                    if (index >= 0)
                    {
                        Orders[index] = updatedOrder;
                    }

                    OrdersView?.Refresh();
                    await _dialogService.ShowInfoAsync("成功", "工单已完成");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"完成工单失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消工单（仅待处理/已分配/处理中状态可取消，更新为已取消）
        /// </summary>
        /// <param name="order">待取消的工单</param>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task CancelOrder(MaintenanceOrder? order)
        {
            if (order == null) return;

            // 状态校验：已完成（4）/已取消（5）的工单不可取消
            if (order.Status == 4 || order.Status == 5)
            {
                await _dialogService.ShowInfoAsync("提示", "已完成或已取消的工单不能再次取消");
                return;
            }

            // 确认取消
            var result = await _dialogService.ShowConfirmAsync("取消工单", $"确定要取消工单\"{order.OrderCode}\"吗？");

            if (result)
            {
                try
                {
                    // 更新状态为已取消（5）
                    var updatedOrder = await _orderService.UpdateStatusAsync(order.Id, 5);

                    // 更新列表数据
                    var index = Orders.IndexOf(order);
                    if (index >= 0)
                    {
                        Orders[index] = updatedOrder;
                    }

                    OrdersView?.Refresh();
                    await _dialogService.ShowInfoAsync("成功", "工单已取消");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"取消工单失败: {ex.Message}");
                }
            }
        }
        #endregion
    }
}