// 引入核心模型：Role（角色实体）、Permission（权限实体）、RolePermission（角色权限关联实体）
using MES_WPF.Core.Models;
// 引入仓储层接口：角色/角色权限/权限仓储（数据访问层）
using MES_WPF.Data.Repositories.SystemManagement;
// 基础系统类：ArgumentNullException（空值校验）、DateTime（创建时间）
using System;
// 集合类型：IEnumerable（权限ID集合、返回列表）
using System.Collections.Generic;
// LINQ扩展：Any()方法（权限校验）
using System.Linq;
// 异步编程：Task/async/await（适配仓储层异步操作）
using System.Threading.Tasks;

// 命名空间：MES_WPF的核心服务层 → 系统管理模块的角色服务
// 层级设计：Service（业务逻辑层）→ Repository（数据访问层）→ Database（数据存储）
namespace MES_WPF.Core.Services.SystemManagement
{
    /// <summary>
    /// 角色服务实现类（业务逻辑层）
    /// 核心职责：
    /// 1. 封装角色相关业务逻辑（角色查询、权限分配、权限校验）
    /// 2. 协调多个仓储层（角色/角色权限/权限）完成复杂业务
    /// 3. 对外暴露统一接口（IRoleService），隔离仓储层细节
    /// 继承关系：
    /// - 继承Service<Role>：复用通用CRUD逻辑（如Add/Update/Delete）
    /// - 实现IRoleService：保证接口规范，便于依赖注入和单元测试
    /// </summary>
    public class RoleService : Service<Role>, IRoleService
    {
        #region 私有只读字段（仓储层依赖）
        /// <summary>
        /// 角色仓储（数据访问层）：封装角色表的CRUD操作
        /// </summary>
        private readonly IRoleRepository _roleRepository;
        /// <summary>
        /// 角色权限关联仓储：封装角色-权限中间表的CRUD操作
        /// </summary>
        private readonly IRolePermissionRepository _rolePermissionRepository;
        /// <summary>
        /// 权限仓储：封装权限表的CRUD操作
        /// </summary>
        private readonly IPermissionRepository _permissionRepository;
        #endregion

        #region 构造函数（依赖注入 + 空值校验）
        /// <summary>
        /// 构造函数（核心：注入仓储层依赖）
        /// 设计原则：
        /// 1. 依赖注入：通过构造函数接收仓储接口，解耦具体实现（如EF Core/ADO.NET）
        /// 2. 空值校验：防止仓储实例为空，提前暴露错误
        /// 3. 基类调用：base(roleRepository)将角色仓储传递给父类，复用通用CRUD
        /// </summary>
        /// <param name="roleRepository">角色仓储（必须非空）</param>
        /// <param name="rolePermissionRepository">角色权限关联仓储（必须非空）</param>
        /// <param name="permissionRepository">权限仓储（必须非空）</param>
        public RoleService(
            IRoleRepository roleRepository,
            IRolePermissionRepository rolePermissionRepository,
            IPermissionRepository permissionRepository)
            : base(roleRepository) // 调用父类构造函数，传入角色仓储（复用通用CRUD）
        {
            // 空值校验：仓储实例为空则抛ArgumentNullException，明确错误源头
            _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
            _rolePermissionRepository = rolePermissionRepository ?? throw new ArgumentNullException(nameof(rolePermissionRepository));
            _permissionRepository = permissionRepository ?? throw new ArgumentNullException(nameof(permissionRepository));
        }
        #endregion

        #region 核心业务方法（角色查询）
        /// <summary>
        /// 根据角色编码查询角色（精确匹配）
        /// 业务场景：
        /// - 登录时校验角色编码有效性
        /// - 分配权限时根据编码定位角色
        /// </summary>
        /// <param name="roleCode">角色编码（如“ADMIN”“OPERATOR”）</param>
        /// <returns>匹配的角色实体（无则返回null）</returns>
        /// <remarks>异步方法：适配仓储层异步查询，避免阻塞UI/业务线程</remarks>
        public async Task<Role> GetByRoleCodeAsync(string roleCode)
        {
            // 委托给角色仓储的编码查询方法，业务层不直接操作数据
            return await _roleRepository.GetByCodeAsync(roleCode);
        }
        #endregion

        #region 核心业务方法（权限查询/校验）
        /// <summary>
        /// 查询指定角色的所有权限
        /// 业务场景：
        /// - 角色管理页面展示角色已分配的权限
        /// - 权限校验前预加载角色所有权限（减少数据库查询）
        /// </summary>
        /// <param name="roleId">角色ID（主键）</param>
        /// <returns>权限实体列表（无权限则返回空列表）</returns>
        public async Task<IEnumerable<Permission>> GetRolePermissionsAsync(int roleId)
        {
            // 委托给角色仓储的关联查询方法（仓储层已封装JOIN逻辑）
            return await _roleRepository.GetRolePermissionsAsync(roleId);
        }

