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
    /// 员工管理视图模型
    /// 核心职责：封装员工管理页面的所有业务逻辑（增删改查、筛选、分页、部门调动、离职处理、状态管理等）
    /// 基于MVVM架构，使用CommunityToolkit.Mvvm实现属性通知和命令绑定，解耦UI与业务逻辑
    /// </summary>
    public partial class EmployeeManagementViewModel : ObservableObject
    {
        #region 依赖注入服务（核心依赖）
        // 员工业务服务：封装员工数据的CRUD及专属业务逻辑（如调动、离职、状态更新）
        private readonly IEmployeeService _employeeService;
        // 部门业务服务：封装部门数据查询（用于员工-部门关联）
        private readonly IDepartmentService _departmentService;
        // 弹窗交互服务：统一管理系统弹窗（错误、确认、信息提示），避免直接耦合WPF弹窗API
        private readonly IDialogService _dialogService;
        #endregion

        #region 视图绑定属性（UI双向绑定的核心属性）
        /// <summary>
        /// 当前选中的员工（绑定到DataGrid的SelectedItem）
        /// 用于删除、编辑、调动、离职等操作的数据源
        /// </summary>
        [ObservableProperty] // CommunityToolkit特性：自动生成属性变更通知（INotifyPropertyChanged）
        private Employee? _selectedEmployee;

        /// <summary>
        /// 搜索关键词（绑定到搜索输入框）
        /// 支持员工编码、姓名、职位、手机号、邮箱的模糊匹配（不区分大小写）
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
        /// 员工总数（绑定到分页控件的总数显示）
        /// 用于计算总页数，仅做展示，未实际参与分页逻辑（当前分页仅前端假分页）
        /// </summary>
        [ObservableProperty]
        private int _totalCount;

        /// <summary>
        /// 入职时间筛选-开始日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内入职的员工
        /// </summary>
        [ObservableProperty]
        private DateTime? _entryDateStart;

        /// <summary>
        /// 入职时间筛选-结束日期（绑定到日期选择控件）
        /// 用于筛选指定时间段内入职的员工
        /// </summary>
        [ObservableProperty]
        private DateTime? _entryDateEnd;

        /// <summary>
        /// 选中的状态筛选条件（绑定到状态筛选下拉框）
        /// 枚举约定：0-全部，1-在职，2-离职，3-休假（与Employee实体的Status字段一致）
        /// </summary>
        [ObservableProperty]
        private byte _selectedStatus = 0; // 初始值为0（全部）

        /// <summary>
        /// 选中的部门筛选条件（绑定到部门筛选下拉框的SelectedValue）
        /// null表示不筛选部门，其他值为部门ID
        /// </summary>
        [ObservableProperty]
        private int? _selectedDepartmentId;

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
        /// 可动态修改（如编辑时改为"编辑员工"）
        /// </summary>
        [ObservableProperty]
        private string _title = "员工管理";

        #region 新增/编辑员工弹窗相关属性（弹窗专属绑定属性）
        /// <summary>
        /// 员工编辑弹窗是否打开（绑定到弹窗的IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isEmployeeDialogOpen;

        /// <summary>
        /// 是否为编辑模式（区分新增/编辑逻辑）
        /// true=编辑已有员工，false=新增员工
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 正在编辑的员工对象（绑定到弹窗的输入控件）
        /// 新增时：初始化新对象；编辑时：创建原对象副本（避免直接修改列表数据）
        /// </summary>
        [ObservableProperty]
        private Employee _editingEmployee = new Employee(); // 初始化为空对象

        /// <summary>
        /// 部门列表（绑定到部门下拉框的ItemsSource）
        /// 供新增/编辑/调动员工时选择部门
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Department> _departments = new ObservableCollection<Department>();
        #endregion

        #region 员工调动弹窗相关属性（调动弹窗专属绑定属性）
        /// <summary>
        /// 员工调动弹窗是否打开（绑定到调动弹窗的IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isTransferDialogOpen;

        /// <summary>
        /// 待调动的员工对象（绑定到调动弹窗的数据源）
        /// </summary>
        [ObservableProperty]
        private Employee? _transferEmployee;

        /// <summary>
        /// 新部门ID（绑定到调动弹窗的部门下拉框的SelectedValue）
        /// </summary>
        [ObservableProperty]
        private int _newDepartmentId;

        /// <summary>
        /// 新职位（绑定到调动弹窗的职位输入框）
        /// </summary>
        [ObservableProperty]
        private string _newPosition = string.Empty;
        #endregion

        #region 员工离职弹窗相关属性（离职弹窗专属绑定属性）
        /// <summary>
        /// 员工离职弹窗是否打开（绑定到离职弹窗的IsOpen属性）
        /// true=弹窗显示，false=弹窗隐藏
        /// </summary>
        [ObservableProperty]
        private bool _isLeaveDialogOpen;

        /// <summary>
        /// 待离职的员工对象（绑定到离职弹窗的数据源）
        /// </summary>
        [ObservableProperty]
        private Employee? _leaveEmployee;

        /// <summary>
        /// 离职日期（绑定到离职弹窗的日期选择控件）
        /// 默认值为当前日期
        /// </summary>
        [ObservableProperty]
        private DateTime _leaveDate = DateTime.Now;
        #endregion

        /// <summary>
        /// 员工列表（核心数据源，绑定到DataGrid的ItemsSource）
        /// ObservableCollection自动触发UI更新（集合变更通知INotifyCollectionChanged）
        /// </summary>
        public ObservableCollection<Employee> Employees { get; } = new();

        /// <summary>
        /// 员工列表视图（用于筛选/排序的包装层）
        /// ICollectionView提供内置的筛选、排序、分组能力，不修改原集合
        /// </summary>
        public ICollectionView? EmployeesView { get; private set; }
        #endregion

        #region 属性变更通知处理（CommunityToolkit特性）
        /// <summary>
        /// SearchKeyword属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：关键词变化时立即刷新筛选视图，实现实时搜索
        /// </summary>
        /// <param name="value">新的搜索关键词</param>
        partial void OnSearchKeywordChanged(string value)
        {
            EmployeesView?.Refresh(); // 刷新筛选：重新执行EmployeeFilter逻辑
        }

        /// <summary>
        /// SelectedStatus属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：状态筛选条件变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的状态值</param>
        partial void OnSelectedStatusChanged(byte value)
        {
            EmployeesView?.Refresh(); // 刷新筛选：重新执行EmployeeFilter逻辑
        }

        /// <summary>
        /// SelectedDepartmentId属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：部门筛选条件变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的部门ID</param>
        partial void OnSelectedDepartmentIdChanged(int? value)
        {
            EmployeesView?.Refresh(); // 刷新筛选：重新执行EmployeeFilter逻辑
        }

        /// <summary>
        /// EntryDateStart属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：入职开始日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的入职开始日期</param>
        partial void OnEntryDateStartChanged(DateTime? value)
        {
            EmployeesView?.Refresh(); // 刷新筛选：重新执行EmployeeFilter逻辑
        }

        /// <summary>
        /// EntryDateEnd属性变更时的自动回调方法（CommunityToolkit生成）
        /// 核心逻辑：入职结束日期变化时立即刷新筛选视图
        /// </summary>
        /// <param name="value">新的入职结束日期</param>
        partial void OnEntryDateEndChanged(DateTime? value)
        {
            EmployeesView?.Refresh(); // 刷新筛选：重新执行EmployeeFilter逻辑
        }
        #endregion

        #region 构造函数（初始化核心逻辑）
        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// 注：需通过DI容器注入服务，避免手动new导致耦合
        /// </summary>
        /// <param name="employeeService">员工服务（必传，空值抛异常）</param>
        /// <param name="departmentService">部门服务（必传，空值抛异常）</param>
        /// <param name="dialogService">弹窗服务（必传，空值抛异常）</param>
        /// <exception cref="ArgumentNullException">服务实例为空时抛出</exception>
        public EmployeeManagementViewModel(
            IEmployeeService employeeService,
            IDepartmentService departmentService,
            IDialogService dialogService)
        {
            // 空值校验：防止空服务导致后续NPE（空指针异常）
            _employeeService = employeeService ?? throw new ArgumentNullException(nameof(employeeService));
            _departmentService = departmentService ?? throw new ArgumentNullException(nameof(departmentService));
            _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));

            // 初始化筛选器：绑定自定义筛选逻辑到EmployeesView
            SetupFilter();

            // 异步加载员工数据（_= 忽略返回值，避免警告；不阻塞构造函数执行）
            _ = LoadEmployeesAsync();

            // 异步加载部门数据（供新增/编辑/调动员工时选择部门）
            _ = LoadDepartmentsAsync();
        }
        #endregion

        #region 私有核心方法（内部业务逻辑封装）
        /// <summary>
        /// 设置员工列表筛选规则（初始化EmployeesView）
        /// 核心：将ICollectionView与Employees集合绑定，并注册自定义筛选逻辑
        /// </summary>
        private void SetupFilter()
        {
            // 获取Employees集合的默认视图（WPF内置的集合视图包装器）
            EmployeesView = CollectionViewSource.GetDefaultView(Employees);
            if (EmployeesView != null)
            {
                // 绑定自定义筛选逻辑：EmployeeFilter方法作为筛选器
                // 每次Refresh()时会执行该方法过滤数据
                EmployeesView.Filter = EmployeeFilter;
            }
        }

        /// <summary>
        /// 员工筛选核心逻辑（关键词+状态+部门+入职时间四重过滤）
        /// 由EmployeesView.Filter调用，返回true表示保留该数据，false表示过滤掉
        /// </summary>
        /// <param name="obj">待筛选的员工对象（ICollectionView传入的集合项）</param>
        /// <returns>是否符合筛选条件</returns>
        private bool EmployeeFilter(object obj)
        {
            // 无筛选条件时（所有筛选条件均为默认值），全部显示
            if (string.IsNullOrWhiteSpace(SearchKeyword) &&
                SelectedStatus == 0 &&
                SelectedDepartmentId == null &&
                !EntryDateStart.HasValue &&
                !EntryDateEnd.HasValue)
            {
                return true;
            }

            // 类型校验：确保obj是Employee类型（避免类型转换异常）
            if (obj is Employee employee)
            {
                // 1. 关键词匹配逻辑：支持员工编码/姓名/职位/手机号/邮箱，不区分大小写
                bool matchesKeyword = string.IsNullOrWhiteSpace(SearchKeyword) ||
                                     (employee.EmployeeCode?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 员工编码匹配（null容错）
                                     (employee.EmployeeName?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) || // 员工姓名匹配（null容错）
                                     (employee.Position?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||    // 职位匹配（null容错）
                                     (employee.Phone?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false) ||       // 手机号匹配（null容错）
                                     (employee.Email?.Contains(SearchKeyword, StringComparison.OrdinalIgnoreCase) ?? false);        // 邮箱匹配（null容错）

                // 2. 状态匹配逻辑：0=全部，1=在职，2=离职，3=休假
                bool matchesStatus = SelectedStatus == 0 || employee.Status == SelectedStatus;

                // 3. 部门匹配逻辑：未选择部门则全部匹配，否则匹配指定部门
                bool matchesDepartment = !SelectedDepartmentId.HasValue || employee.DeptId == SelectedDepartmentId;

                // 4. 入职时间匹配逻辑：筛选指定时间段内入职的员工
                bool matchesEntryDate = true;
                if (EntryDateStart.HasValue && employee.EntryDate < EntryDateStart.Value)
                {
                    matchesEntryDate = false; // 入职时间早于开始时间，过滤掉
                }
                if (EntryDateEnd.HasValue && employee.EntryDate > EntryDateEnd.Value.AddDays(1).AddSeconds(-1))
                {
                    // 入职时间晚于结束时间的23:59:59，过滤掉（实现"包含结束日期全天"的筛选）
                    matchesEntryDate = false;
                }

                // 四重条件满足：关键词 AND 状态 AND 部门 AND 入职时间
                return matchesKeyword && matchesStatus && matchesDepartment && matchesEntryDate;
            }

            // 非Employee类型，直接过滤掉
            return false;
        }

        /// <summary>
        /// 加载所有员工数据（初始化/刷新时调用）
        /// 核心逻辑：从服务层获取数据，更新UI集合，处理异常
        /// </summary>
        private async Task LoadEmployeesAsync()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 清空现有数据：避免重复加载导致数据重复
                Employees.Clear();

                // 异步获取所有员工（服务层封装数据库查询逻辑）
                var employees = await _employeeService.GetAllAsync();

                // 遍历添加到ObservableCollection：触发UI自动更新
                foreach (var employee in employees)
                {
                    Employees.Add(employee);
                }

                // 更新总数：用于分页计算
                TotalCount = Employees.Count;

                // 刷新筛选视图：确保新数据应用筛选规则
                EmployeesView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示错误信息（用户友好）
                await _dialogService.ShowErrorAsync("错误", $"加载员工数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都标记加载完成：隐藏加载动画
                IsRefreshing = false;
            }
        }

        /// <summary>
        /// 加载部门数据（供新增/编辑/调动员工时选择部门）
        /// 核心逻辑：从服务层获取部门，按排序号排序后添加到列表
        /// </summary>
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                // 清空现有数据：避免重复加载
                Departments.Clear();

                // 异步获取所有部门（服务层封装数据库查询逻辑）
                var departments = await _departmentService.GetAllAsync();

                // 按排序号排序后添加到列表（保证部门显示顺序一致）
                foreach (var department in departments.OrderBy(d => d.SortOrder))
                {
                    Departments.Add(department);
                }
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示加载失败
                await _dialogService.ShowErrorAsync("错误", $"加载部门数据失败: {ex.Message}");
            }
        }
        #endregion

        #region 命令方法（RelayCommand：绑定到UI按钮的执行逻辑）
        /// <summary>
        /// 刷新员工列表（绑定到刷新按钮）
        /// 核心逻辑：重新调用LoadEmployeesAsync加载最新数据
        /// </summary>
        [RelayCommand] // CommunityToolkit特性：自动生成ICommand属性（RefreshEmployeesCommand）
        private async Task RefreshEmployees()
        {
            await LoadEmployeesAsync();
        }

        /// <summary>
        /// 执行搜索（绑定到搜索按钮）
        /// 核心逻辑：刷新筛选视图 + 重置页码到第一页
        /// </summary>
        [RelayCommand]
        private async Task SearchEmployees()
        {
            try
            {
                // 标记加载中：UI显示加载动画
                IsRefreshing = true;

                // 刷新筛选视图：应用最新的筛选规则（关键词/状态/部门/入职时间）
                EmployeesView?.Refresh();

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
        /// 核心逻辑：清空所有筛选条件 + 重新搜索
        /// </summary>
        [RelayCommand]
        private async Task ResetSearch()
        {
            // 清空所有筛选条件
            SearchKeyword = string.Empty;
            SelectedStatus = 0;
            SelectedDepartmentId = null;
            EntryDateStart = null;
            EntryDateEnd = null;

            // 重新执行搜索：应用重置后的条件
            await SearchEmployees();
        }

        /// <summary>
        /// 批量删除员工（绑定到批量删除按钮，当前仅支持单个删除）
        /// 核心逻辑：校验选中项 + 校验关联用户 + 二次确认 + 执行删除
        /// </summary>
        [RelayCommand]
        private async Task BatchDelete()
        {
            // 获取选中的员工（当前逻辑：仅支持单个选中，可扩展为多选）
            var selectedEmployees = Employees.Where(e => e == SelectedEmployee).ToList();

            // 无选中项提示：用户友好
            if (selectedEmployees.Count == 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请先选择要删除的员工");
                return;
            }

            // 二次确认：防止误操作（删除不可逆）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除选中的 {selectedEmployees.Count} 个员工吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    foreach (var employee in selectedEmployees)
                    {
                        // 业务规则校验：存在关联用户的员工不能删除
                        var user = await _employeeService.GetRelatedUserAsync(employee.Id);
                        if (user != null)
                        {
                            await _dialogService.ShowErrorAsync("错误", $"员工 {employee.EmployeeName} 存在关联用户，无法删除。");
                            continue; // 跳过当前员工，继续处理下一个
                        }

                        // 执行删除：调用服务层删除方法
                        await _employeeService.DeleteAsync(employee);
                        // 从UI列表移除：触发UI更新
                        Employees.Remove(employee);
                    }

                    // 更新总数：同步删除后的数据量
                    TotalCount = Employees.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "员工已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除员工失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 导出员工数据（绑定到导出按钮，预留功能）
        /// </summary>
        [RelayCommand]
        private async Task ExportEmployees()
        {
            // 临时提示：功能未实现
            await _dialogService.ShowInfoAsync("导出", "员工导出功能尚未实现");
        }

        /// <summary>
        /// 分页跳转（绑定到分页控件的页码按钮/输入框）
        /// 核心逻辑：校验页码合法性，更新当前页码（未实现实际分页加载）
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

            // 备注：实际应用中应调用服务层的分页查询方法加载对应页数据
            // 此处仅做前端页码更新，无实际数据加载逻辑
        }

        /// <summary>
        /// 新增员工（绑定到新增按钮）
        /// 核心逻辑：初始化新增对象 + 设置默认值 + 打开弹窗
        /// </summary>
        [RelayCommand]
        private async Task AddEmployee()
        {
            // 标记为新增模式：区分保存逻辑
            IsEditMode = false;

            // 初始化新员工对象（设置默认值）
            EditingEmployee = new Employee
            {
                Status = 1,          // 默认状态：在职（1）
                Gender = 0,          // 默认性别：未知（0）
                CreateTime = DateTime.Now, // 创建时间：当前时间
                EntryDate = DateTime.Now,  // 入职日期：当前时间
                EmployeeCode = string.Empty, // 员工编码：空（需用户输入）
                EmployeeName = string.Empty, // 员工姓名：空（需用户输入）
                Position = string.Empty,     // 职位：空（需用户输入）
                Phone = string.Empty,        // 手机号：空（可选）
                Email = string.Empty         // 邮箱：空（可选）
            };

            // 设置默认部门：如果有部门数据，默认选中第一个部门
            if (Departments.Count > 0)
            {
                EditingEmployee.DeptId = Departments.First().Id;
            }

            // 打开编辑弹窗：UI显示弹窗
            IsEmployeeDialogOpen = true;
        }

        /// <summary>
        /// 编辑员工（绑定到DataGrid的编辑按钮）
        /// 核心逻辑：创建原对象副本 + 打开弹窗
        /// </summary>
        /// <param name="employee">要编辑的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task EditEmployee(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 标记为编辑模式：区分保存逻辑
            IsEditMode = true;

            // 创建员工副本（深拷贝）：避免直接修改原列表数据（MVVM最佳实践）
            EditingEmployee = new Employee
            {
                Id = employee.Id,               // 员工ID（主键，不可改）
                EmployeeCode = employee.EmployeeCode, // 员工编码
                EmployeeName = employee.EmployeeName, // 员工姓名
                Gender = employee.Gender,       // 性别
                BirthDate = employee.BirthDate, // 出生日期
                IdCard = employee.IdCard,       // 身份证号
                Phone = employee.Phone,         // 手机号
                Email = employee.Email,         // 邮箱
                DeptId = employee.DeptId,       // 部门ID
                Position = employee.Position,   // 职位
                EntryDate = employee.EntryDate, // 入职日期
                LeaveDate = employee.LeaveDate, // 离职日期
                Status = employee.Status,       // 状态
                CreateTime = employee.CreateTime, // 创建时间（不可改）
                UpdateTime = employee.UpdateTime, // 更新时间（保存时更新）
                Remark = employee.Remark        // 备注
            };

            // 打开编辑弹窗：UI显示弹窗
            IsEmployeeDialogOpen = true;
        }

        /// <summary>
        /// 单个删除员工（绑定到DataGrid的删除按钮）
        /// 核心逻辑：校验关联用户 + 二次确认 + 执行删除
        /// </summary>
        /// <param name="employee">要删除的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task DeleteEmployee(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 业务规则校验：存在关联用户的员工不能删除
            var user = await _employeeService.GetRelatedUserAsync(employee.Id);
            if (user != null)
            {
                await _dialogService.ShowErrorAsync("错误", $"员工 {employee.EmployeeName} 存在关联用户，无法删除。");
                return; // 终止删除流程
            }

            // 二次确认：防止误操作（删除不可逆）
            var result = await _dialogService.ShowConfirmAsync("确认删除", $"确定要删除员工\"{employee.EmployeeName}\"吗？此操作不可撤销。");

            if (result) // 用户确认删除
            {
                try
                {
                    // 执行删除：调用服务层删除方法
                    await _employeeService.DeleteAsync(employee);
                    // 从UI列表移除：触发UI更新
                    Employees.Remove(employee);
                    // 更新总数：同步删除后的数据量
                    TotalCount = Employees.Count;
                    // 成功提示：用户友好
                    await _dialogService.ShowInfoAsync("成功", "员工已删除");
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除员工失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 取消编辑（绑定到员工编辑弹窗的取消按钮）
        /// 核心逻辑：关闭弹窗（不保存任何修改）
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            // 关闭编辑弹窗：UI隐藏弹窗
            IsEmployeeDialogOpen = false;
        }

        /// <summary>
        /// 取消调动（绑定到员工调动弹窗的取消按钮）
        /// 核心逻辑：关闭弹窗（不保存任何修改）
        /// </summary>
        [RelayCommand]
        private void CancelTransfer()
        {
            // 关闭调动弹窗：UI隐藏弹窗
            IsTransferDialogOpen = false;
        }

        /// <summary>
        /// 取消离职（绑定到员工离职弹窗的取消按钮）
        /// 核心逻辑：关闭弹窗（不保存任何修改）
        /// </summary>
        [RelayCommand]
        private void CancelLeave()
        {
            // 关闭离职弹窗：UI隐藏弹窗
            IsLeaveDialogOpen = false;
        }

        /// <summary>
        /// 保存员工（绑定到员工编辑弹窗的保存按钮，支持新增/编辑）
        /// 核心逻辑：数据校验 + 保存数据 + 刷新UI
        /// </summary>
        [RelayCommand]
        private async Task SaveEmployee()
        {
            // 数据校验：必填项非空校验
            if (string.IsNullOrWhiteSpace(EditingEmployee.EmployeeName))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入员工姓名");
                return; // 终止保存流程
            }

            if (string.IsNullOrWhiteSpace(EditingEmployee.EmployeeCode))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入员工编码");
                return; // 终止保存流程
            }

            try
            {
                if (IsEditMode) // 编辑模式
                {
                    // 更新更新时间：当前操作时间
                    EditingEmployee.UpdateTime = DateTime.Now;
                    // 执行更新：调用服务层更新方法
                    await _employeeService.UpdateAsync(EditingEmployee);

                    // 更新列表中的数据：替换原对象（触发UI更新）
                    var existingEmployee = Employees.FirstOrDefault(e => e.Id == EditingEmployee.Id);
                    if (existingEmployee != null)
                    {
                        int index = Employees.IndexOf(existingEmployee);
                        Employees[index] = EditingEmployee;
                    }

                    // 成功提示：编辑成功
                    await _dialogService.ShowInfoAsync("成功", "员工信息已更新");
                }
                else // 新增模式
                {
                    // 执行新增：调用服务层新增方法，返回新创建的员工（含自增ID）
                    var newEmployee = await _employeeService.AddAsync(EditingEmployee);

                    // 添加到UI列表：触发UI更新
                    Employees.Add(newEmployee);
                    // 更新总数：同步新增后的数据量
                    TotalCount = Employees.Count;

                    // 成功提示：新增成功
                    await _dialogService.ShowInfoAsync("成功", "员工已创建");
                }

                // 关闭弹窗：保存后隐藏弹窗
                IsEmployeeDialogOpen = false;

                // 刷新视图：确保新数据应用筛选规则
                EmployeesView?.Refresh();
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示保存失败
                await _dialogService.ShowErrorAsync("错误", $"保存员工失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开员工调动弹窗（绑定到DataGrid的调动按钮）
        /// 核心逻辑：初始化调动数据 + 打开弹窗
        /// </summary>
        /// <param name="employee">要调动的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task TransferEmployeeMethod(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 设置待调动的员工对象
            TransferEmployee = employee;

            // 初始化调动默认值：原部门和原职位
            NewDepartmentId = employee.DeptId;
            NewPosition = employee.Position;

            // 打开调动弹窗：UI显示弹窗
            IsTransferDialogOpen = true;
        }

        /// <summary>
        /// 保存员工调动（绑定到调动弹窗的保存按钮）
        /// 核心逻辑：数据校验 + 执行调动 + 更新UI
        /// </summary>
        [RelayCommand]
        private async Task SaveTransfer()
        {
            // 空值校验：待调动员工不能为空
            if (SelectedEmployee == null) return;

            // 数据校验：新职位不能为空
            if (string.IsNullOrWhiteSpace(NewPosition))
            {
                await _dialogService.ShowErrorAsync("错误", "请输入新职位");
                return; // 终止调动流程
            }

            try
            {
                // 执行员工调动：调用服务层调动方法
                await _employeeService.TransferAsync(SelectedEmployee.Id, NewDepartmentId, NewPosition);

                // 获取最新的员工数据：确保UI显示最新状态
                var employee = await _employeeService.GetByIdAsync(SelectedEmployee.Id);
                if (employee != null)
                {
                    // 更新列表中的数据：替换原对象（触发UI更新）
                    var existingEmployee = Employees.FirstOrDefault(e => e.Id == employee.Id);
                    if (existingEmployee != null)
                    {
                        int index = Employees.IndexOf(existingEmployee);
                        Employees[index] = employee;
                    }
                }

                // 关闭调动弹窗：保存后隐藏弹窗
                IsTransferDialogOpen = false;

                // 刷新视图：确保调动后的数据应用筛选规则
                EmployeesView?.Refresh();

                // 成功提示：调动成功
                await _dialogService.ShowInfoAsync("成功", "员工调动已完成");
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示调动失败
                await _dialogService.ShowErrorAsync("错误", $"员工调动失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 打开员工离职弹窗（绑定到DataGrid的离职按钮）
        /// 核心逻辑：初始化离职数据 + 打开弹窗
        /// </summary>
        /// <param name="employee">要离职的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task EmployeeLeave(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 设置待离职的员工对象
            LeaveEmployee = employee;

            // 初始化离职日期：默认当前日期
            LeaveDate = DateTime.Now;

            // 打开离职弹窗：UI显示弹窗
            IsLeaveDialogOpen = true;
        }

        /// <summary>
        /// 保存员工离职（绑定到离职弹窗的保存按钮）
        /// 核心逻辑：执行离职处理 + 更新UI
        /// </summary>
        [RelayCommand]
        private async Task SaveLeave()
        {
            // 空值校验：待离职员工不能为空
            if (LeaveEmployee == null) return;

            try
            {
                // 执行员工离职：调用服务层离职方法
                await _employeeService.LeaveAsync(LeaveEmployee.Id, LeaveDate);

                // 获取最新的员工数据：确保UI显示最新状态（离职状态+离职日期）
                var employee = await _employeeService.GetByIdAsync(LeaveEmployee.Id);
                if (employee != null)
                {
                    // 更新列表中的数据：替换原对象（触发UI更新）
                    var existingEmployee = Employees.FirstOrDefault(e => e.Id == employee.Id);
                    if (existingEmployee != null)
                    {
                        int index = Employees.IndexOf(existingEmployee);
                        Employees[index] = employee;
                    }
                }

                // 关闭离职弹窗：保存后隐藏弹窗
                IsLeaveDialogOpen = false;

                // 刷新视图：确保离职后的数据应用筛选规则
                EmployeesView?.Refresh();

                // 成功提示：离职处理成功
                await _dialogService.ShowInfoAsync("成功", "员工已设置为离职");
            }
            catch (Exception ex)
            {
                // 异常处理：弹窗提示离职处理失败
                await _dialogService.ShowErrorAsync("错误", $"设置员工离职失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 设置员工休假状态（绑定到DataGrid的休假按钮）
        /// 核心逻辑：修改状态为休假 + 保存更新
        /// </summary>
        /// <param name="employee">要设置休假的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task SetOnLeave(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("设置休假", $"确定要将员工\"{employee.EmployeeName}\"设置为休假状态吗？");

            if (result) // 用户确认设置
            {
                try
                {
                    employee.Status = 3; // 修改状态为休假（3=休假）
                    await _employeeService.UpdateAsync(employee); // 保存更新
                    EmployeesView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "员工已设置为休假状态"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示设置失败
                    await _dialogService.ShowErrorAsync("错误", $"设置员工状态失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 设置员工在职状态（绑定到DataGrid的在职按钮）
        /// 核心逻辑：修改状态为在职 + 保存更新
        /// </summary>
        /// <param name="employee">要设置在职的员工（由UI传递选中项）</param>
        [RelayCommand]
        private async Task SetActive(Employee? employee)
        {
            // 空值校验：避免传入null导致异常
            if (employee == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("设置在职", $"确定要将员工\"{employee.EmployeeName}\"设置为在职状态吗？");

            if (result) // 用户确认设置
            {
                try
                {
                    employee.Status = 1; // 修改状态为在职（1=在职）
                    await _employeeService.UpdateAsync(employee); // 保存更新
                    EmployeesView?.Refresh(); // 刷新筛选视图（状态变化后重新筛选）
                    await _dialogService.ShowInfoAsync("成功", "员工已设置为在职状态"); // 成功提示
                }
                catch (Exception ex)
                {
                    // 异常处理：弹窗提示设置失败
                    await _dialogService.ShowErrorAsync("错误", $"设置员工状态失败: {ex.Message}");
                }
            }
        }
        #endregion
    }
}