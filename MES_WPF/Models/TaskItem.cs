using System;
using System.ComponentModel;

namespace MES_WPF.Models
{
    /// <summary>
    /// MES系统待处理任务项数据模型
    /// 核心用途：封装生产/质检/设备等待办任务的展示数据，适配WPF MVVM数据绑定
    /// 应用场景：
    /// - 仪表盘待处理任务列表
    /// - 任务中心的任务列表展示
    /// - 任务详情弹窗的数据载体
    /// 设计规范：
    /// 1. 实现INotifyPropertyChanged接口：属性变更自动刷新UI
    /// 2. 私有字段+公共属性封装：保证数据安全性，统一触发变更通知
    /// 3. 命名规范：私有字段下划线小驼峰，公共属性大驼峰，贴合C#编码标准
    /// </summary>
    public class TaskItem : INotifyPropertyChanged
    {
        #region 私有字段（存储属性值，仅内部访问）
        // 任务名称字段：存储任务的核心描述
        private string _taskName;
        // 计划日期字段：存储任务的计划执行日期（字符串格式，适配UI展示）
        private string _planDate;
        // 优先级字段：存储任务优先级（高/中/低）
        private string _priority;
        // 优先级颜色字段：存储优先级对应的十六进制颜色值（适配UI配色）
        private string _priorityColor;
        #endregion

        #region 公共属性（WPF UI绑定核心，触发属性变更通知）
        /// <summary>
        /// 任务名称（核心展示字段）
        /// 业务场景示例：
        /// - 生产任务："生产计划P10023的生产任务"
        /// - 设备任务："设备A102维护保养"
        /// - 质检任务："原料RH403入库检验"
        /// - 采购任务："原材料采购申请审批"
        /// 绑定UI：任务列表DataGrid的“任务名称”列TextBlock
        /// </summary>
        public string TaskName
        {
            get => _taskName; // 简化getter：直接返回私有字段值
            set
            {
                _taskName = value; // 更新私有字段
                // 触发属性变更通知：UI感知值变化后自动刷新
                OnPropertyChanged(nameof(TaskName));
            }
        }

        /// <summary>
        /// 计划日期（友好展示格式）
        /// 格式规范：
        /// - 近期任务："2024-05-20"（yyyy-MM-dd）
        /// - 远期任务："2024-06-01"
        /// 业务说明：任务的计划执行日期，由生产计划/设备管理模块生成
        /// 绑定UI：任务列表DataGrid的“计划日期”列TextBlock
        /// </summary>
        public string PlanDate
        {
            get => _planDate;
            set
            {
                _planDate = value;
                OnPropertyChanged(nameof(PlanDate));
            }
        }

        /// <summary>
        /// 任务优先级（业务分级）
        /// 分级规范：
        /// - 高：紧急任务（如设备故障、质检异常、订单加急）
        /// - 中：常规任务（如定期设备维护、普通生产计划）
        /// - 低：非紧急任务（如计划制定、数据统计）
        /// 绑定UI：任务列表DataGrid的“优先级”列文本展示
        /// </summary>
        public string Priority
        {
            get => _priority;
            set
            {
                _priority = value;
                OnPropertyChanged(nameof(Priority));
            }
        }

        /// <summary>
        /// 优先级颜色（十六进制RGB颜色字符串，MaterialDesign标准色）
        /// 配色规范（与优先级强绑定）：
        /// - 高优先级：#F44336（红色，警示）
        /// - 中优先级：#FF9800（橙色，提醒）
        /// - 低优先级：#4CAF50（绿色，常规）
        /// 业务作用：UI可视化区分任务紧急程度，提升操作效率
        /// 绑定UI：任务列表DataGrid的“优先级”列背景色/标签色
        /// </summary>
        public string PriorityColor
        {
            get => _priorityColor;
            set
            {
                _priorityColor = value;
                OnPropertyChanged(nameof(PriorityColor));
            }
        }
        #endregion

        #region INotifyPropertyChanged 接口实现（WPF数据绑定核心）
        /// <summary>
        /// 属性变更事件
        /// 触发时机：当任意属性值通过setter更新时
        /// 作用：通知绑定该属性的UI控件（如DataGrid、TextBlock）刷新显示内容
        /// 订阅者：WPF绑定引擎自动订阅，无需手动处理
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知的保护方法
        /// 设计要点：
        /// 1. protected修饰：允许子类继承并重写（扩展通知逻辑）
        /// 2. nameof关键字：避免硬编码属性名，降低维护成本
        /// 3. 空值保护（?.）：无订阅者时不触发，避免空指针异常
        /// </summary>
        /// <param name="propertyName">变更的属性名称（必填）</param>
        protected void OnPropertyChanged(string propertyName)
        {
            // 触发事件：将当前对象和变更的属性名传递给订阅者
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}