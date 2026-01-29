using System;
using System.ComponentModel;

namespace MES_WPF.Models
{
    /// <summary>
    /// MES系统通知项数据模型
    /// 核心用途：封装系统通知的展示数据（如设备告警、生产完成、质检异常等）
    /// 设计规范：
    /// 1. 实现INotifyPropertyChanged接口：支持UI数据绑定（属性变更自动刷新界面）
    /// 2. 所有属性使用私有字段+公共属性封装：保证数据封装性，统一触发属性变更通知
    /// 3. 字段命名遵循下划线+小驼峰，属性命名遵循大驼峰：符合C#编码规范
    /// </summary>
    public class NotificationItem : INotifyPropertyChanged
    {
        #region 私有字段（存储属性值）
        // 通知标题字段：对应UI上的通知内容描述
        private string _title;
        // 通知时间字段：格式通常为"yyyy-MM-dd HH:mm"或"X分钟前"等友好展示格式
        private string _time;
        // 图标类型字段：对应MaterialDesignPackIcon的Kind枚举字符串（如"Alert"、"Check"）
        private string _iconKind;
        // 图标背景色字段：十六进制颜色字符串（如"#F44336"红色、"#4CAF50"绿色）
        private string _iconBackground;
        #endregion

        #region 公共属性（绑定UI，触发属性变更通知）
        /// <summary>
        /// 通知标题（核心展示内容）
        /// 业务场景：
        /// - 设备告警："设备D15检测到异常，请及时处理"
        /// - 生产完成："生产计划P10023已完成"
        /// - 质检异常："质量检测发现不合格品"
        /// 绑定UI：通知列表项的标题文本控件
        /// </summary>
        public string Title
        {
            get => _title; // 简化getter：直接返回私有字段
            set
            {
                _title = value; // 更新私有字段
                // 触发属性变更通知：UI感知到值变化后自动刷新
                OnPropertyChanged(nameof(Title));
            }
        }

        /// <summary>
        /// 通知时间（友好展示格式）
        /// 业务场景：
        /// - 近期通知："10分钟前"、"1小时前"
        /// - 远期通知："2024-05-20 14:30"
        /// 绑定UI：通知列表项的时间文本控件
        /// </summary>
        public string Time
        {
            get => _time;
            set
            {
                _time = value;
                OnPropertyChanged(nameof(Time));
            }
        }

        /// <summary>
        /// 图标类型（MaterialDesign PackIcon枚举字符串）
        /// 业务映射：
        /// - 警告/异常："Alert"（红色警告图标）
        /// - 成功/完成："Check"（绿色对勾图标）
        /// - 信息/通知："Information"（蓝色信息图标）
        /// - 设备相关："Tools"（橙色工具图标）
        /// 绑定UI：通知列表项的PackIcon控件的Kind属性
        /// </summary>
        public string IconKind
        {
            get => _iconKind;
            set
            {
                _iconKind = value;
                OnPropertyChanged(nameof(IconKind));
            }
        }

        /// <summary>
        /// 图标背景色（十六进制RGB颜色字符串）
        /// 业务配色规范（MaterialDesign标准色）：
        /// - 警告/错误：#F44336（红色）
        /// - 成功/完成：#4CAF50（绿色）
        /// - 信息/通知：#2196F3（蓝色）
        /// - 提醒/中优先级：#FF9800（橙色）
        /// 绑定UI：通知列表项图标容器的Background属性
        /// </summary>
        public string IconBackground
        {
            get => _iconBackground;
            set
            {
                _iconBackground = value;
                OnPropertyChanged(nameof(IconBackground));
            }
        }
        #endregion

        #region INotifyPropertyChanged 接口实现
        /// <summary>
        /// 属性变更事件（UI数据绑定核心）
        /// 触发时机：当属性值更新时，通过OnPropertyChanged方法触发
        /// 作用：通知绑定该属性的UI控件刷新显示值
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知的保护方法
        /// 设计：protected修饰 → 子类可重写；virtual可选（当前无需扩展）
        /// </summary>
        /// <param name="propertyName">变更的属性名（使用nameof避免硬编码）</param>
        protected void OnPropertyChanged(string propertyName)
        {
            // 空值保护：只有当有订阅者时才触发事件
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}