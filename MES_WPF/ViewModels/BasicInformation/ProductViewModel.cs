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
    /// 产品管理视图模型
    /// 职责：处理产品管理页面的所有业务逻辑（数据加载、增删改查、分页、筛选等）
    /// 基于MVVM架构，使用CommunityToolkit.Mvvm实现命令和属性通知
    /// </summary>
    public partial class ProductViewModel : ObservableObject
    {
        #region 依赖注入服务
        // 产品业务服务：封装产品相关的数据访问逻辑
        private readonly IProductService _productService;
        // 弹窗服务：统一管理弹窗提示（错误、确认、信息），解耦UI与业务逻辑
        private readonly IDialogService _dialogService;
        #endregion

        #region 内部嵌套类（扩展模型）
        /// <summary>
        /// 扩展Product实体类，添加IsSelected属性用于批量操作（如批量删除）
        /// 注：当前主逻辑未使用该类，仅预留扩展能力
        /// </summary>
        public partial class ProductWithSelection : Product
        {
            /// <summary>
            /// 标记是否选中（用于批量操作）
            /// </summary>
            public bool IsSelected { get; set; }
        }
        #endregion

        #region 可观察属性（绑定到View）
        /// <summary>
        /// 产品列表（核心数据源，绑定到UI的DataGrid）
        /// ObservableCollection自动触发UI更新（属性变更通知）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Product> _products = new();

        /// <summary>
        /// 产品列表视图（对外暴露的只读视图，避免直接修改原集合）
        /// 注：当前仅简单返回_products，可扩展为ICollectionView实现筛选/排序
        /// </summary>
        public ObservableCollection<Product> ProductsView => _products;

        /// <summary>
        /// 当前选中的产品（绑定到DataGrid的SelectedItem）
        /// </summary>
        [ObservableProperty]
        private Product _selectedProduct;

        /// <summary>
        /// 正在编辑的产品（弹窗编辑时的临时对象，避免直接修改原数据）
        /// </summary>
        [ObservableProperty]
        private Product _editingProduct;

        /// <summary>
        /// 搜索关键词（绑定到搜索输入框）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword;

        /// <summary>
        /// 选中的产品类型筛选条件（0=全部，1=成品，2=半成品，3=原材料）
        /// 绑定到筛选下拉框，转换为byte类型匹配实体的ProductType
        /// </summary>
        [ObservableProperty]
        private int _selectedProductType = 0;

        /// <summary>
        /// 选中的状态筛选条件（0=全部，1=启用，2=禁用）
        /// 绑定到状态筛选下拉框，转换为bool类型匹配实体的IsActive
        /// </summary>
        [ObservableProperty]
        private int _selectedStatus = 0;

        /// <summary>
        /// 数据加载中标记（绑定到加载动画的显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 产品编辑弹窗是否打开（绑定到弹窗的IsOpen属性）
        /// </summary>
        [ObservableProperty]
        private bool _isProductDialogOpen;

        /// <summary>
        /// 是否为编辑模式（区分新增/编辑逻辑）
        /// true=编辑已有产品，false=新增产品
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 产品总数（用于分页计算）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 当前页码（绑定到分页控件的当前页）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// </summary>
        /// <param name="productService">产品业务服务</param>
        /// <param name="dialogService">弹窗服务</param>
        public ProductViewModel(IProductService productService, IDialogService dialogService)
        {
            _productService = productService;
            _dialogService = dialogService;

            // 异步加载产品数据（避免UI线程阻塞）
            // 注：Task.Run+async/await 是WPF中异步初始化数据的常用方式
            Task.Run(async () => await LoadProductsAsync());
        }
        #endregion

        #region 分页辅助方法（命令可执行条件）
        /// <summary>
        /// 上一页命令的可执行条件：当前页>1
        /// </summary>
        /// <returns>是否可执行上一页</returns>
        private bool CanPreviousPage() => CurrentPage > 1;

        /// <summary>
        /// 下一页命令的可执行条件：当前页<总页数
        /// </summary>
        /// <returns>是否可执行下一页</returns>
        private bool CanNextPage() => CurrentPage < TotalPages;
        #endregion

        #region 核心业务方法
        /// <summary>
        /// 加载所有产品数据（初始化/刷新时调用）
        /// RelayCommand标记为命令，可直接绑定到UI的按钮/刷新控件
        /// </summary>
        [RelayCommand]
        private async Task LoadProductsAsync()
        {
            try
            {
                // 标记加载中，UI显示加载动画
                IsRefreshing = true;

                // 调用服务层获取所有产品（异步操作，不阻塞UI）
                var products = await _productService.GetAllAsync();
                // 更新产品总数（用于分页）
                TotalCount = products.Count();

                // WPF中UI元素必须在主线程更新，使用Dispatcher.Invoke切换线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    // 清空原有数据（避免重复）
                    Products.Clear();
                    // 批量添加新数据
                    foreach (var product in products)
                    {
                        Products.Add(product);
                    }
                });
            }
            catch (Exception ex)
            {
                // 异常处理：统一弹窗提示错误信息
                await _dialogService.ShowErrorAsync("错误", $"加载产品数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都标记加载完成
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 搜索产品（按关键词/类型/状态筛选）
        /// RelayCommand标记为命令，绑定到搜索按钮
        /// </summary>
        [RelayCommand]
        private async Task SearchProductsAsync()
        {
            try
            {
                IsRefreshing = true;

                // 先获取所有产品，再进行内存筛选（注：建议优化为服务层带筛选条件查询，减少数据传输）
                var products = await _productService.GetAllAsync();

                // 1. 按关键词筛选（产品编码/名称包含关键词，不区分大小写）
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                {
                    products = products.Where(p =>
                        p.ProductCode.Contains(SearchKeyword) ||
                        p.ProductName.Contains(SearchKeyword));
                }

                // 2. 按产品类型筛选（0=全部，>0时转换为byte匹配实体）
                if (SelectedProductType > 0)
                {
                    byte productType = (byte)SelectedProductType;
                    products = products.Where(p => p.ProductType == productType);
                }

                // 3. 按状态筛选（0=全部，1=启用，2=禁用）
                if (SelectedStatus > 0)
                {
                    bool isActive = SelectedStatus == 1;
                    products = products.Where(p => p.IsActive == isActive);
                }

                // 更新筛选后的总数
                TotalCount = products.Count();

                // 更新UI列表（主线程操作）
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Products.Clear();
                    foreach (var product in products)
                    {
                        Products.Add(product);
                    }
                });

                // 筛选后重置为第一页
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索产品失败: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件（清空筛选，恢复初始状态）
        /// RelayCommand标记为命令，绑定到重置按钮
        /// </summary>
        [RelayCommand]
        private void ResetSearch()
        {
            // 清空所有筛选条件
            SearchKeyword = string.Empty;
            SelectedProductType = 0;
            SelectedStatus = 0;

            // 重新加载所有数据
            Task.Run(async () => await LoadProductsAsync());
        }

        /// <summary>
        /// 添加工产品（打开新增弹窗）
        /// RelayCommand标记为命令，绑定到添加按钮
        /// </summary>
        [RelayCommand]
        private void AddProduct()
        {
            // 初始化新的产品对象（设置默认值）
            EditingProduct = new Product
            {
                CreateTime = DateTime.Now, // 默认创建时间为当前时间
                IsActive = true // 默认启用
            };

            // 标记为新增模式
            IsEditMode = false;
            // 打开编辑弹窗
            IsProductDialogOpen = true;
        }

        /// <summary>
        /// 编辑产品（打开编辑弹窗，加载选中产品数据）
        /// RelayCommand标记为命令，绑定到DataGrid的编辑按钮
        /// </summary>
        /// <param name="product">选中的待编辑产品</param>
        [RelayCommand]
        private void EditProduct(Product product)
        {
            // 空值校验：避免选中空数据时出错
            if (product == null) return;

            // 创建产品副本（深拷贝），避免直接修改原集合中的对象（MVVM最佳实践）
            EditingProduct = new Product
            {
                Id = product.Id,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                ProductType = product.ProductType,
                Specification = product.Specification,
                Unit = product.Unit,
                Description = product.Description,
                IsActive = product.IsActive,
                CreateTime = product.CreateTime, // 保留原创建时间
                UpdateTime = DateTime.Now // 更新时间为当前操作时间
            };

            // 标记为编辑模式
            IsEditMode = true;
            // 打开编辑弹窗
            IsProductDialogOpen = true;
        }

        /// <summary>
        /// 删除产品（单条删除）
        /// RelayCommand标记为命令，绑定到DataGrid的删除按钮
        /// </summary>
        /// <param name="product">选中的待删除产品</param>
        [RelayCommand]
        private async Task DeleteProductAsync(Product product)
        {
            if (product == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除产品 {product.ProductName} 吗？");
            if (result)
            {
                try
                {
                    // 调用服务层删除产品
                    await _productService.DeleteByIdAsync(product.Id);
                    // 从UI列表中移除（同步更新）
                    Products.Remove(product);
                    // 更新总数
                    TotalCount--;
                    // 提示删除成功
                    await _dialogService.ShowInfoAsync("成功", "产品删除成功");
                }
                catch (Exception ex)
                {
                    // 异常处理：如外键关联导致删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除产品失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 批量删除产品（预留功能，暂未实现）
        /// 注：需要结合ProductWithSelection的IsSelected属性使用
        /// </summary>
        [RelayCommand]
        private async Task BatchDeleteAsync()
        {
            // 临时提示：功能未实现
            await _dialogService.ShowInfoAsync("提示", "批量删除功能需要模型支持，暂不可用");
            return;

            /* 以下是预留的批量删除逻辑（需启用ProductWithSelection）
            // 筛选选中的产品
            var selectedProducts = Products.Where(p => (p as ProductWithSelection)?.IsSelected ?? false).ToList();
            if (selectedProducts.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择要删除的产品");
                return;
            }

            // 二次确认
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除选中的 {selectedProducts.Count} 个产品吗？");
            if (result)
            {
                try
                {
                    // 批量删除
                    foreach (var product in selectedProducts)
                    {
                        await _productService.DeleteByIdAsync(product.Id);
                        Products.Remove(product);
                    }
                    TotalCount -= selectedProducts.Count;
                    await _dialogService.ShowInfoAsync("成功", "产品批量删除成功");
                }
                catch (Exception ex)
                {
                    await _dialogService.ShowErrorAsync("错误", $"批量删除产品失败: {ex.Message}");
                }
            }
            */
        }

        /// <summary>
        /// 导出产品数据（预留功能，暂未实现）
        /// </summary>
        [RelayCommand]
        private void ExportProducts()
        {
            _dialogService.ShowInfoAsync("提示", "导出功能待实现");
        }

        /// <summary>
        /// 保存产品（新增/编辑统一处理）
        /// RelayCommand标记为命令，绑定到弹窗的保存按钮
        /// </summary>
        [RelayCommand]
        private async Task SaveProductAsync()
        {
            #region 表单验证（必填项校验）
            if (string.IsNullOrWhiteSpace(EditingProduct.ProductCode))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入产品编码");
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingProduct.ProductName))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入产品名称");
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingProduct.Unit))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入计量单位");
                return;
            }
            #endregion

            try
            {
                #region 新增逻辑
                if (!IsEditMode)
                {
                    // 校验产品编码唯一性（新增时）
                    bool exists = await _productService.IsProductCodeExistsAsync(EditingProduct.ProductCode);
                    if (exists)
                    {
                        await _dialogService.ShowInfoAsync("提示", $"产品编码 {EditingProduct.ProductCode} 已存在");
                        return;
                    }

                    // 设置创建时间
                    EditingProduct.CreateTime = DateTime.Now;
                    // 调用服务层新增产品
                    var newProduct = await _productService.AddAsync(EditingProduct);
                    // 添加到UI列表
                    Products.Add(newProduct);
                    // 更新总数
                    TotalCount++;

                    await _dialogService.ShowInfoAsync("成功", "产品添加成功");
                }
                #endregion

                #region 编辑逻辑
                else
                {
                    // 设置更新时间
                    EditingProduct.UpdateTime = DateTime.Now;
                    // 调用服务层更新产品
                    await _productService.UpdateAsync(EditingProduct);

                    // 更新UI列表中的对应产品（替换原对象）
                    var existingProduct = Products.FirstOrDefault(p => p.Id == EditingProduct.Id);
                    if (existingProduct != null)
                    {
                        var index = Products.IndexOf(existingProduct);
                        Products[index] = EditingProduct;
                    }

                    await _dialogService.ShowInfoAsync("成功", "产品更新成功");
                }
                #endregion

                // 保存成功后关闭弹窗
                IsProductDialogOpen = false;
            }
            catch (Exception ex)
            {
                // 异常处理：如数据库约束、网络问题等
                await _dialogService.ShowErrorAsync("错误", $"保存产品失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消编辑（关闭弹窗，不保存修改）
        /// RelayCommand标记为命令，绑定到弹窗的取消按钮
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭弹窗即可，无需额外处理（EditingProduct会在下一次编辑时重置）
            IsProductDialogOpen = false;
        }

        /// <summary>
        /// 查看产品BOM（预留功能，暂未实现）
        /// </summary>
        /// <param name="product">选中的产品</param>
        [RelayCommand]
        private void ViewBOM(Product product)
        {
            if (product == null) return;

            _dialogService.ShowInfoAsync("提示", $"查看产品 {product.ProductName} 的BOM功能待实现");
        }
        #endregion

        #region 分页命令
        /// <summary>
        /// 上一页（带可执行条件校验）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanPreviousPage))]
        private void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                // 注：此处需补充分页加载逻辑（如调用服务层的分页查询方法）
                // await LoadProductsByPageAsync(CurrentPage);
            }
        }

        /// <summary>
        /// 下一页（带可执行条件校验）
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanNextPage))]
        private void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                // 注：此处需补充分页加载逻辑
                // await LoadProductsByPageAsync(CurrentPage);
            }
        }

        /// <summary>
        /// 跳转到指定页（支持int/string类型参数）
        /// </summary>
        /// <param name="pageObj">页码（int或string类型）</param>
        [RelayCommand]
        private void GoToPage(object pageObj)
        {
            // 处理int类型参数
            if (pageObj is int pageInt)
            {
                if (pageInt >= 1 && pageInt <= TotalPages)
                {
                    CurrentPage = pageInt;
                    // 注：此处需补充分页加载逻辑
                    // await LoadProductsByPageAsync(CurrentPage);
                }
            }
            // 处理string类型参数（如输入框的文本）
            else if (pageObj is string pageStr)
            {
                if (int.TryParse(pageStr, out int pageVal) && pageVal >= 1 && pageVal <= TotalPages)
                {
                    CurrentPage = pageVal;
                    // 注：此处需补充分页加载逻辑
                    // await LoadProductsByPageAsync(CurrentPage);
                }
            }
        }
        #endregion

        #region 分页计算属性
        /// <summary>
        /// 总页数（向上取整：(总数+每页数量-1)/每页数量）
        /// </summary>
        private int TotalPages => (TotalCount + PageSize - 1) / PageSize;

        /// <summary>
        /// 每页显示数量（固定10条，可扩展为可配置）
        /// </summary>
        private int PageSize => 10;
        #endregion
    }
}