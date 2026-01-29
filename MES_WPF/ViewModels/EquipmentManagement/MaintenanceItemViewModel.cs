using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    /// 维护项目管理视图模型
    /// 负责维护项目的展示、查询、新增、编辑、删除、排序等业务逻辑处理
    /// </summary>
    public partial class MaintenanceItemViewModel : ObservableObject
    {
        #region 服务依赖
        // 维护项目业务服务
        private readonly IMaintenanceItemService _itemService;
        // 设备维护计划业务服务
        private readonly IEquipmentMaintenancePlanService _planService;
        // 对话框服务（用于弹窗提示）
        private readonly IDialogService _dialogService;
        // 导航服务（用于页面跳转，当前类暂未使用）
        private readonly INavigationService _navigationService;
        #endregion

        #region 视图绑定属性
        /// <summary>
        /// 当前选中的维护项目
        /// </summary>
        [ObservableProperty]
        private MaintenanceItem? _selectedItem;

        /// <summary>
        /// 搜索关键词（用于模糊查询）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 数据刷新状态（用于显示加载动画）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 维护项目总数量
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 选中的项目类型（0:全部, 1:检查, 2:清洁, 3:润滑, 4:更换, 5:调整）
        /// </summary>
        [ObservableProperty]
        private byte _selectedItemType = 0;

        /// <summary>
        /// 选中的维护计划ID
        /// </summary>
        [ObservableProperty]
        private int? _selectedPlanId;

        /// <summary>
        /// 当前页码（分页用）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示数量（分页用）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题
        /// </summary>
        [ObservableProperty]
        private string _title = "维护项目管理";

        /// <summary>
        /// 新增/编辑维护项目的对话框是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isItemDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true:编辑，false:新增）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的维护项目对象
        /// </summary>
        [ObservableProperty]
        private MaintenanceItem _editingItem = new MaintenanceItem();

        /// <summary>
        /// 维护计划集合（用于下拉选择）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<EquipmentMaintenancePlan> _maintenancePlans = new ObservableCollection<EquipmentMaintenancePlan>();

        /// <summary>
        /// 维护项目集合（用于列表展示）
        /// </summary>
        public ObservableCollection<MaintenanceItem> Items { get; } = new();

        /// <summary>
        /// 维护项目视图（用于过滤和排序）
        /// </summary>
        public ICollectionView? ItemsView { get; private set; }
        #endregion

        #region 属性变更回调
        /// <summary>
        /// 搜索关键词变更时的回调方法
        /// 触发视图过滤刷新
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            ItemsView?.Refresh();
        }

        /// <summary>
        /// 选中项目类型变更时的回调方法
        /// 触发视图过滤刷新
        /// </summary>
        /// <param name="value">新的项目类型</param>
        partial void OnSelectedItemTypeChanged(byte value)
        {
            ItemsView?.Refresh();
        }

        /// <summary>
        /// 选中维护计划ID变更时的回调方法
        /// 触发视图过滤刷新
        /// </summary>
        /// <param name="value">新的维护计划ID</param>
        partial void OnSelectedPlanIdChanged(int? value)
        {
            ItemsView?.Refresh();
        }
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数（通过依赖注入初始化服务）
        /// </summary>
        /// <param name="itemService">维护项目服务</param>
        /// <param name="planService">维护计划服务</param>
        /// <param name="dialogService">对话框服务</param>
        /// <param name="navigationService">导航服务</param>
        public MaintenanceItemViewModel(
            IMaintenanceItemService itemService,
            IEquipmentMaintenancePlanService planService,
            IDialogService dialogService,
            INavigationService navigationService)
        {
            // 服务依赖注入校验
            _itemService = itemService ?? throw new ArgumentNullException(nameof(itemService));
            _planService = planService ?? throw new ArgumentNullException(nameof(planService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
            _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));

            // 初始化数据过滤规则
            SetupFilter();

            // 异步加载维护项目数据
            _ = LoadItemsAsync();

            // 异步加载维护计划数据
            _ = LoadMaintenancePlansAsync();
        }
        #endregion

        #region 过滤与初始化
        /// <summary>
        /// 设置维护项目的过滤规则
        /// 初始化ICollectionView并绑定过滤方法
        /// </summary>
        private void SetupFilter()
        {
            // 获取默认的集合视图
            ItemsView = CollectionViewSource.GetDefaultView(Items);
            if (ItemsView != null)
            {
                // 绑定自定义过滤方法
                ItemsView.Filter = ItemFilter;
            }
        }

        /// <summary>
        /// 维护项目的过滤逻辑
        /// 根据搜索关键词、项目类型、维护计划ID进行过滤
        /// </summary>
        /// <param name="obj">待过滤的维护项目对象</param>
        /// <returns>是否符合过滤条件</returns>
        private bool ItemFilter(object obj)
        {
            // 无过滤条件时，全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedItemType == 0 && !SelectedPlanId.HasValue)
            {
                return true;
            }

            // 校验对象类型
            if (obj is MaintenanceItem item)
            {
                // 关键词过滤：匹配项目编码、名称、方法
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (item.ItemCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (item.ItemName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (item.Method?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 项目类型过滤
                bool matchesType = SelectedItemType == 0 || item.ItemType == SelectedItemType;

                // 维护计划过滤
                bool matchesPlan = !SelectedPlanId.HasValue || item.MaintenancePlanId == SelectedPlanId.Value;

                // 满足所有条件则显示
                return matchesKeyword && matchesType && matchesPlan;
            }

            // 非MaintenanceItem类型，不显示
            return false;
        }
        #endregion

        #region 数据加载方法
        /// <summary>
        /// 从服务加载维护项目数据
        /// 并更新到Items集合中
        /// </summary>
        private async Task LoadItemsAsync()
        {
            try
            {
                // 开启加载状态
                IsRefreshing = true;

                // 清空现有数据，避免重复
                Items.Clear();

                // 从服务获取所有维护项目
                var items = await _itemService.GetAllAsync();

                // 将数据添加到可观察集合（触发UI更新）
                foreach (var item in items)
                {
                    Items.Add(item);
                }

                // 更新总数量
                TotalCount = Items.Count;

                // 刷新视图过滤
                ItemsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常提示
                await _dialogService.ShowErrorAsync("错误", $"加载维护项目数据失败: {ex.Message}");
            }
            finally
            {
                // 关闭加载状态
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 从服务加载维护计划数据
        /// 并更新到MaintenancePlans集合中（包含"全部"选项）
        /// </summary>
        private async Task LoadMaintenancePlansAsync()
        {
            try
            {
                // 清空现有数据
                MaintenancePlans.Clear();

                // 添加"全部"选项（ID为0，用于取消计划过滤）
                MaintenancePlans.Add(new EquipmentMaintenancePlan { Id = 0, PlanName = "全部" });

                // 从服务获取所有维护计划
                var plans = await _planService.GetAllAsync();

                // 将数据添加到可观察集合
                foreach (var plan in plans)
                {
                    MaintenancePlans.Add(plan);
                }
            }
            catch (Exception ex)
            {
                // 异常提示
                await _dialogService.ShowErrorAsync("错误", $"加载维护计划数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令方法
        /// <summary>
        /// 刷新维护项目数据的命令
        /// 重新从服务加载最新数据
        /// </summary>
        [RelayCommand]
        private async Task RefreshItems()
        {
            await LoadItemsAsync();
        }

        /// <summary>
        /// 搜索维护项目的命令
        /// 触发视图过滤并重置页码
        /// </summary>
        [RelayCommand]
        private async Task SearchItems()
        {
            try
            {
                IsRefreshing = true;

                // 刷新过滤视图
                ItemsView?.Refresh();

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
        /// 重置搜索条件的命令
        /// 清空所有过滤条件并重新搜索
        /// </summary>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 重置所有过滤条件
            SearchKeyword = string.Empty;
            SelectedItemType = 0;
            SelectedPlanId = null;

            // 重新触发搜索
            await SearchItems();
        }

        /// <summary>
        /// 批量删除维护项目的命令
        /// 删除当前选中的维护项目
        /// </summary>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的维护项目（当前仅支持单选，可扩展为多选）
            var selectedItems = Items.Where(i => i == SelectedItem).ToList();

            // 未选择项目时提示
            if (selectedItems.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的维护项目");
                return;
            }

            // 确认删除
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedItems.Count} 个维护项目吗？此操作不可撤销。");

            if (result)
            {
                try
                {
                    // 遍历删除选中的项目
                    foreach (var item in selectedItems)
                    {
                        await _itemService.DeleteAsync(item);
                        Items.Remove(item);
                    }

                    // 更新总数量
                    TotalCount = Items.Count;
                    await _dialogService.ShowInfoAsync("成功", "维护项目已删除");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除维护项目失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出维护项目数据的命令
        /// 暂未实现具体逻辑
        /// </summary>
        [RelayCommand]
        private async Task ExportItems()
        {
            await _dialogService.ShowInfoAsync("导出", "维护项目导出功能尚未实现");
        }

        /// <summary>
        /// 分页跳转命令
        /// 跳转到指定页码
        /// </summary>
        /// <param name="page">目标页码</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 校验页码合法性
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return;
            }

            // 更新当前页码
            CurrentPage = page;
        }

        /// <summary>
        /// 新增维护项目的命令
        /// 初始化新增对象并打开编辑对话框
        /// </summary>
        [RelayCommand]
        private void AddItem()
        {
            // 切换为新增模式
            IsEditMode = false;
            // 初始化新的维护项目对象
            EditingItem = new MaintenanceItem
            {
                CreateTime = DateTime.Now,
                ItemType = 1, // 默认类型为"检查"
                SequenceNo = 1,
                IsRequired = true,
                ItemCode = GenerateNewItemCode(), // 生成唯一编码
                ItemName = string.Empty
            };

            // 打开编辑对话框
            IsItemDialogOpen = true;
        }

        /// <summary>
        /// 编辑维护项目的命令
        /// 初始化编辑对象并打开编辑对话框
        /// </summary>
        /// <param name="item">待编辑的维护项目</param>
        [RelayCommand]
        private void EditItem(MaintenanceItem? item)
        {
            // 空值校验
            if (item == null) return;

            // 切换为编辑模式
            IsEditMode = true;

            // 复制待编辑对象的属性（避免直接修改原数据）
            EditingItem = new MaintenanceItem
            {
                Id = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemName,
                MaintenancePlanId = item.MaintenancePlanId,
                ItemType = item.ItemType,
                StandardValue = item.StandardValue,
                UpperLimit = item.UpperLimit,
                LowerLimit = item.LowerLimit,
                Unit = item.Unit,
                Method = item.Method,
                Tool = item.Tool,
                SequenceNo = item.SequenceNo,
                IsRequired = item.IsRequired,
                CreateTime = item.CreateTime,
                UpdateTime = item.UpdateTime,
                Remark = item.Remark
            };

            // 打开编辑对话框
            IsItemDialogOpen = true;
        }

        /// <summary>
        /// 删除单个维护项目的命令
        /// </summary>
        /// <param name="item">待删除的维护项目</param>
        [RelayCommand]
        private async Task DeleteItem(MaintenanceItem? item)
        {
            // 空值校验
            if (item == null) return;

            // 确认删除
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除维护项目\"{item.ItemName}\"吗？此操作不可撤销。");

            if (result)
            {
                try
                {
                    // 调用服务删除
                    await _itemService.DeleteAsync(item);
                    // 从集合中移除
                    Items.Remove(item);
                    // 更新总数量
                    TotalCount = Items.Count;
                    await _dialogService.ShowInfoAsync("成功", "维护项目已删除");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除维护项目失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消编辑的命令
        /// 关闭编辑对话框
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsItemDialogOpen = false;
        }

        /// <summary>
        /// 保存维护项目的命令
        /// 根据编辑模式执行新增或更新操作
        /// </summary>
        [RelayCommand]
        private async Task SaveItem()
        {
            // 数据校验：项目名称不能为空
            if (string.IsNullOrWhiteSpace(EditingItem.ItemName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入项目名称");
                return;
            }

            // 数据校验：项目编码不能为空
            if (string.IsNullOrWhiteSpace(EditingItem.ItemCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入项目编码");
                return;
            }

            // 数据校验：必须选择维护计划
            if (EditingItem.MaintenancePlanId <= 0)
            {
                await _dialogService.ShowErrorAsync("错误", "请选择维护计划");
                return;
            }

            try
            {
                if (IsEditMode)
                {
                    // 编辑模式：更新现有项目
                    EditingItem.UpdateTime = DateTime.Now;
                    await _itemService.UpdateAsync(EditingItem);

                    // 更新集合中的数据
                    var existingItem = Items.FirstOrDefault(i => i.Id == EditingItem.Id);
                    if (existingItem != null)
                    {
                        int index = Items.IndexOf(existingItem);
                        Items[index] = EditingItem;
                    }

                    await _dialogService.ShowInfoAsync("成功", "维护项目信息已更新");
                }
                else
                {
                    // 新增模式：创建新项目
                    EditingItem.CreateTime = DateTime.Now;
                    var newItem = await _itemService.AddAsync(EditingItem);

                    // 添加到集合
                    Items.Add(newItem);
                    TotalCount = Items.Count;

                    await _dialogService.ShowInfoAsync("成功", "维护项目已创建");
                }

                // 关闭编辑对话框
                IsItemDialogOpen = false;

                // 刷新视图
                ItemsView?.Refresh();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存维护项目失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 上移维护项目的命令
        /// 调整同计划下项目的排序序号
        /// </summary>
        /// <param name="item">待上移的维护项目</param>
        [RelayCommand]
        private async Task MoveUp(MaintenanceItem? item)
        {
            if (item == null) return;

            // 获取同计划下的所有项目并按序号排序
            var planItems = Items.Where(i => i.MaintenancePlanId == item.MaintenancePlanId)
                                .OrderBy(i => i.SequenceNo)
                                .ToList();

            // 获取当前项目的索引
            int index = planItems.IndexOf(item);
            // 已经是第一个，无法上移
            if (index <= 0) return;

            // 交换与上一个项目的序号
            var prevItem = planItems[index - 1];
            int tempSeq = item.SequenceNo;
            item.SequenceNo = prevItem.SequenceNo;
            prevItem.SequenceNo = tempSeq;

            try
            {
                // 更新数据库
                await _itemService.UpdateAsync(item);
                await _itemService.UpdateAsync(prevItem);

                // 重新加载数据刷新排序
                await LoadItemsAsync();

                // 保持当前项选中状态
                SelectedItem = item;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"移动项目失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 下移维护项目的命令
        /// 调整同计划下项目的排序序号
        /// </summary>
        /// <param name="item">待下移的维护项目</param>
        [RelayCommand]
        private async Task MoveDown(MaintenanceItem? item)
        {
            if (item == null) return;

            // 获取同计划下的所有项目并按序号排序
            var planItems = Items.Where(i => i.MaintenancePlanId == item.MaintenancePlanId)
                                .OrderBy(i => i.SequenceNo)
                                .ToList();

            // 获取当前项目的索引
            int index = planItems.IndexOf(item);
            // 已经是最后一个，无法下移
            if (index >= planItems.Count - 1) return;

            // 交换与下一个项目的序号
            var nextItem = planItems[index + 1];
            int tempSeq = item.SequenceNo;
            item.SequenceNo = nextItem.SequenceNo;
            nextItem.SequenceNo = tempSeq;

            try
            {
                // 更新数据库
                await _itemService.UpdateAsync(item);
                await _itemService.UpdateAsync(nextItem);

                // 重新加载数据刷新排序
                await LoadItemsAsync();

                // 保持当前项选中状态
                SelectedItem = item;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"移动项目失败: {ex.Message}");
            }
        }
        #endregion

        #region 辅助方法
        /// <summary>
        /// 生成新的维护项目编码
        /// 格式：MI + 年月日 + 4位序号
        /// </summary>
        /// <returns>生成的项目编码</returns>
        private string GenerateNewItemCode()
        {
            string dateStr = DateTime.Now.ToString("yyyyMMdd");
            int count = Items.Count + 1;
            return $"MI{dateStr}{count:D4}";
        }
        #endregion
    }
}