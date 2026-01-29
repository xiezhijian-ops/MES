using MES_WPF.Core.Models;
using MES_WPF.Data.Repositories.SystemManagement;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MES_WPF.Core.Services.SystemManagement
{
    /// <summary>
    /// 数据字典核心业务服务实现类
    /// 职责：封装字典（主表）及字典项（子表）的业务操作，对外提供统一的业务接口
    /// 设计：继承通用Service基类复用基础CRUD逻辑；实现IDictionaryService接口定义字典专属业务方法
    /// 依赖：字典仓储（主表）、字典项仓储（子表），通过构造函数注入解耦数据访问层
    /// </summary>
    public class DictionaryService : Service<Dictionary>, IDictionaryService
    {
        /// <summary>
        /// 字典主表仓储（数据访问层）：负责字典主表的数据库操作（如增删改查）
        /// </summary>
        private readonly IDictionaryRepository _dictionaryRepository;

        /// <summary>
        /// 字典项子表仓储（数据访问层）：负责字典项的数据库操作，因字典服务需联动操作子表，故注入该仓储
        /// </summary>
        private readonly IDictionaryItemRepository _dictionaryItemRepository;

        /// <summary>
        /// 构造函数：通过依赖注入初始化仓储实例，确保数据访问层解耦
        /// </summary>
        /// <param name="dictionaryRepository">字典主表仓储实例</param>
        /// <param name="dictionaryItemRepository">字典项子表仓储实例</param>
        /// <exception cref="ArgumentNullException">仓储实例为空时抛出空参数异常，避免空指针</exception>
        public DictionaryService(
            IDictionaryRepository dictionaryRepository,
            IDictionaryItemRepository dictionaryItemRepository)
            : base(dictionaryRepository) // 调用基类构造函数，注入通用仓储（复用基础CRUD）
        {
            // 空值校验：确保字典主表仓储有效
            _dictionaryRepository = dictionaryRepository ?? throw new ArgumentNullException(nameof(dictionaryRepository));
            // 空值校验：确保字典项子表仓储有效
            _dictionaryItemRepository = dictionaryItemRepository ?? throw new ArgumentNullException(nameof(dictionaryItemRepository));
        }

        /// <summary>
        /// 根据字典类型（唯一标识）查询字典主表信息
        /// 业务场景：业务模块（如设备管理）需先通过类型找到字典，再获取其下的字典项
        /// </summary>
        /// <param name="dictType">字典类型（全局唯一，如"EQUIP_STATUS"代表设备状态字典）</param>
        /// <returns>字典主表实体（无匹配时返回null）</returns>
        public async Task<Dictionary> GetByDictTypeAsync(string dictType)
        {
            // 委托仓储层执行数据库查询：按类型精准匹配字典主表
            return await _dictionaryRepository.GetByTypeAsync(dictType);
        }

        /// <summary>
        /// 根据字典ID查询其下所有字典项
        /// 业务场景：字典管理界面选中左侧字典后，加载右侧对应的字典项列表
        /// </summary>
        /// <param name="dictId">字典主表主键ID</param>
        /// <returns>字典项列表（默认按排序号升序排列）</returns>
        public async Task<IEnumerable<DictionaryItem>> GetDictItemsAsync(int dictId)
        {
            // 委托字典项仓储执行查询：按字典ID关联查询子表
            return await _dictionaryItemRepository.GetItemsByDictIdAsync(dictId);
        }

        /// <summary>
        /// 【组合查询】根据字典类型直接获取其下所有字典项
        /// 业务场景：业务模块（如工单管理）无需关心字典ID，仅需通过类型（如"WORK_ORDER_TYPE"）获取可选值
        /// 设计：封装"查字典主表→查字典项"两步操作，简化上层调用
        /// </summary>
        /// <param name="dictType">字典类型（如"MAINTENANCE_TYPE"代表维护类型）</param>
        /// <returns>字典项列表（无匹配字典时返回空列表，避免空指针）</returns>
        public async Task<IEnumerable<DictionaryItem>> GetDictItemsByTypeAsync(string dictType)
        {
            // 第一步：先根据类型查字典主表
            var dictionary = await GetByDictTypeAsync(dictType);

            // 防护逻辑：无匹配字典时返回空列表，避免后续调用GetDictItemsAsync传入无效ID
            if (dictionary == null)
            {
                return new List<DictionaryItem>();
            }

            // 第二步：根据字典ID查字典项
            return await GetDictItemsAsync(dictionary.Id);
        }

        /// <summary>
        /// 添加字典项（子表）
        /// 业务场景：字典管理界面新增字典项时调用
        /// 扩展：自动填充创建时间，无需上层业务关注
        /// </summary>
        /// <param name="dictItem">待添加的字典项实体</param>
        /// <returns>添加后的字典项（包含数据库自增ID）</returns>
        public async Task<DictionaryItem> AddDictItemAsync(DictionaryItem dictItem)
        {
            // 业务规则：自动填充创建时间（统一由服务层处理，保证数据一致性）
            dictItem.CreateTime = DateTime.Now;

            // 委托仓储层执行插入操作，返回包含主键的实体
            return await _dictionaryItemRepository.AddAsync(dictItem);
        }

        /// <summary>
        /// 更新字典项（子表）
        /// 业务场景：字典管理界面编辑字典项时调用
        /// 扩展：自动填充更新时间，捕获异常并返回操作结果
        /// </summary>
        /// <param name="dictItem">待更新的字典项实体（需包含主键ID）</param>
        /// <returns>更新结果：true=成功，false=失败（如主键无效、数据库异常）</returns>
        public async Task<bool> UpdateDictItemAsync(DictionaryItem dictItem)
        {
            try
            {
                // 业务规则：自动填充更新时间（统一由服务层处理）
                dictItem.UpdateTime = DateTime.Now;

                // 委托仓储层执行更新操作
                await _dictionaryItemRepository.UpdateAsync(dictItem);
                return true;
            }
            catch
            {
                // 异常捕获：屏蔽底层异常细节，向上返回布尔值（简化上层判断）
                // 注：实际项目可细化异常类型，返回具体错误信息
                return false;
            }
        }

        /// <summary>
        /// 删除单个字典项（子表）
        /// 业务场景：字典管理界面删除单个字典项时调用
        /// </summary>
        /// <param name="dictItemId">字典项主键ID</param>
        /// <returns>删除结果：true=成功，false=失败（如ID不存在、数据库异常）</returns>
        public async Task<bool> DeleteDictItemAsync(int dictItemId)
        {
            try
            {
                // 委托仓储层执行删除操作（按ID删除）
                await _dictionaryItemRepository.DeleteByIdAsync(dictItemId);
                return true;
            }
            catch
            {
                // 异常捕获：屏蔽底层异常，返回操作结果
                return false;
            }
        }

        /// <summary>
        /// 批量删除字典项（子表）
        /// 业务场景：预留批量操作功能（如字典管理界面多选删除）
        /// 设计：遍历ID集合逐个删除，兼容仓储层无批量删除接口的场景
        /// </summary>
        /// <param name="dictItemIds">待删除的字典项ID集合</param>
        /// <returns>删除结果：true=全部删除成功，false=任意一个失败（或参数为空）</returns>
        public async Task<bool> BatchDeleteDictItemsAsync(IEnumerable<int> dictItemIds)
        {
            try
            {
                // 遍历ID集合，逐个执行删除操作
                foreach (var id in dictItemIds)
                {
                    await _dictionaryItemRepository.DeleteByIdAsync(id);
                }
                return true;
            }
            catch
            {
                // 异常捕获：只要有一个删除失败，整体返回false
                return false;
            }
        }
    }
}