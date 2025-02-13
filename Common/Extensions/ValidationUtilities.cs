using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TKW.Framework.Common.Extensions
{
    /// <summary>
    /// 数据验证工具类
    /// </summary>
    public static class ValidationUtilities
    {
        /// <summary>
        /// 通过DataAnnotations元数据验证数据有效性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <returns></returns>
        public static IEnumerable<ValidationResult> Validate<T>(T model) where T : class
        {
            model.AssertNotNull(message: nameof(model));

            var validationContext = new ValidationContext(model);
            return Validate(model, validationContext);
        }

        /// <summary>
        /// 使用指定的DataAnnotations验证上下文验证数据有效性
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="model"></param>
        /// <param name="validationContext"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance" /> is <see langword="null" />.</exception>
        public static IEnumerable<ValidationResult> Validate<T>(T model, ValidationContext validationContext) where T : class
        {
            model.AssertNotNull(message: nameof(model));
            validationContext.AssertNotNull(message: nameof(validationContext));

            var results = new List<ValidationResult>();
            Validator.TryValidateObject(model, validationContext, results, true);

            return results;
        }
    }
}
