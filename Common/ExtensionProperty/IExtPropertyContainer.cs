using TKW.Framework.Common.Extensions;

namespace TKW.Framework.Common.ExtensionProperty
{
    /// <summary>
    /// 扩展属性容器
    /// </summary>
    public interface IExtPropertyContainer
    {
        /// <summary>
        /// 版本
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 命名空间
        /// </summary>
        string NameSpace { get; }

        /// <summary>
        /// 序列化成Json
        /// </summary>
        /// <returns></returns>
        string ToJsonString();
    }

    /// <summary>
    /// 扩展属性容器泛型版本
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IExtPropertyContainerGeneric<out T> : IExtPropertyContainer where T : class
    {
        /// <summary>
        /// 业务内容
        /// </summary>
        T Content { get; }
    }

    /// <summary>
    /// 扩展属性容器泛型版本基类
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class ExtPropertyContainerGenericBase<T> : IExtPropertyContainerGeneric<T> where T : class
    {
        #region Implementation of IExtModelContainer

        public abstract string Version { get; }
        public abstract string NameSpace { get; }

        #endregion

        #region Implementation of IExtModelContainerGeneric<out T>

        public virtual T Content { get; set; }

        #endregion

        /// <summary>
        /// 序列化成Json
        /// </summary>
        /// <returns></returns>
        public virtual string ToJsonString()
        {
            return this.ToJson();
        }
    }
}
