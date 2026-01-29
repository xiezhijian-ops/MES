using MES_WPF.Core.Services.BasicInformation;
using MES_WPF.Model.BasicInformation;
using MES_WPF.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MES_WPF.ViewModels.BasicInformation
{
    /// <summary>
    /// BOM管理视图模型（MVVM模式）
    /// 核心职责：封装BOM（物料清单）的查询、新增、编辑、删除、批量操作、默认版本设置等业务逻辑
    /// 适配视图：BOM管理页面（数据绑定、命令响应）
    /// 依赖服务：BOM服务（数据访问）、产品服务（关联产品查询）、对话框服务（用户交互）
    /// </summary>
    public partial class BOMViewModel : ObservableObject
    {
        #region 依赖注入服务
        // BOM核心业务服务（增删改查、默认版本设置等）
        private readonly IBOMService _bomService;
        // 产品服务（加载关联产品列表）
        private readonly IProductService _productService;
        // 对话框服务（统一弹窗交互：提示、确认、错误）
        private readonly IDialogService _dialogService;
        #endregion

        #region 可绑定属性（ObservableProperty自动生成INotifyPropertyChanged实现）
        /// <summary>
        /// BOM列表数据源（ObservableCollection支持UI自动更新）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<BOM> _boms = new();

        /// <summary>
        /// 扩展BOM类：添加选中状态属性（用于批量删除）
        /// 说明：继承BOM实体，新增IsSelected字段，避免修改原始实体类
        /// 注：当前未实际使用，需配合视图勾选框绑定
        /// </summary>
        public partial class BOMWithSelection : BOM
        {
            public bool IsSelected { get; set; }
        }

        /// <summary>
        /// BOM列表对外暴露的属性（兼容视图绑定）
        /// </summary>
        public ObservableCollection<BOM> BOMs => _boms;
        /// <summary>
        /// BOM视图展示数据源（与BOMs同源，可按需扩展过滤逻辑）
        /// </summary>
        public ObservableCollection<BOM> BOMsView => _boms;

        /// <summary>
        /// 关联产品列表（用于BOM选择所属产品）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        /// <summary>
        /// 当前选中的BOM（视图列表选中项绑定）
        /// </summary>
        [ObservableProperty]
        private BOM _selectedBOM;

        /// <summary>
        /// 正在编辑/新增的BOM对象（弹窗表单绑定）
        /// </summary>
        [ObservableProperty]
        private BOM _editingBOM;

        /// <summary>
        /// 搜索关键词（BOM编码/名称模糊查询）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword;

        /// <summary>
        /// 选中的产品ID（按产品筛选BOM）
        /// </summary>
        [ObservableProperty]
        private int? _selectedProductId;

        /// <summary>
        /// 选中的状态（按BOM状态筛选：0=全部，1=草稿，2=审核中，3=已发布等）
        /// </summary>
        [ObservableProperty]
        private int _selectedStatus = 0;

        /// <summary>
        /// 是否只显示默认BOM（同一产品只能有一个默认BOM）
        /// </summary>
        [ObservableProperty]
        private bool _onlyDefaultBOM;

        /// <summary>
        /// 是否正在刷新数据（控制加载动画显示）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// BOM编辑/新增弹窗是否打开
        /// </summary>
        [ObservableProperty]
        private bool _isBOMDialogOpen;

        /// <summary>
        /// 是否为编辑模式（true=编辑，false=新增）
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// BOM总条数（分页用）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 当前页码（分页用）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示条数（固定10条，可按需改为可配置）
        /// </summary>
        private int PageSize => 10;

        /// <summary>
        /// 总页数（分页计算：向上取整）
        /// </summary>
        private int TotalPages => (TotalCount + PageSize - 1) / PageSize;
        #endregion

        #region 构造函数（初始化服务与数据）
        /// <summary>
        /// 构造函数：通过依赖注入获取服务，初始化加载产品和BOM数据
        /// </summary>
        public BOMViewModel(IBOMService bomService, IProductService productService, IDialogService dialogService)
        {
            _bomService = bomService;
            _productService = productService;
            _dialogService = dialogService;

            // 异步加载初始数据（Task.Run避免阻塞UI线程）
            Task.Run(async () =>
            {
                await LoadProductsAsync(); // 先加载产品（BOM需关联产品）
                await LoadBOMsAsync();    // 再加载BOM列表
            });
        }
        #endregion

        #region 分页命令可执行条件（控制按钮是否可用）
        /// <summary>
        /// 上一页可执行条件：当前页码>1
        /// </summary>
        private bool CanPreviousPage() => CurrentPage > 1;

        /// <summary>
        /// 下一页可执行条件：当前页码<总页数
        /// </summary>
        private bool CanNextPage() => CurrentPage < TotalPages;
        #endregion

        #region 数据加载方法
        /// <summary>
        /// 加载产品列表（用于BOM选择所属产品、产品筛选）
        /// </summary>
        private async Task LoadProductsAsync()
        {
            try
            {
                // 调用产品服务获取所有产品
                var products = await _productService.GetAllAsync();

                // UI线程更新（WPF必须在UI线程操作绑定集合）
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
                // 异常提示（通过对话框服务统一展示）
                await _dialogService.ShowErrorAsync("错误", $"加载产品数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载所有BOM数据（初始加载、重置搜索时使用）
        /// </summary>
        private async Task LoadBOMsAsync()
        {
            try
            {
                IsRefreshing = true; // 开启加载状态（UI显示加载动画）

                // 调用BOM服务获取所有BOM
                var boms = await _bomService.GetAllAsync();
                TotalCount = boms.Count(); // 更新总条数（分页计算用）

                // UI线程更新BOM列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _boms.Clear();
                    foreach (var bom in boms)
                    {
                        _boms.Add(bom);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载BOM数据失败: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false; // 关闭加载状态
            }
        }
        #endregion

        #region 搜索与重置命令
        /// <summary>
        /// 搜索BOM（多条件组合筛选）
        /// RelayCommand：绑定视图搜索按钮，支持异步执行
        /// </summary>
        [RelayCommand]
        private async Task SearchBOMsAsync()
        {
            try
            {
                IsRefreshing = true;

                // 1. 获取所有BOM数据（基础数据源）
                var boms = await _bomService.GetAllAsync();

                // 2. 多条件筛选（按优先级依次过滤）
                // 关键词筛选：BOM编码/名称模糊匹配
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                {
                    boms = boms.Where(b =>
                        b.BomCode.Contains(SearchKeyword) ||
                        b.BomName.Contains(SearchKeyword));
                }

                // 产品筛选：匹配选中的产品ID
                if (SelectedProductId.HasValue)
                {
                    boms = boms.Where(b => b.ProductId == SelectedProductId.Value);
                }

                // 状态筛选：选中状态>0时生效（0=全部）
                if (SelectedStatus > 0)
                {
                    byte status = (byte)SelectedStatus; // 转换为实体对应的byte类型
                    boms = boms.Where(b => b.Status == status);
                }

                // 默认BOM筛选：只显示标记为默认的BOM
                if (OnlyDefaultBOM)
                {
                    boms = boms.Where(b => b.IsDefault);
                }

                // 3. 更新分页与列表数据
                TotalCount = boms.Count(); // 筛选后的总条数
                CurrentPage = 1; // 重置为第一页

                // 4. UI更新筛选后的BOM列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _boms.Clear();
                    foreach (var bom in boms)
                    {
                        _boms.Add(bom);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索BOM失败: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件（恢复初始状态并重新加载所有BOM）
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            // 重置所有筛选条件
            SearchKeyword = string.Empty;
            SelectedProductId = null;
            SelectedStatus = 0;
            OnlyDefaultBOM = false;

            // 重新加载所有BOM（异步执行，不阻塞UI）
            Task.Run(async () => await LoadBOMsAsync());
        }
        #endregion

        #region BOM新增/编辑/删除命令
        /// <summary>
        /// 新增BOM（打开新增弹窗）
        /// </summary>
        [RelayCommand]
        private async Task AddBOMAsync()
        {
            // 业务校验：无产品时不允许创建BOM（BOM必须关联产品）
            if (Products.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先添加产品，再创建BOM");
                return;
            }

            // 初始化新增BOM对象（设置默认值）
            EditingBOM = new BOM
            {
                CreateTime = DateTime.Now,    // 创建时间默认当前时间
                EffectiveDate = DateTime.Now, // 生效日期默认当前时间
                Status = 1,                   // 初始状态：草稿（业务约定1=草稿）
                IsDefault = false             // 默认不设为默认BOM
            };

            IsEditMode = false; // 标记为新增模式
            IsBOMDialogOpen = true; // 打开编辑弹窗
        }

        /// <summary>
        /// 编辑BOM（打开编辑弹窗，传入选中的BOM对象）
        /// </summary>
        [RelayCommand]
        private void EditBOM(BOM bom)
        {
            if (bom == null) return; // 防空：未选中BOM时不执行

            // 深拷贝选中的BOM对象（避免直接修改列表中的原始数据，提交后再更新）
            EditingBOM = new BOM
            {
                Id = bom.Id,
                BomCode = bom.BomCode,
                BomName = bom.BomName,
                ProductId = bom.ProductId,
                Version = bom.Version,
                Status = bom.Status,
                EffectiveDate = bom.EffectiveDate,
                ExpiryDate = bom.ExpiryDate,
                IsDefault = bom.IsDefault,
                CreateTime = bom.CreateTime,
                UpdateTime = DateTime.Now // 更新时间设为当前时间
            };

            IsEditMode = true; // 标记为编辑模式
            IsBOMDialogOpen = true; // 打开编辑弹窗
        }

        /// <summary>
        /// 删除BOM（单个删除）
        /// </summary>
        [RelayCommand]
        private async Task DeleteBOMAsync(BOM bom)
        {
            if (bom == null) return; // 防空校验

            // 确认弹窗：避免误操作
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除BOM {bom.BomName} 吗？");
            if (result) // 用户点击确认
            {
                try
                {
                    // 额外校验：默认BOM删除警告（默认BOM可能被生产流程引用）
                    if (bom.IsDefault)
                    {
                        var confirmDefault = await _dialogService.ShowConfirmAsync("警告", "当前BOM是默认版本，删除后可能影响其他功能。确定要删除吗？");
                        if (!confirmDefault) return; // 取消删除
                    }

                    // 调用服务删除BOM（数据库层面删除）
                    await _bomService.DeleteByIdAsync(bom.Id);
                    // 从列表中移除（UI即时更新）
                    _boms.Remove(bom);
                    TotalCount--; // 总条数减1

                    // 操作成功提示
                    await _dialogService.ShowInfoAsync("成功", "BOM删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"删除BOM失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 批量删除BOM（暂未实现，需配合BOMWithSelection类）
        /// 说明：当前BOM列表数据源是ObservableCollection<BOM>，无法直接绑定IsSelected属性
        /// 需将数据源改为ObservableCollection<BOMWithSelection>，并在视图添加勾选框绑定IsSelected
        /// </summary>
        [RelayCommand]
        private async Task BatchDeleteAsync()
        {
            // 临时提示：告知用户功能未实现
            await _dialogService.ShowInfoAsync("提示", "批量删除功能需要模型支持，暂不可用");
            return;

            /* 完整实现步骤：
            1. 将 _boms 类型改为 ObservableCollection<BOMWithSelection>
            2. 视图列表添加 CheckBox，绑定 IsSelected 属性
            3. 取消上面的return，启用以下代码：

            // 筛选选中的BOM
            var selectedBOMs = _boms.Where(b => b.IsSelected).ToList();
            if (selectedBOMs.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择要删除的BOM");
                return;
            }

            // 确认批量删除
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除选中的 {selectedBOMs.Count} 个BOM吗？");
            if (result)
            {
                try
                {
                    // 检查是否包含默认BOM
                    var hasDefault = selectedBOMs.Any(b => b.IsDefault);
                    if (hasDefault)
                    {
                        var confirmDefault = await _dialogService.ShowConfirmAsync("警告", "选中的BOM中包含默认版本，删除后可能影响其他功能。确定要删除吗？");
                        if (!confirmDefault) return;
                    }

                    // 批量删除（建议服务层提供批量删除接口，减少数据库交互）
                    foreach (var bom in selectedBOMs)
                    {
                        await _bomService.DeleteByIdAsync(bom.Id);
                        _boms.Remove(bom);
                    }
                    
                    TotalCount -= selectedBOMs.Count;
                    await _dialogService.ShowInfoAsync("成功", "BOM批量删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"批量删除BOM失败: {ex.Message}");
                }
            }
            */
        }
        #endregion

        #region 导出/保存/取消命令
        /// <summary>
        /// 导出BOM数据（暂未实现）
        /// 典型实现：导出为Excel，包含BOM基本信息+明细物料
        /// </summary>
        [RelayCommand]
        private void ExportBOMs()
        {
            _dialogService.ShowInfoAsync("提示", "导出功能待实现");
        }

        /// <summary>
        /// 保存BOM（新增/编辑共用）
        /// 核心逻辑：表单校验 → 编码唯一性校验 → 默认BOM处理 → 保存数据 → UI更新
        /// </summary>
        [RelayCommand]
        private async Task SaveBOMAsync()
        {
            // 1. 表单必填项校验（前端基础校验，避免无效请求）
            if (string.IsNullOrWhiteSpace(EditingBOM.BomCode))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入BOM编码");
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingBOM.BomName))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入BOM名称");
                return;
            }
            if (EditingBOM.ProductId <= 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择产品");
                return;
            }
            if (string.IsNullOrWhiteSpace(EditingBOM.Version))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入版本号");
                return;
            }
            if (EditingBOM.EffectiveDate == default)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择生效日期");
                return;
            }

            try
            {
                // 2. 新增模式：BOM编码唯一性校验（避免重复编码）
                if (!IsEditMode)
                {
                    bool exists = await _bomService.IsBOMCodeExistsAsync(EditingBOM.BomCode);
                    if (exists)
                    {
                        await _dialogService.ShowInfoAsync("提示", $"BOM编码 {EditingBOM.BomCode} 已存在");
                        return;
                    }
                }

                // 3. 默认BOM处理：如果当前BOM设为默认，取消同一产品下其他BOM的默认状态
                if (EditingBOM.IsDefault)
                {
                    await _bomService.SetDefaultBOMAsync(EditingBOM.Id, EditingBOM.ProductId);
                }

                // 4. 区分新增/编辑，执行保存逻辑
                if (IsEditMode)
                {
                    // 编辑模式：更新现有BOM
                    EditingBOM.UpdateTime = DateTime.Now; // 更新时间戳
                    await _bomService.UpdateAsync(EditingBOM);

                    // UI更新：替换列表中的旧数据
                    var existingBOM = _boms.FirstOrDefault(b => b.Id == EditingBOM.Id);
                    if (existingBOM != null)
                    {
                        var index = _boms.IndexOf(existingBOM);
                        _boms[index] = EditingBOM;
                    }

                    await _dialogService.ShowInfoAsync("成功", "BOM更新成功");
                }
                else
                {
                    // 新增模式：创建新BOM
                    EditingBOM.CreateTime = DateTime.Now; // 创建时间戳
                    var newBOM = await _bomService.AddAsync(EditingBOM); // 服务层返回新增后的BOM（含自增ID）
                    _boms.Add(newBOM); // 加入列表
                    TotalCount++; // 总条数加1

                    await _dialogService.ShowInfoAsync("成功", "BOM添加成功");
                }

                // 5. 保存后关闭弹窗
                IsBOMDialogOpen = false;

                // 6. 重新加载BOM列表（同步默认BOM状态变化）
                await LoadBOMsAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存BOM失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消编辑/新增（关闭弹窗，不保存数据）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsBOMDialogOpen = false;
        }
        #endregion

        #region BOM明细查看/默认版本设置命令
        /// <summary>
        /// 查看BOM明细（物料清单详情）
        /// 典型实现：打开BOM明细弹窗，展示该BOM包含的所有子物料、用量、损耗率等
        /// </summary>
        [RelayCommand]
        private void ViewBOMItems(BOM bom)
        {
            if (bom == null) return;

            _dialogService.ShowInfoAsync("提示", $"查看BOM {bom.BomName} 明细功能待实现");
        }

        /// <summary>
        /// 设置默认BOM（同一产品只能有一个默认BOM，用于生产时自动选择）
        /// </summary>
        [RelayCommand]
        private async Task SetDefaultBOMAsync(BOM bom)
        {
            if (bom == null) return;

            try
            {
                // 校验1：已为默认BOM，无需重复操作
                if (bom.IsDefault)
                {
                    await _dialogService.ShowInfoAsync("提示", "该BOM已经是默认版本");
                    return;
                }

                // 校验2：只有已发布状态的BOM才能设为默认（业务规则：草稿/审核中的BOM不可用）
                if (bom.Status != 3) // 3=已发布（业务约定）
                {
                    await _dialogService.ShowInfoAsync("提示", "只有已发布的BOM才能设为默认版本");
                    return;
                }

                // 调用服务设置默认BOM（内部会取消同一产品其他BOM的默认状态）
                await _bomService.SetDefaultBOMAsync(bom.Id, bom.ProductId);

                // 重新加载BOM列表，更新默认状态显示
                await LoadBOMsAsync();

                await _dialogService.ShowInfoAsync("成功", $"已将BOM {bom.BomName} 设为默认版本");
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"设置默认BOM失败: {ex.Message}");
            }
        }
        #endregion

        #region 分页命令（暂未实现数据分页，仅页码切换）
        /// <summary>
        /// 上一页（页码减1）
        /// CanExecute：通过CanPreviousPage控制按钮是否可用
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPreviousPage))]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                // 待实现：加载当前页码对应的BOM数据（需服务层支持分页查询）
                // await LoadBOMsByPageAsync(CurrentPage, PageSize);
            }
        }

        /// <summary>
        /// 下一页（页码加1）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanNextPage))]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                // 待实现：加载当前页码对应的BOM数据
                // await LoadBOMsByPageAsync(CurrentPage, PageSize);
            }
        }

        /// <summary>
        /// 跳转到指定页（支持int/string类型参数，适配输入框输入）
        /// </summary>
        [RelayCommand]
        private void GoToPage(object pageObj)
        {
            // 处理int类型参数（如分页控件绑定的页码）
            if (pageObj is int pageInt)
            {
                if (pageInt >= 1 && pageInt <= TotalPages)
                {
                    CurrentPage = pageInt;
                    // 待实现：加载指定页码数据
                    // await LoadBOMsByPageAsync(CurrentPage, PageSize);
                }
            }
            // 处理string类型参数（如输入框输入的页码文本）
            else if (pageObj is string pageStr)
            {
                if (int.TryParse(pageStr, out int pageVal) && pageVal >= 1 && pageVal <= TotalPages)
                {
                    CurrentPage = pageVal;
                    // 待实现：加载指定页码数据
                    // await LoadBOMsByPageAsync(CurrentPage, PageSize);
                }
            }
        }
        #endregion
    }
}