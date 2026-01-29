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
using MES_WPF.Core.Services.SystemManagement;
using MES_WPF.Core.Models;

namespace MES_WPF.ViewModels.BasicInformation
{
    /// <summary>
    /// 资源管理视图模型
    /// 职责：封装资源（含设备）管理页面的所有业务逻辑（数据加载、增删改查、筛选、分页、关联设备/部门操作）
    /// 基于MVVM架构，使用CommunityToolkit.Mvvm实现属性通知和命令绑定
    /// </summary>
    public partial class ResourceViewModel : ObservableObject
    {
        #region 依赖注入服务
        // 资源业务服务：封装资源基础CRUD及专属业务逻辑
        private readonly IResourceService _resourceService;
        // 设备业务服务：封装设备（资源子类）的专属操作
        private readonly IEquipmentService _equipmentService;
        // 部门业务服务：用于加载部门列表，实现资源-部门关联
        private readonly IDepartmentService _departmentService;
        // 弹窗服务：统一管理系统弹窗（错误、确认、信息提示），解耦UI与业务逻辑
        private readonly IDialogService _dialogService;
        #endregion

        #region 可观察属性（绑定到View）
        /// <summary>
        /// 资源列表（核心数据源，绑定到DataGrid/列表控件）
        /// ObservableCollection自动触发UI更新（属性变更通知）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Resource> _resources = new();

        /// <summary>
        /// 资源列表视图（对外暴露的只读视图，避免直接修改原集合）
        /// </summary>
        public ObservableCollection<Resource> ResourcesView => _resources;

        /// <summary>
        /// 部门列表（绑定到部门选择下拉框，实现资源归属部门选择）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Department> _departments = new();

        /// <summary>
        /// 当前选中的资源（绑定到DataGrid的SelectedItem）
        /// </summary>
        [ObservableProperty]
        private Resource _selectedResource;

        /// <summary>
        /// 正在编辑的资源（弹窗编辑时的临时对象，避免直接修改原数据）
        /// </summary>
        [ObservableProperty]
        private Resource _editingResource;

        /// <summary>
        /// 正在编辑的设备信息（资源类型为设备时的附属数据）
        /// </summary>
        [ObservableProperty]
        private Equipment _editingEquipment;

        /// <summary>
        /// 搜索关键词（绑定到搜索输入框，支持资源编码/名称模糊查询）
        /// </summary>
        [ObservableProperty]
        private string _searchKeyword;

        /// <summary>
        /// 选中的资源类型筛选条件（0=全部，1=设备，2=人员，3=物料等）
        /// 绑定到类型筛选下拉框，转换为byte类型匹配实体的ResourceType
        /// </summary>
        [ObservableProperty]
        private int _selectedResourceType = 0;

        /// <summary>
        /// 选中的状态筛选条件（0=全部，1=可用，2=禁用，3=维护中）
        /// 绑定到状态筛选下拉框，转换为byte类型匹配实体的Status
        /// </summary>
        [ObservableProperty]
        private int _selectedStatus = 0;

        /// <summary>
        /// 数据加载中标记（绑定到加载动画的显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isRefreshing;

        /// <summary>
        /// 资源编辑弹窗是否打开（绑定到弹窗的IsOpen属性）
        /// </summary>
        [ObservableProperty]
        private bool _isResourceDialogOpen;

        /// <summary>
        /// 是否为编辑模式（区分新增/编辑逻辑）
        /// true=编辑已有资源，false=新增资源
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode;

        /// <summary>
        /// 是否为设备类型资源（控制弹窗中设备信息区域的显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isEquipmentResource;

        /// <summary>
        /// 资源总数（用于分页计算）
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
        /// <param name="resourceService">资源业务服务</param>
        /// <param name="equipmentService">设备业务服务</param>
        /// <param name="departmentService">部门业务服务</param>
        /// <param name="dialogService">弹窗服务</param>
        public ResourceViewModel(
            IResourceService resourceService,
            IEquipmentService equipmentService,
            IDepartmentService departmentService,
            IDialogService dialogService)
        {
            _resourceService = resourceService;
            _equipmentService = equipmentService;
            _departmentService = departmentService;
            _dialogService = dialogService;

            // 异步初始化数据（先加载部门，再加载资源，避免UI线程阻塞）
            Task.Run(async () =>
            {
                await LoadDepartmentsAsync();
                await LoadResourcesAsync();
            });
        }
        #endregion

