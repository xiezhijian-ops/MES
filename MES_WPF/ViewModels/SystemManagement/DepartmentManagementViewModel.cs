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
    /// 部门管理视图模型
    /// 核心职责：封装部门管理页面的所有业务逻辑（增删改查、筛选、分页、状态管理、父部门关联等）
    /// 基于MVVM架构，使用CommunityToolkit.Mvvm实现属性通知和命令绑定，解耦UI与业务逻辑
    /// </summary>
    public partial class DepartmentManagementViewModel : ObservableObject
    {
        #region 依赖注入服务（核心依赖）
        // 部门业务服务：封装部门数据的CRUD及专属业务逻辑（如路径更新、子部门查询）
        private readonly IDepartmentService _departmentService;
        // 弹窗交互服务：统一管理系统弹窗（错误、确认、信息提示），避免直接耦合WPF弹窗API
        private readonly IDialogService _dialogService;
        #endregion

        #region 视图绑定属性（UI双向绑定的核心属性）
        /// <summary>
        /// 当前选中的部门（绑定到DataGrid/TreeView的SelectedItem）
        /// 用于删除、编辑、查看员工等操作的数据源
        /// </summary>
        [ObservableProperty] // CommunityToolkit特性：自动生成属性变更通知（INotifyPropertyChanged）
        private Department? _selectedDepartment;

        /// <summary>
        /// 搜索关键词（绑定到搜索输入框）
        /// 支持部门名称、编码、负责人的模糊匹配（不区分大小写）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword = string.Empty; // 初始化为空字符串，避免null

        /// <summary>
        /// 数据加载状态标识（绑定到加载动画的Visibility）
        /// true=加载中（显示动画），false=加载完成（隐藏动画）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 部门总数（绑定到分页控件的总数显示）
        /// 用于计算总页数，仅做展示，未实际参与分页逻辑（当前分页仅前端假分页）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 选中的状态筛选条件（绑定到状态筛选下拉框）
        /// 枚举约定：0-全部，1-正常，2-禁用（与Department实体的Status字段一致）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 0; // 初始值为0（全部）

        /// <summary>
        /// 当前页码（绑定到分页控件的当前页输入框/按钮）
        /// 初始值为1（默认第一页）
        /// </summary>
        [ObservableProperty]
        private int _currentPage = 1;

        /// <summary>
        /// 每页显示数量（绑定到分页控件的页大小选择框）
        /// 固定为10，可扩展为可配置项（如10/20/50）
        /// </summary>
        [ObservableProperty]
        private int _pageSize = 10;

        /// <summary>
        /// 页面标题（绑定到窗口/页面的Title属性）
        /// 可动态修改（如编辑时改为"编辑部门"）
        /// </summary>
        [ObservableProperty]
        private string _title = "部门管理";

        #region 新增/编辑部门弹窗相关属性（弹窗专属绑定属性）
        /// <summary>
        /// 部门编辑弹窗是否打开（绑定到弹窗的IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isDepartmentDialogOpen;

        /// <summary>
        /// 是否为编辑模式（区分新增/编辑逻辑）
        /// true=编辑已有部门，false=新增部门
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的部门对象（绑定到弹窗的输入控件）
        /// 新增时：初始化新对象；编辑时：创建原对象副本（避免直接修改列表数据）
        /// </summary>
        [ObservableProperty]
        private Department _editingDepartment = new Department(); // 初始化为空对象

        /// <summary>
        /// 选中的父部门ID（绑定到父部门下拉框的SelectedValue）
        /// 0表示顶级部门（ParentId=null），其他值为父部门的Id
        /// </summary>
        [ObservableProperty]
        private int? _selectedParentDepartmentId;

        /// <summary>
        /// 可选的父部门列表（绑定到父部门下拉框的ItemsSource）
        /// 包含"顶级部门"选项+所有可选父部门（编辑时排除当前部门及其子部门）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Department> _parentDepartments = new ObservableCollection<Department>();
        #endregion

        /// <summary>
        /// 部门列表（核心数据源，绑定到DataGrid的ItemsSource）
        /// ObservableCollection自动触发UI更新（集合变更通知INotifyCollectionChanged）
        /// </summary>
        public ObservableCollection<Department> Departments { get; } = new();

        /// <summary>
        /// 部门列表视图（用于筛选/排序的包装层）
        /// ICollectionView提供内置的筛选、排序、分组能力，不修改原集合
        /// </summary>
        public ICollectionView? DepartmentsView { get; private set; }
        #endregion

        #region 属性变更通知处理（CommunityToolkit特性）
        /// <summary>
        /// SearchKeyword属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：关键词变化时立即刷新筛选视图，实现实时搜索
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            DepartmentsView?.Refresh(); // 刷新筛选：重新执行DepartmentFilter逻辑
        }

        /// <summary>
        /// SelectedStatus属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：状态筛选条件变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的状态值</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            DepartmentsView?.Refresh(); // 刷新筛选：重新执行DepartmentFilter逻辑
        }
        #endregion

        #region 构造函数（初始化核心逻辑）
        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// 注：需通过DI容器注入服务，避免手动new导致耦合
        /// </summary>
        /// <param name="departmentService">部门服务（必传，空值抛异常）</param>
        /// <param name="dialogService">弹窗服务（必传，空值抛异常）</param>
        /// <exception cref="ArgumentNullException">服务实例为空时抛出</exception>
        public DepartmentManagementViewModel(
            IDepartmentService departmentService,
            IDialogService dialogService)
        {
            // 空值校验：防止空服务导致后续NPE（空指针异常）
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化筛选器：绑定自定义筛选逻辑到DepartmentsView
            SetupFilter();

            // 异步加载部门数据（_= 忽略返回值，避免警告；不阻塞构造函数执行）
            _ = LoadDepartmentsAsync();
        }
        #endregion

        #region 私有核心方法（内部业务逻辑封装）
        /// <summary>
        /// 设置部门列表筛选规则（初始化DepartmentsView）
        /// 核心：将ICollectionView与Departments集合绑定，并注册自定义筛选逻辑
        /// </summary>
        private void SetupFilter()
        {
            // 获取Departments集合的默认视图（WPF内置的集合视图包装器）
            DepartmentsView = CollectionViewSource.GetDefaultView(Departments);
            if (DepartmentsView != null)
            {
                // 绑定自定义筛选逻辑：DepartmentFilter方法作为筛选器
                // 每次Refresh()时会执行该方法过滤数据
                DepartmentsView.Filter = DepartmentFilter;
            }
        }

        /// <summary>
        /// 部门筛选核心逻辑（关键词+状态双重过滤）
        /// 由DepartmentsView.Filter调用，返回true表示保留该数据，false表示过滤掉
        /// </summary>
        /// <param name="obj">待筛选的部门对象（ICollectionView传入的集合项）</param>
        /// <returns>是否符合筛选条件</returns>
        private bool DepartmentFilter(object obj)
        {
            // 无筛选条件时（关键词为空+状态为全部），全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) && SelectedStatus == 0)
            {
                return true;
            }

            // 类型校验：确保obj是Department类型（避免类型转换异常）
            if (obj is Department department)
            {
                // 1. 关键词匹配逻辑：支持名称/编码/负责人，不区分大小写
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (department.DeptName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 部门名称匹配（null容错）
                                     (department.DeptCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 部门编码匹配（null容错）
                                     (department.Leader?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);    // 负责人匹配（null容错）

                // 2. 状态匹配逻辑：0=全部，1=正常，2=禁用
                bool matchesStatus = SelectedStatus == 0 || department.Status == SelectedStatus;

                // 双重条件满足：关键词匹配 AND 状态匹配
                return matchesKeyword && matchesStatus;
            }

            // 非Department类型，直接过滤掉
            return false;
        }

        /// <summary>
        /// 加载所有部门数据（初始化/刷新时调用）
        /// 核心逻辑：从服务层获取数据，更新UI集合，处理异常
        /// </summary>
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 清空现有数据：避免重复加载导致数据重复
                Departments.Clear();

                // 异步获取所有部门（服务层封装数据库查询逻辑）
                var departments = await _departmentService.GetAllAsync();

                // 遍历添加到ObservableCollection：触发UI自动更新
                foreach (var department in departments)
                {
                    Departments.Add(department);
                }

                // 更新总数：用于分页计算
                TotalCount = Departments.Count;

                // 刷新筛选视图：确保新数据应用筛选规则
                DepartmentsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示错误信息（用户友好）
                await _dialogService.ShowErrorAsync("错误", $"加载部门数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都标记加载完成：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 加载父部门列表（新增/编辑弹窗的父部门下拉框数据源）
        /// 核心逻辑：添加顶级部门选项 + 过滤可选父部门（避免循环引用）
        /// </summary>
        private async Task LoadParentDepartmentsAsync()
        {
            try
            {
                // 清空现有数据：避免重复加载
                ParentDepartments.Clear();

                // 添加顶级部门选项：Id=0，名称="顶级部门"，ParentId=null（标识根节点）
                ParentDepartments.Add(new Department { Id = 0, DeptName = "顶级部门", ParentId = null });

                // 获取所有部门数据
                var departments = await _departmentService.GetAllAsync();

                // 编辑模式下的特殊处理：排除当前部门及其子部门（避免设置自己为父部门，导致循环引用）
                if (IsEditMode)
                {
                    // 获取当前编辑部门及其所有子部门的ID集合
                    var childDepts = await _departmentService.GetDepartmentAndChildrenAsync(EditingDepartment.Id);
                    var childIds = childDepts.Select(d => d.Id).ToList();

                    // 过滤：排除当前部门 + 其子部门
                    departments = departments.Where(d => !childIds.Contains(d.Id) && d.Id != EditingDepartment.Id);
                }

                // 将可选父部门添加到列表（供下拉选择）
                foreach (var department in departments)
                {
                    ParentDepartments.Add(department);
                }
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示加载失败
                await _dialogService.ShowErrorAsync("错误", $"加载父部门数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令方法（RelayCommand：绑定到UI按钮的执行逻辑）
        /// <summary>
        /// 刷新部门列表（绑定到刷新按钮）
        /// 核心逻辑：重新调用LoadDepartmentsAsync加载最新数据
        /// </summary>
        [RelayCommand] // CommunityToolkit特性：自动生成ICommand属性（RefreshDepartmentsCommand）
        private async Task RefreshDepartments()
        {
            await LoadDepartmentsAsync();
        }

        /// <summary>
        /// 执行搜索（绑定到搜索按钮）
        /// 核心逻辑：刷新筛选视图 + 重置页码到第一页
        /// </summary>
        [RelayCommand]
        private async Task SearchDepartments()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 刷新筛选视图：应用最新的关键词/状态筛选规则
                DepartmentsView?.Refresh();

                // 搜索后重置页码：默认显示第一页结果
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示搜索失败
                await _dialogService.ShowErrorAsync("错误", $"搜索失败: {ex.Message}");
            }
            finally
            {
                // 标记加载完成：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 重置搜索条件（绑定到重置按钮）
        /// 核心逻辑：清空关键词 + 重置状态筛选 + 重新搜索
        /// </summary>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空搜索关键词
            SearchKeyword = string.Empty;
            // 重置状态筛选为全部
            SelectedStatus = 0;

            // 重新执行搜索：应用重置后的条件
            await SearchDepartments();
        }

        /// <summary>
        /// 批量删除部门（绑定到批量删除按钮，当前仅支持单个删除）
        /// 核心逻辑：校验选中项 + 业务规则校验（无子部门/无员工） + 执行删除
        /// </summary>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的部门（当前逻辑：仅支持单个选中，可扩展为多选）
            var selectedDepartments = Departments.Where(d => d == SelectedDepartment).ToList();

            // 无选中项提示：用户友好
            if (selectedDepartments.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的部门");
                return;
            }

            // 二次确认：防止误操作（删除不可逆）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedDepartments.Count} 个部门吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    foreach (var department in selectedDepartments)
                    {
                        // 业务规则1：有子部门不能删除（需先删除子部门）
                        var children = await _departmentService.GetChildDepartmentsAsync(department.Id);
                        if (children.Any())
                        {
                            await _dialogService.ShowErrorAsync("错误", $"部门 {department.DeptName} 存在子部门，无法删除。请先删除子部门。");
                            continue; // 跳过当前部门，继续处理下一个
                        }

                        // 业务规则2：有员工不能删除（需先移除员工）
                        var employees = await _departmentService.GetDepartmentEmployeesAsync(department.Id);
                        if (employees.Any())
                        {
                            await _dialogService.ShowErrorAsync("错误", $"部门 {department.DeptName} 存在员工，无法删除。请先移除部门下的员工。");
                            continue; // 跳过当前部门，继续处理下一个
                        }

                        // 执行删除：调用服务层删除方法
                        await _departmentService.DeleteAsync(department);
                        // 从UI列表移除：触发UI更新
                        Departments.Remove(department);
                    }

                    // 更新总数：同步删除后的数据量
                    TotalCount = Departments.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "部门已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除部门失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出部门数据（绑定到导出按钮，预留功能）
        /// </summary>
        [RelayCommand]
        private async Task ExportDepartments()
        {
            // 临时提示：功能未实现
            await _dialogService.ShowInfoAsync("导出", "部门导出功能尚未实现");
        }

        /// <summary>
        /// 分页跳转（绑定到分页控件的页码按钮/输入框）
        /// 核心逻辑：校验页码合法性，更新当前页码
        /// </summary>
        /// <param name="page">目标页码（由UI传递）</param>
        [RelayCommand]
        private async Task GoToPage(int page)
        {
            // 页码合法性校验：1 ≤ page ≤ 总页数（总页数=向上取整(总数/页大小)）
            if (page < 1 || page > (TotalCount + PageSize - 1) / PageSize)
            {
                return; // 非法页码，直接返回
            }

            // 更新当前页码：UI自动刷新分页显示
            CurrentPage = page;
        }

        /// <summary>
        /// 新增部门（绑定到新增按钮）
        /// 核心逻辑：初始化新增对象 + 加载父部门列表 + 打开弹窗
        /// </summary>
        [RelayCommand]
        private async Task AddDepartment()
        {
            // 标记为新增模式：区分保存逻辑
            IsEditMode = false;

            // 初始化新部门对象（设置默认值）
            EditingDepartment = new Department
            {
                Status = 1,          // 默认状态：正常（1）
                CreateTime = DateTime.Now, // 创建时间：当前时间
                DeptCode = string.Empty,   // 部门编码：空（需用户输入）
                DeptName = string.Empty,   // 部门名称：空（需用户输入）
                DeptPath = string.Empty,   // 部门路径：空（保存后自动生成）
                Leader = string.Empty,     // 负责人：空（可选）
                Phone = string.Empty,      // 联系电话：空（可选）
                Email = string.Empty,      // 邮箱：空（可选）
                ParentId = null,           // 父部门ID：默认顶级（null）
                SortOrder = 1              // 排序号：默认1
            };

            // 加载父部门列表：供下拉选择
            await LoadParentDepartmentsAsync();

            // 默认选中顶级部门：下拉框默认选中第一项
            SelectedParentDepartmentId = 0;

            // 打开编辑弹窗：UI显示弹窗
            IsDepartmentDialogOpen = true;
        }

        /// <summary>
        /// 编辑部门（绑定到DataGrid的编辑按钮）
        /// 核心逻辑：创建原对象副本 + 加载父部门列表 + 打开弹窗
        /// </summary>
        /// <param name="department">要编辑的部门（由UI传递选中项）</param>
        [RelayCommand]
        private async Task EditDepartment(Department? department)
        {
            // 空值校验：避免传入null导致异常
            if (department == null) return;

            // 标记为编辑模式：区分保存逻辑
            IsEditMode = true;

            // 创建部门副本（深拷贝）：避免直接修改原列表数据（MVVM最佳实践）
            EditingDepartment = new Department
            {
                Id = department.Id,               // 部门ID（主键，不可改）
                DeptCode = department.DeptCode,   // 部门编码
                DeptName = department.DeptName,   // 部门名称
                DeptPath = department.DeptPath,   // 部门路径（保存时自动更新）
                Leader = department.Leader,       // 负责人
                Phone = department.Phone,         // 联系电话
                Email = department.Email,         // 邮箱
                ParentId = department.ParentId,   // 父部门ID
                SortOrder = department.SortOrder, // 排序号
                Status = department.Status,       // 状态
                CreateTime = department.CreateTime, // 创建时间（不可改）
                UpdateTime = department.UpdateTime, // 更新时间（保存时更新）
                Remark = department.Remark        // 备注
            };

            // 加载父部门列表（排除当前部门及其子部门，避免循环引用）
            await LoadParentDepartmentsAsync();

            // 设置当前父部门选中项：默认选中原父部门（无则选顶级）
            SelectedParentDepartmentId = EditingDepartment.ParentId ?? 0;

            // 打开编辑弹窗：UI显示弹窗
            IsDepartmentDialogOpen = true;
        }

        /// <summary>
        /// 单个删除部门（绑定到DataGrid的删除按钮）
        /// 核心逻辑：业务规则校验 + 二次确认 + 执行删除
        /// </summary>
        /// <param name="department">要删除的部门（由UI传递选中项）</param>
        [RelayCommand]
        private async Task DeleteDepartment(Department? department)
        {
            // 空值校验：避免传入null导致异常
            if (department == null) return;

            // 业务规则校验1：有子部门不能删除
            var children = await _departmentService.GetChildDepartmentsAsync(department.Id);
            if (children.Any())
            {
                await _dialogService.ShowErrorAsync("错误", $"部门 {department.DeptName} 存在子部门，无法删除。请先删除子部门。");
                return; // 终止删除流程
            }

            // 业务规则校验2：有员工不能删除
            var employees = await _departmentService.GetDepartmentEmployeesAsync(department.Id);
            if (employees.Any())
            {
                await _dialogService.ShowErrorAsync("错误", $"部门 {department.DeptName} 存在员工，无法删除。请先移除部门下的员工。");
                return; // 终止删除流程
            }

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除部门\"{department.DeptName}\"吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    // 执行删除：调用服务层删除方法
                    await _departmentService.DeleteAsync(department);
                    // 从UI列表移除：触发UI更新
                    Departments.Remove(department);
                    // 更新总数：同步删除后的数据量
                    TotalCount = Departments.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "部门已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除部门失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消编辑（绑定到弹窗的取消按钮）
        /// 核心逻辑：关闭弹窗（不保存任何修改）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭编辑弹窗：UI隐藏弹窗
            IsDepartmentDialogOpen = false;
        }

        /// <summary>
        /// 保存部门（绑定到弹窗的保存按钮，支持新增/编辑）
        /// 核心逻辑：数据校验 + 父部门ID处理 + 保存数据 + 更新路径 + 刷新UI
        /// </summary>
        [RelayCommand]
        private async Task SaveDepartment()
        {
            // 数据校验：必填项非空校验
            if (string.IsNullOrWhiteSpace(EditingDepartment.DeptName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入部门名称");
                return; // 终止保存流程
            }

            if (string.IsNullOrWhiteSpace(EditingDepartment.DeptCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入部门编码");
                return; // 终止保存流程
            }

            try
            {
                // 处理父部门ID：0表示顶级部门（ParentId=null），其他值为选中的父部门ID
                EditingDepartment.ParentId = SelectedParentDepartmentId == 0 ? null : SelectedParentDepartmentId;

                if (IsEditMode) // 编辑模式
                {
                    // 更新更新时间：当前操作时间
                    EditingDepartment.UpdateTime = DateTime.Now;
                    // 执行更新：调用服务层更新方法
                    await _departmentService.UpdateAsync(EditingDepartment);

                    // 更新部门路径：递归更新当前部门及子部门的路径（如1/2/3）
                    await _departmentService.UpdateDepartmentPathAsync(EditingDepartment.Id);

                    // 更新列表中的数据：替换原对象（触发UI更新）
                    var existingDepartment = Departments.FirstOrDefault(d => d.Id == EditingDepartment.Id);
                    if (existingDepartment != null)
                    {
                        int index = Departments.IndexOf(existingDepartment);
                        Departments[index] = EditingDepartment;
                    }

                    // 成功提示：编辑成功
                    await _dialogService.ShowInfoAsync("成功", "部门信息已更新");
                }
                else // 新增模式
                {
                    // 执行新增：调用服务层新增方法，返回新创建的部门（含自增ID）
                    var newDepartment = await _departmentService.AddAsync(EditingDepartment);

                    // 更新部门路径：为新部门生成层级路径
                    await _departmentService.UpdateDepartmentPathAsync(newDepartment.Id);

                    // 添加到UI列表：触发UI更新
                    Departments.Add(newDepartment);
                    // 更新总数：同步新增后的数据量
                    TotalCount = Departments.Count;

                    // 成功提示：新增成功
                    await _dialogService.ShowInfoAsync("成功", "部门已创建");
                }

                // 关闭弹窗：保存后隐藏弹窗
                IsDepartmentDialogOpen = false;

                // 刷新视图：确保新数据应用筛选规则
                DepartmentsView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示保存失败
                await _dialogService.ShowErrorAsync("错误", $"保存部门失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 禁用部门（绑定到DataGrid的禁用按钮）
        /// 核心逻辑：修改状态为禁用 + 保存更新
        /// </summary>
        /// <param name="department">要禁用的部门（由UI传递选中项）</param>
        [RelayCommand]
        private async Task DisableDepartment(Department? department)
        {
            // 空值校验：避免传入null导致异常
            if (department == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("禁用部门", $"确定要禁用部门\"{department.DeptName}\"吗？");

            if (result) // 用户确认禁用
            {
                try
                {
                    department.Status = 2; // 修改状态为禁用（2=禁用）
                    await _departmentService.UpdateAsync(department); // 保存更新
                    DepartmentsView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "部门已禁用"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示禁用失败
                    await _dialogService.ShowErrorAsync("错误", $"禁用部门失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 启用部门（绑定到DataGrid的启用按钮）
        /// 核心逻辑：修改状态为正常 + 保存更新
        /// </summary>
        /// <param name="department">要启用的部门（由UI传递选中项）</param>
        [RelayCommand]
        private async Task EnableDepartment(Department? department)
        {
            // 空值校验：避免传入null导致异常
            if (department == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("启用部门", $"确定要启用部门\"{department.DeptName}\"吗？");

            if (result) // 用户确认启用
            {
                try
                {
                    department.Status = 1; // 修改状态为正常（1=正常）
                    await _departmentService.UpdateAsync(department); // 保存更新
                    DepartmentsView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "部门已启用"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示启用失败
                    await _dialogService.ShowErrorAsync("错误", $"启用部门失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 查看部门下的员工（绑定到DataGrid的查看员工按钮，预留功能）
        /// 核心逻辑：获取部门员工 + 提示员工数量（实际项目可扩展为打开员工列表）
        /// </summary>
        /// <param name="department">目标部门（由UI传递选中项）</param>
        [RelayCommand]
        private async Task ViewEmployees(Department? department)
        {
            // 空值校验：避免传入null导致异常
            if (department == null) return;

            try
            {
                // 获取部门下的所有员工
                var employees = await _departmentService.GetDepartmentEmployeesAsync(department.Id);
                if (!employees.Any())
                {
                    // 无员工提示：用户友好
                    await _dialogService.ShowInfoAsync("提示", $"部门 {department.DeptName} 暂无员工");
                    return;
                }

                // 临时提示：实际项目可替换为打开员工列表弹窗/页面
                await _dialogService.ShowInfoAsync("查看员工", $"部门 {department.DeptName} 有 {employees.Count()} 名员工");
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示获取员工失败
                await _dialogService.ShowErrorAsync("错误", $"获取部门员工失败: {ex.Message}");
            }
        }
        #endregion
    }
}