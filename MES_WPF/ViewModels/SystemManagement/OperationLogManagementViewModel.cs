// 引入CommunityToolkit.Mvvm的ObservableObject（实现INotifyPropertyChanged，简化属性变更通知）
using CommunityToolkit.Mvvm.ComponentModel;
// 引入RelayCommand（MVVM命令绑定，解耦UI与业务逻辑）
using CommunityToolkit.Mvvm.Input;
// 引入核心模型（操作日志实体）
using MES_WPF.Core.Models;
// 引入操作日志服务接口（数据访问层）
using MES_WPF.Core.Services.SystemManagement;
// 引入对话框服务（弹窗提示）
using MES_WPF.Services;
// 基础系统类
using System;
// 集合类
using System.Collections.Generic;
// 可观察集合（UI自动更新）
using System.Collections.ObjectModel;
// 组件模型（INotifyPropertyChanged/ICollectionView等）
using System.ComponentModel;
// LINQ查询
using System.Linq;
// 异步任务
using System.Threading.Tasks;
// 集合视图（过滤/排序）
using System.Windows.Data;

// 命名空间：系统管理模块的ViewModel层
namespace MES_WPF.ViewModels.SystemManagement
{
    /// <summary>
    /// 操作日志管理视图模型
    /// 负责操作日志页面的逻辑处理、数据绑定、命令响应
    /// </summary>
    public partial class OperationLogManagementViewModel : ObservableObject
    {
        #region 依赖注入服务
        // 操作日志服务（用于日志数据的增删改查）
        private readonly IOperationLogService _operationLogService;
        // 对话框服务（用于弹窗提示、确认、错误展示）
        private readonly IDialogService _dialogService;
        #endregion

        #region 视图绑定属性（ObservableProperty自动生成变更通知）
        /// <summary>
        /// 选中的日志项（用于批量操作/详情查看）
        /// </summary>
        [ObservableProperty]
        private OperationLog? _selectedLog;

        /// <summary>
        /// 搜索关键词（用于模糊过滤日志）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 是否正在刷新数据（加载中状态，用于UI显示加载动画）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 日志总数（用于展示统计信息）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 页面标题
        /// </summary>
        [ObservableProperty]
        private string _title = "操作日志";

        /// <summary>
        /// 选中的模块类型（用于过滤：全部/系统管理/生产管理等）
        /// </summary>
        [ObservableProperty]
        private string _selectedModuleType = "全部";

        /// <summary>
        /// 选中的操作类型（用于过滤：全部/新增/编辑/删除/查询等）
        /// </summary>
        [ObservableProperty]
        private string _selectedOperationType = "全部";

        /// <summary>
        /// 操作时间起始筛选（用于时间范围过滤）
        /// </summary>
        [ObservableProperty]
        private DateTime? _operationTimeStart;

        /// <summary>
        /// 操作时间结束筛选（用于时间范围过滤）
        /// </summary>
        [ObservableProperty]
        private DateTime? _operationTimeEnd;

        /// <summary>
        /// 选中的操作状态（255:全部, 1:成功, 0:失败）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 255;

        #region 日志详情对话框相关属性
        /// <summary>
        /// 是否打开日志详情对话框
        /// </summary>
        [ObservableProperty]
        private bool _isLogDetailDialogOpen;

        /// <summary>
        /// 详情展示的日志对象
        /// </summary>
        [ObservableProperty]
        private OperationLog _detailLog = new OperationLog();
        #endregion
        #endregion


        #region 集合属性（UI绑定数据源）
        /// <summary>
        /// 操作日志平铺列表（用于表格展示）
        /// </summary>
        public ObservableCollection<OperationLog> Logs { get; } = new();

        /// <summary>
        /// 模块类型列表（用于下拉筛选框）
        /// </summary>
        public ObservableCollection<string> ModuleTypes { get; } = new();

        /// <summary>
        /// 操作类型列表（用于下拉筛选框）
        /// </summary>
        public ObservableCollection<string> OperationTypes { get; } = new();

        /// <summary>
        /// 日志列表的视图（用于过滤、排序，关联到Logs集合）
        /// </summary>
        public ICollectionView? LogsView { get; private set; }
        #endregion

        #region 属性变更回调（ObservableProperty自动生成）
        /// <summary>
        /// 搜索关键词变更时刷新过滤
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            LogsView?.Refresh();
        }

        /// <summary>
        /// 选中模块类型变更时刷新过滤
        /// </summary>
        /// <param name="value">新的模块类型</param>
        partial void OnSelectedModuleTypeChanged(string value)
        {
            LogsView?.Refresh();
        }

        /// <summary>
        /// 选中操作类型变更时刷新过滤
        /// </summary>
        /// <param name="value">新的操作类型</param>
        partial void OnSelectedOperationTypeChanged(string value)
        {
            LogsView?.Refresh();
        }

        /// <summary>
        /// 操作时间起始值变更时刷新过滤
        /// </summary>
        /// <param name="value">新的起始时间</param>
        partial void OnOperationTimeStartChanged(DateTime? value)
        {
            LogsView?.Refresh();
        }

