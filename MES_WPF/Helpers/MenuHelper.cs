using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace MES_WPF.Helpers
{
    /// <summary>
    /// 菜单辅助工具类（静态类）
    /// 核心职责：
    /// 1. 封装MES系统左侧导航菜单的交互逻辑（展开/折叠、图标切换）
    /// 2. 提供视觉树元素查找、父级菜单定位、标题提取等通用工具方法
    /// 3. 处理TreeViewItem的事件绑定，解耦UI逻辑与业务逻辑
    /// 设计特点：
    /// - 静态类：无需实例化，直接通过类名调用
    /// - 视觉树操作：基于VisualTreeHelper实现深层元素查找
    /// - 事件封装：统一处理菜单展开/折叠的图标切换、事件冒泡控制
    /// </summary>
    public static class MenuHelper
    {
        /// <summary>
        /// 初始化菜单项的交互行为（核心入口方法）
        /// 业务场景：MES系统左侧导航菜单初始化时调用，为每个TreeViewItem绑定事件
        /// 功能说明：
        /// 1. 绑定Expanded/Collapsed事件（控制展开/折叠图标切换）
        /// 2. 为菜单头部Grid绑定点击事件（手动切换展开状态，阻止事件冒泡）
        /// </summary>
        /// <param name="menuItem">需要初始化的TreeViewItem菜单项（如系统首页、基础信息等）</param>
        public static void SetupMenuItem(TreeViewItem menuItem)
        {
            // 空值校验：避免传入null导致空指针异常
            if (menuItem == null) return;

            // 绑定展开/折叠事件：触发时自动切换Chevron图标（Down ↔ Up）
            menuItem.Expanded += MenuItemExpanded;
            menuItem.Collapsed += MenuItemCollapsed;

            // 为菜单头部（Grid容器）绑定左键点击事件：手动切换展开状态
            // 场景：用户点击菜单标题区域时，也能展开/折叠子菜单（而非仅点击图标）
            if (menuItem.Header is Grid headerGrid)
            {
                headerGrid.MouseLeftButtonDown += (s, args) =>
                {
                    // 切换展开状态：展开→折叠，折叠→展开
                    menuItem.IsExpanded = !menuItem.IsExpanded;
                    // 阻止事件冒泡：避免点击菜单标题时触发TreeView的选中事件
                    args.Handled = true;
                };
            }
        }

        /// <summary>
        /// 菜单项展开事件处理方法（私有，内部调用）
        /// 功能：将展开图标从ChevronDown（向下）切换为ChevronUp（向上）
        /// </summary>
        /// <param name="sender">触发事件的TreeViewItem</param>
        /// <param name="e">路由事件参数</param>
        private static void MenuItemExpanded(object sender, RoutedEventArgs e)
        {
            // 类型转换：确保sender是TreeViewItem且Header是Grid容器（符合MES菜单UI结构）
            if (sender is TreeViewItem item && item.Header is Grid grid)
            {
                // 查找Grid中的PackIcon（MaterialDesign图标控件）
                var packIcon = FindVisualChild<PackIcon>(grid);
                // 图标切换：ChevronDown → ChevronUp（表示菜单已展开）
                if (packIcon != null && packIcon.Kind == PackIconKind.ChevronDown)
                {
                    packIcon.Kind = PackIconKind.ChevronUp;
                }
            }
        }

        /// <summary>
        /// 菜单项折叠事件处理方法（私有，内部调用）
        /// 功能：将折叠图标从ChevronUp（向上）切换为ChevronDown（向下）
        /// </summary>
        /// <param name="sender">触发事件的TreeViewItem</param>
        /// <param name="e">路由事件参数</param>
        private static void MenuItemCollapsed(object sender, RoutedEventArgs e)
        {
            // 类型转换：确保sender是TreeViewItem且Header是Grid容器
            if (sender is TreeViewItem item && item.Header is Grid grid)
            {
                // 查找Grid中的PackIcon图标控件
                var packIcon = FindVisualChild<PackIcon>(grid);
                // 图标切换：ChevronUp → ChevronDown（表示菜单已折叠）
                if (packIcon != null && packIcon.Kind == PackIconKind.ChevronUp)
                {
                    packIcon.Kind = PackIconKind.ChevronDown;
                }
            }
        }

        /// <summary>
        /// 递归查找视觉树中的指定类型子元素（通用工具方法）
        /// 适用场景：MES菜单UI结构嵌套较深时，定位深层控件（如PackIcon、TextBlock）
        /// 原理：基于VisualTreeHelper遍历视觉树，支持多层嵌套查找
        /// </summary>
        /// <typeparam name="T">要查找的控件类型（如PackIcon、TextBlock、StackPanel）</typeparam>
        /// <param name="parent">父级依赖对象（如Grid、StackPanel）</param>
        /// <returns>找到的子元素（未找到返回null）</returns>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            // 空值校验：父级对象为null直接返回
            if (parent == null) return null;

            // 遍历父级对象的所有子元素
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                // 获取第i个子元素
                var child = VisualTreeHelper.GetChild(parent, i);

                // 匹配目标类型：找到则直接返回
                if (child is T typedChild)
                {
                    return typedChild;
                }

                // 递归查找：当前子元素未匹配时，继续查找其子元素（深层嵌套）
                var result = FindVisualChild<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            // 未找到指定类型的子元素
            return null;
        }

        /// <summary>
        /// 查找指定TreeViewItem的父级TreeViewItem（向上遍历视觉树）
        /// 业务场景：MES多级菜单中，定位子菜单的父级菜单（如BOM管理→基础信息）
        /// </summary>
        /// <param name="item">当前子菜单项</param>
        /// <returns>父级TreeViewItem（无父级返回null）</returns>
        public static TreeViewItem FindParentTreeViewItem(TreeViewItem item)
        {
            // 从当前项的父级开始遍历
            DependencyObject parent = VisualTreeHelper.GetParent(item);
            // 循环向上查找：直到找到TreeViewItem或父级为null
            while (parent != null && !(parent is TreeViewItem))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            // 转换为TreeViewItem并返回
            return parent as TreeViewItem;
        }

        /// <summary>
        /// 提取TreeViewItem的标题文本（适配MES菜单的UI结构）
        /// 支持两种Header类型：
        /// 1. Grid容器（嵌套StackPanel+TextBlock）：提取TextBlock的Text
        /// 2. 纯字符串：直接返回字符串
        /// </summary>
        /// <param name="item">目标菜单项</param>
        /// <returns>菜单标题文本（空值返回空字符串）</returns>
        public static string GetMenuItemTitle(TreeViewItem item)
        {
            // 空值校验
            if (item == null) return string.Empty;

            // 获取菜单头部对象
            var header = item.Header;

            // 场景1：Header是Grid容器（MES菜单标准结构）
            if (header is Grid grid)
            {
                // 查找Grid中的StackPanel
                var stackPanel = FindVisualChild<StackPanel>(grid);
                if (stackPanel != null)
                {
                    // 查找StackPanel中的TextBlock（标题文本容器）
                    var textBlock = FindVisualChild<TextBlock>(stackPanel);
                    return textBlock?.Text ?? string.Empty;
                }
            }
            // 场景2：Header是纯字符串（简化菜单结构）
            else if (header is string headerText)
            {
                return headerText;
            }

            // 未匹配到有效标题
            return string.Empty;
        }
    }
}