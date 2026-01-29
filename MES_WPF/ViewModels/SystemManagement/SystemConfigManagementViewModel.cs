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
    /// 系统配置管理视图模型（MVVM模式）
    /// 核心职责：封装系统配置的查询、新增、编辑、删除、状态切换等业务逻辑，适配UI层数据绑定
    /// 技术要点：CommunityToolkit.Mvvm（ObservableObject/RelayCommand）、ICollectionView（数据筛选）、异步操作（避免UI阻塞）
    /// </summary>
    public partial class SystemConfigManagementViewModel : ObservableObject
    {
        #region 依赖注入服务（构造函数注入）
        /// <summary>
        /// 系统配置业务服务（封装数据访问和业务规则）
        /// </summary>
        private readonly ISystemConfigService _systemConfigService;

        /// <summary>
        /// 对话框服务（统一弹窗交互：提示、确认、错误）
        /// </summary>
        private readonly IDialogService _dialogService;
        #endregion

        #region 可绑定属性（ObservableProperty自动生成INotifyPropertyChanged）
        /// <summary>
        /// 当前选中的系统配置项（UI列表选中项绑定）
        /// </summary>
        [ObservableProperty]
        private SystemConfig? _selectedConfig;

        /// <summary>
        /// 搜索关键词（支持配置键/名称/值/备注模糊匹配）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 是否正在刷新数据（控制UI加载动画显示）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 配置总条数（UI显示统计用）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 页面标题（UI标题栏绑定）
        /// </summary>
        [ObservableProperty]
        private string _title = "系统配置";

        /// <summary>
        /// 选中的配置类型（用于分类筛选，默认"全部"）
        /// </summary>
        [ObservableProperty]
        private string _selectedConfigType = "全部";

        // 配置编辑/新增对话框相关属性
        /// <summary>
        /// 配置对话框是否打开（控制弹窗显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isConfigDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true=编辑现有配置，false=新增配置）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑/新增的配置对象（对话框表单绑定）
        /// </summary>
        [ObservableProperty]
        private SystemConfig _editingConfig = new SystemConfig();

        /// <summary>
        /// 系统配置列表数据源（ObservableCollection支持UI自动更新）
        /// </summary>
        public ObservableCollection<SystemConfig> Configs { get; } = new();

        /// <summary>
        /// 配置类型列表（用于筛选下拉框绑定，自动从配置数据中收集）
        /// </summary>
        public ObservableCollection<string> ConfigTypes { get; } = new();

        /// <summary>
        /// 配置列表的视图包装（用于数据筛选、排序，绑定UI列表控件）
        /// </summary>
        public ICollectionView? ConfigsView { get; private set; }
        #endregion

        #region 属性变更回调（Partial方法，由ObservableProperty自动触发）
        /// <summary>
        /// 搜索关键词变更时触发：刷新筛选视图
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            ConfigsView?.Refresh();
        }

        /// <summary>
        /// 选中配置类型变更时触发：刷新筛选视图
        /// </summary>
        /// <param name="value">新的配置类型</param>
        partial void OnSelectedConfigTypeChanged(string value)
        {
            ConfigsView?.Refresh();
        }
        #endregion

        #region 构造函数（初始化依赖+配置筛选+加载数据）
        /// <summary>
        /// 构造函数：注入服务，初始化筛选器，加载配置数据
        /// </summary>
        /// <param name="systemConfigService">系统配置服务</param>
        /// <param name="dialogService">对话框服务</param>
        /// <exception cref="ArgumentNullException">服务注入为空时抛出</exception>
        public SystemConfigManagementViewModel(
            ISystemConfigService systemConfigService,
            IDialogService dialogService)
        {
            // 空值校验：确保依赖服务注入成功
            _systemConfigService = systemConfigService ?? throw new ArgumentNullException(nameof(systemConfigService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化数据筛选器（绑定ConfigsView的过滤规则）
            SetupFilter();

            // 异步加载配置数据（_ = 不等待，避免阻塞构造函数）
            _ = LoadConfigsAsync();
        }
        #endregion

        #region 筛选器初始化（核心：绑定ICollectionView的过滤规则）
        /// <summary>
        /// 初始化数据筛选器：将ConfigsView与Configs绑定，并设置过滤逻辑
        /// ICollectionView作用：在内存中对集合进行筛选/排序，不修改原集合
        /// </summary>
        private void SetupFilter()
        {
            // 获取Configs集合的默认视图（WPF内置的视图包装）
            ConfigsView = CollectionViewSource.GetDefaultView(Configs);
            if (ConfigsView != null)
            {
                // 绑定过滤方法：每次Refresh时执行ConfigFilter
                ConfigsView.Filter = ConfigFilter;
            }
        }

        /// <summary>
        /// 配置数据过滤规则（核心筛选逻辑）
        /// 支持：关键词模糊匹配（键/名称/值/备注）+ 配置类型精准匹配
        /// </summary>
        /// <param name="obj">待筛选的配置对象</param>
        /// <returns>true=符合筛选条件，false=不符合</returns>
        private bool ConfigFilter(object obj)
        {
            // 无筛选条件时，显示所有数据
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedConfigType == "全部")
            {
                return true;
            }

            // 类型校验：确保筛选对象是SystemConfig
            if (obj is SystemConfig config)
            {
                // 关键词筛选：忽略大小写，支持多字段匹配
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (config.ConfigKey?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (config.ConfigName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (config.ConfigValue?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                                     (config.Remark?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);

                // 配置类型筛选："全部"匹配所有，否则精准匹配类型
                bool matchesType = SelectedConfigType == "全部" || config.ConfigType == SelectedConfigType;

                // 双重条件满足时返回true
                return matchesKeyword && matchesType;
            }

            // 非SystemConfig类型直接过滤
            return false;
        }
        #endregion

        #region 数据加载（核心：异步获取配置+更新UI集合）
        /// <summary>
        /// 异步加载所有系统配置数据
        /// 步骤：1. 调用服务获取数据 2. 收集配置类型 3. UI线程更新集合 4. 刷新筛选视图
        /// </summary>
        private async Task LoadConfigsAsync()
        {
            try
            {
                // 开启加载状态（UI显示加载动画）
                IsRefreshing = true;

                // 调用服务层获取所有配置（异步操作，不阻塞UI）
                var configs = await _systemConfigService.GetAllAsync();

                // 用HashSet收集配置类型：自动去重，初始包含"全部"
                var configTypesSet = new HashSet<string> { "全部" };

                // UI线程更新集合：WPF中集合操作必须在UI线程执行
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空旧数据，避免重复
                    Configs.Clear();
                    ConfigTypes.Clear();

                    // 初始化类型列表：先添加"全部"
                    ConfigTypes.Add("全部");
                    SelectedConfigType = "全部";

                    // 遍历配置数据，填充列表+收集类型
                    foreach (var config in configs)
                    {
                        Configs.Add(config);

                        // 收集非空且未重复的配置类型
                        if (!string.IsNullOrEmpty(config.ConfigType) && !configTypesSet.Contains(config.ConfigType))
                        {
                            configTypesSet.Add(config.ConfigType);
                            ConfigTypes.Add(config.ConfigType);
                        }
                    }

                    // 更新总条数
                    TotalCount = Configs.Count;

                    // 刷新筛选视图，确保过滤规则生效
                    ConfigsView?.Refresh();
                });
            }
            catch (Exception ex)
            {
                // 异常提示：通过对话框服务统一展示
                await _dialogService.ShowErrorAsync("错误", $"加载系统配置数据失败: {ex.Message}");
            }
            finally
            {
                // 关闭加载状态（无论成功/失败都执行）
                IsRefreshing = false;
            }
        }
        #endregion

        #region 命令：刷新/重置搜索（RelayCommand绑定UI按钮）
        /// <summary>
        /// 刷新配置数据：清空筛选条件+重新加载所有数据
        /// </summary>
        [RelayCommand]
        private async Task RefreshConfigs()
        {
            // UI线程重置筛选条件（避免跨线程操作）
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SearchKeyword = string.Empty;
                SelectedConfigType = "全部";
            });

            // 重新加载数据
            await LoadConfigsAsync();
        }

        /// <summary>
        /// 重置搜索条件：清空关键词+恢复类型为"全部"+刷新视图
        /// 区别于RefreshConfigs：仅重置筛选，不重新请求数据
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SearchKeyword = string.Empty;
                SelectedConfigType = "全部";

                // 刷新视图，显示所有数据
                ConfigsView?.Refresh();
            });
        }
        #endregion

        #region 命令：新增/编辑/取消配置（对话框操作）
        /// <summary>
        /// 新增配置：初始化默认值+打开对话框（新增模式）
        /// </summary>
        [RelayCommand]
        private void AddConfig()
        {
            // 初始化新增配置的默认值
            EditingConfig = new SystemConfig
            {
                Status = 1,          // 默认启用
                CreateTime = DateTime.Now, // 创建时间
                CreateBy = 1,        // 临时用户ID（实际应从认证服务获取）
                IsSystem = false     // 非系统配置（系统配置不可删除）
            };

            IsEditMode = false;     // 标记为新增模式
            IsConfigDialogOpen = true; // 打开对话框
        }

        /// <summary>
        /// 编辑配置：深拷贝选中配置+打开对话框（编辑模式）
        /// 深拷贝目的：避免直接修改列表中的原始数据，提交后再更新
        /// </summary>
        /// <param name="config">待编辑的配置项（UI选中项）</param>
        [RelayCommand]
        private void EditConfig(SystemConfig? config)
        {
            // 空值校验：未选中配置时不执行
            if (config == null)
                return;

            // 深拷贝选中的配置到编辑对象
            EditingConfig = new SystemConfig
            {
                Id = config.Id,
                ConfigKey = config.ConfigKey,
                ConfigValue = config.ConfigValue,
                ConfigName = config.ConfigName,
                ConfigType = config.ConfigType,
                IsSystem = config.IsSystem,
                Status = config.Status,
                CreateBy = config.CreateBy,
                CreateTime = config.CreateTime,
                UpdateTime = DateTime.Now, // 更新时间戳
                Remark = config.Remark
            };

            IsEditMode = true;      // 标记为编辑模式
            IsConfigDialogOpen = true; // 打开对话框
        }

        /// <summary>
        /// 取消编辑/新增：关闭对话框（不保存数据）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsConfigDialogOpen = false;
        }
        #endregion

        #region 命令：保存配置（新增/编辑共用）
        /// <summary>
        /// 保存配置：表单校验 → 区分新增/编辑 → 调用服务 → 更新UI
        /// 核心逻辑：新增时校验配置键唯一性，编辑时更新现有数据
        /// </summary>
        [RelayCommand]
        private async Task SaveConfig()
        {
            try
            {
                // 表单必填项校验（前端基础校验）
                if (string.IsNullOrWhiteSpace(EditingConfig.ConfigKey) ||
                    string.IsNullOrWhiteSpace(EditingConfig.ConfigValue) ||
                    string.IsNullOrWhiteSpace(EditingConfig.ConfigName) ||
                    string.IsNullOrWhiteSpace(EditingConfig.ConfigType))
                {
                    await _dialogService.ShowErrorAsync("错误", "配置键、配置值、配置名称和配置类型不能为空");
                    return;
                }

                // 编辑模式：更新现有配置
                if (IsEditMode)
                {
                    // 更新时间戳
                    EditingConfig.UpdateTime = DateTime.Now;
                    // 调用服务更新数据库
                    await _systemConfigService.UpdateAsync(EditingConfig);

                    // UI列表更新：替换原始数据
                    var existingConfig = Configs.FirstOrDefault(c => c.Id == EditingConfig.Id);
                    if (existingConfig != null)
                    {
                        existingConfig.ConfigKey = EditingConfig.ConfigKey;
                        existingConfig.ConfigValue = EditingConfig.ConfigValue;
                        existingConfig.ConfigName = EditingConfig.ConfigName;
                        existingConfig.ConfigType = EditingConfig.ConfigType;
                        existingConfig.Status = EditingConfig.Status;
                        existingConfig.UpdateTime = EditingConfig.UpdateTime;
                        existingConfig.Remark = EditingConfig.Remark;
                    }

                    await _dialogService.ShowInfoAsync("成功", "系统配置更新成功");
                }
                // 新增模式：创建新配置
                else
                {
                    // 配置键唯一性校验（避免重复）
                    var existingConfig = Configs.FirstOrDefault(c => c.ConfigKey.Equals(EditingConfig.ConfigKey, StringComparison.OrdinalIgnoreCase));
                    if (existingConfig != null)
                    {
                        await _dialogService.ShowErrorAsync("错误", "配置键已存在");
                        return;
                    }

                    // 调用服务新增配置（数据库层面）
                    var newConfig = await _systemConfigService.AddAsync(EditingConfig);

                    // UI列表更新：添加新配置
                    Configs.Add(newConfig);
                    TotalCount = Configs.Count;

                    // 新增配置类型：若为新类型，添加到类型列表
                    if (!string.IsNullOrEmpty(newConfig.ConfigType) && !ConfigTypes.Contains(newConfig.ConfigType))
                    {
                        ConfigTypes.Add(newConfig.ConfigType);
                    }

                    await _dialogService.ShowInfoAsync("成功", "系统配置添加成功");
                }

                // 关闭对话框
                IsConfigDialogOpen = false;

                // 刷新筛选视图，确保新数据生效
                ConfigsView?.Refresh();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存系统配置失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令：删除/切换配置状态（业务规则：系统配置不可删除）
        /// <summary>
        /// 删除配置：校验系统配置 → 确认删除 → 调用服务 → 更新UI
        /// 核心规则：IsSystem=true的配置不可删除（防止误删系统核心配置）
        /// </summary>
        /// <param name="config">待删除的配置项</param>
        [RelayCommand]
        private async Task DeleteConfig(SystemConfig? config)
        {
            if (config == null)
                return;

            // 系统配置保护：禁止删除系统级配置
            if (config.IsSystem)
            {
                await _dialogService.ShowErrorAsync("警告", "系统配置不允许删除");
                return;
            }

            // 确认删除：避免误操作
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除配置\"{config.ConfigName}\"吗？");
            if (result)
            {
                try
                {
                    // 调用服务删除数据库中的配置
                    await _systemConfigService.DeleteByIdAsync(config.Id);

                    // UI列表更新：移除配置项
                    Configs.Remove(config);
                    TotalCount = Configs.Count;

                    // 更新配置类型列表（删除后可能有类型无数据，需清理）
                    UpdateConfigTypes();

                    await _dialogService.ShowInfoAsync("成功", "系统配置删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除系统配置失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 更新配置类型列表：删除配置后，重新收集现有配置的类型（清理空类型）
        /// </summary>
        private void UpdateConfigTypes()
        {
            // 清空类型列表，保留"全部"
            ConfigTypes.Clear();
            ConfigTypes.Add("全部");

            // 重新收集现有配置的类型
            foreach (var config in Configs)
            {
                if (!string.IsNullOrEmpty(config.ConfigType) && !ConfigTypes.Contains(config.ConfigType))
                {
                    ConfigTypes.Add(config.ConfigType);
                }
            }
        }

        /// <summary>
        /// 切换配置状态：启用/禁用（Status：1=启用，0=禁用）
        /// 异常处理：更新失败时恢复原状态，保证数据一致性
        /// </summary>
        /// <param name="config">待切换状态的配置项</param>
        [RelayCommand]
        private async Task ToggleConfigStatus(SystemConfig? config)
        {
            if (config == null)
                return;

            try
            {
                // 临时保存原状态（用于异常恢复）
                var originalStatus = config.Status;

                // 切换状态：1↔0
                config.Status = config.Status == 1 ? (byte)0 : (byte)1;
                config.UpdateTime = DateTime.Now;

                // 调用服务更新数据库
                await _systemConfigService.UpdateAsync(config);

                // 刷新视图，更新状态显示
                ConfigsView?.Refresh();

                // 提示操作成功
                await _dialogService.ShowInfoAsync("成功", $"系统配置已{(config.Status == 1 ? "启用" : "禁用")}");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"更新系统配置状态失败: {ex.Message}");

                // 异常恢复：还原状态
                config.Status = config.Status == 1 ? (byte)0 : (byte)1;
            }
        }
        #endregion
    }
}