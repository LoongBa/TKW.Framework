using TKW.Framework.Domain.Exceptions;
using TKW.Framework.Domain.Interfaces;

namespace TKW.Framework.Domain.Delegates;

/// <summary>
/// �û�Ȩ����֤ί��
/// </summary>
/// <exception cref="UserPrivilegeException"></exception>
public delegate void ValidateUserPrivilegeHandler<in TF>(TF function, string userIdOrName) where TF : IFunction;

/*
    /// <summary>
    /// �û���֤ί��
    /// </summary>
    /// <exception cref="AuthenticationException"></exception>
    public delegate void AuthenticateUserHandler(string userIdOrName);
*/