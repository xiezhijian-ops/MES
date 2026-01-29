using MES_WPF.Data.Repositories.BasicInformation;
using MES_WPF.Model.BasicInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Core.Services.BasicInformation
{
    /// <summary>
    /// BOM（物料清单）服务实现类
    /// 核心职责：封装BOM相关业务逻辑，作为仓储层（数据访问）与视图模型层（UI交互）的中间层
    /// 设计模式：依赖注入+仓储模式，解耦数据访问与业务逻辑，便于维护和单元测试
    /// 继承自通用Service基类，复用通用CRUD方法（Add/Update/Delete/GetById等）
    /// </summary>
    public class BOMService : Service<BOM>, IBOMService
    {
        #region 依赖注入（仓储层）
        // BOM仓储接口：通过依赖注入获取，仅用于BOM专属数据查询（通用CRUD已在基类实现）
        private readonly IBOMRepository _bomRepository;
        #endregion

        #region 构造函数（初始化依赖）
        /// <summary>
        /// 构造函数：注入BOM仓储，传递给父类通用Service
        /// </summary>
        /// <param name="bomRepository">BOM仓储实例（由依赖注入容器提供）</param>
        public BOMService(IBOMRepository bomRepository) : base(bomRepository)
        {
            _bomRepository = bomRepository;
        }
        #endregion

        #region 基础查询方法（封装仓储层查询，适配业务场景）
        /// <summary>
        /// 根据BOM编码获取单个BOM（精准查询）
        /// 核心用途：BOM编码唯一性校验、快速定位特定BOM
        /// </summary>
        /// <param name="bomCode">BOM编码（如BOM-PROD-001）</param>
        /// <returns>匹配的BOM对象，无匹配则返回null</returns>
        public async Task<BOM> GetByCodeAsync(string bomCode)
        {
            // 直接调用仓储层专属查询方法，服务层不处理数据访问细节
            return await _bomRepository.GetByCodeAsync(bomCode);
        }

        /// <summary>
        /// 根据产品ID获取该产品下的所有BOM列表
        /// 核心用途：产品关联BOM查询、默认BOM设置时的批量处理
        /// </summary>
        /// <param name="productId">产品唯一标识</param>
        /// <returns>该产品下的所有BOM集合</returns>
        public async Task<IEnumerable<BOM>> GetByProductIdAsync(int productId)
        {
            return await _bomRepository.GetByProductIdAsync(productId);
        }

        /// <summary>
        /// 获取指定产品的默认BOM（同一产品仅一个默认BOM）
        /// 核心用途：生产工单创建、物料需求计算时自动选择默认BOM
        /// </summary>
        /// <param name="productId">产品唯一标识</param>
        /// <returns>该产品的默认BOM，无则返回null</returns>
        public async Task<BOM> GetDefaultByProductIdAsync(int productId)
        {
            return await _bomRepository.GetDefaultByProductIdAsync(productId);
        }

        /// <summary>
        /// 根据状态获取BOM列表（按业务状态筛选）
        /// 核心用途：筛选草稿/审核中/已发布/已失效的BOM
        /// </summary>
        /// <param name="status">BOM状态（1=草稿，2=审核中，3=已发布，4=已失效等，业务约定）</param>
        /// <returns>对应状态的BOM集合</returns>
        public async Task<IEnumerable<BOM>> GetByStatusAsync(byte status)
        {
            return await _bomRepository.GetByStatusAsync(status);
        }
        #endregion

        #region 核心业务方法（包含复杂业务逻辑处理）
        /// <summary>
        /// 设置指定产品的默认BOM（核心业务逻辑）
        /// 业务规则：同一产品只能有一个默认BOM，设置新默认时自动取消旧默认
        /// </summary>
        /// <param name="bomId">要设为默认的BOM ID</param>
        /// <param name="productId">所属产品ID</param>
        /// <returns>设置成功返回true</returns>
        /// <exception cref="ArgumentException">BOM不存在或不属于该产品时抛出</exception>
        public async Task<bool> SetDefaultBOMAsync(int bomId, int productId)
        {
            // 1. 查询该产品下的所有BOM（用于后续批量处理默认状态）
            var boms = (await GetByProductIdAsync(productId)).ToList();

            // 2. 校验目标BOM是否存在且属于当前产品（避免非法设置）
            var targetBom = boms.FirstOrDefault(b => b.Id == bomId);
            if (targetBom == null)
            {
                throw new ArgumentException($"BOM ID {bomId} 不存在或不属于产品 {productId}");
            }

            // 3. 取消该产品下所有BOM的默认状态（保证唯一默认）
            foreach (var bom in boms.Where(b => b.IsDefault))
            {
                bom.IsDefault = false;       // 取消默认标记
                bom.UpdateTime = DateTime.Now; // 更新时间戳（审计字段）
                await UpdateAsync(bom);      // 调用基类通用更新方法
            }

            // 4. 设置目标BOM为默认状态
            targetBom.IsDefault = true;    // 标记为默认
            targetBom.UpdateTime = DateTime.Now; // 更新时间戳
            await UpdateAsync(targetBom);  // 保存更新

            return true;
        }

        /// <summary>
        /// 更新BOM状态（状态流转控制）
        /// 核心用途：BOM从草稿→审核中→已发布→已失效的状态变更
        /// </summary>
        /// <param name="bomId">要更新状态的BOM ID</param>
        /// <param name="status">目标状态（业务约定的状态码）</param>
        /// <returns>更新后的BOM对象</returns>
        /// <exception cref="ArgumentException">BOM不存在时抛出</exception>
        public async Task<BOM> UpdateStatusAsync(int bomId, byte status)
        {
            // 1. 查询BOM是否存在（不存在则抛出异常）
            var bom = await GetByIdAsync(bomId);
            if (bom == null)
            {
                throw new ArgumentException($"BOM ID {bomId} 不存在");
            }

            // 2. 更新状态和时间戳（状态变更需记录更新时间）
            bom.Status = status;
            bom.UpdateTime = DateTime.Now;

            // 3. 调用基类通用更新方法，返回更新后的对象
            return await UpdateAsync(bom);
        }

        /// <summary>
        /// 检查BOM编码是否已存在（新增BOM时的唯一性校验）
        /// 核心用途：避免同一系统中出现重复的BOM编码
        /// </summary>
        /// <param name="bomCode">要校验的BOM编码</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public async Task<bool> IsBOMCodeExistsAsync(string bomCode)
        {
            // 调用GetByCodeAsync查询，存在则返回true
            var bom = await GetByCodeAsync(bomCode);
            return bom != null;
        }
        #endregion
    }
}