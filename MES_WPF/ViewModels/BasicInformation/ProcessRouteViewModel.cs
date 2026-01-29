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
    /// 工艺路线视图模型（MVVM 架构核心层）
    /// 职责：封装工艺路线及关联工艺步骤的业务逻辑、数据状态、UI 交互命令
    /// 与视图（View）通过数据绑定通信，通过服务层（Service）操作数据，不直接依赖数据库
    /// </summary>
    public partial class ProcessRouteViewModel : ObservableObject
    {
        // 依赖注入：工艺路线服务（封装工艺路线增删改查、状态更新等业务操作）
        private readonly IProcessRouteService _processRouteService;
        // 依赖注入：产品服务（用于加载产品列表，供工艺路线关联产品选择）
        private readonly IProductService _productService;
        // 依赖注入：工艺步骤服务（封装工艺步骤的增删改查、排序等操作）
        private readonly IRouteStepService _routeStepService;
        // 依赖注入：工序服务（用于加载工序列表，供工艺步骤关联工序选择）
        private readonly IOperationService _operationService;
        // 依赖注入：对话框服务（统一管理弹窗提示、确认框，解耦 UI 与业务逻辑）
        private readonly IDialogService _dialogService;

        #region 可观察属性（ObservableProperty）
        // 说明：使用 CommunityToolkit.Mvvm 的 [ObservableProperty] 特性
        // 自动生成属性的 get/set 方法和 PropertyChanged 事件，无需手动实现 INotifyPropertyChanged
        // UI 绑定这些属性后，属性值变化时 UI 会自动刷新

        /// <summary>
        /// 工艺路线数据源集合（ObservableCollection 支持 UI 自动刷新）
        /// 存储所有加载/筛选后的工艺路线数据，绑定到视图的工艺路线列表控件
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<ProcessRoute> _processRoutes = new();

        /// <summary>
        /// 产品列表（用于工艺路线关联产品的下拉选择框绑定）
        /// 初始化时加载所有产品数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        /// <summary>
        /// 工序列表（用于工艺步骤关联工序的下拉选择框绑定）
        /// 初始化时加载所有工序数据
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Operation> _operations = new();

        /// <summary>
        /// 当前选中的工艺路线（绑定视图工艺路线列表的选中项）
        /// 选中项变化时触发 OnSelectedProcessRouteChanged 回调，加载对应工艺步骤
        /// </summary>
        [ObservableProperty]
        private ProcessRoute _selectedProcessRoute;

        /// <summary>
        /// 选中工艺路线变化时的回调方法（由 [ObservableProperty] 自动触发）
        /// 职责：当选中新的工艺路线时，加载该路线对应的工艺步骤
        /// </summary>
        /// <param name="value">新选中的工艺路线（null 表示取消选中）</param>
        partial void OnSelectedProcessRouteChanged(ProcessRoute value)
        {
            if (value != null)
            {
                // 异步加载工艺步骤（Task.Run 避免阻塞 UI 线程）
                Task.Run(() => LoadRouteStepsAsync(value.Id));
            }
        }

        /// <summary>
        /// 工艺步骤数据源集合（绑定到视图的工艺步骤列表控件）
        /// 存储当前选中工艺路线的所有步骤，按步骤序号（StepNo）排序
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<RouteStep> _routeSteps = new();

        /// <summary>
        /// 当前选中的工艺步骤（绑定视图工艺步骤列表的选中项）
        /// 用于编辑、删除、移动等操作的目标对象
        /// </summary>
        [ObservableProperty]
        private RouteStep _selectedRouteStep;

        /// <summary>
        /// 正在编辑/新增的工艺路线对象（绑定到工艺路线编辑弹窗的表单控件）
        /// 临时存储表单数据，避免直接修改原始数据源
        /// </summary>
        [ObservableProperty]
        private ProcessRoute _editingProcessRoute;

        /// <summary>
        /// 正在编辑/新增的工艺步骤对象（绑定到工艺步骤编辑弹窗的表单控件）
        /// 临时存储表单数据，避免直接修改原始数据源
        /// </summary>
        [ObservableProperty]
        private RouteStep _editingRouteStep;

        /// <summary>
        /// 搜索关键词（绑定视图的搜索输入框）
        /// 用于模糊查询工艺路线编码、名称、版本、关联产品名称
        /// </summary>
        [ObservableProperty]
        private string _searchText;

        /// <summary>
        /// 状态筛选条件（绑定视图的状态下拉筛选框）
        /// 0=全部，1=草稿，2=审核中，3=已发布，4=已作废（与 ProcessRoute.Status 字段对应）
        /// </summary>
        [ObservableProperty]
        private byte _statusFilter;

        /// <summary>
        /// 状态筛选条件变化时的回调方法（由 [ObservableProperty] 自动触发）
        /// 职责：筛选条件变化后，重新加载工艺路线列表
        /// </summary>
        /// <param name="value">新的状态筛选值</param>
        partial void OnStatusFilterChanged(byte value)
        {
            // 异步加载筛选后的工艺路线
            Task.Run(() => LoadProcessRoutesAsync());
        }

        /// <summary>
        /// 选中的产品 ID（绑定视图的产品下拉筛选框）
        /// 用于按产品筛选工艺路线（0=全部）
        /// </summary>
        [ObservableProperty]
        private int _selectedProductId;

        /// <summary>
        /// 选中产品 ID 变化时的回调方法（由 [ObservableProperty] 自动触发）
        /// 职责：产品筛选条件变化后，加载该产品对应的工艺路线
        /// </summary>
        /// <param name="value">新选中的产品 ID</param>
        partial void OnSelectedProductIdChanged(int value)
        {
            if (value > 0)
            {
                // 按产品 ID 加载工艺路线
                Task.Run(() => LoadProcessRoutesByProductAsync(value));
            }
        }

        /// <summary>
        /// 是否为工艺路线编辑模式（绑定视图工艺路线编辑弹窗的显示状态）
        /// true=显示编辑弹窗，false=隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>
        /// 是否为工艺步骤编辑模式（绑定视图工艺步骤编辑弹窗的显示状态）
        /// true=显示编辑弹窗，false=隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isEditingStep;

        /// <summary>
        /// 是否正在处理异步操作（绑定视图的加载动画）
        /// 数据加载、搜索、保存等操作时设为 true，结束后设为 false，控制动画显示/隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isBusy;

        #endregion

        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// 由 DI 容器自动注入所需服务，初始化时加载基础数据
        /// </summary>
        /// <param name="processRouteService">工艺路线服务</param>
        /// <param name="productService">产品服务</param>
        /// <param name="routeStepService">工艺步骤服务</param>
        /// <param name="operationService">工序服务</param>
        /// <param name="dialogService">对话框服务</param>
        public ProcessRouteViewModel(
            IProcessRouteService processRouteService,
            IProductService productService,
            IRouteStepService routeStepService,
            IOperationService operationService,
            IDialogService dialogService)
        {
            // 赋值依赖服务（私有只读字段，避免后续被修改）
            _processRouteService = processRouteService;
            _productService = productService;
            _routeStepService = routeStepService;
            _operationService = operationService;
            _dialogService = dialogService;

            // 初始化时异步加载基础数据（产品、工序、工艺路线）
            // 避免阻塞 UI 线程，保证程序启动流畅
            Task.Run(async () =>
            {
                await LoadProductsAsync();    // 加载产品列表
                await LoadOperationsAsync();  // 加载工序列表
                await LoadProcessRoutesAsync();// 加载工艺路线列表
            });
        }

        /// <summary>
        /// 加载工艺路线列表（RelayCommand：绑定视图的"刷新"按钮）
        /// 职责：根据状态筛选条件，加载所有符合条件的工艺路线
        /// </summary>
        [RelayCommand]
        private async Task LoadProcessRoutesAsync()
        {
            try
            {
                // 开始加载：显示加载动画
                IsBusy = true;

                // 调用服务层方法，按状态筛选加载工艺路线
                var routes = StatusFilter > 0
                    ? await _processRouteService.GetByStatusAsync(StatusFilter) // 按状态筛选
                    : await _processRouteService.GetAllAsync(); // 加载所有

                // UI 线程更新数据源：ObservableCollection 非线程安全，需通过 Dispatcher 调度
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessRoutes.Clear(); // 清空现有数据（避免重复）
                    foreach (var route in routes)
                    {
                        ProcessRoutes.Add(route); // 批量添加新数据
                    }
                });
            }
            catch (Exception ex)
            {
                // 异常处理：显示加载失败提示
                await _dialogService.ShowErrorAsync("错误", $"加载工艺路线失败：{ex.Message}");
            }
            finally
            {
                // 结束加载：隐藏加载动画（无论成功/失败都执行）
                IsBusy = false;
            }
        }

        /// <summary>
        /// 按产品 ID 加载工艺路线（私有方法，供产品筛选回调调用）
        /// 职责：加载指定产品关联的所有工艺路线
        /// </summary>
        /// <param name="productId">产品 ID</param>
        private async Task LoadProcessRoutesByProductAsync(int productId)
        {
            try
            {
                IsBusy = true;
                // 调用服务层方法，按产品 ID 查询工艺路线
                var routes = await _processRouteService.GetByProductIdAsync(productId);

                // UI 线程更新数据源
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessRoutes.Clear();
                    foreach (var route in routes)
                    {
                        ProcessRoutes.Add(route);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载工艺路线失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 加载产品列表（私有方法，初始化时调用）
        /// 职责：加载所有产品数据，供工艺路线关联产品选择和筛选使用
        /// </summary>
        private async Task LoadProductsAsync()
        {
            try
            {
                // 调用产品服务加载所有产品
                var products = await _productService.GetAllAsync();

                // UI 线程更新产品列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Products.Clear();
                    foreach (var product in products)
                    {
                        Products.Add(product);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载产品信息失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 加载工序列表（私有方法，初始化时调用）
        /// 职责：加载所有工序数据，供工艺步骤关联工序选择使用
        /// </summary>
        private async Task LoadOperationsAsync()
        {
            try
            {
                // 调用工序服务加载所有工序
                var operations = await _operationService.GetAllAsync();

                // UI 线程更新工序列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Operations.Clear();
                    foreach (var operation in operations)
                    {
                        Operations.Add(operation);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载工序信息失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 加载工艺步骤（私有方法，选中工艺路线时调用）
        /// 职责：加载指定工艺路线的所有步骤，按步骤序号排序
        /// </summary>
        /// <param name="routeId">工艺路线 ID</param>
        private async Task LoadRouteStepsAsync(int routeId)
        {
            try
            {
                // 调用工艺步骤服务，按工艺路线 ID 查询步骤
                var steps = await _routeStepService.GetByRouteIdAsync(routeId);

                // UI 线程更新步骤列表（按 StepNo 升序排序，保证步骤顺序正确）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RouteSteps.Clear();
                    foreach (var step in steps.OrderBy(s => s.StepNo))
                    {
                        RouteSteps.Add(step);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载工艺步骤失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 搜索工艺路线（RelayCommand：绑定视图的"搜索"按钮）
        /// 职责：根据关键词、状态、产品筛选条件，过滤工艺路线数据
        /// </summary>
        [RelayCommand]
        private async Task SearchProcessRoutesAsync()
        {
            try
            {
                IsBusy = true;
                // 先加载所有工艺路线（后续在内存中筛选，适用于数据量较小场景）
                var routes = await _processRouteService.GetAllAsync();

                // 1. 按关键词筛选（模糊匹配编码、名称、版本、产品名称，忽略大小写）
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    routes = routes.Where(r =>
                        r.RouteCode?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        r.RouteName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        r.Version?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true ||
                        r.Product?.ProductName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true
                    );
                }

                // 2. 按状态筛选（状态筛选>0时生效）
                if (StatusFilter > 0)
                {
                    routes = routes.Where(r => r.Status == StatusFilter);
                }

                // 3. 按产品筛选（产品ID>0时生效）
                if (SelectedProductId > 0)
                {
                    routes = routes.Where(r => r.ProductId == SelectedProductId);
                }

                // UI 线程更新筛选后的工艺路线列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProcessRoutes.Clear();
                    foreach (var route in routes)
                    {
                        ProcessRoutes.Add(route);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索工艺路线失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 添加工艺路线（RelayCommand：绑定视图的"新增"按钮）
        /// 职责：初始化新增工艺路线对象，打开编辑弹窗
        /// </summary>
        [RelayCommand]
        private void AddProcessRoute()
        {
            // 初始化新增工艺路线的默认值
            EditingProcessRoute = new ProcessRoute
            {
                Status = 1,          // 默认状态：草稿（1=草稿）
                IsDefault = false,   // 默认非默认路线
                CreateTime = DateTime.Now, // 创建时间设为当前时间
                Version = "1.0"      // 默认版本号 1.0
            };
            IsEditing = true; // 打开工艺路线编辑弹窗
        }

        /// <summary>
        /// 编辑工艺路线（RelayCommand：绑定视图的"编辑"按钮）
        /// 职责：复制选中工艺路线的数据到编辑对象，打开编辑弹窗
        /// </summary>
        [RelayCommand]
        private void EditProcessRoute()
        {
            // 校验：未选中工艺路线时直接返回（避免空引用）
            if (SelectedProcessRoute == null) return;

            // 创建选中对象的副本（深拷贝思路）：避免直接修改原始数据源，支持取消编辑
            EditingProcessRoute = new ProcessRoute
            {
                Id = SelectedProcessRoute.Id,               // 保留原始 ID（用于更新）
                RouteCode = SelectedProcessRoute.RouteCode, // 工艺路线编码
                RouteName = SelectedProcessRoute.RouteName, // 工艺路线名称
                ProductId = SelectedProcessRoute.ProductId, // 关联产品 ID
                Version = SelectedProcessRoute.Version,     // 版本号
                Status = SelectedProcessRoute.Status,       // 状态
                IsDefault = SelectedProcessRoute.IsDefault, // 是否默认路线
                CreateTime = SelectedProcessRoute.CreateTime, // 创建时间（不允许修改）
                UpdateTime = DateTime.Now                   // 更新时间设为当前时间
            };
            IsEditing = true; // 打开工艺路线编辑弹窗
        }

        /// <summary>
        /// 保存工艺路线（RelayCommand：绑定编辑弹窗的"保存"按钮）
        /// 职责：校验表单数据，根据 ID 是否为 0 判断是新增还是更新
        /// </summary>
        [RelayCommand]
        private async Task SaveProcessRouteAsync()
        {
            if (EditingProcessRoute == null) return;

            try
            {
                // 表单校验：必填字段不能为空，避免无效数据提交
                if (string.IsNullOrWhiteSpace(EditingProcessRoute.RouteCode))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "工艺路线编码不能为空");
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingProcessRoute.RouteName))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "工艺路线名称不能为空");
                    return;
                }

                if (EditingProcessRoute.ProductId <= 0)
                {
                    await _dialogService.ShowErrorAsync("验证错误", "请选择关联产品");
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingProcessRoute.Version))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "版本号不能为空");
                    return;
                }

                // 新增模式（ID=0）：检查工艺路线编码是否已存在（避免重复编码）
                if (EditingProcessRoute.Id == 0 &&
                    await _processRouteService.IsRouteCodeExistsAsync(EditingProcessRoute.RouteCode))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "该工艺路线编码已存在");
                    return;
                }

                ProcessRoute savedRoute;
                if (EditingProcessRoute.Id == 0) // 新增模式
                {
                    EditingProcessRoute.CreateTime = DateTime.Now; // 确认创建时间
                    savedRoute = await _processRouteService.AddAsync(EditingProcessRoute); // 调用服务新增
                    await _dialogService.ShowInfoAsync("成功", "工艺路线添加成功");
                }
                else // 更新模式
                {
                    EditingProcessRoute.UpdateTime = DateTime.Now; // 更新时间设为当前时间
                    savedRoute = await _processRouteService.UpdateAsync(EditingProcessRoute); // 调用服务更新
                    await _dialogService.ShowInfoAsync("成功", "工艺路线更新成功");
                }

                // 保存后刷新工艺路线列表，保持 UI 与数据库一致
                await LoadProcessRoutesAsync();

                // 自动选中保存后的工艺路线，提升用户体验
                SelectedProcessRoute = ProcessRoutes.FirstOrDefault(r => r.Id == savedRoute.Id);

                // 关闭编辑弹窗，清空编辑对象
                IsEditing = false;
                EditingProcessRoute = null;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存工艺路线失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除工艺路线（RelayCommand：绑定视图的"删除"按钮）
        /// 职责：弹出确认框，确认后删除选中工艺路线及关联的工艺步骤
        /// </summary>
        [RelayCommand]
        private async Task DeleteProcessRouteAsync()
        {
            // 校验：未选中工艺路线时直接返回
            if (SelectedProcessRoute == null) return;

            // 弹出确认框：告知用户删除后果（关联步骤也会删除），避免误操作
            var result = await _dialogService.ShowConfirmAsync("确认", "确定要删除此工艺路线吗？此操作将同时删除所有相关的工艺步骤。");
            if (!result) return; // 用户取消删除，直接返回

            try
            {
                // 调用服务层删除工艺路线（服务层需实现级联删除关联步骤，或此处先删步骤再删路线）
                await _processRouteService.DeleteByIdAsync(SelectedProcessRoute.Id);
                await _dialogService.ShowInfoAsync("成功", "工艺路线删除成功");

                // 刷新工艺路线列表，清空选中状态和步骤列表
                await LoadProcessRoutesAsync();
                SelectedProcessRoute = null;
                RouteSteps.Clear();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"删除工艺路线失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 设置默认工艺路线（RelayCommand：绑定视图的"设为默认"按钮）
        /// 职责：将选中工艺路线设为对应产品的默认路线，取消该产品其他路线的默认状态
        /// </summary>
        [RelayCommand]
        private async Task SetDefaultRouteAsync()
        {
            if (SelectedProcessRoute == null) return;

            // 弹出确认框：告知用户操作后果（取消其他默认路线）
            var result = await _dialogService.ShowConfirmAsync("确认", "确定要将此工艺路线设为默认吗？这将取消当前产品的其他默认工艺路线。");
            if (!result) return;

            try
            {
                // 调用服务层设置默认路线（服务层已处理"取消其他默认"逻辑）
                await _processRouteService.SetDefaultRouteAsync(SelectedProcessRoute.Id, SelectedProcessRoute.ProductId);
                await _dialogService.ShowInfoAsync("成功", "已将此工艺路线设为默认");

                // 刷新列表，更新默认状态显示
                await LoadProcessRoutesAsync();
                SelectedProcessRoute = ProcessRoutes.FirstOrDefault(r => r.Id == SelectedProcessRoute.Id);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"设置默认工艺路线失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新工艺路线状态（RelayCommand：绑定视图的状态更新按钮组）
        /// 职责：修改选中工艺路线的状态（草稿/审核中/已发布/已作废）
        /// </summary>
        /// <param name="status">目标状态（1=草稿，2=审核中，3=已发布，4=已作废）</param>
        [RelayCommand]
        private async Task UpdateStatusAsync(byte status)
        {
            if (SelectedProcessRoute == null) return;

            // 将状态值转换为中文描述（提升用户体验，明确操作目标）
            string statusText = status switch
            {
                1 => "草稿",
                2 => "审核中",
                3 => "已发布",
                4 => "已作废",
                _ => "未知状态"
            };

            // 弹出确认框：确认是否修改状态
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要将此工艺路线状态更改为\"{statusText}\"吗？");
            if (!result) return;

            try
            {
                // 调用服务层更新状态
                await _processRouteService.UpdateStatusAsync(SelectedProcessRoute.Id, status);
                await _dialogService.ShowInfoAsync("成功", "工艺路线状态更新成功");

                // 刷新列表，更新状态显示
                await LoadProcessRoutesAsync();
                SelectedProcessRoute = ProcessRoutes.FirstOrDefault(r => r.Id == SelectedProcessRoute.Id);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"更新工艺路线状态失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消工艺路线编辑（RelayCommand：绑定编辑弹窗的"取消"按钮）
        /// 职责：关闭编辑弹窗，清空编辑对象（放弃当前编辑内容）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            EditingProcessRoute = null;
            IsEditing = false;
        }

        #region 工艺步骤管理（独立功能块，封装工艺步骤相关操作）

        /// <summary>
        /// 添加工艺步骤（RelayCommand：绑定视图的"添加工艺步骤"按钮）
        /// 职责：初始化新增工艺步骤对象，打开步骤编辑弹窗
        /// </summary>
        [RelayCommand]
        private void AddRouteStep()
        {
            // 校验：未选中工艺路线时，无法添加工艺步骤
            if (SelectedProcessRoute == null) return;

            // 计算下一步骤序号：默认步长10，便于后续插入步骤（如现有最大序号20，新增为30）
            int nextOrder = 10;
            if (RouteSteps.Any())
            {
                nextOrder = RouteSteps.Max(s => s.StepNo) + 10;
            }

            // 初始化新增工艺步骤的默认值
            EditingRouteStep = new RouteStep
            {
                RouteId = SelectedProcessRoute.Id, // 关联当前选中的工艺路线
                StepNo = nextOrder,                // 步骤序号
                IsKeyOperation = true,             // 默认是关键工序
                IsQualityCheckPoint = false,       // 默认不是质检点
                SetupTime = 0,                     // 准备时间默认0
                ProcessTime = 0,                   // 加工时间默认0
                WaitTime = 0                       // 等待时间默认0
            };
            IsEditingStep = true; // 打开工艺步骤编辑弹窗
        }

        /// <summary>
        /// 编辑工艺步骤（RelayCommand：绑定视图的"编辑工艺步骤"按钮）
        /// 职责：复制选中工艺步骤的数据到编辑对象，打开步骤编辑弹窗
        /// </summary>
        [RelayCommand]
        private void EditRouteStep()
        {
            // 校验：未选中工艺步骤时直接返回
            if (SelectedRouteStep == null) return;

            // 创建选中步骤的副本：避免直接修改原始数据源，支持取消编辑
            EditingRouteStep = new RouteStep
            {
                Id = SelectedRouteStep.Id,                       // 步骤ID
                RouteId = SelectedRouteStep.RouteId,             // 关联工艺路线ID
                StepNo = SelectedRouteStep.StepNo,               // 步骤序号
                OperationId = SelectedRouteStep.OperationId,     // 关联工序ID
                Description = SelectedRouteStep.Description,     // 步骤描述
                IsKeyOperation = SelectedRouteStep.IsKeyOperation, // 是否关键工序
                IsQualityCheckPoint = SelectedRouteStep.IsQualityCheckPoint, // 是否质检点
                SetupTime = SelectedRouteStep.SetupTime,         // 准备时间
                ProcessTime = SelectedRouteStep.ProcessTime,     // 加工时间
                WaitTime = SelectedRouteStep.WaitTime,           // 等待时间
                WorkstationTypeId = SelectedRouteStep.WorkstationTypeId // 工作站类型ID
            };
            IsEditingStep = true; // 打开工艺步骤编辑弹窗
        }

        /// <summary>
        /// 保存工艺步骤（RelayCommand：绑定步骤编辑弹窗的"保存"按钮）
        /// 职责：校验步骤数据，根据 ID 是否为 0 判断是新增还是更新
        /// </summary>
        [RelayCommand]
        private async Task SaveRouteStepAsync()
        {
            if (EditingRouteStep == null) return;

            try
            {
                // 表单校验：必须选择关联工序
                if (EditingRouteStep.OperationId <= 0)
                {
                    await _dialogService.ShowErrorAsync("验证错误", "请选择工序");
                    return;
                }

                RouteStep savedStep;
                if (EditingRouteStep.Id == 0) // 新增模式
                {
                    savedStep = await _routeStepService.AddAsync(EditingRouteStep); // 调用服务新增步骤
                    await _dialogService.ShowInfoAsync("成功", "工艺步骤添加成功");
                }
                else // 更新模式
                {
                    savedStep = await _routeStepService.UpdateAsync(EditingRouteStep); // 调用服务更新步骤
                    await _dialogService.ShowInfoAsync("成功", "工艺步骤更新成功");
                }

                // 刷新当前工艺路线的步骤列表，保持 UI 与数据库一致
                await LoadRouteStepsAsync(SelectedProcessRoute.Id);

                // 自动选中保存后的步骤，提升用户体验
                SelectedRouteStep = RouteSteps.FirstOrDefault(s => s.Id == savedStep.Id);

                // 关闭步骤编辑弹窗，清空编辑对象
                IsEditingStep = false;
                EditingRouteStep = null;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存工艺步骤失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 删除工艺步骤（RelayCommand：绑定视图的"删除工艺步骤"按钮）
        /// 职责：弹出确认框，确认后删除选中的工艺步骤
        /// </summary>
        [RelayCommand]
        private async Task DeleteRouteStepAsync()
        {
            if (SelectedRouteStep == null) return;

            // 弹出确认框：避免误删除
            var result = await _dialogService.ShowConfirmAsync("确认", "确定要删除此工艺步骤吗？");
            if (!result) return;

            try
            {
                // 调用服务层删除步骤
                await _routeStepService.DeleteByIdAsync(SelectedRouteStep.Id);
                await _dialogService.ShowInfoAsync("成功", "工艺步骤删除成功");

                // 刷新步骤列表，清空选中状态
                await LoadRouteStepsAsync(SelectedProcessRoute.Id);
                SelectedRouteStep = null;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"删除工艺步骤失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 上移工艺步骤（RelayCommand：绑定视图的"上移"按钮）
        /// 职责：交换当前步骤与上一步骤的序号，调整步骤顺序
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanMoveStepUp))]
        private async Task MoveStepUpAsync()
        {
            if (SelectedRouteStep == null || !CanMoveStepUp()) return;

            try
            {
                // 获取当前步骤在列表中的索引
                var currentIndex = RouteSteps.IndexOf(SelectedRouteStep);
                // 获取上一步骤（索引-1）
                var previousStep = RouteSteps[currentIndex - 1];

                // 交换两个步骤的序号（核心逻辑：通过序号控制显示顺序）
                int tempOrder = SelectedRouteStep.StepNo;
                SelectedRouteStep.StepNo = previousStep.StepNo;
                previousStep.StepNo = tempOrder;

                // 调用服务层更新两个步骤的序号（持久化到数据库）
                await _routeStepService.UpdateAsync(SelectedRouteStep);
                await _routeStepService.UpdateAsync(previousStep);

                // 刷新步骤列表（按新序号排序，UI 显示顺序更新）
                await LoadRouteStepsAsync(SelectedProcessRoute.Id);

                // 保持当前步骤选中状态，提升用户体验
                SelectedRouteStep = RouteSteps.FirstOrDefault(s => s.Id == SelectedRouteStep.Id);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"移动工艺步骤失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 下移工艺步骤（RelayCommand：绑定视图的"下移"按钮）
        /// 职责：交换当前步骤与下一步骤的序号，调整步骤顺序
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanMoveStepDown))]
        private async Task MoveStepDownAsync()
        {
            if (SelectedRouteStep == null || !CanMoveStepDown()) return;

            try
            {
                // 获取当前步骤在列表中的索引
                var currentIndex = RouteSteps.IndexOf(SelectedRouteStep);
                // 获取下一步骤（索引+1）
                var nextStep = RouteSteps[currentIndex + 1];

                // 交换两个步骤的序号
                int tempOrder = SelectedRouteStep.StepNo;
                SelectedRouteStep.StepNo = nextStep.StepNo;
                nextStep.StepNo = tempOrder;

                // 持久化更新到数据库
                await _routeStepService.UpdateAsync(SelectedRouteStep);
                await _routeStepService.UpdateAsync(nextStep);

                // 刷新步骤列表，保持选中状态
                await LoadRouteStepsAsync(SelectedProcessRoute.Id);
                SelectedRouteStep = RouteSteps.FirstOrDefault(s => s.Id == SelectedRouteStep.Id);
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"移动工艺步骤失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 上移步骤的可执行条件（CanExecute）
        /// 规则：步骤列表至少有2个步骤，且当前步骤不是第一个
        /// </summary>
        private bool CanMoveStepUp()
        {
            if (SelectedRouteStep == null || RouteSteps.Count <= 1) return false;
            int currentIndex = RouteSteps.IndexOf(SelectedRouteStep);
            return currentIndex > 0; // 索引>0 表示不是第一个步骤
        }

        /// <summary>
        /// 下移步骤的可执行条件（CanExecute）
        /// 规则：步骤列表至少有2个步骤，且当前步骤不是最后一个
        /// </summary>
        private bool CanMoveStepDown()
        {
            if (SelectedRouteStep == null || RouteSteps.Count <= 1) return false;
            int currentIndex = RouteSteps.IndexOf(SelectedRouteStep);
            return currentIndex < RouteSteps.Count - 1; // 索引<列表长度-1 表示不是最后一个步骤
        }

        /// <summary>
        /// 取消工艺步骤编辑（RelayCommand：绑定步骤编辑弹窗的"取消"按钮）
        /// 职责：关闭步骤编辑弹窗，清空编辑对象
        /// </summary>
        [RelayCommand]
        private void CancelEditStep()
        {
            EditingRouteStep = null;
            IsEditingStep = false;
        }

        #endregion
    }
}