// 引入核心模型：User（用户）、Role（角色）、Permission（权限）、UserRole（用户角色关联）、RolePermission（角色权限关联）
using MES_WPF.Core.Models;
// 引入EF Core核心类：DbContext（数据库上下文）、DbSet、LINQ查询扩展、异步操作
using Microsoft.EntityFrameworkCore;
// 基础系统类：DateTime（登录时间）、IP地址字符串
using System;
// 集合类型：IEnumerable（角色/权限列表）
using System.Collections.Generic;
// LINQ扩展：Join/Where/Select/Distinct等查询方法
using System.Linq;
// 异步编程：Task/async/await（适配EF Core异步操作）
using System.Threading.Tasks;

// 命名空间：MES_WPF的数据访问层 → 系统管理模块的用户仓储
// 层级定位：数据访问层（Repository），封装用户表及关联表的数据库操作
// 核心职责：用户CRUD、登录校验、角色/权限关联查询、登录信息更新
namespace MES_WPF.Data.Repositories.SystemManagement
{
    /// <summary>
    /// 用户仓储实现类（数据访问层）
    /// 核心职责：
    /// 1. 封装User表通用CRUD（继承Repository<User>复用基础逻辑）
    /// 2. 实现用户个性化查询（按用户名、登录校验、角色/权限关联）
    /// 3. 处理用户登录信息更新等业务型数据操作
    /// 设计原则：
    /// - 异步优先：所有数据库操作使用EF Core异步方法，避免阻塞线程
    /// - 纯数据操作：仅负责数据库交互，无业务逻辑（如密码加密在Service层）
    /// - 关联查询：通过LINQ Join实现多表关联，等价于SQL的JOIN操作
    /// </summary>
    public class UserRepository : Repository<User>, IUserRepository
    {
        /// <summary>
        /// 构造函数（注入数据库上下文）
        /// 设计说明：
        /// - 调用父类Repository<User>构造函数，传递MesDbContext
        /// - 父类已封装通用CRUD（Add/Update/Delete/GetById等），子类专注个性化查询
        /// </summary>
        /// <param name="context">MES系统数据库上下文（EF Core核心，管理数据库连接和实体映射）</param>
        public UserRepository(MesDbContext context) : base(context)
        {
        }

        #region 基础查询：根据用户名查询用户
        /// <summary>
        /// 根据用户名精确查询用户（异步）
        /// 业务场景：
        /// - 登录前置校验（检查用户名是否存在）
        /// - 用户管理页面根据用户名搜索
        /// 技术细节：
        /// - FirstOrDefaultAsync：EF Core异步查询，无匹配项返回null
        /// - 索引优化：Username字段建议建立唯一索引，提升查询性能
        /// </summary>
        /// <param name="username">用户名（如“admin”“operator01”）</param>
        /// <returns>匹配的用户实体（无则返回null）</returns>
        public async Task<User> GetByUsernameAsync(string username)
        {
            // _dbSet：父类Repository<User>定义的User表DbSet<User>
            // Where(u => u.Username == username)：过滤用户名精确匹配的记录
            // FirstOrDefaultAsync：取第一条匹配记录，无则返回null（避免返回集合）
            return await _dbSet.FirstOrDefaultAsync(u => u.Username == username);
        }
        #endregion

        #region 核心操作：用户登录校验
        /// <summary>
        /// 验证用户登录信息（异步）
        /// 业务规则：
        /// 1. 用户名+密码精确匹配
        /// 2. 用户状态为正常（Status=1，禁用用户无法登录）
        /// 安全提示：
        /// - 注释明确标注“密码需哈希比较”：实际项目中禁止明文存储/比较密码
        /// - 密码哈希（如MD5/SHA256/BCrypt）应在Service层处理，仓储层仅做数值比较
        /// </summary>
        /// <param name="username">用户名</param>
        /// <param name="password">密码（建议传入哈希后的值）</param>
        /// <returns>匹配的用户实体（无匹配/禁用则返回null）</returns>
        public async Task<User> ValidateUserAsync(string username, string password)
        {
            // 注意：生产环境必须将密码转换为哈希值后再比较（如BCrypt.Net.HashPassword）
            // 此处仅为示例，明文密码存在严重安全风险！
            return await _dbSet.FirstOrDefaultAsync(u =>
                u.Username == username &&       // 用户名匹配
                u.Password == password &&       // 密码匹配（建议哈希后比较）
                u.Status == 1);                 // 用户状态为正常（1=正常，0=禁用）
        }
        #endregion

