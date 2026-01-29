// 引入核心模型：Role（角色）、Permission（权限）、RolePermission（角色权限关联）、User（用户）、UserRole（用户角色关联）
using MES_WPF.Core.Models;
// 引入EF Core核心类：DbContext（数据库上下文）、DbSet、Join/Where等LINQ扩展、事务管理
using Microsoft.EntityFrameworkCore;
// 基础系统类：DateTime（创建时间）、ArgumentNullException（隐含空值校验）
using System;
// 集合类型：IEnumerable（权限ID集合、返回列表）
using System.Collections.Generic;
// LINQ扩展：Any()/Select()/Join()等查询方法
using System.Linq;
// 异步编程：Task/async/await（适配EF Core异步操作）
using System.Threading.Tasks;

// 命名空间：MES_WPF的数据访问层 → 系统管理模块的角色仓储
// 层级设计：Repository（数据访问层）→ DbContext（EF Core）→ Database（数据库）
// 核心职责：封装角色相关的数据库操作，对外暴露业务友好的接口
namespace MES_WPF.Data.Repositories.SystemManagement
{
    /// <summary>
    /// 角色仓储实现类（数据访问层）
    /// 核心职责：
    /// 1. 封装角色表（Role）的CRUD操作（继承Repository<Role>复用通用逻辑）
    /// 2. 实现角色关联数据查询（权限、用户）
    /// 3. 处理复杂业务的数据库事务（如权限分配）
    /// 设计原则：
    /// - 依赖DbContext：通过构造函数注入，解耦数据库连接配置
    /// - 异步优先：所有数据库操作使用EF Core异步方法，避免阻塞线程
    /// - 纯数据操作：仅负责数据库交互，不包含业务逻辑（业务逻辑在Service层）
    /// </summary>
    public class RoleRepository : Repository<Role>, IRoleRepository
    {
        /// <summary>
        /// 构造函数（注入数据库上下文）
        /// 设计说明：
        /// - 继承父类Repository<Role>的构造函数，将DbContext传递给父类
        /// - 父类已封装通用CRUD（Add/Update/Delete/GetById等），子类专注于个性化查询
        /// </summary>
        /// <param name="context">MES系统数据库上下文（MesDbContext）</param>
        public RoleRepository(MesDbContext context) : base(context)
        {
        }

        #region 基础查询：根据角色编码查询角色
        /// <summary>
        /// 根据角色编码精确查询角色（异步）
        /// 业务场景：
        /// - 登录时校验角色编码有效性
        /// - 权限分配时根据编码定位角色
        /// 技术细节：
        /// - FirstOrDefaultAsync：EF Core异步查询，无匹配项返回null
        /// - 索引优化：RoleCode字段建议建立唯一索引，提升查询性能
        /// </summary>
        /// <param name="roleCode">角色编码（如“ADMIN”“PROD_OPERATOR”）</param>
        /// <returns>匹配的角色实体（无则返回null）</returns>
        public async Task<Role> GetByCodeAsync(string roleCode)
        {
            // _dbSet：父类Repository<Role>定义的Role表DbSet<Role>
            // Where(r => r.RoleCode == roleCode)：过滤角色编码匹配的记录
            // FirstOrDefaultAsync：取第一条匹配记录，无则返回null（避免返回集合）
            return await _dbSet.FirstOrDefaultAsync(r => r.RoleCode == roleCode);
        }
        #endregion

        #region 关联查询：获取角色的权限列表
        /// <summary>
        /// 查询指定角色的所有启用状态权限（异步）
        /// 业务逻辑：
        /// 1. 关联查询RolePermission（角色权限中间表）和Permission（权限表）
        /// 2. 过滤出启用状态（Status=1）的权限（禁用权限不返回）
        /// 技术细节：
        /// - Join方法：EF Core LINQ Join，等价于SQL的INNER JOIN
        /// - ToListAsync：异步加载所有结果到内存，返回IEnumerable<Permission>
        /// </summary>
        /// <param name="roleId">角色ID（Role表主键）</param>
        /// <returns>权限实体列表（无权限返回空列表）</returns>
        public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(int roleId)
        {
            // 分步解析：
            // 1. _context.Permissions：权限表DbSet<Permission>
            // 2. Join(关联表, 主表关联键, 关联表关联键, 结果投影)：
            //    - 关联表：RolePermissions中当前角色的记录（rp => rp.RoleId == roleId）
            //    - 主表关联键：permission.Id（权限表主键）
            //    - 关联表关联键：rolePermission.PermissionId（中间表权限ID）
            //    - 结果投影：只返回Permission实体（过滤中间表字段）
            // 3. Where(p => p.Status == 1)：过滤启用状态的权限（1=启用，0=禁用）
            // 4. ToListAsync()：异步执行查询，返回List<Permission>（实现IEnumerable<Permission>）
            return await _context.Permissions
                .Join(
                    _context.RolePermissions.Where(rp => rp.RoleId == roleId), // 筛选当前角色的权限关联记录
                    permission => permission.Id,                              // 权限表关联键
                    rolePermission => rolePermission.PermissionId,            // 中间表权限关联键
                    (permission, rolePermission) => permission                 // 只返回权限实体
                )
                .Where(p => p.Status == 1) // 仅返回启用状态的权限
                .ToListAsync();
        }
        #endregion

