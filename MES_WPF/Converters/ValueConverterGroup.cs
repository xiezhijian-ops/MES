using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace MES_WPF.Converters
{
    /// <summary>
    /// 值转换器组，用于将多个IValueConverter按顺序组合执行
    /// 适用场景：需要对数据进行多步转换（如先格式化再判断可见性）
    /// 继承自List<IValueConverter>以存储转换器集合，同时实现IValueConverter接口以支持转换逻辑
    /// </summary>
    public class ValueConverterGroup : List<IValueConverter>, IValueConverter
    {
        /// <summary>
        /// 正向转换：按添加顺序依次执行所有转换器
        /// </summary>
        /// <param name="value">输入值（初始数据或上一步转换的结果）</param>
        /// <param name="targetType">目标类型（最终转换结果的类型）</param>
        /// <param name="parameter">转换参数（支持多参数，用|分隔，对应每个转换器）</param>
        /// <param name="culture">区域信息（用于处理本地化转换，如日期、数字格式）</param>
        /// <returns>经过所有转换器处理后的最终结果</returns>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 如果转换器集合为空，直接返回原始值（无转换操作）
            if (Count == 0)
            {
                return value;
            }

            // 解析参数：将传入的字符串参数按|分割为数组，每个元素对应一个转换器的参数
            // 例如：parameter为"true|5"时，第一个转换器用"true"，第二个用"5"
            var parameters = parameter?.ToString()?.Split('|');

            // 遍历转换器集合，按顺序执行转换
            for (int i = 0; i < Count; i++)
            {
                // 获取当前转换器对应的参数：如果参数数组存在且索引有效，则取对应元素，否则为null
                var currentParam = parameters != null && i < parameters.Length ? parameters[i] : null;

                // 执行当前转换器的转换逻辑，将结果作为下一个转换器的输入值
                value = this[i].Convert(value, targetType, currentParam, culture);
            }

            // 返回最终转换结果
            return value;
        }

        /// <summary>
        /// 反向转换：从目标值转换回源值（当前未实现）
        /// 原因：多转换器的反向逻辑复杂且很少用到，如需支持需根据实际场景实现
        /// </summary>
        /// <param name="value">目标值</param>
        /// <param name="targetType">源类型</param>
        /// <param name="parameter">转换参数</param>
        /// <param name="culture">区域信息</param>
        /// <returns>源值（未实现，抛出异常）</returns>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // 抛出异常明确告知当前不支持反向转换
            throw new NotImplementedException("ValueConverterGroup does not support ConvertBack");
        }
    }
}