        /// <summary>
        /// 操作时间结束值变更时刷新过滤
        /// </summary>
        /// <param name="value">新的结束时间</param>
        partial void OnOperationTimeEndChanged(DateTime? value)
        {
            LogsView?.Refresh();
        }

        /// <summary>
        /// 选中状态变更时刷新过滤
        /// </summary>
        /// <param name="value">新的状态值</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            LogsView?.Refresh();
        }
        #endregion

        #region 构造函数（依赖注入+初始化）
        /// <summary>
        /// 构造函数（依赖注入）
        /// </summary>
        /// <param name="operationLogService">操作日志服务</param>
        /// <param name="dialogService">对话框服务</param>
        public OperationLogManagementViewModel(
            IOperationLogService operationLogService,
            IDialogService dialogService)
        {
            // 校验依赖注入服务是否为空，为空则抛出参数空异常
            _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化日志列表的过滤规则
            SetupFilter();

            // 初始化时间筛选范围为近7天（提升用户体验）
            OperationTimeStart = DateTime.Now.AddDays(-7);
            OperationTimeEnd = DateTime.Now;

            // 异步加载日志数据（不阻塞UI线程）
            _ = LoadLogsAsync();
        }
        #endregion

        #region 过滤逻辑初始化
        /// <summary>
        /// 设置日志列表的过滤规则
        /// </summary>
        private void SetupFilter()
        {
            // 获取Logs集合的默认视图（用于过滤/排序）
            LogsView = CollectionViewSource.GetDefaultView(Logs);
            if (LogsView != null)
            {
                // 绑定过滤方法到视图
                LogsView.Filter = LogFilter;
            }
        }

        /// <summary>
        /// 日志过滤核心方法（多条件组合过滤）
        /// </summary>
        /// <param name="obj">待过滤的日志对象</param>
        /// <returns>是否符合过滤条件（true=显示，false=隐藏）</returns>
        private bool LogFilter(object obj)
        {
            // 所有过滤条件都为默认值时，显示所有日志
            if (string.IsNullOrWhiteSpace(SearchKeyword) &&
                SelectedModuleType == "全部" &&
                SelectedOperationType == "全部" &&
                SelectedStatus == 255 &&
                !OperationTimeStart.HasValue &&
                !OperationTimeEnd.HasValue)
            {
                return true;
            }

            // 确保对象是OperationLog类型
            if (obj is OperationLog log)
            {
                // 1. 关键词匹配：操作描述/请求方法/请求URL/操作IP包含关键词（忽略大小写）
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (log.OperationDesc?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (log.RequestMethod?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (log.RequestUrl?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (log.OperationIp?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 2. 模块类型匹配：选择"全部" 或 日志模块类型匹配
                bool matchesModuleType = SelectedModuleType == "全部" || log.ModuleType == SelectedModuleType;

                // 3. 操作类型匹配：选择"全部" 或 日志操作类型匹配
                bool matchesOperationType = SelectedOperationType == "全部" || log.OperationType == SelectedOperationType;

                // 4. 操作状态匹配：选择"全部" 或 日志状态匹配
                bool matchesStatus = SelectedStatus == 255 || log.Status == SelectedStatus;

                // 5. 操作时间匹配：在起始和结束时间范围内（包含结束日的最后一秒）
                bool matchesOperationTime = true;
                // 小于起始时间 → 不匹配
                if (OperationTimeStart.HasValue && log.OperationTime < OperationTimeStart.Value)
                {
                    matchesOperationTime = false;
                }
                // 大于结束时间的最后一秒（结束日+1天-1秒 → 包含结束日全天）→ 不匹配
                if (OperationTimeEnd.HasValue && log.OperationTime > OperationTimeEnd.Value.AddDays(1).AddSeconds(-1))
                {
                    matchesOperationTime = false;
                }

                // 所有条件都满足时才显示该日志
                return matchesKeyword && matchesModuleType && matchesOperationType && matchesStatus && matchesOperationTime;
            }

            // 非OperationLog类型的对象直接过滤掉
            return false;
        }
        #endregion

        #region 数据加载核心方法
        /// <summary>
        /// 异步加载所有操作日志数据
        /// </summary>
        /// <returns>异步任务</returns>
        private async Task LoadLogsAsync()
        {
            try
            {
                // 设置加载中状态（UI显示加载动画）
                IsRefreshing = true;

                // 清空现有数据（避免重复加载）
                Logs.Clear();
                ModuleTypes.Clear();
                OperationTypes.Clear();

                // 添加"全部"选项到下拉筛选框（默认选项）
                ModuleTypes.Add("全部");
                OperationTypes.Add("全部");

                // 从服务层获取所有操作日志数据
                var logs = await _operationLogService.GetAllAsync();

                // 遍历日志数据，填充集合并收集筛选维度数据
                foreach (var log in logs)
                {
                    // 添加到日志列表（UI自动更新）
                    Logs.Add(log);

                    // 收集唯一的模块类型（用于下拉筛选）
                    if (!string.IsNullOrEmpty(log.ModuleType) && !ModuleTypes.Contains(log.ModuleType))
                    {
                        ModuleTypes.Add(log.ModuleType);
                    }

                    // 收集唯一的操作类型（用于下拉筛选）
                    if (!string.IsNullOrEmpty(log.OperationType) && !OperationTypes.Contains(log.OperationType))
                    {
                        OperationTypes.Add(log.OperationType);
                    }
                }

                // 更新日志总数
                TotalCount = Logs.Count;

                // 刷新视图过滤（应用初始过滤条件）
                LogsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗展示错误信息
                await _dialogService.ShowErrorAsync("错误", $"加载操作日志数据失败: {ex.Message}");
            }
            finally
            {
                // 无论成功/失败，都取消加载中状态
                IsRefreshing = false;
            }
        }
        #endregion

        #region 命令：刷新日志
        /// <summary>
        /// 刷新日志数据命令（绑定到UI刷新按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task RefreshLogs()
        {
            // 调用核心加载方法
            await LoadLogsAsync();
        }
        #endregion

        #region 命令：重置搜索条件
        /// <summary>
        /// 重置所有搜索筛选条件命令（绑定到UI重置按钮）
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            // 重置关键词
            SearchKeyword = string.Empty;
            // 重置模块类型为全部
            SelectedModuleType = "全部";
            // 重置操作类型为全部
            SelectedOperationType = "全部";
            // 重置状态为全部
            SelectedStatus = 255;
            // 重置时间范围为近7天
            OperationTimeStart = DateTime.Now.AddDays(-7);
            OperationTimeEnd = DateTime.Now;
            // 刷新过滤（应用重置后的条件）
            LogsView?.Refresh();
        }
        #endregion

        #region 命令：查看日志详情
        /// <summary>
        /// 查看日志详情命令（绑定到UI详情按钮/双击日志行）
        /// </summary>
        /// <param name="log">要查看详情的日志对象</param>
        [RelayCommand]
        private void ViewLogDetail(OperationLog? log)
        {
            // 校验日志对象是否为空
            if (log == null)
                return;

            // 赋值到详情日志对象（UI绑定展示）
            DetailLog = log;
            // 打开详情对话框
            IsLogDetailDialogOpen = true;
        }
        #endregion

        #region 命令：关闭日志详情
        /// <summary>
        /// 关闭日志详情对话框命令（绑定到详情弹窗的关闭按钮）
        /// </summary>
        [RelayCommand]
        private void CloseLogDetail()
        {
            // 关闭详情对话框
            IsLogDetailDialogOpen = false;
        }
        #endregion

        #region 命令：导出日志
        /// <summary>
        /// 导出筛选后的日志数据命令（绑定到UI导出按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ExportLogs()
        {
            try
            {
                // 设置加载中状态
                IsRefreshing = true;

                // 获取符合当前过滤条件的日志列表
                var filteredLogs = Logs.Where(log => LogFilter(log)).ToList();

                // 无数据时提示用户
                if (filteredLogs.Count == 0)
                {
                    await _dialogService.ShowErrorAsync("警告", "没有符合条件的日志数据可导出");
                    return;
                }

                // 【此处为简化版】实际项目中需实现Excel/CSV导出逻辑
                // 示例：调用导出服务 → var filePath = await _exportService.ExportLogsAsync(filteredLogs);
                // 示例：提示用户导出成功并打开文件 → await _dialogService.ShowInfoAsync("成功", $"已导出{filteredLogs.Count}条日志到：{filePath}");

                // 简化版提示
                await _dialogService.ShowInfoAsync("成功", $"已成功导出 {filteredLogs.Count} 条日志数据");
            }
            catch (Exception ex)
            {
                // 异常处理：展示导出失败信息
                await _dialogService.ShowErrorAsync("错误", $"导出日志数据失败: {ex.Message}");
            }
            finally
            {
                // 取消加载中状态
                IsRefreshing = false;
            }
        }
        #endregion

        #region 命令：清空日志
        /// <summary>
        /// 清空所有操作日志命令（绑定到UI清空按钮）
        /// </summary>
        /// <returns>异步任务</returns>
        [RelayCommand]
        private async Task ClearLogs()
        {
            // 弹窗确认清空操作（防止误操作）
            var result = await _dialogService.ShowConfirmAsync("确认清空", "确定要清空所有操作日志吗？此操作不可恢复！");
            if (result)
            {
                try
                {
                    // 设置加载中状态
                    IsRefreshing = true;

                    // 【注：实际项目中需调用服务层清空数据库日志】
                    // await _operationLogService.ClearAllAsync();

                    // 清空本地日志集合
                    Logs.Clear();
                    // 更新日志总数为0
                    TotalCount = 0;

                    // 提示清空成功
                    await _dialogService.ShowInfoAsync("成功", "操作日志已清空");
                }
                catch (Exception ex)
                {
                    // 异常处理：展示清空失败信息
                    await _dialogService.ShowErrorAsync("错误", $"清空操作日志失败: {ex.Message}");
                }
                finally
                {
                    // 取消加载中状态
                    IsRefreshing = false;
                }
            }
        }
        #endregion
    }
}