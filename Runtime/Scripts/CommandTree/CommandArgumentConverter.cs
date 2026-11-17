using System;
using System.Globalization;

namespace VoyageForge.Terminal
{
    /// <summary>Console 生成代码使用的基础类型转换和服务容器注入帮助器。</summary>
    /// <remarks>
    /// Console 位置参数最初都是字符串。生成的 Invoke 方法会在调用业务处理器前使用 Convert&lt;T&gt;。
    /// 若命令只是转发 RPC 请求，应优先把原始值交给 Rpc.Executor 的 IArgumentConverter，避免两层规则不一致。
    /// </remarks>
    public static class CommandArgumentConverter
    {
        /// <summary>把原始输入值转换成 T，转换失败时抛出格式或转换异常。</summary>
        public static T Convert<T>(object rawValue) { return (T)Convert(rawValue, typeof(T)); }

        /// <summary>尝试从宿主 IServiceProvider 获取服务。日志器作为可选依赖，没有注册时返回 default(T)。</summary>
        public static T GetService<T>(IServiceProvider services)
        {
            if (services == null) return default(T);
            object service = services.GetService(typeof(T));
            return service == null ? default(T) : (T)service;
        }

        /// <summary>根据目标 CLR 类型执行转换。</summary>
        public static object Convert(object rawValue, Type targetType)
        {
            if (targetType == null) throw new ArgumentNullException(nameof(targetType));
            Type nullableType = Nullable.GetUnderlyingType(targetType);
            Type actualType = nullableType ?? targetType;

            // 空值只可赋给引用类型或 Nullable<T>；int 等普通值类型必须明确提示参数错误。
            if (rawValue == null)
            {
                if (!actualType.IsValueType || nullableType != null) return null;
                throw new FormatException("不能将空参数转换为 " + targetType.FullName);
            }

            // 已经是目标类型则原样返回，减少不必要的转换与装箱拆箱。
            if (actualType.IsInstanceOfType(rawValue)) return rawValue;

            string text = System.Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            if (actualType == typeof(string)) return text;
            if (actualType.IsEnum) return Enum.Parse(actualType, text, true);
            if (actualType == typeof(Guid)) return Guid.Parse(text);
            if (actualType == typeof(TimeSpan)) return TimeSpan.Parse(text, CultureInfo.InvariantCulture);

            // InvariantCulture 保证 1.5 不会因为系统小数分隔符设置而被解析成其他值。
            return System.Convert.ChangeType(rawValue, actualType, CultureInfo.InvariantCulture);
        }
    }
}
