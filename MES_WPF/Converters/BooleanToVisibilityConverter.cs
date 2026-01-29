using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace MES_WPF.Converters
{
    /// <summary>
    /// 布尔值与可见性枚举转换器
    /// 实现 IValueConverter 接口，用于在数据绑定中实现 bool/int 类型到 Visibility 类型的双向转换
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 将源数据（bool 或 int 类型）转换为目标 UI 可见性（Visibility 枚举）
        /// </summary>
        /// <param name="value">源数据值（支持 bool 或 int 类型）</param>
        /// <param name="targetType">目标类型（通常为 Visibility）</param>
        /// <param name="parameter">转换参数（可选值："inverse" 表示反转转换结果）</param>
        /// <param name="culture">区域性信息（未使用）</param>
        /// <returns>转换后的 Visibility 枚举值（Visible 或 Collapsed）</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 初始化布尔值为 false
            bool boolValue = false;

            // 如果源值是 bool 类型，直接赋值
            if (value is bool b)
            {
                boolValue = b;
            }
            // 如果源值是 int 类型，转换为 bool（大于 0 则为 true，常用于判断集合数量是否大于 0）
            else if (value is int i)
            {
                boolValue = i > 0; // 关键改进点：将数字计数转为布尔值（例如集合数量是否大于0）
            }

            // 如果参数为 "inverse"（不区分大小写），反转布尔值
            if (parameter != null && parameter.ToString().ToLower() == "inverse")
            {
                boolValue = !boolValue;
            }

            // 根据最终布尔值返回对应的可见性：true 对应 Visible，false 对应 Collapsed
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// 将可见性枚举（Visibility）转换回布尔值（bool）
        /// 用于双向绑定场景，从 UI 反馈到数据源
        /// </summary>
        /// <param name="value">Visibility 枚举值</param>
        /// <param name="targetType">目标类型（通常为 bool）</param>
        /// <param name="parameter">转换参数（可选值："inverse" 表示反转结果）</param>
        /// <param name="culture">区域性信息（未使用）</param>
        /// <returns>转换后的 bool 值</returns>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 如果值是 Visibility 类型
            if (value is Visibility visibility)
            {
                // 将 Visible 转换为 true，其他（如 Collapsed）转换为 false
                bool result = visibility == Visibility.Visible;

                // 如果参数为 "inverse"，反转结果
                if (parameter != null && parameter.ToString().ToLower() == "inverse")
                {
                    result = !result;
                }

                return result;
            }

            // 非 Visibility 类型时返回默认值 false
            return false;
        }
    }
}