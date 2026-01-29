using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MES_WPF.Core.Models;
using MES_WPF.Core.Services.EquipmentManagement;
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
    /// 设备维护执行记录视图模型（MVVM模式）
    /// 核心职责：封装维护执行记录的查询、新增、编辑、删除、完成、导出、筛选等业务逻辑
    /// 适配视图：设备维护执行记录页面（数据绑定、命令响应）
    /// 依赖服务：维护执行记录服务、维护工单服务、对话框服务、导航服务
    /// </summary>
    public partial class MaintenanceExecutionViewModel : ObservableObject
    {
        #region 依赖注入服务（构造函数注入，解耦业务逻辑与数据访问）
        // 维护执行记录核心服务（增删改查、完成状态更新等）
        private readonly IMaintenanceExecutionService _maintenanceExecutionService;
        // 维护工单服务（关联工单数据查询）
        private readonly IMaintenanceOrderService _maintenanceOrderService;
        // 对话框服务（统一弹窗交互：提示、确认、错误、输入框）
        private readonly IDialogService _dialogService;
        // 导航服务（页面跳转，当前类暂未使用）
        private readonly INavigationService _navigationService;
        #endregion

        #region 可绑定属性（ObservableProperty自动生成INotifyPropertyChanged实现，支持UI自动更新）
        /// <summary>
        /// 当前选中的维护执行记录（列表选中项绑定）
        /// </summary>
        [ObservableProperty]
        private MaintenanceExecution? _selectedExecution;

        /// <summary>
        /// 搜索关键词（结果描述/备注模糊查询）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 是否正在刷新数据（控制加载动画显示）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 维护执行记录总条数（分页计算用）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 开始日期（按执行开始时间筛选）
        /// </summary>
        [ObservableProperty]
        private DateTime? _startDate;

        /// <summary>
        /// 结束日期（按执行结束时间筛选）
        /// </summary>
        [ObservableProperty]
        private DateTime? _endDate;

        /// <summary>
        /// 选中的执行结果（0=全部，1=正常，2=异常）
        /// </summary>
        [ObservableProperty]
        private byte _selectedResult = 0;

        /// <summary>
        /// 选中的设备ID（按设备筛选执行记录，需关联工单获取设备ID）
        /// </summary>
        [ObservableProperty]
        private int? _selectedEquipmentId;

        /// <summary>
        /// 当前页码（分页用）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示条数（分页用，固定10条）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题（视图绑定显示）
        /// </summary>
        [ObservableProperty]
        private string _title = "维护执行记录";

        #region 新增/编辑执行记录弹窗相关属性
        /// <summary>
        /// 新增/编辑执行记录弹窗是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isExecutionDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true=编辑，false=新增）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑/新增的执行记录对象（弹窗表单绑定）
        /// </summary>
        [ObservableProperty]
        private MaintenanceExecution _editingExecution = new MaintenanceExecution();

        /// <summary>
        /// 维护工单列表（弹窗选择所属工单）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<MaintenanceOrder> _orders = new ObservableCollection<MaintenanceOrder>();

        /// <summary>
        /// 选中的工单ID（弹窗表单绑定）
        /// </summary>
        [ObservableProperty]
        private int? _selectedOrderId;

        /// <summary>
        /// 执行人列表（弹窗选择执行人）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<User> _executors = new ObservableCollection<User>();

        /// <summary>
        /// 选中的执行人ID（弹窗表单绑定）
        /// </summary>
        [ObservableProperty]
        private int? _selectedExecutorId;

        /// <summary>
        /// 图片URL列表（维护现场照片，弹窗展示/编辑）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _imageUrls = new ObservableCollection<string>();
        #endregion

        /// <summary>
        /// 维护执行记录列表数据源（ObservableCollection支持UI自动更新）
        /// </summary>
        public ObservableCollection<MaintenanceExecution> Executions { get; } = new();

        /// <summary>
        /// 维护执行记录视图（支持过滤/排序，绑定到DataGrid）
        /// </summary>
        public ICollectionView? ExecutionsView { get; private set; }
        #endregion

        #region 属性变更回调（属性值变化时自动触发，用于刷新过滤条件）
        /// <summary>
        /// 搜索关键词变更时，刷新过滤视图
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            ExecutionsView?.Refresh();
        }

        /// <summary>
        /// 执行结果筛选条件变更时，刷新过滤视图
        /// </summary>
        /// <param name="value">新的执行结果值</param>
        partial void OnSelectedResultChanged(byte value)
        {
            ExecutionsView?.Refresh();
        }

        /// <summary>
        /// 开始日期筛选条件变更时，刷新过滤视图
        /// </summary>
        /// <param name="value">新的开始日期</param>
        partial void OnStartDateChanged(DateTime? value)
        {
            ExecutionsView?.Refresh();
        }

        /// <summary>
        /// 结束日期筛选条件变更时，刷新过滤视图
        /// </summary>
        /// <param name="value">新的结束日期</param>
        partial void OnEndDateChanged(DateTime? value)
        {
            ExecutionsView?.Refresh();
        }

        /// <summary>
        /// 设备ID筛选条件变更时，刷新过滤视图
        /// </summary>
        /// <param name="value">新的设备ID</param>
        partial void OnSelectedEquipmentIdChanged(int? value)
        {
            ExecutionsView?.Refresh();
        }
        #endregion

        #region 构造函数（初始化服务、过滤规则、加载初始数据）
        /// <summary>
        /// 构造函数：依赖注入初始化，设置过滤规则，加载基础数据
        /// </summary>
        /// <param name="maintenanceExecutionService">维护执行记录服务</param>
        /// <param name="maintenanceOrderService">维护工单服务</param>
        /// <param name="dialogService">对话框服务</param>
        /// <param name="navigationService">导航服务</param>
        /// <exception cref="ArgumentNullException">服务注入为空时抛出</exception>
        public MaintenanceExecutionViewModel(
            IMaintenanceExecutionService maintenanceExecutionService,
            IMaintenanceOrderService maintenanceOrderService,
            IDialogService dialogService,
            INavigationService navigationService)
        {
            // 服务注入校验（空值保护）
            _maintenanceExecutionService = maintenanceExecutionService ?? throw new ArgumentNullException(nameof(maintenanceExecutionService));
            _maintenanceOrderService = maintenanceOrderService ?? throw new ArgumentNullException(nameof(maintenanceOrderService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // 初始化过滤规则（绑定到ExecutionsView）
            SetupFilter();

            // 异步加载初始数据（Task.Run避免阻塞UI线程）
            _ = LoadExecutionsAsync();   // 加载执行记录
            _ = LoadOrdersAsync();       // 加载关联工单
            _ = LoadExecutorsAsync();    // 加载执行人列表
        }
        #endregion

        #region 过滤规则设置（核心过滤逻辑）
        /// <summary>
        /// 设置维护执行记录的过滤规则（绑定到ExecutionsView）
        /// </summary>
        private void SetupFilter()
        {
            // 获取Executions集合的默认视图（支持过滤/排序）
            ExecutionsView = CollectionViewSource.GetDefaultView(Executions);
            if (ExecutionsView != null)
            {
                // 绑定过滤方法：ExecutionFilter
                ExecutionsView.Filter = ExecutionFilter;
            }
        }

        /// <summary>
        /// 维护执行记录过滤逻辑（多条件组合筛选）
        /// </summary>
        /// <param name="obj">待过滤的执行记录对象</param>
        /// <returns>true=符合条件，false=不符合</returns>
        private bool ExecutionFilter(object obj)
        {
            // 无筛选条件时，全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) &&
                SelectedResult == 0 &&
                !SelectedEquipmentId.HasValue &&
                !StartDate.HasValue &&
                !EndDate.HasValue)
            {
                return true;
            }

            // 类型校验：确保obj是MaintenanceExecution类型
            if (obj is MaintenanceExecution execution)
            {
                // 1. 关键词筛选：结果描述/备注模糊匹配（忽略大小写）
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (execution.ResultDescription?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (execution.Remark?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 2. 执行结果筛选：0=全部，否则匹配指定结果
                bool matchesResult = SelectedResult == 0 ||
                                    (execution.ExecutionResult.HasValue && execution.ExecutionResult.Value == SelectedResult);

                // 3. 设备筛选：通过工单ID关联设备ID，匹配选中的设备ID
                bool matchesEquipment = !SelectedEquipmentId.HasValue ||
                                       (GetEquipmentIdFromOrder(execution.MaintenanceOrderId) == SelectedEquipmentId.Value);

                // 4. 日期范围筛选：开始时间≥StartDate，结束时间≤EndDate（EndDate包含当天最后一秒）
                bool matchesDateRange = true;
                if (StartDate.HasValue && execution.StartTime < StartDate.Value)
                {
                    matchesDateRange = false;
                }
                if (EndDate.HasValue && (execution.EndTime.HasValue && execution.EndTime > EndDate.Value.AddDays(1).AddSeconds(-1)))
                {
                    matchesDateRange = false;
                }

                // 所有条件都满足时，返回true（显示该记录）
                return matchesKeyword && matchesResult && matchesEquipment && matchesDateRange;
            }

            // 非MaintenanceExecution类型，返回false（不显示）
            return false;
        }

        /// <summary>
        /// 辅助方法：从维护工单ID获取关联的设备ID
        /// </summary>
        /// <param name="orderId">工单ID</param>
        /// <returns>设备ID（无匹配时返回0）</returns>
        private int GetEquipmentIdFromOrder(int orderId)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            return order?.EquipmentId ?? 0;
        }
        #endregion

        #region 数据加载方法（核心数据获取逻辑）
        /// <summary>
        /// 加载所有维护执行记录（初始加载/刷新时调用）
        /// </summary>
        private async Task LoadExecutionsAsync()
        {
            try
            {
                IsRefreshing = true; // 开启加载状态（UI显示加载动画）

                // 清空现有数据（避免重复加载）
                Executions.Clear();

                // 调用服务获取所有执行记录
                var executions = await _maintenanceExecutionService.GetAllAsync();

                // UI线程更新（此处可直接更新，因ObservableCollection线程安全）
                foreach (var execution in executions)
                {
                    Executions.Add(execution);
                }

                // 更新总条数（分页计算用）
                TotalCount = Executions.Count;

                // 刷新过滤视图（确保筛选条件生效）
                ExecutionsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常提示（统一通过对话框服务展示）
                await _dialogService.ShowErrorAsync("错误", $"加载维护执行记录失败: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false; // 关闭加载状态
            }
        }

        /// <summary>
        /// 加载维护工单列表（弹窗选择工单时使用）
        /// </summary>
        private async Task LoadOrdersAsync()
        {
            try
            {
                // 清空现有数据
                Orders.Clear();

                // 调用服务获取所有工单
                var orders = await _maintenanceOrderService.GetAllAsync();

                // 添加到工单列表
                foreach (var order in orders)
                {
                    Orders.Add(order);
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载维护工单数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载执行人列表（弹窗选择执行人时使用）
        /// </summary>
        private async Task LoadExecutorsAsync()
        {
            try
            {
                // 清空现有数据
                Executors.Clear();

                // 从系统服务获取所有用户（执行人）
                var users = await App.GetService<Core.Services.SystemManagement.IUserService>().GetAllAsync();

                // 添加到执行人列表
                foreach (var user in users)
                {
                    Executors.Add(user);
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载执行人数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 基础操作命令（刷新/搜索/重置/导出/分页）
        /// <summary>
        /// 刷新维护执行记录（绑定到视图刷新按钮）
        /// </summary>
        [RelayCommand]
        private async Task RefreshExecutions()
        {
            await LoadExecutionsAsync();
        }

        /// <summary>
        /// 搜索维护执行记录（绑定到视图搜索按钮，触发过滤逻辑）
        /// </summary>
        [RelayCommand]
        private async Task SearchExecutions()
        {
            try
            {
                IsRefreshing = true;

                // 刷新过滤视图（应用所有筛选条件）
                ExecutionsView?.Refresh();

                // 重置到第一页（搜索后默认显示第一页）
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
        /// 重置搜索条件（绑定到视图重置按钮）
        /// </summary>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 重置所有筛选条件为默认值
            SearchKeyword = string.Empty;
            SelectedResult = 0;
            SelectedEquipmentId = null;
            StartDate = null;
            EndDate = null;

            // 重新触发搜索（应用重置后的条件）
            await SearchExecutions();
        }

        /// <summary>
        /// 导出维护执行记录（暂未实现，绑定到视图导出按钮）
        /// </summary>
        [RelayCommand]
        private async Task ExportExecutions()
        {
            await _dialogService.ShowInfoAsync("导出", "维护执行记录导出功能尚未实现");
        }

        /// <summary>
        /// 跳转到指定页码（分页控件绑定）
        /// </summary>
        /// <param name="page">目标页码</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 页码合法性校验（≥1且≤总页数）
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return;
            }

            // 更新当前页码（视图绑定显示）
            CurrentPage = page;
            // 注：此处未实现分页加载数据，需结合服务层分页接口完善
        }
        #endregion

        #region 新增/编辑/删除执行记录命令（核心业务操作）
        /// <summary>
        /// 新增维护执行记录（绑定到视图新增按钮，打开新增弹窗）
        /// </summary>
        [RelayCommand]
        private void AddExecution()
        {
            // 重置编辑模式为新增
            IsEditMode = false;

            // 初始化新增执行记录对象（设置默认值）
            EditingExecution = new MaintenanceExecution
            {
                StartTime = DateTime.Now,  // 开始时间默认当前时间
                CreateTime = DateTime.Now  // 创建时间默认当前时间
            };

            // 默认选中第一个工单和执行人（提升用户体验）
            SelectedOrderId = Orders.FirstOrDefault()?.Id;
            SelectedExecutorId = Executors.FirstOrDefault()?.Id;

            // 清空图片URL列表
            ImageUrls.Clear();

            // 打开新增/编辑弹窗
            IsExecutionDialogOpen = true;
        }

        /// <summary>
        /// 编辑维护执行记录（绑定到视图编辑按钮，打开编辑弹窗）
        /// </summary>
        /// <param name="execution">待编辑的执行记录</param>
        [RelayCommand]
        private void EditExecution(MaintenanceExecution? execution)
        {
            // 空值保护：未选中记录时不执行
            if (execution == null) return;

            // 标记为编辑模式
            IsEditMode = true;

            // 深拷贝选中的记录（避免直接修改列表原始数据）
            EditingExecution = new MaintenanceExecution
            {
                Id = execution.Id,
                MaintenanceOrderId = execution.MaintenanceOrderId,
                ExecutorId = execution.ExecutorId,
                StartTime = execution.StartTime,
                EndTime = execution.EndTime,
                LaborTime = execution.LaborTime,
                ExecutionResult = execution.ExecutionResult,
                ResultDescription = execution.ResultDescription,
                ImageUrls = execution.ImageUrls,
                CreateTime = execution.CreateTime,
                UpdateTime = execution.UpdateTime,
                Remark = execution.Remark
            };

            // 设置弹窗选中的工单和执行人
            SelectedOrderId = execution.MaintenanceOrderId;
            SelectedExecutorId = execution.ExecutorId;

            // 解析图片URL（JSON字符串转列表）
            ImageUrls.Clear();
            if (!string.IsNullOrEmpty(execution.ImageUrls))
            {
                try
                {
                    var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(execution.ImageUrls);
                    if (urls != null)
                    {
                        foreach (var url in urls)
                        {
                            ImageUrls.Add(url);
                        }
                    }
                }
                catch { /* 解析失败时忽略，避免弹窗无法打开 */ }
            }

            // 打开编辑弹窗
            IsExecutionDialogOpen = true;
        }

        /// <summary>
        /// 删除维护执行记录（绑定到视图删除按钮）
        /// </summary>
        /// <param name="execution">待删除的执行记录</param>
        [RelayCommand]
        private async Task DeleteExecution(MaintenanceExecution? execution)
        {
            // 空值保护
            if (execution == null) return;

            // 确认弹窗：避免误操作
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除该维护执行记录吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    // 调用服务删除记录
                    await _maintenanceExecutionService.DeleteAsync(execution);

                    // 从列表中移除（UI即时更新）
                    Executions.Remove(execution);

                    // 更新总条数
                    TotalCount = Executions.Count;

                    // 操作成功提示
                    await _dialogService.ShowInfoAsync("成功", "维护执行记录已删除");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除维护执行记录失败: {ex.Message}");
                }
            }
        }
        #endregion

        #region 弹窗操作命令（取消/保存/完成/图片管理）
        /// <summary>
        /// 取消新增/编辑（绑定到弹窗取消按钮，关闭弹窗）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsExecutionDialogOpen = false;
        }

        /// <summary>
        /// 保存维护执行记录（新增/编辑共用，绑定到弹窗保存按钮）
        /// </summary>
        [RelayCommand]
        private async Task SaveExecution()
        {
            // 前端表单校验（必填项检查）
            if (!SelectedOrderId.HasValue)
            {
                await _dialogService.ShowErrorAsync("错误", "请选择维护工单");
                return;
            }
            if (!SelectedExecutorId.HasValue)
            {
                await _dialogService.ShowErrorAsync("错误", "请选择执行人");
                return;
            }

            try
            {
                // 赋值工单和执行人ID（从弹窗选中项绑定）
                EditingExecution.MaintenanceOrderId = SelectedOrderId.Value;
                EditingExecution.ExecutorId = SelectedExecutorId.Value;

                // 序列化图片URL列表为JSON字符串（存储到数据库）
                if (ImageUrls.Count > 0)
                {
                    EditingExecution.ImageUrls = System.Text.Json.JsonSerializer.Serialize(ImageUrls.ToList());
                }

                if (IsEditMode)
                {
                    // 编辑模式：更新现有记录
                    EditingExecution.UpdateTime = DateTime.Now; // 更新时间戳
                    await _maintenanceExecutionService.UpdateAsync(EditingExecution);

                    // 替换列表中的旧数据（UI即时更新）
                    var existingExecution = Executions.FirstOrDefault(e => e.Id == EditingExecution.Id);
                    if (existingExecution != null)
                    {
                        int index = Executions.IndexOf(existingExecution);
                        Executions[index] = EditingExecution;
                    }

                    await _dialogService.ShowInfoAsync("成功", "维护执行记录已更新");
                }
                else
                {
                    // 新增模式：创建新记录
                    var newExecution = await _maintenanceExecutionService.AddAsync(EditingExecution);

                    // 添加到列表
                    Executions.Add(newExecution);

                    // 更新总条数
                    TotalCount = Executions.Count;

                    await _dialogService.ShowInfoAsync("成功", "维护执行记录已创建");
                }

                // 关闭弹窗
                IsExecutionDialogOpen = false;

                // 刷新过滤视图（确保新数据生效）
                ExecutionsView?.Refresh();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存维护执行记录失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 完成维护执行记录（绑定到视图完成按钮，更新执行状态）
        /// </summary>
        /// <param name="execution">待完成的执行记录</param>
        [RelayCommand]
        private async Task CompleteExecution(MaintenanceExecution? execution)
        {
            // 空值保护
            if (execution == null) return;

            // 业务规则校验：已完成的记录不允许再次完成
            if (execution.EndTime.HasValue)
            {
                await _dialogService.ShowInfoAsync("提示", "该维护执行记录已经完成");
                return;
            }

            // 确认完成操作
            var result = await _dialogService.ShowConfirmAsync("完成维护", "确定要完成该维护执行记录吗？");

            if (result)
            {
                try
                {
                    // 输入执行结果（1=正常，2=异常）
                    var executionResult = await _dialogService.ShowInputAsync("执行结果", "请选择执行结果(1:正常,2:异常):", "1");
                    byte resultValue = 1;
                    if (!string.IsNullOrEmpty(executionResult))
                    {
                        byte.TryParse(executionResult, out resultValue);
                    }

                    // 输入结果描述
                    var resultDescription = await _dialogService.ShowInputAsync("结果描述", "请输入结果描述:", "");

                    // 调用服务完成记录（更新结束时间、执行结果、描述等）
                    var updatedExecution = await _maintenanceExecutionService.CompleteExecutionAsync(
                        execution.Id,
                        resultValue,
                        resultDescription ?? string.Empty);

                    // 替换列表中的旧数据
                    var existingExecution = Executions.FirstOrDefault(e => e.Id == updatedExecution.Id);
                    if (existingExecution != null)
                    {
                        int index = Executions.IndexOf(existingExecution);
                        Executions[index] = updatedExecution;
                    }

                    // 刷新视图
                    ExecutionsView?.Refresh();

                    await _dialogService.ShowInfoAsync("成功", "维护执行记录已完成");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"完成维护执行记录失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 添加图片（绑定到弹窗添加图片按钮，暂为输入URL模拟）
        /// </summary>
        [RelayCommand]
        private async Task AddImage()
        {
            // 实际项目中应调用文件选择对话框，此处简化为输入URL
            var imageUrl = await _dialogService.ShowInputAsync("添加图片", "请输入图片URL:", "");

            if (!string.IsNullOrEmpty(imageUrl))
            {
                ImageUrls.Add(imageUrl);
            }
        }

        /// <summary>
        /// 移除图片（绑定到弹窗图片删除按钮）
        /// </summary>
        /// <param name="imageUrl">待移除的图片URL</param>
        [RelayCommand]
        private void RemoveImage(string imageUrl)
        {
            if (!string.IsNullOrEmpty(imageUrl))
            {
                ImageUrls.Remove(imageUrl);
            }
        }
        #endregion
    }
}