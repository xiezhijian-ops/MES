using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Controls;

namespace MES_WPF.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Stack<Type> _navigationStack = new Stack<Type>();
        private ContentControl _contentControl;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        //设置用于显示视图的容器控件
        public void SetContentControl(ContentControl contentControl)
        {
            _contentControl = contentControl;
        }

        //通过视图名称导航到指定视图，支持多种命名约定查找视图类型。
        public void NavigateTo(string viewName)
        {
            // 尝试多种命名约定来查找视图类型
            Type viewType = null;

            // 约定1：直接使用viewName（全限定名匹配）
            viewType = Type.GetType($"MES_WPF.Views.{viewName}");

            // 约定2：若未找到，且名称不以View结尾，则自动加View后缀
            if (viewType == null && !viewName.EndsWith("View"))
            {
                viewType = Type.GetType($"MES_WPF.Views.{viewName}View");
            }

            // 约定3：前两种失败则扫描当前程序集，按类型名模糊匹配（忽略大小写）
            if (viewType == null)
            {
                var assembly = Assembly.GetExecutingAssembly(); // 获取当前程序集
                foreach (var type in assembly.GetTypes()) // 遍历所有类型
                {
                    // 匹配规则：类型名=viewName 或 类型名=viewName+View（忽略大小写）
                    if (type.Name.Equals(viewName, StringComparison.OrdinalIgnoreCase) ||
                        type.Name.Equals($"{viewName}View", StringComparison.OrdinalIgnoreCase))
                    {
                        viewType = type;
                        break; // 找到后立即退出循环，提升性能
                    }
                }
            }

            // 找到视图类型后，调用强类型导航方法
            if (viewType != null)
            {
                NavigateTo(viewType);
            }
        }

        //通过视图类型直接导航到指定视图。
        public void NavigateTo(Type viewType)
        {
            // 防护：容器未设置则不执行导航
            if (_contentControl == null)
                return;

            // 从DI容器获取视图实例（核心：依赖注入创建视图）
            var view = _serviceProvider.GetService(viewType);
            if (view != null)
            {
                // 更新容器内容：显示目标视图
                _contentControl.Content = view;

                // 导航栈维护：避免重复添加相同视图类型
                if (_navigationStack.Count == 0 || _navigationStack.Peek() != viewType)
                {
                    _navigationStack.Push(viewType); // 压入栈顶
                }
            }
        }

        //返回上一个视图（实现 “后退” 功能）。
        public void NavigateBack()
        {
            // 防护：容器未设置 或 栈中元素≤1（仅当前视图/无历史）→ 不返回
            if (_contentControl == null || _navigationStack.Count <= 1)
                return;

            _navigationStack.Pop(); // 弹出当前视图（栈顶）
            if (_navigationStack.Count > 0)
            {
                // 获取上一个视图类型（新的栈顶）
                var previousViewType = _navigationStack.Peek();
                // 从DI获取上一个视图实例
                var view = _serviceProvider.GetService(previousViewType);
                if (view != null)
                {
                    // 更新容器内容：显示上一个视图
                    _contentControl.Content = view;
                }
            }
        }
        //通过依赖注入容器解析指定类型的视图模型（ViewModel）。
        public T ResolveViewModel<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
        }
    }
}