using MES_WPF.Core.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Data.Repositories.SystemManagement
{
    /// <summary>
    /// 字典项仓储类
    /// 封装字典项（DictionaryItem）的专属数据访问逻辑，继承通用仓储基类
    /// 核心作用：管理系统通用字典数据（如状态、类型、枚举类数据），实现数据复用与统一维护
    /// </summary>
    public class DictionaryItemRepository : Repository<DictionaryItem>, IDictionaryItemRepository
    {
        /// <summary>
        /// 构造函数：注入数据库上下文，传递给父类通用仓储
        /// </summary>
        /// <param name="context">MES数据库上下文</param>
        public DictionaryItemRepository(MesDbContext context) : base(context)
        {
        }

        /// <summary>
        /// 根据字典ID获取启用状态的字典项列表
        /// 字典ID关联Dictionary表，一个字典（如"设备状态"）对应多个字典项（如"启用/禁用/维修中"）
        /// </summary>
        /// <param name="dictId">字典主表ID</param>
        /// <returns>该字典下的启用状态字典项列表（按排序号升序）</returns>
        public async Task<IEnumerable<DictionaryItem>> GetItemsByDictIdAsync(int dictId)
        {
            return await _dbSet
                .Where(di => di.DictId == dictId && di.Status == 1) // 1=启用状态（业务约定）
                .OrderBy(di => di.SortOrder) // 按排序号控制前端展示顺序（如"启用"排在"禁用"前）
                .ToListAsync();
        }

        /// <summary>
        /// 根据字典类型获取启用状态的字典项列表（更常用的查询方式）
        /// 字典类型是业务标识（如"equipment_status"），比字典ID更易记忆和使用
        /// </summary>
        /// <param name="dictType">字典类型（如"status"、"equipment_type"）</param>
        /// <returns>该类型字典下的启用状态字典项列表，无则返回空列表</returns>
        public async Task<IEnumerable<DictionaryItem>> GetItemsByDictTypeAsync(string dictType)
        {
            // 第一步：先从字典主表（Dictionary）查询匹配类型的启用状态字典
            var dictionary = await _context.Dictionaries
                .FirstOrDefaultAsync(d => d.DictType == dictType && d.Status == 1);

            // 无匹配字典则返回空列表，避免空引用
            if (dictionary == null)
            {
                return new List<DictionaryItem>();
            }

            // 第二步：复用GetItemsByDictIdAsync方法获取字典项，减少代码冗余
            return await GetItemsByDictIdAsync(dictionary.Id);
        }

        /// <summary>
        /// 获取字典项的"值-文本"键值对映射（前端下拉框/枚举展示专用）
        /// 例如：{"1":"启用", "2":"禁用"}，简化前端数据绑定逻辑
        /// </summary>
        /// <param name="dictType">字典类型</param>
        /// <returns>字典项值为Key，字典项文本为Value的键值对集合</returns>
        public async Task<IDictionary<string, string>> GetDictItemMapAsync(string dictType)
        {
            // 先获取字典项列表，再转换为键值对
            var items = await GetItemsByDictTypeAsync(dictType);
            return items.ToDictionary(i => i.ItemValue, i => i.ItemText);
        }

        /// <summary>
        /// 检查指定字典下的字典项值是否已存在（新增/编辑时的唯一性校验）
        /// 确保同一字典下的"值"不重复（如"状态"字典下不能有两个"1=启用"）
        /// </summary>
        /// <param name="dictId">字典ID</param>
        /// <param name="itemValue">字典项值（如"1"、"2"）</param>
        /// <param name="excludeId">排除的字典项ID（编辑时排除自身）</param>
        /// <returns>存在返回true，不存在返回false</returns>
        public async Task<bool> IsItemValueExistsAsync(int dictId, string itemValue, int? excludeId = null)
        {
            // 基础查询：匹配字典ID + 字典项值
            var query = _dbSet.Where(di => di.DictId == dictId && di.ItemValue == itemValue);

            // 编辑场景：排除当前编辑的字典项ID（避免校验自身）
            if (excludeId.HasValue)
            {
                query = query.Where(di => di.Id != excludeId.Value);
            }

            // AnyAsync：高效判断是否存在匹配数据（无需查询全量）
            return await query.AnyAsync();
        }
    }
}