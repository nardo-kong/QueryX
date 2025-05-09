using System.ComponentModel; // 需要引入此命名空间
using System.Runtime.CompilerServices; // 需要引入此命名空间

namespace QueryX.ViewModels // 确保命名空间与你的项目名称匹配
{
    // public abstract class ViewModelBase : INotifyPropertyChanged
    // 将其设为 public abstract 意味着它本身不能被实例化，必须被继承
    // INotifyPropertyChanged 接口是 WPF 数据绑定更新的关键
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        // 当 ViewModel 的属性值发生变化时，会触发此事件
        // UI 元素会监听这个事件，以便更新显示
        public event PropertyChangedEventHandler? PropertyChanged;

        // 一个受保护的（protected）可被子类调用的方法，用于触发 PropertyChanged 事件
        // virtual 关键字允许子类在需要时重写此方法的行为
        // [CallerMemberName] 特性是 C# 的一个便捷功能：
        // 如果在调用 OnPropertyChanged() 时不提供参数，编译器会自动将调用该方法的属性或方法的名称作为参数传入
        // 例如，在名为 "UserName" 的属性的 set 访问器中调用 OnPropertyChanged()，propertyName 会自动变为 "UserName"
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            // 使用 ?. 安全调用操作符，确保在没有订阅者时不会抛出空引用异常
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // （可选）可以添加一个辅助方法来简化设置属性值并触发通知的过程
        // 这个方法检查新值是否与旧值不同，如果不同，则更新字段并触发通知
        // ref T field: 使用 ref 传递字段本身，允许方法直接修改它
        // T value: 要设置的新值
        // propertyName: 同样使用 [CallerMemberName] 获取属性名
        protected virtual bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            // 如果新旧值相同，则不进行任何操作，返回 false
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            // 更新后备字段的值
            field = value;
            // 触发属性变更通知
            OnPropertyChanged(propertyName);
            // 返回 true 表示属性值已更改
            return true;
        }
    }
}