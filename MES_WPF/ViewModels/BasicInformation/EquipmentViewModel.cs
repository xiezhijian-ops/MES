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
    /// 设备管理视图模型
    /// 负责设备信息的CRUD操作、数据加载、筛选搜索、维护记录管理等业务逻辑
    /// 采用MVVM模式，通过CommunityToolkit.Mvvm实现属性通知和命令绑定
    /// </summary>
    public partial class EquipmentViewModel : ObservableObject
    {
        #region 依赖服务注入
        // 设备服务：负责设备相关数据访问操作（数据库交互）
        private readonly IEquipmentService _equipmentService;
        // 资源服务：负责资源相关数据访问（用于设备关联的资源下拉选择）
        private readonly IResourceService _resourceService;
        // 对话框服务：统一管理弹窗交互（错误提示、确认框、输入框等）
        private readonly IDialogService _dialogService;
        #endregion

        #region 绑定属性（UI交互数据）
        /// <summary>
        /// 设备列表（绑定到UI数据网格）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Equipment> _equipments = new();

        /// <summary>
        /// 资源列表（绑定到UI下拉选择框，用于设备关联资源选择）
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<Resource> _resources = new();

        /// <summary>
        /// 选中的设备（绑定到UI数据网格的选中项）
        /// </summary>
        [ObservableProperty]
        private Equipment _selectedEquipment;

        /// <summary>
        /// 新建/编辑的设备实体（绑定到编辑表单的输入控件）
        /// </summary>
        [ObservableProperty]
        private Equipment _editingEquipment;

        /// <summary>
        /// 搜索文本（绑定到UI搜索输入框）
        /// </summary>
        [ObservableProperty]
        private string _searchText;

        /// <summary>
        /// 是否只显示需要维护的设备（绑定到UI复选框）
        /// </summary>
        [ObservableProperty]
        private bool _showMaintenanceRequired;

        /// <summary>
        /// 是否处于编辑模式（控制编辑表单的显示/隐藏）
        /// </summary>
        [ObservableProperty]
        private bool _isEditing;

        /// <summary>
        /// 是否正在处理异步操作（控制加载状态显示）
        /// </summary>
        [ObservableProperty]
        private bool _isBusy;
        #endregion

        #region 属性变更回调
        /// <summary>
        /// 当"是否显示需要维护的设备"属性变更时触发
        /// 自动重新加载设备列表，实现实时筛选
        /// </summary>
        /// <param name="value">新的属性值</param>
        partial void OnShowMaintenanceRequiredChanged(bool value)
        {
            // 异步加载设备列表（避免阻塞UI）
            Task.Run(() => LoadEquipmentsAsync());
        }
        #endregion

        #region 构造函数（依赖注入+初始化）
        /// <summary>
        /// 构造函数：通过依赖注入获取所需服务
        /// 初始化时加载资源列表和设备列表
        /// </summary>
        /// <param name="equipmentService">设备服务</param>
        /// <param name="resourceService">资源服务</param>
        /// <param name="dialogService">对话框服务</param>
        public EquipmentViewModel(
            IEquipmentService equipmentService,
            IResourceService resourceService,
            IDialogService dialogService)
        {
            _equipmentService = equipmentService;
            _resourceService = resourceService;
            _dialogService = dialogService;

            // 异步初始化数据（资源列表优先加载，因为设备编辑需要资源选择）
            Task.Run(async () =>
            {
                await LoadResourcesAsync();
                await LoadEquipmentsAsync();
            });
        }
        #endregion

        #region 数据加载方法
        /// <summary>
        /// 加载设备列表（支持按"是否需要维护"筛选）
        /// 绑定到UI的"刷新"按钮命令
        /// </summary>
        [RelayCommand]
        private async Task LoadEquipmentsAsync()
        {
            try
            {
                // 设置忙碌状态，显示加载指示器
                IsBusy = true;

                // 根据筛选条件获取设备数据
                var equipments = ShowMaintenanceRequired
                    ? await _equipmentService.GetMaintenanceRequiredEquipmentsAsync() // 获取需要维护的设备
                    : await _equipmentService.GetAllAsync(); // 获取所有设备

                // 由于WPF UI控件只能在UI线程更新，使用Dispatcher调度
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Equipments.Clear();
                    foreach (var equipment in equipments)
                    {
                        Equipments.Add(equipment);
                    }
                });
            }
            catch (Exception ex)
            {
                // 异常处理：显示错误信息
                await _dialogService.ShowErrorAsync("错误", $"加载设备信息失败：{ex.Message}");
            }
            finally
            {
                // 无论成功失败，都取消忙碌状态
                IsBusy = false;
            }
        }

        /// <summary>
        /// 加载资源列表（用于设备关联的下拉选择）
        /// 初始化时自动调用，无需绑定UI命令
        /// </summary>
        private async Task LoadResourcesAsync()
        {
            try
            {
                // 从服务获取所有资源数据
                var resources = await _resourceService.GetAllAsync();

                // 调度到UI线程更新绑定集合
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
                await _dialogService.ShowErrorAsync("错误", $"加载资源信息失败：{ex.Message}");
            }
        }
        #endregion

        #region 搜索筛选方法
        /// <summary>
        /// 设备搜索功能（支持多条件组合筛选）
        /// 绑定到UI的"搜索"按钮命令
        /// 筛选条件：搜索文本（序列号、型号、厂商、资源名）+ 是否需要维护
        /// </summary>
        [RelayCommand]
        private async Task SearchEquipmentsAsync()
        {
            try
            {
                IsBusy = true;
                // 先获取所有设备数据
                var equipments = await _equipmentService.GetAllAsync();

                // 1. 按搜索文本筛选（忽略大小写）
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    equipments = equipments.Where(e =>
                        e.SerialNumber?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true || // 序列号包含搜索文本
                        e.EquipmentModel?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true || // 设备型号包含
                        e.Manufacturer?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true || // 厂商包含
                        e.Resource?.ResourceName?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) == true // 关联资源名包含
                    );
                }

                // 2. 按"是否需要维护"筛选（下次维护日期≤今天）
                if (ShowMaintenanceRequired)
                {
                    var today = DateTime.Today;
                    equipments = equipments.Where(e => e.NextMaintenanceDate.HasValue && e.NextMaintenanceDate.Value <= today);
                }

                // 更新UI绑定的设备列表
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Equipments.Clear();
                    foreach (var equipment in equipments)
                    {
                        Equipments.Add(equipment);
                    }
                });
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"搜索设备失败：{ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }
        #endregion

        #region 设备编辑操作（添加/编辑/保存/取消）
        /// <summary>
        /// 新增设备（初始化编辑实体，进入编辑模式）
        /// 绑定到UI的"添加"按钮命令
        /// </summary>
        [RelayCommand]
        private void AddEquipment()
        {
            // 初始化新设备实体，设置默认值（提升用户体验）
            EditingEquipment = new Equipment
            {
                PurchaseDate = DateTime.Today, // 采购日期默认今天
                LastMaintenanceDate = DateTime.Today, // 上次维护默认今天
                NextMaintenanceDate = DateTime.Today.AddDays(30), // 下次维护默认30天后
                MaintenanceCycle = 30 // 维护周期默认30天
            };
            // 进入编辑模式（显示编辑表单）
            IsEditing = true;
        }

        /// <summary>
        /// 编辑设备（复制选中设备数据，进入编辑模式）
        /// 绑定到UI的"编辑"按钮命令
        /// </summary>
        [RelayCommand]
        private void EditEquipment()
        {
            // 校验是否选中设备（未选中则不执行）
            if (SelectedEquipment == null) return;

            // 深拷贝选中设备数据（避免直接修改原实体，支持取消编辑）
            EditingEquipment = new Equipment
            {
                Id = SelectedEquipment.Id,
                ResourceId = SelectedEquipment.ResourceId,
                EquipmentModel = SelectedEquipment.EquipmentModel,
                Manufacturer = SelectedEquipment.Manufacturer,
                SerialNumber = SelectedEquipment.SerialNumber,
                PurchaseDate = SelectedEquipment.PurchaseDate,
                WarrantyPeriod = SelectedEquipment.WarrantyPeriod,
                MaintenanceCycle = SelectedEquipment.MaintenanceCycle,
                LastMaintenanceDate = SelectedEquipment.LastMaintenanceDate,
                NextMaintenanceDate = SelectedEquipment.NextMaintenanceDate,
                IpAddress = SelectedEquipment.IpAddress,
                OpcUaEndpoint = SelectedEquipment.OpcUaEndpoint
            };
            // 进入编辑模式
            IsEditing = true;
        }

        /// <summary>
        /// 保存设备（新增/更新统一处理）
        /// 绑定到UI的"保存"按钮命令
        /// </summary>
        [RelayCommand]
        private async Task SaveEquipmentAsync()
        {
            // 校验编辑实体是否存在
            if (EditingEquipment == null) return;

            try
            {
                // 1. 表单验证（必填字段校验）
                if (EditingEquipment.ResourceId <= 0)
                {
                    await _dialogService.ShowErrorAsync("验证错误", "请选择关联资源");
                    return;
                }

                if (string.IsNullOrWhiteSpace(EditingEquipment.SerialNumber))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "序列号不能为空");
                    return;
                }

                // 2. 唯一性校验（新增时检查序列号是否已存在）
                if (EditingEquipment.Id == 0 &&
                    await _equipmentService.IsSerialNumberExistsAsync(EditingEquipment.SerialNumber))
                {
                    await _dialogService.ShowErrorAsync("验证错误", "该序列号已存在");
                    return;
                }

                // 3. 保存数据（根据Id判断是新增还是更新）
                Equipment savedEquipment;
                if (EditingEquipment.Id == 0)
                {
                    // Id=0表示新增
                    savedEquipment = await _equipmentService.AddAsync(EditingEquipment);
                    await _dialogService.ShowInfoAsync("成功", "设备添加成功");
                }
                else
                {
                    // Id≠0表示更新现有设备
                    savedEquipment = await _equipmentService.UpdateAsync(EditingEquipment);
                    await _dialogService.ShowInfoAsync("成功", "设备更新成功");
                }

                // 4. 保存后处理
                await LoadEquipmentsAsync(); // 刷新设备列表
                SelectedEquipment = Equipments.FirstOrDefault(e => e.Id == savedEquipment.Id); // 选中新增/更新的设备
                IsEditing = false; // 退出编辑模式
                EditingEquipment = null; // 清空编辑实体
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"保存设备失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消编辑（清空编辑实体，退出编辑模式）
        /// 绑定到UI的"取消"按钮命令
        /// </summary>
        [RelayCommand]
        private void CancelEdit()
        {
            EditingEquipment = null;
            IsEditing = false;
        }
        #endregion

        #region 设备删除操作
        /// <summary>
        /// 删除设备（带确认弹窗，防止误操作）
        /// 绑定到UI的"删除"按钮命令
        /// </summary>
        [RelayCommand]
        private async Task DeleteEquipmentAsync()
        {
            // 校验是否选中设备
            if (SelectedEquipment == null) return;

            // 显示确认弹窗，用户确认后才执行删除
            var result = await _dialogService.ShowConfirmAsync("确认", "确定要删除此设备吗？");
            if (!result) return;

            try
            {
                // 调用服务删除设备
                await _equipmentService.DeleteByIdAsync(SelectedEquipment.Id);
                await _dialogService.ShowInfoAsync("成功", "设备删除成功");
                await LoadEquipmentsAsync(); // 刷新列表
                SelectedEquipment = null; // 清空选中状态
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"删除设备失败：{ex.Message}");
            }
        }
        #endregion

        #region 设备维护相关操作
        /// <summary>
        /// 记录设备维护（更新维护日期）
        /// 绑定到UI的"记录维护"按钮命令
        /// 操作逻辑：更新上次维护日期为今天，下次维护日期=今天+维护周期
        /// </summary>
        [RelayCommand]
        private async Task RecordMaintenanceAsync()
        {
            if (SelectedEquipment == null) return;

            // 确认弹窗：说明操作影响
            var result = await _dialogService.ShowConfirmAsync("确认", "确定要记录设备维护吗？此操作将更新上次维护日期和下次维护日期。");
            if (!result) return;

            try
            {
                // 调用服务记录维护（服务内部处理日期计算）
                var updatedEquipment = await _equipmentService.RecordMaintenanceAsync(SelectedEquipment.Id);
                await _dialogService.ShowInfoAsync("成功", "设备维护记录更新成功");
                await LoadEquipmentsAsync(); // 刷新列表
                SelectedEquipment = Equipments.FirstOrDefault(e => e.Id == updatedEquipment.Id); // 保持选中状态
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"记录设备维护失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 更新设备保养周期（支持自定义天数输入）
        /// 绑定到UI的"更新保养周期"按钮命令
        /// </summary>
        [RelayCommand]
        private async Task UpdateMaintenanceCycleAsync()
        {
            if (SelectedEquipment == null) return;

            // 显示输入框：默认值为当前保养周期
            var cycleString = await _dialogService.ShowInputAsync(
                "更新保养周期",
                "请输入新的保养周期（天数）：",
                SelectedEquipment.MaintenanceCycle?.ToString() ?? "30");

            // 用户取消输入则返回
            if (string.IsNullOrEmpty(cycleString)) return;

            // 校验输入是否为有效正整数
            if (!int.TryParse(cycleString, out int cycle) || cycle <= 0)
            {
                await _dialogService.ShowErrorAsync("错误", "请输入有效的天数");
                return;
            }

            try
            {
                // 调用服务更新保养周期
                var updatedEquipment = await _equipmentService.UpdateMaintenanceCycleAsync(SelectedEquipment.Id, cycle);
                await _dialogService.ShowInfoAsync("成功", "设备保养周期更新成功");
                await LoadEquipmentsAsync(); // 刷新列表
                SelectedEquipment = Equipments.FirstOrDefault(e => e.Id == updatedEquipment.Id); // 保持选中状态
            }
            catch (Exception ex)
            {
                await _dialogService.ShowErrorAsync("错误", $"更新设备保养周期失败：{ex.Message}");
            }
        }
        #endregion
    }
}