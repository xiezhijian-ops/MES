using MES_WPF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Data.Repositories.SystemManagement
{
    /// <summary>
    /// 部门仓储类
    /// 继承通用仓储基类，实现部门专属的数据访问逻辑
    /// 仓储模式：封装部门表的CRUD及专属业务查询，解耦数据访问与业务逻辑
    /// </summary>
    public class DepartmentRepository : Repository<Department>, IDepartmentRepository
    {
        /// <summary>
        /// 构造函数：注入数据库上下文，传递给父类通用仓储
        /// </summary>
        /// <param name="context">MES数据库上下文</param>
        public DepartmentRepository(MesDbContext context) : base(context)
        {
        }

        /// <summary>
        /// 根据部门编码获取单个部门（精准查询）
        /// 部门编码是唯一标识，用于快速定位特定部门
        /// </summary>
        /// <param name="deptCode">部门编码（如ADMIN、PROD）</param>
        /// <returns>匹配的部门对象，无匹配则返回null</returns>
        public async Task<Department> GetByCodeAsync(string deptCode)
        {
            // FirstOrDefaultAsync：异步查询第一条匹配数据，无则返回null
            return await _dbSet.FirstOrDefaultAsync(d => d.DeptCode == deptCode);
        }

        /// <summary>
        /// 获取所有启用状态的部门，并整理为树形结构的根节点
        /// 注：此处未直接构建嵌套树形对象，仅返回顶级部门，前端可通过ParentId递归组装树
        /// </summary>
        /// <returns>顶级部门列表（ParentId为null的部门）</returns>
        public async Task<IEnumerable<Department>> GetDepartmentTreeAsync()
        {
            // 1. 查询所有启用状态的部门，按排序号升序排列
            var departments = await _dbSet
                .Where(d => d.Status == 1) // 1：业务约定的"启用/正常"状态
                .OrderBy(d => d.SortOrder) // 按排序号控制前端展示顺序
                .ToListAsync();

            // 2. 筛选出顶级部门（无父部门的根节点）
            var rootDepartments = departments.Where(d => d.ParentId == null).ToList();

            // 设计说明：
            // - 若需返回完整嵌套树形结构，需递归为每个根节点添加Children属性
            // - 当前设计仅返回根节点，降低服务端计算成本，由前端根据ParentId组装树
            return rootDepartments;
        }

        /// <summary>
        /// 获取指定父部门下的所有启用状态子部门
        /// 用于树形结构的懒加载（点击父节点加载子节点）
        /// </summary>
        /// <param name="parentId">父部门ID</param>
        /// <returns>子部门列表</returns>
        public async Task<IEnumerable<Department>> GetChildrenAsync(int parentId)
        {
            return await _dbSet
                .Where(d => d.ParentId == parentId && d.Status == 1) // 匹配父ID + 启用状态
                .OrderBy(d => d.SortOrder) // 按排序号展示
                .ToListAsync();
        }

        /// <summary>
        /// 获取指定部门下的所有在职员工
        /// 关联查询Employee表，实现部门-员工的关联业务逻辑
        /// </summary>
        /// <param name="deptId">部门ID</param>
        /// <returns>该部门下的在职员工列表</returns>
        public async Task<IEnumerable<Employee>> GetDepartmentEmployeesAsync(int deptId)
        {
            // 直接通过上下文访问Employee表，无需依赖Employee仓储，简化关联查询
            return await _context.Employees
                .Where(e => e.DeptId == deptId && e.Status == 1) // 1：员工"在职"状态
                .OrderBy(e => e.EmployeeCode) // 按工号排序
                .ToListAsync();
        }

        /// <summary>
        /// 递归更新部门路径（核心业务逻辑）
        /// 部门路径格式：父部门ID,当前部门ID（如1,2,3），用于快速定位部门层级
        /// 当部门的父级变更时，需同步更新自身及所有子部门的路径
        /// </summary>
        /// <param name="deptId">待更新的部门ID</param>
        /// <returns>更新是否成功（存在该部门则为true）</returns>
        public async Task<bool> UpdateDeptPathAsync(int deptId)
        {
            // 1. 查询当前部门，无则返回更新失败
            var department = await _dbSet.FindAsync(deptId);
            if (department == null)
            {
                return false;
            }

            // 2. 构建当前部门的路径
            string deptPath = deptId.ToString(); // 基础路径：自身ID
            if (department.ParentId.HasValue)
            {
                // 有父部门：拼接父部门路径 + 自身ID
                var parentDept = await _dbSet.FindAsync(department.ParentId.Value);
                if (parentDept != null && !string.IsNullOrEmpty(parentDept.DeptPath))
                {
                    deptPath = parentDept.DeptPath + "," + deptId;
                }
            }

            // 3. 更新当前部门的路径并保存
            department.DeptPath = deptPath;
            await _context.SaveChangesAsync();

            // 4. 递归更新所有子部门的路径（关键：父路径变更，子路径需同步）
            var children = await GetChildrenAsync(deptId);
            foreach (var child in children)
            {
                await UpdateDeptPathAsync(child.Id);
            }

            // 设计说明：
            // - 递归更新保证整个部门树的路径一致性
            // - 若部门层级过深（如10层以上），需注意递归深度，可优化为循环实现
            return true;
        }
    }
}