        #region 事务操作：为角色分配权限（全量覆盖）
        /// <summary>
        /// 为角色分配权限（异步+事务）
        /// 核心逻辑：
        /// 1. 开启数据库事务（保证删除旧权限、添加新权限原子性）
        /// 2. 删除角色原有所有权限关联记录
        /// 3. 批量添加新的权限关联记录
        /// 4. 提交事务（成功）/回滚事务（失败）
        /// 业务规则：
        /// - 全量覆盖：先删后加，避免重复权限关联
        /// - 审计字段：记录操作人ID和创建时间（用于追溯权限变更）
        /// </summary>
        /// <param name="roleId">目标角色ID</param>
        /// <param name="permissionIds">新分配的权限ID集合（空集合则清空权限）</param>
        /// <param name="operatorId">操作人ID（当前登录用户ID，审计字段）</param>
        /// <exception cref="Exception">事务失败时抛出异常（由Service层捕获）</exception>
        public async Task AssignPermissionsAsync(int roleId, IEnumerable<int> permissionIds, int operatorId)
        {
            // 1. 开启数据库事务（EF Core异步事务）
            // using声明：事务自动释放，无需手动Dispose
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 2. 删除角色原有所有权限关联记录
                // 2.1 查询当前角色的所有权限关联记录
                var existingRolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == roleId) // 筛选当前角色的关联记录
                    .ToListAsync();                   // 加载到内存（避免后续查询重复）

                // 2.2 批量删除（RemoveRange比逐条Remove性能更高）
                _context.RolePermissions.RemoveRange(existingRolePermissions);

                // 3. 批量添加新的权限关联记录（权限ID集合非空时）
                if (permissionIds != null && permissionIds.Any())
                {
                    // 3.1 将权限ID集合转换为RolePermission实体集合
                    var rolePermissions = permissionIds.Select(pid => new RolePermission
                    {
                        RoleId = roleId,          // 关联角色ID
                        PermissionId = pid,       // 关联权限ID
                        CreateBy = operatorId,    // 操作人ID（审计字段）
                        CreateTime = DateTime.Now // 创建时间（审计字段）
                    });

                    // 3.2 批量添加（AddRangeAsync比逐条AddAsync性能更高）
                    await _context.RolePermissions.AddRangeAsync(rolePermissions);
                }

                // 4. 提交事务前保存所有更改（EF Core批量提交）
                await _context.SaveChangesAsync();

                // 5. 提交事务（所有操作生效）
                await transaction.CommitAsync();
            }
            catch
            {
                // 捕获异常：回滚事务（所有操作撤销）
                await transaction.RollbackAsync();

                // 重新抛出异常：由Service层处理（如返回false、记录日志）
                throw;
            }
        }
        #endregion

        #region 关联查询：获取角色的用户列表
        /// <summary>
        /// 查询指定角色的所有正常状态用户（异步）
        /// 业务场景：
        /// - 角色管理页面展示角色已分配的用户
        /// - 权限校验时查询角色下的所有用户
        /// 技术细节：
        /// - Join UserRoles（用户角色中间表）和Users（用户表）
        /// - 过滤正常状态（Status=1）的用户（禁用用户不返回）
        /// </summary>
        /// <param name="roleId">角色ID（Role表主键）</param>
        /// <returns>用户实体列表（无用户返回空列表）</returns>
        public async Task<IEnumerable<User>> GetRoleUsersAsync(int roleId)
        {
            // 分步解析：
            // 1. _context.Users：用户表DbSet<User>
            // 2. Join(关联表, 主表关联键, 关联表关联键, 结果投影)：
            //    - 关联表：UserRoles中当前角色的记录（ur => ur.RoleId == roleId）
            //    - 主表关联键：user.Id（用户表主键）
            //    - 关联表关联键：userRole.UserId（中间表用户ID）
            //    - 结果投影：只返回User实体
            // 3. Where(u => u.Status == 1)：过滤正常状态的用户（1=正常，0=禁用）
            // 4. ToListAsync()：异步执行查询，返回List<User>
            return await _context.Users
                .Join(
                    _context.UserRoles.Where(ur => ur.RoleId == roleId), // 筛选当前角色的用户关联记录
                    user => user.Id,                                    // 用户表关联键
                    userRole => userRole.UserId,                        // 中间表用户关联键
                    (user, userRole) => user                             // 只返回用户实体
                )
                .Where(u => u.Status == 1) // 仅返回正常状态的用户
                .ToListAsync();
        }
        #endregion
    }
}