using MES_WPF.Core.Models;
using MES_WPF.Data.Repositories.SystemManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Core.Services.SystemManagement
{
    /// <summary>
    /// 部门服务实现类
    /// 职责：封装部门相关的业务逻辑（增删改查、树结构构建、路径维护、关联员工查询等）
    /// 继承自通用Service，复用基础CRUD能力，扩展部门专属业务逻辑
    /// </summary>
    public class DepartmentService : Service<Department>, IDepartmentService
    {
        #region 依赖仓储
        // 部门仓储：封装部门数据访问逻辑（数据库操作）
        private readonly IDepartmentRepository _departmentRepository;
        // 员工仓储：用于关联查询部门下的员工数据
        private readonly IEmployeeRepository _employeeRepository;
        #endregion

        #region 构造函数
        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// </summary>
        /// <param name="departmentRepository">部门仓储（必传，空值抛异常）</param>
        /// <param name="employeeRepository">员工仓储（必传，空值抛异常）</param>
        /// <exception cref="ArgumentNullException">仓储实例为空时抛出</exception>
        public DepartmentService(
            IDepartmentRepository departmentRepository,
            IEmployeeRepository employeeRepository)
            : base(departmentRepository) // 父类初始化：通用Service接收基础仓储
        {
            // 空值校验：防止空仓储导致后续操作NPE
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
        }
        #endregion

        #region 基础查询（扩展）
        /// <summary>
        /// 根据部门编码查询单个部门（编码唯一）
        /// </summary>
        /// <param name="deptCode">部门编码（如"DEPT001"）</param>
        /// <returns>匹配的部门实体，无匹配则返回null</returns>
        public async Task<Department> GetByDeptCodeAsync(string deptCode)
        {
            // 委托仓储层执行编码查询（仓储层已处理数据库查询逻辑）
            return await _departmentRepository.GetByCodeAsync(deptCode);
        }
        #endregion

        #region 树结构相关
        /// <summary>
        /// 获取部门树形结构（递归构建，根节点ParentId=null）
        /// 适用于前端树形控件展示（如TreeView、ElTree等）
        /// </summary>
        /// <returns>根部门列表（包含递归子部门）</returns>
        public async Task<IEnumerable<Department>> GetDepartmentTreeAsync()
        {
            // 1. 获取全量部门数据（基础CRUD方法，继承自Service）
            var allDepartments = await GetAllAsync();

            // 2. 递归构建树形结构，根节点父ID为null
            return BuildDepartmentTree(allDepartments.ToList(), null);
        }

        /// <summary>
        /// 获取指定父部门下的直接子部门（非递归，仅一级）
        /// </summary>
        /// <param name="parentId">父部门ID（根部门传0或null）</param>
        /// <returns>直接子部门列表</returns>
        public async Task<IEnumerable<Department>> GetChildDepartmentsAsync(int parentId)
        {
            // 委托仓储层查询一级子部门（仓储层已做ParentId等值查询）
            return await _departmentRepository.GetChildrenAsync(parentId);
        }

        /// <summary>
        /// 递归构建部门树形结构（核心私有方法）
        /// 注：当前仅返回层级结构，但未给Department实体添加Children属性，需前端自行处理
        /// </summary>
        /// <param name="departments">待构建的全量部门列表</param>
        /// <param name="parentId">当前层级的父部门ID（根节点为null）</param>
        /// <returns>当前层级的部门列表（包含递归子部门）</returns>
        private IEnumerable<Department> BuildDepartmentTree(List<Department> departments, int? parentId)
        {
            // 1. 筛选当前层级的部门（父ID匹配）
            var nodes = departments.Where(d => d.ParentId == parentId).ToList();

            // 2. 递归为每个节点构建子节点（核心逻辑）
            foreach (var node in nodes)
            {
                var children = BuildDepartmentTree(departments, node.Id);
                // 【设计约束】：Department实体无Children属性，此处无法直接挂载子节点
                // 优化建议：创建DepartmentTreeNode类（继承Department，添加List<DepartmentTreeNode> Children属性）
            }

            // 3. 返回当前层级节点
            return nodes;
        }
        #endregion

        #region 部门-员工关联
        /// <summary>
        /// 查询指定部门下的所有员工（包含子部门？需看仓储层实现）
        /// </summary>
        /// <param name="deptId">部门ID</param>
        /// <returns>员工列表</returns>
        public async Task<IEnumerable<Employee>> GetDepartmentEmployeesAsync(int deptId)
        {
            // 委托员工仓储查询部门关联员工（仓储层已做DeptId等值查询）
            return await _employeeRepository.GetByDepartmentAsync(deptId);
        }
        #endregion

        #region 部门路径维护
        /// <summary>
        /// 更新部门路径（递归更新当前部门+所有子部门）
        /// 部门路径格式：父部门ID,当前部门ID（如"1,5,12"，根部门为"1"）
        /// 用途：快速查询部门层级、权限过滤、数据归类等
        /// </summary>
        /// <param name="deptId">待更新的部门ID</param>
        /// <returns>更新是否成功（异常时返回false）</returns>
        public async Task<bool> UpdateDepartmentPathAsync(int deptId)
        {
            try
            {
                // 1. 获取当前部门（基础CRUD方法）
                var department = await GetByIdAsync(deptId);
                if (department == null)
                {
                    return false; // 部门不存在，更新失败
                }

                // 2. 递归构建当前部门的完整路径
                string path = await BuildDepartmentPathAsync(department);

                // 3. 更新当前部门路径并保存
                department.DeptPath = path;
                await UpdateAsync(department); // 基础更新方法，继承自Service

                // 4. 递归更新所有子部门路径（核心：子部门路径依赖父部门）
                var childDepartments = await GetChildDepartmentsAsync(deptId);
                foreach (var child in childDepartments)
                {
                    await UpdateDepartmentPathAsync(child.Id);
                }

                return true;
            }
            catch
            {
                // 异常吞掉：返回false，上层可捕获并提示
                // 优化建议：记录日志（如log4net、Serilog），便于排查问题
                return false;
            }
        }

        /// <summary>
        /// 递归构建部门完整路径（私有辅助方法）
        /// </summary>
        /// <param name="department">当前部门</param>
        /// <returns>部门完整路径（如"1,5,12"）</returns>
        private async Task<string> BuildDepartmentPathAsync(Department department)
        {
            // 终止条件：根部门（ParentId=null），路径为自身ID
            if (department.ParentId == null)
            {
                return department.Id.ToString();
            }

            // 递归获取父部门路径
            var parent = await GetByIdAsync(department.ParentId.Value);
            if (parent == null)
            {
                // 父部门不存在，路径仅包含自身ID（容错处理）
                return department.Id.ToString();
            }

            // 拼接父路径 + 当前ID（核心逻辑）
            string parentPath = await BuildDepartmentPathAsync(parent);
            return $"{parentPath},{department.Id}";
        }
        #endregion

        #region 递归查询部门及子部门
        /// <summary>
        /// 获取指定部门及其所有递归子部门（平级列表，非树形）
        /// 用途：批量操作（如删除部门时级联删除子部门、权限授权时包含子部门等）
        /// </summary>
        /// <param name="deptId">根部门ID</param>
        /// <returns>部门列表（当前部门+所有子部门）</returns>
        public async Task<IEnumerable<Department>> GetDepartmentAndChildrenAsync(int deptId)
        {
            var result = new List<Department>();

            // 1. 添加当前部门
            var department = await GetByIdAsync(deptId);
            if (department != null)
            {
                result.Add(department);

                // 2. 递归添加所有子部门
                await GetAllChildDepartmentsAsync(deptId, result);
            }

            return result;
        }

        /// <summary>
        /// 递归获取所有子部门并添加到结果列表（私有辅助方法）
        /// </summary>
        /// <param name="parentId">父部门ID</param>
        /// <param name="departments">结果列表（引用传递，累加数据）</param>
        private async Task GetAllChildDepartmentsAsync(int parentId, List<Department> departments)
        {
            // 1. 获取一级子部门
            var children = await GetChildDepartmentsAsync(parentId);

            // 2. 遍历并递归（深度优先）
            foreach (var child in children)
            {
                departments.Add(child); // 添加当前子部门
                await GetAllChildDepartmentsAsync(child.Id, departments); // 递归获取孙子部门
            }
        }
        #endregion
    }
}