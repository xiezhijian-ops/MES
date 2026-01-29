using MES_WPF.Core.Models;
using MES_WPF.Data.Repositories.SystemManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Core.Services.SystemManagement
{
    /// <summary>
    /// 字典项业务服务实现类
    /// 封装字典项的所有业务操作，依赖仓储层实现数据持久化，对外提供统一的业务接口
    /// 继承通用Service基类，复用基础CRUD逻辑；实现IDictionaryItemService接口，定义字典项专属业务方法
    /// </summary>
    public class DictionaryItemService : Service<DictionaryItem>, IDictionaryItemService
    {
        /// <summary>
        /// 字典项仓储接口（数据访问层），负责字典项的数据库操作
        /// </summary>
        private readonly IDictionaryItemRepository _dictionaryItemRepository;

        /// <summary>
        /// 构造函数：通过依赖注入初始化字典项仓储
        /// </summary>
        /// <param name="dictionaryItemRepository">字典项仓储实例</param>
        /// <exception cref="ArgumentNullException">仓储实例为空时抛出</exception>
        public DictionaryItemService(IDictionaryItemRepository dictionaryItemRepository)
            : base(dictionaryItemRepository) // 调用基类构造函数，注入通用仓储
        {
            // 空值校验，确保仓储实例有效
            _dictionaryItemRepository = dictionaryItemRepository ?? throw new ArgumentNullException(nameof(dictionaryItemRepository));
        }

        /// <summary>
        /// 根据字典ID查询对应的所有字典项
        /// 场景：选中左侧字典后，加载右侧字典项列表
        /// </summary>
        /// <param name="dictId">字典主键ID</param>
        /// <returns>字典项列表（按排序号升序）</returns>
        public async Task<IEnumerable<DictionaryItem>> GetByDictIdAsync(int dictId)
        {
            // 委托仓储层执行数据库查询，返回指定字典下的所有项
            return await _dictionaryItemRepository.GetItemsByDictIdAsync(dictId);
        }

        /// <summary>
        /// 根据字典类型查询对应的所有字典项
        /// 场景：业务模块（如设备管理）根据字典类型（如"设备状态"）获取可选值
        /// </summary>
        /// <param name="dictType">字典类型（唯一标识，如"EQUIP_STATUS"）</param>
        /// <returns>字典项列表</returns>
        public async Task<IEnumerable<DictionaryItem>> GetByDictTypeAsync(string dictType)
        {
            return await _dictionaryItemRepository.GetItemsByDictTypeAsync(dictType);
        }

        /// <summary>
        /// 获取字典项的"值-文本"映射字典
        /// 场景：下拉框绑定（如将字典项值作为value，文本作为显示内容）
        /// </summary>
        /// <param name="dictType">字典类型</param>
        /// <returns>键：字典项值；值：字典项文本</returns>
        public async Task<IDictionary<string, string>> GetDictItemMapAsync(string dictType)
        {
            return await _dictionaryItemRepository.GetDictItemMapAsync(dictType);
        }

        /// <summary>
        /// 检查指定字典下的字典项值是否已存在（用于新增/编辑时的唯一性校验）
        /// </summary>
        /// <param name="dictId">字典ID</param>
        /// <param name="itemValue">字典项值（需校验的内容）</param>
        /// <param name="excludeId">排除的字典项ID（编辑时排除自身）</param>
        /// <returns>true=已存在，false=不存在</returns>
        public async Task<bool> IsItemValueExistsAsync(int dictId, string itemValue, int? excludeId = null)
        {
            // 委托仓储层执行唯一性查询
            return await _dictionaryItemRepository.IsItemValueExistsAsync(dictId, itemValue, excludeId);
        }

        /// <summary>
        /// 批量删除字典项
        /// 场景：批量操作功能（当前界面未实现，预留扩展）
        /// </summary>
        /// <param name="ids">待删除的字典项ID集合</param>
        /// <returns>true=删除成功，false=删除失败（参数为空/执行异常）</returns>
        public async Task<bool> BatchDeleteAsync(IEnumerable<int> ids)
        {
            // 空值/空集合校验：无待删除ID时直接返回失败
            if (ids == null || !ids.Any())
            {
                return false;
            }

            try
            {
                // 遍历ID集合，逐个删除（可优化为仓储层批量SQL，提升性能）
                foreach (var id in ids)
                {
                    await _dictionaryItemRepository.DeleteByIdAsync(id);
                }

                // 全部删除完成后返回成功
                return true;
            }
            catch
            {
                // 捕获所有异常（如数据库连接异常、外键关联异常），返回删除失败
                // 注：实际项目中可细化异常类型，返回具体错误信息
                return false;
            }
        }
    }
}