        #region 关联查询：获取用户拥有的角色列表
        /// <summary>
        /// 查询指定用户的所有启用状态角色（异步）
        /// 业务场景：
        /// - 登录后加载用户角色，用于权限控制
        /// - 用户管理页面展示用户已分配的角色
        /// 技术细节：
        /// - Join UserRoles（用户角色中间表）和Roles（角色表），等价于SQL INNER JOIN
        /// - 过滤启用状态角色（Status=1），禁用角色不返回
        /// </summary>
        /// <param name="userId">用户ID（User表主键）</param>
        /// <returns>角色实体列表（无角色返回空列表）</returns>
        public async Task<IEnumerable<Role>> GetUserRolesAsync(int userId)
        {
            // 分步解析：
            // 1. _context.Roles：角色表DbSet<Role>
            // 2. Join(关联表, 主表关联键, 关联表关联键, 结果投影)：
            //    - 关联表：UserRoles中当前用户的记录（ur => ur.UserId == userId）
            //    - 主表关联键：role.Id（角色表主键）
            //    - 关联表关联键：userRole.RoleId（中间表角色ID）
            //    - 结果投影：只返回Role实体（过滤中间表字段）
            // 3. Where(r => r.Status == 1)：过滤启用状态的角色（1=启用，0=禁用）
            // 4. ToListAsync()：异步执行查询，返回List<Role>（实现IEnumerable<Role>）
            return await _context.Roles
                .Join(
                    _context.UserRoles.Where(ur => ur.UserId == userId), // 筛选当前用户的角色关联记录
                    role => role.Id,                                    // 角色表关联键
                    userRole => userRole.RoleId,                        // 中间表角色关联键
                    (role, userRole) => role                             // 只返回角色实体
                )
                .Where(r => r.Status == 1) // 仅返回启用状态的角色
                .ToListAsync();
        }
        #endregion

        #region 关联查询：获取用户拥有的权限列表
        /// <summary>
        /// 查询指定用户的所有启用状态权限（去重）
        /// 业务逻辑：
        /// 1. 先查询用户所有角色ID（从UserRoles中间表）
        /// 2. 再通过RolePermissions中间表关联查询这些角色的权限
        /// 3. 过滤启用状态权限并去重（避免同一权限被多个角色分配）
        /// 业务场景：
        /// - 登录后加载用户所有权限，用于页面/按钮级权限控制
        /// - 权限校验时快速判断用户是否拥有指定权限
        /// </summary>
        /// <param name="userId">用户ID（User表主键）</param>
        /// <returns>权限实体列表（无权限返回空列表）</returns>
        public async Task<IEnumerable<Permission>> GetUserPermissionsAsync(int userId)
        {
            // 第一步：查询用户所有角色ID（从UserRoles中间表）
            // Select(ur => ur.RoleId)：仅提取RoleId字段，减少数据传输
            // ToListAsync()：加载到内存，避免后续多次查询数据库
            var roleIds = await _context.UserRoles
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            // 第二步：查询这些角色对应的所有启用状态权限（去重）
            return await _context.Permissions
                .Join(
                    // 关联表：RolePermissions中包含用户角色ID的记录
                    _context.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)),
                    permission => permission.Id,          // 权限表关联键（主键）
                    rolePermission => rolePermission.PermissionId, // 中间表权限关联键
                    (permission, rolePermission) => permission       // 只返回权限实体
                )
                .Where(p => p.Status == 1) // 过滤启用状态的权限（1=启用）
                .Distinct()                // 去重：同一权限可能被多个角色分配
                .ToListAsync();            // 异步加载到列表
        }
        #endregion

        #region 业务操作：更新用户最后登录信息
        /// <summary>
        /// 更新用户最后登录信息（异步）
        /// 业务场景：
        /// - 用户登录成功后，记录登录时间和登录IP
        /// - 用于审计日志、用户行为分析
        /// 技术细节：
        /// - FindAsync：根据主键快速查询（比FirstOrDefaultAsync性能更高）
        /// - 无显式Update：EF Core跟踪实体状态变更，SaveChangesAsync自动更新
        /// </summary>
        /// <param name="userId">用户ID（主键）</param>
        /// <param name="loginIp">登录IP地址（如“192.168.1.100”）</param>
        public async Task UpdateLastLoginInfoAsync(int userId, string loginIp)
        {
            // 根据主键查询用户（FindAsync：EF Core主键查询优化，性能最优）
            var user = await _dbSet.FindAsync(userId);

            // 空值校验：用户不存在则直接返回（避免空指针）
            if (user != null)
            {
                // 更新登录信息（EF Core自动跟踪实体状态变更）
                user.LastLoginTime = DateTime.Now; // 当前登录时间
                user.LastLoginIp = loginIp;        // 登录IP地址

                // 保存变更（父类Repository<User>的SaveChangesAsync方法，封装_context.SaveChangesAsync()）
                await SaveChangesAsync();
            }
        }
        #endregion
    }
}