        #region 属性变更回调
        /// <summary>
        /// EditingResource属性变更时的回调方法（CommunityToolkit.Mvvm特性）
        /// 核心逻辑：根据资源类型自动标记是否为设备资源，控制UI显示
        /// </summary>
        /// <param name="value">新的编辑资源对象</param>
        partial void OnEditingResourceChanged(Resource value)
        {
            if (value != null)
            {
                // 资源类型1为设备（需与业务约定的枚举值保持一致）
                IsEquipmentResource = value.ResourceType == 1;
            }
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

        #region 数据加载方法
        /// <summary>
        /// 加载部门数据（供资源归属部门选择）
        /// 私有方法：仅初始化时调用，不绑定到UI命令
        /// </summary>
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                // 调用部门服务获取全量部门数据
                var departments = await _departmentService.GetAllAsync();

                // WPF中UI元素必须在主线程更新，使用Dispatcher.Invoke切换线程
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Departments.Clear();
                    foreach (var department in departments)
                    {
                        Departments.Add(department);
                    }
                });
            }
            catch (Exception ex)
            {
                // 异常处理：统一弹窗提示错误信息
                await _dialogService.ShowErrorAsync("错误", $"加载部门数据失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 加载所有资源数据（初始化/刷新时调用）
        /// RelayCommand标记为命令，可直接绑定到UI的刷新按钮
        /// </summary>
        [RelayCommand]
        private async Task LoadResourcesAsync()
        {
            try
            {
                // 标记加载中，UI显示加载动画
                IsRefreshing = true;

                // 调用资源服务获取全量资源数据（异步操作，不阻塞UI）
                var resources = await _resourceService.GetAllAsync();
                // 更新资源总数（用于分页计算）
                TotalCount = resources.Count();

                // 主线程更新UI列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Resources.Clear();
                    foreach (var resource in resources)
                    {
                        Resources.Add(resource);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载资源数据失败: {ex.Message}");
            }
            finally
            {
                // 无论是否异常，都标记加载完成
                IsRefreshing = false;
            }
        }
        #endregion

        #region 筛选/搜索方法
        /// <summary>
        /// 搜索资源（按关键词/类型/状态筛选）
        /// RelayCommand标记为命令，绑定到搜索按钮
        /// </summary>
        [RelayCommand]
        private async Task SearchResourcesAsync()
        {
            try
            {
                IsRefreshing = true;

                // 先获取全量资源，再进行内存筛选（优化建议：改为服务层带条件查询，减少数据传输）
                var resources = await _resourceService.GetAllAsync();

                // 1. 按关键词筛选（资源编码/名称包含关键词，模糊匹配）
                if (!string.IsNullOrWhiteSpace(SearchKeyword))
                {
                    resources = resources.Where(r =>
                        r.ResourceCode.Contains(SearchKeyword) ||
                        r.ResourceName.Contains(SearchKeyword));
                }

                // 2. 按资源类型筛选（0=全部，>0时转换为byte匹配实体）
                if (SelectedResourceType > 0)
                {
                    byte resourceType = (byte)SelectedResourceType;
                    resources = resources.Where(r => r.ResourceType == resourceType);
                }

                // 3. 按状态筛选（0=全部，>0时转换为byte匹配实体）
                if (SelectedStatus > 0)
                {
                    byte status = (byte)SelectedStatus;
                    resources = resources.Where(r => r.Status == status);
                }

                // 更新筛选后的总数
                TotalCount = resources.Count();

                // 主线程更新UI列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Resources.Clear();
                    foreach (var resource in resources)
                    {
                        Resources.Add(resource);
                    }
                });

                // 筛选后重置为第一页
                CurrentPage = 1;
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索资源失败: {ex.Message}");
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
            SelectedResourceType = 0;
            SelectedStatus = 0;

            // 重新加载全量资源数据
            Task.Run(async () => await LoadResourcesAsync());
        }
        #endregion

        #region 资源增删改方法
        /// <summary>
        /// 添加资源（打开新增弹窗）
        /// RelayCommand标记为命令，绑定到添加按钮
        /// </summary>
        [RelayCommand]
        private void AddResource()
        {
            // 初始化新的资源对象（设置默认值）
            EditingResource = new Resource
            {
                ResourceType = 1, // 默认类型为设备
                Status = 1,       // 默认状态为可用
                CreateTime = DateTime.Now // 默认创建时间为当前时间
            };

            // 初始化设备信息（新增设备资源时使用）
            EditingEquipment = new Equipment();

            // 标记为新增模式
            IsEditMode = false;
            // 默认标记为设备资源（控制UI显示）
            IsEquipmentResource = true;
            // 打开编辑弹窗
            IsResourceDialogOpen = true;
        }

        /// <summary>
        /// 编辑资源（打开编辑弹窗，加载选中资源及关联设备数据）
        /// RelayCommand标记为命令，绑定到DataGrid的编辑按钮
        /// </summary>
        /// <param name="resource">选中的待编辑资源</param>
        [RelayCommand]
        private async Task EditResourceAsync(Resource resource)
        {
            // 空值校验：避免选中空数据时出错
            if (resource == null) return;

            // 创建资源副本（深拷贝），避免直接修改原集合中的对象（MVVM最佳实践）
            EditingResource = new Resource
            {
                Id = resource.Id,
                ResourceCode = resource.ResourceCode,
                ResourceName = resource.ResourceName,
                ResourceType = resource.ResourceType,
                DepartmentId = resource.DepartmentId,
                Status = resource.Status,
                Description = resource.Description,
                CreateTime = resource.CreateTime, // 保留原创建时间
                UpdateTime = DateTime.Now         // 更新时间为当前操作时间
            };

            // 如果是设备类型资源，加载关联的设备信息
            if (resource.ResourceType == 1) // 1:设备（业务约定）
            {
                try
                {
                    // 获取资源关联的设备数据
                    var equipment = await _equipmentService.GetByResourceIdAsync(resource.Id);
                    if (equipment != null)
                    {
                        // 拷贝设备数据到编辑对象
                        EditingEquipment = new Equipment
                        {
                            ResourceId = equipment.ResourceId,
                            EquipmentModel = equipment.EquipmentModel,
                            Manufacturer = equipment.Manufacturer,
                            SerialNumber = equipment.SerialNumber,
                            PurchaseDate = equipment.PurchaseDate,
                            WarrantyPeriod = equipment.WarrantyPeriod,
                            MaintenanceCycle = equipment.MaintenanceCycle,
                            LastMaintenanceDate = equipment.LastMaintenanceDate,
                            NextMaintenanceDate = equipment.NextMaintenanceDate,
                            IpAddress = equipment.IpAddress,
                            OpcUaEndpoint = equipment.OpcUaEndpoint
                        };
                    }
                    else
                    {
                        // 无设备数据时初始化空对象（关联当前资源ID）
                        EditingEquipment = new Equipment { ResourceId = resource.Id };
                    }
                }
                catch (Exception ex)
                {
                    // 设备信息加载失败不阻塞编辑，仅提示
                    await _dialogService.ShowErrorAsync("错误", $"加载设备信息失败: {ex.Message}");
                    EditingEquipment = new Equipment { ResourceId = resource.Id };
                }
            }
            else
            {
                // 非设备类型资源，清空设备编辑对象
                EditingEquipment = new Equipment();
            }

            // 标记为编辑模式
            IsEditMode = true;
            // 打开编辑弹窗
            IsResourceDialogOpen = true;
        }

        /// <summary>
        /// 删除资源（单条删除，级联删除关联设备）
        /// RelayCommand标记为命令，绑定到DataGrid的删除按钮
        /// </summary>
        /// <param name="resource">选中的待删除资源</param>
        [RelayCommand]
        private async Task DeleteResourceAsync(Resource resource)
        {
            if (resource == null) return;

            // 二次确认：防止误操作
            var result = await _dialogService.ShowConfirmAsync("确认", $"确定要删除资源 {resource.ResourceName} 吗？");
            if (result)
            {
                try
                {
                    // 如果是设备类型资源，先删除关联的设备信息（级联删除）
                    if (resource.ResourceType == 1)
                    {
                        var equipment = await _equipmentService.GetByResourceIdAsync(resource.Id);
                        if (equipment != null)
                        {
                            await _equipmentService.DeleteByIdAsync(equipment.Id);
                        }
                    }

                    // 删除资源主数据
                    await _resourceService.DeleteByIdAsync(resource.Id);
                    // 从UI列表中移除
                    Resources.Remove(resource);
                    // 更新总数
                    TotalCount--;
                    // 提示删除成功
                    await _dialogService.ShowInfoAsync("成功", "资源删除成功");
                }
                catch (Exception ex)
                {
                    // 异常处理：如外键关联、资源被使用等导致删除失败
                    await _dialogService.ShowErrorAsync("错误", $"删除资源失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 批量删除资源（预留功能，暂未实现）
        /// 注：需要结合ResourceWithSelection的IsSelected属性使用
        /// </summary>
        [RelayCommand]
        private async Task BatchDeleteAsync()
        {
            await _dialogService.ShowInfoAsync("提示", "批量删除功能需要模型支持，暂不可用");
        }

        /// <summary>
        /// 导出资源数据（预留功能，暂未实现）
        /// </summary>
        [RelayCommand]
        private void ExportResources()
        {
            _dialogService.ShowInfoAsync("提示", "导出功能待实现");
        }

        /// <summary>
        /// 保存资源（新增/编辑统一处理，级联保存设备信息）
        /// RelayCommand标记为命令，绑定到弹窗的保存按钮
        /// </summary>
        [RelayCommand]
        private async Task SaveResourceAsync()
        {
            #region 表单验证（必填项校验）
            if (string.IsNullOrWhiteSpace(EditingResource.ResourceCode))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入资源编码");
                return;
            }

            if (string.IsNullOrWhiteSpace(EditingResource.ResourceName))
            {
                await _dialogService.ShowInfoAsync("提示", "请输入资源名称");
                return;
            }

            if (EditingResource.ResourceType <= 0)
            {
                await _dialogService.ShowInfoAsync("提示", "请选择资源类型");
                return;
            }
            #endregion

            try
            {
                // 校验资源编码唯一性（仅新增时校验）
                if (!IsEditMode)
                {
                    bool exists = await _resourceService.IsResourceCodeExistsAsync(EditingResource.ResourceCode);
                    if (exists)
                    {
                        await _dialogService.ShowInfoAsync("提示", $"资源编码 {EditingResource.ResourceCode} 已存在");
                        return;
                    }
                }

                Resource savedResource;
                #region 资源主数据保存
                if (IsEditMode)
                {
                    // 编辑模式：更新资源
                    EditingResource.UpdateTime = DateTime.Now;
                    savedResource = await _resourceService.UpdateAsync(EditingResource);

                    // 更新UI列表中的对应资源（替换原对象）
                    var existingResource = Resources.FirstOrDefault(r => r.Id == EditingResource.Id);
                    if (existingResource != null)
                    {
                        int index = Resources.IndexOf(existingResource);
                        Resources[index] = savedResource;
                    }
                }
                else
                {
                    // 新增模式：添加资源
                    EditingResource.CreateTime = DateTime.Now;
                    savedResource = await _resourceService.AddAsync(EditingResource);
                    // 添加到UI列表
                    Resources.Add(savedResource);
                    // 更新总数
                    TotalCount++;
                }
                #endregion

                #region 设备信息级联保存（仅设备类型资源）
                if (EditingResource.ResourceType == 1 && EditingEquipment != null)
                {
                    // 关联资源ID（新增资源时需绑定新生成的ID）
                    EditingEquipment.ResourceId = savedResource.Id;

                    // 判断设备信息是新增还是更新
                    var existingEquipment = await _equipmentService.GetByResourceIdAsync(savedResource.Id);
                    if (existingEquipment != null)
                    {
                        // 更新现有设备信息
                        EditingEquipment.Id = existingEquipment.Id;
                        await _equipmentService.UpdateAsync(EditingEquipment);
                    }
                    else
                    {
                        // 新增设备信息
                        await _equipmentService.AddAsync(EditingEquipment);
                    }
                }
                #endregion

                // 提示保存成功
                await _dialogService.ShowInfoAsync("成功", IsEditMode ? "资源更新成功" : "资源添加成功");
                // 关闭编辑弹窗
                IsResourceDialogOpen = false;
            }
            catch (Exception ex)
            {
                // 异常处理：如数据库约束、网络问题等
                await _dialogService.ShowErrorAsync("错误", $"保存资源失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 取消编辑（关闭弹窗，不保存修改）
        /// RelayCommand标记为命令，绑定到弹窗的取消按钮
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            IsResourceDialogOpen = false;
        }
        #endregion

        #region 设备详情查看
        /// <summary>
        /// 查看设备详情（仅设备类型资源）
        /// RelayCommand标记为命令，绑定到DataGrid的查看详情按钮
        /// </summary>
        /// <param name="resource">选中的资源（需为设备类型）</param>
        [RelayCommand]
        private async Task ViewEquipmentAsync(Resource resource)
        {
            // 校验：非空 + 设备类型
            if (resource == null || resource.ResourceType != 1) return;

            try
            {
                // 获取资源关联的设备数据
                var equipment = await _equipmentService.GetByResourceIdAsync(resource.Id);
                if (equipment != null)
                {
                    // 拼接设备详情信息（临时方案，建议后续封装为设备详情ViewModel）
                    string info = $"设备详情:\n" +
                        $"型号: {equipment.EquipmentModel}\n" +
                        $"制造商: {equipment.Manufacturer}\n" +
                        $"序列号: {equipment.SerialNumber}\n" +
                        $"购买日期: {equipment.PurchaseDate?.ToString("yyyy-MM-dd")}\n" +
                        $"保修期: {equipment.WarrantyPeriod} 个月\n" +
                        $"保养周期: {equipment.MaintenanceCycle} 天\n" +
                        $"上次保养: {equipment.LastMaintenanceDate?.ToString("yyyy-MM-dd")}\n" +
                        $"下次保养: {equipment.NextMaintenanceDate?.ToString("yyyy-MM-dd")}\n" +
                        $"IP地址: {equipment.IpAddress}";

                    // 弹窗显示设备详情
                    await _dialogService.ShowInfoAsync("设备详情", info);
                }
                else
                {
                    await _dialogService.ShowInfoAsync("提示", "该资源没有关联的设备信息");
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"加载设备详情失败: {ex.Message}");
            }
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
                // await LoadResourcesByPageAsync(CurrentPage);
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
                // await LoadResourcesByPageAsync(CurrentPage);
            }
        }

        /// <summary>
        /// 跳转到指定页（支持int/string类型参数）
        /// </summary>
        /// <param name="pageObj">页码（int或string类型）</param>
        [RelayCommand]
        private void GoToPage(object pageObj)
        {
            // 处理int类型参数（如分页控件的数字按钮）
            if (pageObj is int pageInt)
            {
                if (pageInt >= 1 && pageInt <= TotalPages)
                {
                    CurrentPage = pageInt;
                    // 注：此处需补充分页加载逻辑
                    // await LoadResourcesByPageAsync(CurrentPage);
                }
            }
            // 处理string类型参数（如输入框的文本）
            else if (pageObj is string pageStr)
            {
                if (int.TryParse(pageStr, out int pageVal) && pageVal >= 1 && pageVal <= TotalPages)
                {
                    CurrentPage = pageVal;
                    // 注：此处需补充分页加载逻辑
                    // await LoadResourcesByPageAsync(CurrentPage);
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

    #region 扩展类（批量操作预留）
    /// <summary>
    /// 扩展Resource实体类，添加IsSelected属性用于批量操作（如批量删除）
    /// 注：当前主逻辑未使用该类，仅预留扩展能力
    /// </summary>
    public partial class ResourceWithSelection : Resource
    {
        /// <summary>
        /// 标记是否选中（用于批量操作）
        /// </summary>
        public bool IsSelected { get; set; }
    }
    #endregion
}