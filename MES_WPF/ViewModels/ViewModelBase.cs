// 引入INotifyPropertyChanged接口：实现数据变更通知UI的核心接口
using System.ComponentModel;
// 引入CallerMemberName特性：自动获取调用属性名，无需手动传参
using System.Runtime.CompilerServices;

// 命名空间：MES_WPF的视图模型层 → 所有ViewModel的基类在此定义
namespace MES_WPF.ViewModels
{
    /// <summary>
    /// 所有视图模型的基类（抽象类，不可实例化）
    /// 核心职责：
    /// 1. 实现INotifyPropertyChanged：统一处理属性变更通知（UI刷新）
    /// 2. 提供SetProperty通用方法：简化属性赋值+通知逻辑
    /// 设计原则：
    /// - 抽象类：强制子类继承，统一MVVM基础能力
    /// - 通用方法：避免每个ViewModel重复编写PropertyChanged和SetProperty逻辑
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        /// <summary>
        /// 属性变更事件（INotifyPropertyChanged接口要求）
        /// 触发时机：属性值变更时，通知UI更新绑定
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 触发属性变更通知（核心方法）
        /// </summary>
        /// <param name="propertyName">变更的属性名（默认自动获取，无需手动传参）</param>
        /// <remarks>
        /// CallerMemberName特性：调用时自动传入当前属性名（如CurrentView赋值时，自动传"CurrentView"）
        /// virtual：允许子类重写（特殊场景扩展通知逻辑）
        /// </remarks>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            // 空值校验：避免无订阅者时空指针
            // Invoke触发事件：通知所有订阅者（UI控件）属性已变更
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// 通用属性赋值方法（核心封装）
        /// 逻辑：
        /// 1. 对比新旧值，相同则返回false（避免无效通知）
        /// 2. 不同则更新字段值，触发属性变更通知
        /// 3. 返回是否变更成功（供子类扩展判断）
        /// </summary>
        /// <typeparam name="T">属性类型（泛型适配所有类型）</typeparam>
        /// <param name="field">私有字段（ref传递，允许修改）</param>
        /// <param name="value">新值</param>
        /// <param name="propertyName">属性名（自动获取）</param>
        /// <returns>是否成功变更（true=变更，false=未变更）</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            // 对比新旧值：使用Equals避免值类型/引用类型的比较问题
            if (Equals(field, value)) return false;

            // 更新私有字段值
            field = value;

            // 触发属性变更通知，UI刷新
            OnPropertyChanged(propertyName);

            // 返回变更成功标识
            return true;
        }
    }
}