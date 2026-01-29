using System;
using System.Globalization;
using System.Windows.Data;

namespace MES_WPF.Converters
{
    /// <summary>
    /// 索引/状态值偏移转换器（实现IValueConverter接口）
    /// 核心功能：对整数类型的状态值/索引值进行指定数值的偏移计算
    /// 场景：WPF绑定中，将状态码（1/2/3）转换为列表索引（0/1/2），或反向转换
    /// </summary>
    public class IndexConverter : IValueConverter
    {
        /// <summary>
        /// 正向转换方法（源值 → 目标值）
        /// 用途：将原始状态值（如1/2/3）加上偏移量，转换为列表索引（如0/1/2）
        /// </summary>
        /// <param name="value">绑定源的原始值（预期为整数类型的状态码，如1/2/3）</param>
        /// <param name="targetType">绑定目标属性的类型（此处为int）</param>
        /// <param name="parameter">偏移量参数（字符串类型，需可转换为int，如"-1"）</param>
        /// <param name="culture">区域化信息（默认使用系统文化）</param>
        /// <returns>
        /// 1. 若源值为null → 返回-1（无效索引）
        /// 2. 若源值可解析为int → 源值 + 偏移量
        /// 3. 解析失败 → 返回-1
        /// </returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 源值为空时返回无效索引-1
            if (value == null) return -1;

            // 尝试将源值转换为整数状态码
            if (int.TryParse(value.ToString(), out int status))
            {
                // 将参数转换为偏移量（如parameter="-1" → offset=-1）
                int offset = System.Convert.ToInt32(parameter);
                // 状态值 + 偏移量 = 目标索引
                return status + offset;
            }

            // 源值无法解析为整数时，返回无效索引
            return -1;
        }

        /// <summary>
        /// 反向转换方法（目标值 → 源值）
        /// 用途：将列表选中索引（如0/1/2）减去偏移量，还原为原始状态值（如1/2/3）
        /// </summary>
        /// <param name="value">绑定目标的当前值（预期为整数类型的索引，如0/1/2）</param>
        /// <param name="targetType">绑定源属性的类型（此处为int）</param>
        /// <param name="parameter">偏移量参数（字符串类型，需可转换为int，如"-1"）</param>
        /// <param name="culture">区域化信息（默认使用系统文化）</param>
        /// <returns>
        /// 1. 若源值为null → 返回0（默认状态值）
        /// 2. 若源值可解析为int → 索引 - 偏移量
        /// 3. 解析失败 → 返回0
        /// </returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 目标值为空时返回默认状态值0
            if (value == null) return 0;

            // 尝试将目标值转换为整数索引
            if (int.TryParse(value.ToString(), out int index))
            {
                // 将参数转换为偏移量
                int offset = System.Convert.ToInt32(parameter);
                // 索引 - 偏移量 = 原始状态值
                return index - offset;
            }

            // 目标值无法解析为整数时，返回默认状态值
            return 0;
        }
    }
}