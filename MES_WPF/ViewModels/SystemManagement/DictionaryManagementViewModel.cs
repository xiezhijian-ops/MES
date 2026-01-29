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
    /// 字典管理模块ViewModel（MVVM架构的核心层）
    /// 职责：封装字典管理的所有业务逻辑、数据状态、UI交互命令
    /// 设计：基于CommunityToolkit.Mvvm实现，通过ObservableProperty自动通知UI更新，RelayCommand绑定UI命令
    /// 依赖：字典服务、字典项服务、弹窗服务（通过构造函数注入解耦）
    /// </summary>
    public partial class DictionaryManagementViewModel : ObservableObject
    {
        #region 依赖服务
        /// <summary>
        /// 字典主表业务服务（封装字典CRUD及关联操作）
        /// </summary>
        private readonly IDictionaryService _dictionaryService;

        /// <summary>
        /// 字典项子表业务服务（封装字典项CRUD及排序操作）
        /// </summary>
        private readonly IDictionaryItemService _dictionaryItemService;

        /// <summary>
        /// 弹窗服务（封装提示框、确认框、错误框等UI交互）
        /// </summary>
        private readonly IDialogService _dialogService;
        #endregion

        #region 核心状态属性（ObservableProperty自动触发UI更新）
        /// <summary>
        /// 左侧选中的字典（双向绑定UI选中项）
        /// 触发逻辑：选中变化时自动加载对应字典项（OnSelectedDictionaryChanged）
        /// </summary>
        [ObservableProperty]
        private Dictionary? _selectedDictionary;

        /// <summary>
        /// 搜索关键词（绑定搜索框输入）
        /// 触发逻辑：值变化时自动刷新字典过滤结果（OnSearchKeywordChanged）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty;

        /// <summary>
        /// 数据加载状态（绑定加载动画显隐）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 字典总数（绑定UI统计展示）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 页面标题（绑定UI标题栏）
        /// </summary>
        [ObservableProperty]
        private string _title = "字典管理";
        #endregion

        #region 字典弹窗状态属性
        /// <summary>
        /// 字典新增/编辑弹窗显隐状态
        /// </summary>
        [ObservableProperty]
        private bool _isDictionaryDialogOpen;

        /// <summary>
        /// 字典操作模式（true=编辑，false=新增）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 弹窗编辑的字典实体（绑定弹窗表单）
        /// </summary>
        [ObservableProperty]
        private Dictionary _editingDictionary = new Dictionary();
        #endregion

        #region 字典项弹窗状态属性
        /// <summary>
        /// 字典项新增/编辑弹窗显隐状态
        /// </summary>
        [ObservableProperty]
        private bool _isDictItemDialogOpen;

        /// <summary>
        /// 弹窗编辑的字典项实体（绑定弹窗表单）
        /// </summary>
        [ObservableProperty]
        private DictionaryItem _editingDictItem = new DictionaryItem();

        /// <summary>
        /// 字典项操作模式（true=编辑，false=新增）
        /// </summary>
        [ObservableProperty]
        private bool _isDictItemEditMode;

        /// <summary>
        /// 右侧选中的字典项（双向绑定UI选中项）
        /// </summary>
        [ObservableProperty]
        private DictionaryItem? _selectedDictItem;
        #endregion

        #region 数据集合（ObservableCollection自动通知UI集合变化）
        /// <summary>
        /// 字典主表集合（绑定左侧字典列表）
        /// </summary>
        public ObservableCollection<Dictionary> Dictionaries { get; } = new();

        /// <summary>
        /// 字典项子表集合（绑定右侧字典项列表）
        /// </summary>
        public ObservableCollection<DictionaryItem> DictionaryItems { get; } = new();

        /// <summary>
        /// 字典集合视图（用于过滤、排序，绑定左侧列表）
        /// </summary>
        public ICollectionView? DictionariesView { get; private set; }

        /// <summary>
        /// 字典项集合视图（用于过滤、排序，绑定右侧列表）
        /// </summary>
        public ICollectionView? DictionaryItemsView { get; private set; }
        #endregion

        #region 属性变更回调（CommunityToolkit.Mvvm自动生成）
        /// <summary>
        /// 搜索关键词变更回调：刷新字典过滤结果
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            DictionariesView?.Refresh(); // 触发过滤器重新执行
        }

        /// <summary>
        /// 选中字典变更回调：加载对应字典项
        /// </summary>
        /// <param name="value">新选中的字典（null=取消选中）</param>
        partial void OnSelectedDictionaryChanged(Dictionary? value)
        {
            if (value != null)
            {
                // 选中字典时加载其下的字典项（异步执行，不阻塞UI）
                _ = LoadDictionaryItemsAsync(value.Id);
            }
            else
            {
                // 取消选中时清空字典项列表
                DictionaryItems.Clear();
            }
        }
        #endregion

        #region 构造函数（初始化依赖+数据+过滤器）
        /// <summary>
        /// 构造函数：通过依赖注入初始化服务，设置过滤器，加载初始数据
        /// </summary>
        /// <param name="dictionaryService">字典服务</param>
        /// <param name="dictionaryItemService">字典项服务</param>
        /// <param name="dialogService">弹窗服务</param>
        /// <exception cref="ArgumentNullException">服务为空时抛出</exception>
        public DictionaryManagementViewModel(
            IDictionaryService dictionaryService,
            IDictionaryItemService dictionaryItemService,
            IDialogService dialogService)
        {
            // 空值校验，确保依赖服务有效
            _dictionaryService = dictionaryService ?? throw new ArgumentNullException(nameof(dictionaryService));
            _dictionaryItemService = dictionaryItemService ?? throw new ArgumentNullException(nameof(dictionaryItemService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化集合视图过滤器
            SetupFilters();

            // 加载字典主表数据（异步执行，不阻塞UI初始化）
            _ = LoadDictionariesAsync();
        }
        #endregion

        #region 过滤器初始化
        /// <summary>
        /// 初始化集合视图过滤器（字典搜索过滤、字典项默认排序）
        /// </summary>
        private void SetupFilters()
        {
            // 初始化字典集合视图：绑定到Dictionaries集合，用于搜索过滤
            DictionariesView = CollectionViewSource.GetDefaultView(Dictionaries);
            if (DictionariesView != null)
            {
                DictionariesView.Filter = DictionaryFilter; // 设置字典过滤规则
            }

            // 初始化字典项集合视图：绑定到DictionaryItems集合，默认按SortOrder排序
            DictionaryItemsView = CollectionViewSource.GetDefaultView(DictionaryItems);
        }

        /// <summary>
        /// 字典过滤规则（根据搜索关键词匹配名称/类型/备注）
        /// </summary>
        /// <param name="obj">待过滤的字典实体</param>
        /// <returns>true=显示，false=隐藏</returns>
        private bool DictionaryFilter(object obj)
        {
            // 无搜索关键词时显示所有字典
            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                return true;
            }

            // 匹配字典名称/类型/备注（忽略大小写）
            if (obj is Dictionary dictionary)
            {
                return (dictionary.DictName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (dictionary.DictType?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                       (dictionary.Remark?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            // 非字典实体时隐藏
            return false;
        }
        #endregion

        #region 数据加载核心方法
        /// <summary>
        /// 加载所有字典主表数据（初始化/刷新时调用）
        /// 特性：异步执行+UI线程更新集合+加载状态管理+异常处理
        /// </summary>
        private async Task LoadDictionariesAsync()
        {
            try
            {
                // 开启加载状态（显示加载动画）
                IsRefreshing = true;

                // 调用服务层获取所有字典（异步数据库查询）
                var dictionaries = await _dictionaryService.GetAllAsync();

                // UI集合必须在UI线程更新，通过Dispatcher切换线程
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空旧数据，避免重复
                    Dictionaries.Clear();

                    // 添加新数据到集合
                    foreach (var dictionary in dictionaries)
                    {
                        Dictionaries.Add(dictionary);
                    }

                    // 更新字典总数
                    TotalCount = Dictionaries.Count;

                    // 刷新过滤视图，确保搜索条件生效
                    DictionariesView?.Refresh();
                });
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载字典数据失败: {ex.Message}");
            }
            finally
            {
                // 关闭加载状态（隐藏加载动画）
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 加载指定字典下的所有字典项（选中字典时调用）
        /// </summary>
        /// <param name="dictId">字典主键ID</param>
        private async Task LoadDictionaryItemsAsync(int dictId)
        {
            try
            {
                // 调用服务层获取字典项（按字典ID查询）
                var items = await _dictionaryItemService.GetByDictIdAsync(dictId);

                // UI线程更新集合
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空旧数据
                    DictionaryItems.Clear();

                    // 添加新数据（按排序号升序）
                    foreach (var item in items.OrderBy(i => i.SortOrder))
                    {
                        DictionaryItems.Add(item);
                    }

                    // 刷新字典项视图
                    DictionaryItemsView?.Refresh();
                });
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"加载字典项数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 字典操作命令（RelayCommand绑定UI按钮）
        /// <summary>
        /// 刷新字典数据命令（绑定刷新按钮/搜索按钮）
        /// 逻辑：清空搜索条件→重新加载字典→刷新字典项（若有选中）
        /// </summary>
        [RelayCommand]
        private async Task RefreshDictionaries()
        {
            // 清除搜索条件，显示所有字典
            SearchKeyword = string.Empty;

            // 重新加载字典主表
            await LoadDictionariesAsync();

            // 若有选中字典，同步刷新其字典项
            if (SelectedDictionary != null)
            {
                await LoadDictionaryItemsAsync(SelectedDictionary.Id);
            }
        }

        /// <summary>
        /// 重置搜索命令（绑定重置按钮）
        /// 逻辑：清空搜索关键词→刷新过滤视图
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            SearchKeyword = string.Empty;
            DictionariesView?.Refresh(); // 显示所有字典
        }

        /// <summary>
        /// 新增字典命令（绑定"+新增字典"按钮）
        /// 逻辑：初始化空字典→设置新增模式→打开弹窗
        /// </summary>
        [RelayCommand]
        private void AddDictionary()
        {
            // 初始化新增字典实体（默认值）
            EditingDictionary = new Dictionary
            {
                DictType = "DICT_", // 字典类型前缀，提示用户规范命名
                Status = 1, // 默认启用
                CreateTime = DateTime.Now, // 自动填充创建时间
                CreateBy = 1 // 临时用户ID，实际应从认证服务获取
            };

            // 设置为新增模式
            IsEditMode = false;
            // 打开字典弹窗
            IsDictionaryDialogOpen = true;
        }

        /// <summary>
        /// 编辑字典命令（绑定字典列表"编辑"按钮）
        /// </summary>
        /// <param name="dictionary">待编辑的字典（UI传参）</param>
        [RelayCommand]
        private void EditDictionary(Dictionary? dictionary)
        {
            // 空值校验：未选中字典时不执行
            if (dictionary == null)
                return;

            // 复制选中字典数据到编辑实体（避免直接修改原集合数据）
            EditingDictionary = new Dictionary
            {
                Id = dictionary.Id,
                DictType = dictionary.DictType,
                DictName = dictionary.DictName,
                Status = dictionary.Status,
                CreateBy = dictionary.CreateBy,
                CreateTime = dictionary.CreateTime,
                UpdateTime = DateTime.Now, // 自动填充更新时间
                Remark = dictionary.Remark
            };

            // 设置为编辑模式
            IsEditMode = true;
            // 打开字典弹窗
            IsDictionaryDialogOpen = true;
        }

        /// <summary>
        /// 取消字典编辑命令（绑定弹窗"取消"按钮）
        /// 逻辑：关闭字典弹窗
        /// </summary>
        [RelayCommand]
        private void CancelDictEdit()
        {
            IsDictionaryDialogOpen = false;
        }

        /// <summary>
        /// 保存字典命令（绑定弹窗"保存"按钮）
        /// 逻辑：校验→新增/更新→更新集合→关闭弹窗→提示结果
        /// </summary>
        [RelayCommand]
        private async Task SaveDictionary()
        {
            try
            {
                // 表单校验：字典类型和名称不能为空
                if (string.IsNullOrWhiteSpace(EditingDictionary.DictType) ||
                    string.IsNullOrWhiteSpace(EditingDictionary.DictName))
                {
                    await _dialogService.ShowErrorAsync("错误", "字典类型和字典名称不能为空");
                    return;
                }

                if (IsEditMode)
                {
                    // 编辑模式：更新字典
                    EditingDictionary.UpdateTime = DateTime.Now; // 自动填充更新时间
                    await _dictionaryService.UpdateAsync(EditingDictionary);

                    // 更新集合中的对应字典（同步UI显示）
                    var existingDict = Dictionaries.FirstOrDefault(d => d.Id == EditingDictionary.Id);
                    if (existingDict != null)
                    {
                        existingDict.DictType = EditingDictionary.DictType;
                        existingDict.DictName = EditingDictionary.DictName;
                        existingDict.Status = EditingDictionary.Status;
                        existingDict.UpdateTime = EditingDictionary.UpdateTime;
                        existingDict.Remark = EditingDictionary.Remark;
                    }

                    await _dialogService.ShowInfoAsync("成功", "字典更新成功");
                }
                else
                {
                    // 新增模式：校验字典类型唯一性
                    var existingDict = Dictionaries.FirstOrDefault(d => d.DictType.Equals(EditingDictionary.DictType, StringComparison.OrdinalIgnoreCase));
                    if (existingDict != null)
                    {
                        await _dialogService.ShowErrorAsync("错误", "字典类型已存在");
                        return;
                    }

                    // 添加新字典
                    var newDict = await _dictionaryService.AddAsync(EditingDictionary);

                    // 更新集合（同步UI显示）
                    Dictionaries.Add(newDict);
                    TotalCount = Dictionaries.Count;

                    await _dialogService.ShowInfoAsync("成功", "字典添加成功");
                }

                // 关闭弹窗
                IsDictionaryDialogOpen = false;
                // 刷新视图，确保过滤/排序生效
                DictionariesView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误弹窗
                await _dialogService.ShowErrorAsync("错误", $"保存字典失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除字典命令（绑定字典列表"删除"按钮）
        /// 逻辑：确认删除→删除字典及关联项→更新集合→提示结果
        /// </summary>
        /// <param name="dictionary">待删除的字典（UI传参）</param>
        [RelayCommand]
        private async Task DeleteDictionary(Dictionary? dictionary)
        {
            // 空值校验
            if (dictionary == null)
                return;

            // 确认删除（防止误操作）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除字典\"{dictionary.DictName}\"吗？此操作将同时删除所有相关字典项，且不可恢复。");
            if (result)
            {
                try
                {
                    // 删除字典（服务层内部级联删除字典项）
                    await _dictionaryService.DeleteByIdAsync(dictionary.Id);

                    // 从集合中移除（同步UI显示）
                    Dictionaries.Remove(dictionary);
                    TotalCount = Dictionaries.Count;

                    // 若删除的是当前选中字典，清空选中状态和字典项
                    if (SelectedDictionary?.Id == dictionary.Id)
                    {
                        SelectedDictionary = null;
                        DictionaryItems.Clear();
                    }

                    await _dialogService.ShowInfoAsync("成功", "字典删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除字典失败: {ex.Message}");
                }
            }
        }
        #endregion

        #region 字典项操作命令
        /// <summary>
        /// 新增字典项命令（绑定"+新增字典项"按钮）
        /// 逻辑：校验选中字典→初始化空字典项→设置新增模式→打开弹窗
        /// </summary>
        [RelayCommand]
        private void AddDictItem()
        {
            // 校验：未选中字典时提示
            if (SelectedDictionary == null)
            {
                _dialogService.ShowErrorAsync("提示", "请先选择一个字典");
                return;
            }

            // 获取当前最大排序号（新增项排在最后）
            int maxSortOrder = DictionaryItems.Any() ? DictionaryItems.Max(i => i.SortOrder) : 0;

            // 初始化新增字典项实体
            EditingDictItem = new DictionaryItem
            {
                DictId = SelectedDictionary.Id, // 关联当前选中字典
                SortOrder = maxSortOrder + 10, // 排序号间隔10，预留调整空间
                Status = 1, // 默认启用
                CreateTime = DateTime.Now // 自动填充创建时间
            };

            // 设置为新增模式
            IsDictItemEditMode = false;
            // 打开字典项弹窗
            IsDictItemDialogOpen = true;
        }

        /// <summary>
        /// 编辑字典项命令（绑定字典项列表"编辑"按钮）
        /// </summary>
        /// <param name="dictItem">待编辑的字典项（UI传参）</param>
        [RelayCommand]
        private void EditDictItem(DictionaryItem? dictItem)
        {
            // 空值校验
            if (dictItem == null)
                return;

            // 复制选中字典项数据到编辑实体
            EditingDictItem = new DictionaryItem
            {
                Id = dictItem.Id,
                DictId = dictItem.DictId,
                ItemValue = dictItem.ItemValue,
                ItemText = dictItem.ItemText,
                ItemDesc = dictItem.ItemDesc,
                SortOrder = dictItem.SortOrder,
                Status = dictItem.Status,
                CreateTime = dictItem.CreateTime,
                UpdateTime = DateTime.Now, // 自动填充更新时间
                Remark = dictItem.Remark
            };

            // 设置为编辑模式
            IsDictItemEditMode = true;
            // 打开字典项弹窗
            IsDictItemDialogOpen = true;
        }

        /// <summary>
        /// 取消字典项编辑命令（绑定弹窗"取消"按钮）
        /// </summary>
        [RelayCommand]
        private void CancelDictItemEdit()
        {
            IsDictItemDialogOpen = false;
        }

        /// <summary>
        /// 保存字典项命令（绑定弹窗"保存"按钮）
        /// 逻辑：校验→新增/更新→更新集合→关闭弹窗→提示结果
        /// </summary>
        [RelayCommand]
        private async Task SaveDictItem()
        {
            try
            {
                // 表单校验：字典项值和文本不能为空
                if (string.IsNullOrWhiteSpace(EditingDictItem.ItemValue) ||
                    string.IsNullOrWhiteSpace(EditingDictItem.ItemText))
                {
                    await _dialogService.ShowErrorAsync("错误", "字典项值和字典项文本不能为空");
                    return;
                }

                if (IsDictItemEditMode)
                {
                    // 编辑模式：更新字典项
                    EditingDictItem.UpdateTime = DateTime.Now;
                    await _dictionaryItemService.UpdateAsync(EditingDictItem);

                    // 更新集合中的对应项（同步UI显示）
                    var existingItem = DictionaryItems.FirstOrDefault(i => i.Id == EditingDictItem.Id);
                    if (existingItem != null)
                    {
                        existingItem.ItemValue = EditingDictItem.ItemValue;
                        existingItem.ItemText = EditingDictItem.ItemText;
                        existingItem.ItemDesc = EditingDictItem.ItemDesc;
                        existingItem.SortOrder = EditingDictItem.SortOrder;
                        existingItem.Status = EditingDictItem.Status;
                        existingItem.UpdateTime = EditingDictItem.UpdateTime;
                        existingItem.Remark = EditingDictItem.Remark;
                    }

                    await _dialogService.ShowInfoAsync("成功", "字典项更新成功");
                }
                else
                {
                    // 新增模式：校验字典项值唯一性
                    var existingItem = DictionaryItems.FirstOrDefault(i =>
                        i.DictId == EditingDictItem.DictId &&
                        i.ItemValue.Equals(EditingDictItem.ItemValue, StringComparison.OrdinalIgnoreCase));

                    if (existingItem != null)
                    {
                        await _dialogService.ShowErrorAsync("错误", "字典项值在当前字典下已存在");
                        return;
                    }

                    // 添加新字典项
                    var newItem = await _dictionaryItemService.AddAsync(EditingDictItem);

                    // 更新集合（同步UI显示）
                    DictionaryItems.Add(newItem);

                    // 刷新视图，确保排序生效
                    DictionaryItemsView?.Refresh();

                    await _dialogService.ShowInfoAsync("成功", "字典项添加成功");
                }

                // 关闭弹窗
                IsDictItemDialogOpen = false;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存字典项失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 删除字典项命令（绑定字典项列表"删除"按钮）
        /// </summary>
        /// <param name="dictItem">待删除的字典项（UI传参）</param>
        [RelayCommand]
        private async Task DeleteDictItem(DictionaryItem? dictItem)
        {
            // 空值校验
            if (dictItem == null)
                return;

            // 确认删除
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除字典项\"{dictItem.ItemText}\"吗？");
            if (result)
            {
                try
                {
                    // 删除字典项
                    await _dictionaryItemService.DeleteByIdAsync(dictItem.Id);

                    // 从集合中移除
                    DictionaryItems.Remove(dictItem);

                    await _dialogService.ShowInfoAsync("成功", "字典项删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除字典项失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 字典项上移命令（绑定"上移"按钮）
        /// 逻辑：交换排序号→更新数据库→调整集合顺序→异常时恢复排序号
        /// </summary>
        /// <param name="dictItem">待移动的字典项（UI传参）</param>
        [RelayCommand]
        private async Task MoveDictItemUp(DictionaryItem? dictItem)
        {
            // 空值/数量校验：无项或仅1项时不执行
            if (dictItem == null || DictionaryItems.Count <= 1)
                return;

            // 获取当前项索引
            int currentIndex = DictionaryItems.IndexOf(dictItem);
            // 已在第一个位置时不执行
            if (currentIndex <= 0)
                return;

            // 获取上一个项
            var previousItem = DictionaryItems[currentIndex - 1];

            // 临时保存排序号（异常时恢复）
            int tempSortOrder = dictItem.SortOrder;

            // 交换排序号
            dictItem.SortOrder = previousItem.SortOrder;
            previousItem.SortOrder = tempSortOrder;

            try
            {
                // 更新数据库排序号
                await _dictionaryItemService.UpdateAsync(dictItem);
                await _dictionaryItemService.UpdateAsync(previousItem);

                // 调整集合顺序（同步UI显示）
                DictionaryItems.Move(currentIndex, currentIndex - 1);
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误+恢复排序号
                await _dialogService.ShowErrorAsync("错误", $"移动字典项失败: {ex.Message}");
                dictItem.SortOrder = tempSortOrder;
                previousItem.SortOrder = previousItem.SortOrder;
            }
        }

        /// <summary>
        /// 字典项下移命令（绑定"下移"按钮）
        /// 逻辑同上移，仅索引判断相反
        /// </summary>
        /// <param name="dictItem">待移动的字典项（UI传参）</param>
        [RelayCommand]
        private async Task MoveDictItemDown(DictionaryItem? dictItem)
        {
            if (dictItem == null || DictionaryItems.Count <= 1)
                return;

            int currentIndex = DictionaryItems.IndexOf(dictItem);
            // 已在最后一个位置时不执行
            if (currentIndex < 0 || currentIndex >= DictionaryItems.Count - 1)
                return;

            var nextItem = DictionaryItems[currentIndex + 1];

            // 临时保存排序号
            int tempSortOrder = dictItem.SortOrder;

            // 交换排序号
            dictItem.SortOrder = nextItem.SortOrder;
            nextItem.SortOrder = tempSortOrder;

            try
            {
                // 更新数据库
                await _dictionaryItemService.UpdateAsync(dictItem);
                await _dictionaryItemService.UpdateAsync(nextItem);

                // 调整集合顺序
                DictionaryItems.Move(currentIndex, currentIndex + 1);
            }
            catch (Exception ex)
            {
                // 异常恢复
                await _dialogService.ShowErrorAsync("错误", $"移动字典项失败: {ex.Message}");
                dictItem.SortOrder = tempSortOrder;
                nextItem.SortOrder = nextItem.SortOrder;
            }
        }
        #endregion
    }
}