using MES_WPF.Core.Services.BasicInformation;
using MES_WPF.Model.BasicInformation;
using MES_WPF.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MES_WPF.ViewModels.BasicInformation
{
    /// <summary>
    /// 工序管理视图模型（MVVM架构的核心层）
    /// 职责：封装工序管理的业务逻辑、数据状态、UI交互命令，与视图（View）通过数据绑定通信
    /// </summary>
    public partial class OperationViewModel : ObservableObject
    {
        // 依赖注入：工序服务（封装工序相关数据操作，如增删改查）
        private readonly IOperationService _operationService;
        // 依赖注入：对话框服务（统一管理弹窗提示、确认框，解耦UI与业务逻辑）
        private readonly IDialogService _dialogService;

        /// <summary>
        /// 扩展Operation实体类（继承自基础工序模型）
        /// 新增IsSelected属性：用于批量删除时的勾选状态绑定
        /// 设计思路：不修改原始实体类，通过继承扩展UI所需的临时状态
        /// </summary>
        public partial class OperationWithSelection : Operation
        {
            // 勾选状态：true=已选中，false=未选中（绑定视图中的复选框）
            public bool IsSelected { get; set; }
        }

        #region 可观察属性（ObservableProperty）
        // 说明：使用CommunityToolkit.Mvvm的[ObservableProperty]特性，自动生成属性的get/set和PropertyChanged事件
        // 无需手动实现INotifyPropertyChanged接口，简化代码并支持UI自动更新

        /// <summary>
        /// 工序数据源集合（ObservableCollection支持UI自动刷新）
        /// 存储所有加载/筛选后的工序数据，绑定到视图的列表控件
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Operation> _operations = new();

        /// <summary>
        /// 工序列表对外暴露的视图数据源（与_operations同源）
        /// 设计思路：预留扩展点，可后续添加过滤、排序等视图层逻辑，不影响原始数据源
        /// </summary>
        public ObservableCollection<Operation> OperationsView => _operations;

        /// <summary>
        /// 当前选中的工序（绑定视图列表的选中项）
        /// 视图中选中某行时，该属性自动更新；反之修改该属性也会同步视图选中状态
        /// </summary>
        [ObservableProperty]
        private Operation _selectedOperation;

        /// <summary>
        /// 正在编辑/新增的工序对象（绑定弹窗表单的输入控件）
        /// 用于临时存储表单数据，避免直接修改原始数据源
        /// </summary>
        [ObservableProperty]
        private Operation _editingOperation;

        /// <summary>
        /// 搜索关键词（绑定视图的搜索输入框）
        /// 用于模糊查询工序编码/名称
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword;

        /// <summary>
        /// 选中的工序类型（绑定视图的工序类型下拉框）
        /// 0=全部，1+对应具体类型（与数据字典中的工序类型编码一致）
        /// </summary>
        [ObservableProperty]
        private int _selectedOperationType = 0;

        /// <summary>
        /// 选中的状态（绑定视图的状态下拉框）
        /// 0=全部，1=启用，2=禁用（对应IsActive字段：true=启用，false=禁用）
        /// </summary>
        [ObservableProperty]
        private int _selectedStatus = 0;

        /// <summary>
        /// 是否正在刷新数据（绑定视图的加载动画）
        /// 数据加载/搜索时设为true，结束后设为false，控制动画显示/隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 工序编辑/新增弹窗是否打开（绑定视图弹窗的Visibility/IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isOperationDialogOpen;

        /// <summary>
        /// 是否为编辑模式（控制弹窗表单的标题、逻辑区分）
        /// true=编辑现有工序，false=新增工序
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 工序总条数（绑定视图的分页控件，显示总记录数）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 当前页码（绑定视图的分页控件，记录当前所在页）
        /// 初始值为1（默认显示第一页）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        #endregion

        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// 说明：通过DI容器自动注入所需服务，避免硬编码创建实例，降低耦合
        /// </summary>
        /// <param name="operationService">工序服务（数据操作）</param>
        /// <param name="dialogService">对话框服务（UI交互）</param>
        public OperationViewModel(IOperationService operationService, IDialogService dialogService)
        {
            // 赋值依赖服务（私有只读字段，避免后续被修改）
            _operationService = operationService;
            _dialogService = dialogService;

            // 异步加载工序数据（Task.Run避免阻塞UI线程，保证程序启动流畅）
            Task.Run(async () => await LoadOperationsAsync());
        }

        /// <summary>
        /// 上一页命令的可执行条件（RelayCommand的CanExecute）
        /// 只有当前页码>1时，上一页按钮才可用
        /// </summary>
        private bool CanPreviousPage() => CurrentPage > 1;

        /// <summary>
        /// 下一页命令的可执行条件（RelayCommand的CanExecute）
        /// 只有当前页码<总页数时，下一页按钮才可用
        /// </summary>
        private bool CanNextPage() => CurrentPage < TotalPages;

        /// <summary>
        /// 加载工序数据（RelayCommand：绑定视图的"刷新"按钮）
        /// 职责：从服务层获取所有工序数据，更新数据源集合和总条数
        /// </summary>
        [RelayCommand]
        private async Task LoadOperationsAsync()
        {
            try
            {
                // 开始加载：显示加载动画
                IsRefreshing = true;

                // 调用服务层方法，异步获取所有工序数据（避免阻塞UI）
                var operations = await _operationService.GetAllAsync();

                // 更新总记录数（用于分页计算）
                TotalCount = operations.Count();

                // UI线程更新数据源：ObservableCollection非线程安全，需通过Dispatcher调度到UI线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空现有数据（避免重复添加）
                    Operations.Clear();
                    // 批量添加新数据到集合（触发UI自动刷新）
                    foreach (var operation in operations)
                    {
                        Operations.Add(operation);
                    }
                });
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误提示（通过对话框服务，统一UI风格）
                await _dialogService.ShowErrorAsync("错误", $"加载工序数据失败: {ex.Message}");
            }
            finally
            {
                // 结束加载：隐藏加载动画（无论成功/失败都执行）
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 搜索工序（RelayCommand：绑定视图的"搜索"按钮）
        /// 职责：根据关键词、工序类型、状态筛选数据，更新视图显示
        /// </summary>
        [RelayCommand]
        private async Task SearchOperationsAsync()
        {
            try
            {
                // 开始搜索：显示加载动画
                IsRefreshing = true;

                // 1. 先获取所有工序数据（后续在内存中筛选，适用于数据量较小场景）
                var operations = await _operationService.GetAllAsync();

                // 2. 按关键词筛选（模糊匹配工序编码或名称，忽略大小写）
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                {
                    operations = operations.Where(o =>
                        o.OperationCode.Contains(SearchKeyword) ||
                        o.OperationName.Contains(SearchKeyword));
                }

                // 3. 按工序类型筛选（选中类型>0时生效，转换为byte类型匹配实体字段）
                if (SelectedOperationType > 0)
                {
                    byte operationType = (byte)SelectedOperationType;
                    operations = operations.Where(o => o.OperationType == operationType);
                }

                // 4. 按状态筛选（选中状态>0时生效：1=启用，2=禁用）
                if (SelectedStatus > 0)
                {
                    bool isActive = SelectedStatus == 1; // 状态1对应启用（IsActive=true）
                    operations = operations.Where(o => o.IsActive == isActive);
                }

                // 5. 更新筛选后的总记录数
                TotalCount = operations.Count();

                // 6. UI线程更新数据源（与加载逻辑一致，保证线程安全）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Operations.Clear();
                    foreach (var operation in operations)
                    {
                        Operations.Add(operation);
                    }
                });

                // 7. 重置当前页码为1（筛选后默认显示第一页）
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                // 异常处理：显示搜索失败提示
                await _dialogService.ShowErrorAsync("错误", $"搜索工序失败: {ex.Message}");
            }
            finally
            {
                // 结束搜索：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件（RelayCommand：绑定视图的"重置"按钮）
        /// 职责：清空所有搜索条件，恢复显示全部数据
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            // 清空搜索关键词
            SearchKeyword = string.Empty;
            // 重置工序类型为"全部"（0）
            SelectedOperationType = 0;
            // 重置状态为"全部"（0）
            SelectedStatus = 0;

            // 重新加载所有数据（异步执行，不阻塞UI）
            Task.Run(async () => await LoadOperationsAsync());
        }

        /// <summary>
        /// 添加工序（RelayCommand：绑定视图的"新增"按钮）
        /// 职责：初始化新增工序对象，打开编辑弹窗
        /// </summary>
        [RelayCommand]
        private void AddOperation()
        {
            // 初始化新增工序对象（设置默认值）
            EditingOperation = new Operation
            {
                CreateTime = DateTime.Now, // 创建时间设为当前时间
                IsActive = true, // 默认启用状态
                StandardTime = 0 // 标准工时默认0（需用户输入）
            };

            // 设为新增模式（false=新增）
            IsEditMode = false;
            // 打开工序编辑弹窗
            IsOperationDialogOpen = true;
        }

        /// <summary>
        /// 编辑工序（RelayCommand：绑定视图列表的"编辑"按钮）
        /// 职责：复制选中的工序数据到编辑对象，打开编辑弹窗
        /// </summary>
        /// <param name="operation">视图中选中的工序对象（通过命令参数传递）</param>
        [RelayCommand]
        private void EditOperation(Operation operation)
        {
            // 校验：若未选中工序，直接返回（避免空引用）
            if (operation == null) return;

            // 创建选中工序的副本（深拷贝思路）：避免直接修改原始数据源，保证数据安全性
            EditingOperation = new Operation
            {
                Id = operation.Id, // 保留原始ID（用于后续更新）
                OperationCode = operation.OperationCode, // 工序编码
                OperationName = operation.OperationName, // 工序名称
                OperationType = operation.OperationType, // 工序类型
                Department = operation.Department, // 所属部门
                Description = operation.Description, // 描述
                StandardTime = operation.StandardTime, // 标准工时
                IsActive = operation.IsActive, // 启用状态
                CreateTime = operation.CreateTime, // 创建时间（不允许修改）
                UpdateTime = DateTime.Now // 更新时间设为当前时间
            };

            // 设为编辑模式（true=编辑）
            IsEditMode = true;
            // 打开工序编辑弹窗
            IsOperationDialogOpen = true;
        }

        /// <summary>
        /// 删除工序（RelayCommand：绑定视图列表的"删除"按钮）
        /// 职责：弹出确认框，确认后删除选中工序
        /// </summary>
        /// <param name="operation">视图中选中的工序对象（通过命令参数传递）</param>
        [RelayCommand]
        private async Task DeleteOperationAsync(Operation operation)
        {
            // 校验：若未选中工序，直接返回
            if (operation == null) return;

            // 弹出确认框：询问用户是否确认删除（避免误操作）
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除工序 {operation.OperationName} 吗？");
            if (result) // 用户点击"确认"
            {
                try
                {
                    // 调用服务层方法，异步删除工序（根据ID）
                    await _operationService.DeleteByIdAsync(operation.Id);
                    // 从本地数据源中移除该工序（UI自动刷新）
                    Operations.Remove(operation);
                    // 更新总记录数（减1）
                    TotalCount--;
                    // 显示删除成功提示
                    await _dialogService.ShowInfoAsync("成功", "工序删除成功");
                }
                catch (Exception ex)
                {
                    // 异常处理：显示删除失败提示（如工序已被引用，无法删除）
                    await _dialogService.ShowErrorAsync("错误", $"删除工序失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 批量删除工序（RelayCommand：绑定视图的"批量删除"按钮）
        /// 状态：暂未实现（需配合OperationWithSelection类使用）
        /// </summary>
        [RelayCommand]
        private async Task BatchDeleteAsync()
        {
            // 临时提示：告知用户功能暂不可用（因模型设计未完善）
            await _dialogService.ShowInfoAsync("提示", "批量删除功能需要模型支持，暂不可用");
            return;

            /*
            // 以下是批量删除的预留实现逻辑（需启用OperationWithSelection作为数据源）
            // 1. 筛选出所有选中的工序（通过IsSelected属性）
            var selectedOperations = Operations.Where(o => (o as OperationWithSelection)?.IsSelected ?? false).ToList();
            
            // 2. 校验：若未选中任何工序，提示用户
            if (selectedOperations.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择要删除的工序");
                return;
            }

            // 3. 弹出确认框：告知用户选中的工序数量，确认是否删除
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除选中的 {selectedOperations.Count} 个工序吗？");
            if (result)
            {
                try
                {
                    // 4. 循环删除每个选中的工序
                    foreach (var operation in selectedOperations)
                    {
                        await _operationService.DeleteByIdAsync(operation.Id);
                        Operations.Remove(operation);
                    }
                    // 5. 更新总记录数（减去删除的数量）
                    TotalCount -= selectedOperations.Count;
                    // 6. 显示批量删除成功提示
                    await _dialogService.ShowInfoAsync("成功", "工序批量删除成功");
                }
                catch (Exception ex)
                {
                    // 异常处理：显示批量删除失败提示
                    await _dialogService.ShowErrorAsync("错误", $"批量删除工序失败: {ex.Message}");
                }
            }
            */
        }

        /// <summary>
        /// 导出工序数据（RelayCommand：绑定视图的"导出"按钮）
        /// 状态：待实现（预留功能入口）
        /// </summary>
        [RelayCommand]
        private void ExportOperations()
        {
            // 临时提示：告知用户功能待实现
            _dialogService.ShowInfoAsync("提示", "导出功能待实现");
        }

        /// <summary>
        /// 保存工序（RelayCommand：绑定弹窗的"保存"按钮）
        /// 职责：校验表单数据，根据模式（新增/编辑）执行对应操作
        /// </summary>
        [RelayCommand]
        private async Task SaveOperationAsync()
        {
            // 表单校验：必填字段不能为空，避免无效数据提交
            if (string.IsNullOrWhiteSpace(EditingOperation.OperationCode))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入工序编码");
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingOperation.OperationName))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入工序名称");
                return;
            }

            if (EditingOperation.StandardTime <= 0)
            {
                await _dialogService.ShowInfoAsync("提示", "标准工时必须大于0");
                return;
            }

            try
            {
                // 新增模式：检查工序编码是否已存在（避免重复编码）
                if (!IsEditMode)
                {
                    bool exists = await _operationService.IsOperationCodeExistsAsync(EditingOperation.OperationCode);
                    if (exists)
                    {
                        await _dialogService.ShowInfoAsync("提示", $"工序编码 {EditingOperation.OperationCode} 已存在");
                        return;
                    }
                }

                if (IsEditMode) // 编辑模式：更新现有工序
                {
                    // 更新时间设为当前时间（记录最后修改时间）
                    EditingOperation.UpdateTime = DateTime.Now;
                    // 调用服务层方法，异步更新工序数据
                    await _operationService.UpdateAsync(EditingOperation);

                    // 同步更新本地数据源（保证UI与数据库一致）
                    var existingOperation = Operations.FirstOrDefault(o => o.Id == EditingOperation.Id);
                    if (existingOperation != null)
                    {
                        // 替换原有对象（触发UI刷新）
                        var index = Operations.IndexOf(existingOperation);
                        Operations[index] = EditingOperation;
                    }

                    // 显示更新成功提示
                    await _dialogService.ShowInfoAsync("成功", "工序更新成功");
                }
                else // 新增模式：创建新工序
                {
                    // 确认创建时间（避免用户手动修改）
                    EditingOperation.CreateTime = DateTime.Now;
                    // 调用服务层方法，异步添加新工序（返回新增后的完整对象，包含自增ID）
                    var newOperation = await _operationService.AddAsync(EditingOperation);
                    // 添加到本地数据源（UI自动刷新）
                    Operations.Add(newOperation);
                    // 更新总记录数（加1）
                    TotalCount++;

                    // 显示添加成功提示
                    await _dialogService.ShowInfoAsync("成功", "工序添加成功");
                }

                // 保存完成：关闭编辑弹窗
                IsOperationDialogOpen = false;
            }
            catch (Exception ex)
            {
                // 异常处理：显示保存失败提示（如数据库连接错误、数据校验失败等）
                await _dialogService.ShowErrorAsync("错误", $"保存工序失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消编辑（RelayCommand：绑定弹窗的"取消"按钮）
        /// 职责：关闭编辑弹窗，放弃当前编辑内容
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭工序编辑弹窗（EditingOperation中的临时数据会被保留，但不会影响数据源）
            IsOperationDialogOpen = false;
        }

        /// <summary>
        /// 上一页（RelayCommand：绑定分页控件的"上一页"按钮）
        /// 职责：切换到当前页的前一页
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPreviousPage))]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                //  TODO：实现分页加载逻辑（当前仅切换页码，未加载对应页数据）
                // 需修改为：根据CurrentPage和PageSize从服务层获取对应页数据
            }
        }

        /// <summary>
        /// 下一页（RelayCommand：绑定分页控件的"下一页"按钮）
        /// 职责：切换到当前页的后一页
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanNextPage))]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                //  TODO：实现分页加载逻辑（当前仅切换页码，未加载对应页数据）
                // 需修改为：根据CurrentPage和PageSize从服务层获取对应页数据
            }
        }

        /// <summary>
        /// 跳转到指定页（RelayCommand：绑定分页控件的"跳转"按钮）
        /// 职责：根据用户输入的页码，切换到对应页
        /// </summary>
        /// <param name="pageObj">用户输入的页码（可能是int或string类型）</param>
        [RelayCommand]
        private void GoToPage(object pageObj)
        {
            // 处理int类型页码（如分页控件的数字按钮）
            if (pageObj is int pageInt)
            {
                // 校验页码有效性（1<=页码<=总页数）
                if (pageInt >= 1 && pageInt <= TotalPages)
                {
                    CurrentPage = pageInt;
                    //  TODO：实现分页加载逻辑
                }
            }
            // 处理string类型页码（如用户输入的文本框）
            else if (pageObj is string pageStr)
            {
                // 解析字符串为int，且校验有效性
                if (int.TryParse(pageStr, out int pageVal) && pageVal >= 1 && pageVal <= TotalPages)
                {
                    CurrentPage = pageVal;
                    //  TODO：实现分页加载逻辑
                }
            }
        }

        /// <summary>
        /// 总页数（计算属性）
        /// 公式：(总记录数 + 每页条数 - 1) / 每页条数 → 向上取整（避免小数页）
        /// </summary>
        private int TotalPages => (TotalCount + PageSize - 1) / PageSize;

        /// <summary>
        /// 每页显示数量（固定10条，可改为可配置属性）
        /// </summary>
        private int PageSize => 10;
    }
}