        /// <summary>
        /// 校验角色是否拥有指定权限（权限编码精确匹配）
        /// 核心权限控制逻辑：
        /// - 按钮级权限控制（如“生产计划-删除”）
        /// - 页面级权限控制（如“系统管理页面访问”）
        /// </summary>
        /// <param name="roleId">角色ID</param>
        /// <param name="permissionCode">权限编码（如“PROD_PLAN_DELETE”“SYS_MANAGE_VIEW”）</param>
        /// <returns>true=有权限，false=无权限</returns>
        public async Task<bool> HasPermissionAsync(int roleId, string permissionCode)
        {
            // 第一步：获取角色所有权限（复用已有方法，减少代码冗余）
            var permissions = await GetRolePermissionsAsync(roleId);

            // 第二步：LINQ校验是否存在匹配的权限编码
            // Any()：延迟执行，找到第一个匹配项即返回，性能优化
            return permissions.Any(p => p.PermissionCode == permissionCode);
        }
        #endregion

        #region 核心业务方法（权限分配）
        /// <summary>
        /// 为角色分配权限（全量覆盖：先删后加）
        /// 业务规则：
        /// 1. 先删除角色原有所有权限（避免重复分配）
        /// 2. 批量添加新的权限关联记录
        /// 3. 记录操作人ID和创建时间（审计日志）
        /// </summary>
        /// <param name="roleId">目标角色ID</param>
        /// <param name="permissionIds">新分配的权限ID集合（空集合则清空角色权限）</param>
        /// <param name="operatorId">操作人ID（当前登录用户ID，用于审计）</param>
        /// <returns>true=分配成功，false=分配失败（异常捕获）</returns>
        public async Task<bool> AssignPermissionsAsync(int roleId, IEnumerable<int> permissionIds, int operatorId)
        {
            try
            {
                // 第一步：删除角色原有所有权限（全量覆盖逻辑）
                await _rolePermissionRepository.DeleteByIdAsync(roleId);

                // 第二步：遍历新权限ID，批量添加角色-权限关联记录
                foreach (var permissionId in permissionIds)
                {
                    // 构建角色权限关联实体（中间表记录）
                    var rolePermission = new RolePermission
                    {
                        RoleId = roleId,          // 关联角色ID
                        PermissionId = permissionId, // 关联权限ID
                        CreateBy = operatorId,    // 操作人ID（审计字段）
                        CreateTime = DateTime.Now // 创建时间（审计字段）
                    };

                    // 添加单条关联记录（仓储层已封装事务/批量操作可扩展）
                    await _rolePermissionRepository.AddAsync(rolePermission);
                }

                // 所有操作完成，返回成功
                return true;
            }
            catch
            {
                // 捕获所有异常（如数据库连接失败、主键冲突等）
                // 业务层不处理具体异常类型，仅返回失败状态（上层可统一处理）
                return false;
            }
        }
        #endregion

        #region 待实现方法（角色分类查询）
        /// <summary>
        /// 查询所有系统角色（预留方法）
        /// 业务说明：
        /// - 系统角色：内置角色（如管理员、超级管理员），不可删除
        /// - 角色类型标识：1=系统角色（硬编码，后续可抽为枚举）
        /// </summary>
        /// <returns>系统角色列表</returns>
        /// <exception cref="NotImplementedException">方法暂未实现</exception>
        public async Task<IEnumerable<Role>> GetSystemRolesAsync()
        {
            // 抛出未实现异常：明确告知调用方该方法暂未开发
            throw new NotImplementedException("Method not implemented yet.");

            // 预留实现代码：通过角色类型查询系统角色
            //return await _roleRepository.GetRolesByTypeAsync(1); // 1表示系统角色
        }

        /// <summary>
        /// 查询所有业务角色（预留方法）
        /// 业务说明：
        /// - 业务角色：用户自定义角色（如生产操作员、质检员），可增删改
        /// - 角色类型标识：2=业务角色
        /// </summary>
        /// <returns>业务角色列表</returns>
        /// <exception cref="NotImplementedException">方法暂未实现</exception>
        public async Task<IEnumerable<Role>> GetBusinessRolesAsync()
        {
            throw new NotImplementedException("Method not implemented yet.");

            // 预留实现代码：通过角色类型查询业务角色
            //return await _roleRepository.GetRolesByTypeAsync(2); // 2表示业务角色
        }
        #endregion
    }
}