using MES_WPF.Data.Repositories.BasicInformation;
using MES_WPF.Model.BasicInformation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MES_WPF.Core.Services.BasicInformation
{
    /// <summary>
    /// 工艺路线步骤服务实现类（服务层）
    /// 职责：封装工艺路线步骤的业务逻辑，对外提供统一的数据操作接口
    /// 继承自泛型基类 Service<RouteStep>：复用基类的通用CRUD方法（如GetByIdAsync、AddAsync等）
    /// 实现 IRouteStepService 接口：定义工艺路线步骤的专属业务方法
    /// </summary>
    public class RouteStepService : Service<RouteStep>, IRouteStepService
    {
        /// <summary>
        /// 工艺路线步骤仓储接口（依赖注入）
        /// 用于调用仓储层的自定义查询/操作方法（基类仓储无法满足的专属逻辑）
        /// </summary>
        private readonly IRouteStepRepository _routeStepRepository;

        /// <summary>
        /// 构造函数（依赖注入初始化）
        /// </summary>
        /// <param name="routeStepRepository">工艺路线步骤仓储实现类实例（由DI容器注入）</param>
        public RouteStepService(IRouteStepRepository routeStepRepository)
            : base(routeStepRepository) // 调用基类构造函数，传递仓储对象（满足基类对通用仓储的依赖）
        {
            // 初始化自定义仓储字段（用于调用仓储层的专属方法）
            _routeStepRepository = routeStepRepository;
        }

        /// <summary>
        /// 获取指定工艺路线下的所有步骤（按步骤序号排序）
        /// 场景：编辑工艺路线时展示所有步骤、生产执行时按步骤流转
        /// </summary>
        /// <param name="routeId">工艺路线ID（关联RouteStep表的RouteId字段）</param>
        /// <returns>该工艺路线的所有步骤集合（IEnumerable<RouteStep>）</returns>
        public async Task<IEnumerable<RouteStep>> GetByRouteIdAsync(int routeId)
        {
            // 调用仓储层自定义方法，查询指定工艺路线的所有步骤
            return await _routeStepRepository.GetByRouteIdAsync(routeId);
        }

        /// <summary>
        /// 获取指定工艺路线的关键工序步骤
        /// 场景：生产过程中重点监控关键工序、统计关键工序的生产数据
        /// </summary>
        /// <param name="routeId">工艺路线ID</param>
        /// <returns>关键工序步骤集合（IsKeyOperation=true的RouteStep）</returns>
        public async Task<IEnumerable<RouteStep>> GetKeyStepsByRouteIdAsync(int routeId)
        {
            // 调用仓储层专属方法，筛选关键工序步骤
            return await _routeStepRepository.GetKeyStepsByRouteIdAsync(routeId);
        }

        /// <summary>
        /// 获取指定工艺路线的质检点步骤
        /// 场景：质检计划制定、生产过程中触发质检流程
        /// </summary>
        /// <param name="routeId">工艺路线ID</param>
        /// <returns>质检点步骤集合（IsQualityCheckPoint=true的RouteStep）</returns>
        public async Task<IEnumerable<RouteStep>> GetQualityCheckPointsByRouteIdAsync(int routeId)
        {
            // 调用仓储层专属方法，筛选质检点步骤
            return await _routeStepRepository.GetQualityCheckPointsByRouteIdAsync(routeId);
        }

        /// <summary>
        /// 获取使用特定工序的所有工艺路线步骤
        /// 场景：修改/删除工序时，校验该工序是否被工艺路线引用（避免数据引用异常）
        /// </summary>
        /// <param name="operationId">工序ID（关联RouteStep表的OperationId字段）</param>
        /// <returns>使用该工序的所有工艺路线步骤集合</returns>
        public async Task<IEnumerable<RouteStep>> GetByOperationIdAsync(int operationId)
        {
            // 调用仓储层专属方法，查询工序关联的所有步骤
            return await _routeStepRepository.GetByOperationIdAsync(operationId);
        }

        /// <summary>
        /// 批量添加工艺路线步骤
        /// 场景：新建工艺路线时一次性添加多个步骤、复制工艺路线时批量创建步骤
        /// 优势：批量操作减少数据库交互次数，提升性能
        /// </summary>
        /// <param name="steps">待添加的步骤集合（需包含RouteId、OperationId、StepNo等必填字段）</param>
        /// <returns>添加成功后的步骤集合（包含自增ID等数据库生成的字段）</returns>
        public async Task<IEnumerable<RouteStep>> AddRangeAsync(IEnumerable<RouteStep> steps)
        {
            // 初始化结果集合（存储添加成功后的步骤）
            var result = new List<RouteStep>();

            // 循环调用基类的AddAsync方法（复用基类的通用新增逻辑，如数据校验、时间戳设置）
            foreach (var step in steps)
            {
                result.Add(await AddAsync(step));
            }

            // 返回添加后的完整步骤集合（包含数据库生成的ID）
            return result;
        }

        /// <summary>
        /// 删除指定工艺路线的所有步骤（级联删除）
        /// 场景：删除工艺路线时，同步删除其关联的所有步骤（避免数据冗余/外键约束异常）
        /// </summary>
        /// <param name="routeId">工艺路线ID</param>
        public async Task DeleteByRouteIdAsync(int routeId)
        {
            // 1. 先查询该工艺路线下的所有步骤
            var steps = await GetByRouteIdAsync(routeId);

            // 2. 循环调用基类的DeleteAsync方法，逐个删除步骤（基类已处理数据库交互）
            foreach (var step in steps)
            {
                await DeleteAsync(step);
            }
        }

        /// <summary>
        /// 更新步骤的关键工序状态
        /// 场景：编辑工艺路线时，标记/取消标记某步骤为关键工序
        /// </summary>
        /// <param name="stepId">工艺步骤ID（RouteStep表的主键）</param>
        /// <param name="isKeyOperation">是否为关键工序（true=是，false=否）</param>
        /// <returns>更新后的工艺步骤对象</returns>
        /// <exception cref="ArgumentException">步骤ID不存在时抛出异常</exception>
        public async Task<RouteStep> UpdateKeyOperationStatusAsync(int stepId, bool isKeyOperation)
        {
            // 1. 调用基类的GetByIdAsync方法，查询步骤是否存在（基类已封装主键查询逻辑）
            var step = await GetByIdAsync(stepId);
            if (step == null)
            {
                // 2. 步骤不存在时抛出参数异常（明确错误原因，便于上层处理）
                throw new ArgumentException($"工艺步骤ID {stepId} 不存在");
            }

            // 3. 更新关键工序状态
            step.IsKeyOperation = isKeyOperation;

            // 4. 调用基类的UpdateAsync方法，保存更新（基类已处理UpdateTime自动设置等逻辑）
            return await UpdateAsync(step);
        }

        /// <summary>
        /// 更新步骤的质检点状态
        /// 场景：编辑工艺路线时，标记/取消标记某步骤为质检点
        /// </summary>
        /// <param name="stepId">工艺步骤ID</param>
        /// <param name="isQualityCheckPoint">是否为质检点（true=是，false=否）</param>
        /// <returns>更新后的工艺步骤对象</returns>
        /// <exception cref="ArgumentException">步骤ID不存在时抛出异常</exception>
        public async Task<RouteStep> UpdateQualityCheckPointStatusAsync(int stepId, bool isQualityCheckPoint)
        {
            // 1. 查询步骤是否存在（复用基类通用查询方法）
            var step = await GetByIdAsync(stepId);
            if (step == null)
            {
                // 2. 步骤不存在时抛出异常
                throw new ArgumentException($"工艺步骤ID {stepId} 不存在");
            }

            // 3. 更新质检点状态
            step.IsQualityCheckPoint = isQualityCheckPoint;

            // 4. 保存更新（复用基类通用更新方法）
            return await UpdateAsync(step);
        }

        /// <summary>
        /// 重新排序工艺路线步骤（核心业务方法）
        /// 场景：编辑工艺路线时，拖拽调整步骤顺序，同步更新StepNo字段
        /// 逻辑：根据传入的步骤ID顺序，重新分配StepNo（从1开始递增）
        /// </summary>
        /// <param name="routeId">工艺路线ID（确保排序的步骤属于同一工艺路线）</param>
        /// <param name="stepIds">按新顺序排列的步骤ID集合（如：[3,1,2] 表示步骤3排第1位，步骤1排第2位）</param>
        /// <exception cref="ArgumentException">步骤ID不存在或不属于当前工艺路线时抛出异常</exception>
        public async Task ReorderStepsAsync(int routeId, IEnumerable<int> stepIds)
        {
            // 1. 查询该工艺路线下的所有步骤，并转换为字典（Key=步骤ID，Value=步骤对象）
            // 目的：通过ID快速查找步骤，提升排序效率（避免循环查询数据库）
            var steps = (await GetByRouteIdAsync(routeId)).ToDictionary(s => s.Id);

            // 2. 将步骤ID集合转换为列表（便于通过索引赋值StepNo）
            var stepIdsList = stepIds.ToList();

            // 3. 验证步骤ID的合法性：确保所有传入的步骤ID都属于当前工艺路线
            foreach (var stepId in stepIdsList)
            {
                if (!steps.ContainsKey(stepId))
                {
                    throw new ArgumentException($"步骤ID {stepId} 不存在或不属于工艺路线 {routeId}");
                }
            }

            // 4. 重新排序：按传入的ID顺序，分配新的StepNo（从1开始）
            for (int i = 0; i < stepIdsList.Count; i++)
            {
                // 4.1 通过ID从字典中获取步骤对象
                var step = steps[stepIdsList[i]];
                // 4.2 更新步骤序号（i从0开始，所以+1确保序号从1递增）
                step.StepNo = i + 1;
                // 4.3 调用基类的UpdateAsync方法，保存序号更新
                await UpdateAsync(step);
            }
        }
    }
}