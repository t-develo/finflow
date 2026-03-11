namespace FinFlow.Domain.Exceptions;

/// <summary>
/// ビジネスルール違反を表すカスタム例外の基底クラス
/// </summary>
public class DomainException : Exception
{
    public DomainException(string userMessage) : base(userMessage) { }
}

/// <summary>
/// 対象リソースが見つからない場合の例外（404相当）
/// </summary>
public class NotFoundException : KeyNotFoundException
{
    public NotFoundException(string userMessage) : base(userMessage) { }
}

/// <summary>
/// 削除不可など業務上の競合を表す例外（409相当）
/// </summary>
public class ConflictException : Exception
{
    public ConflictException(string userMessage) : base(userMessage) { }
}

/// <summary>
/// 入力値の業務バリデーション違反を表す例外（400相当）
/// </summary>
public class ValidationException : ArgumentException
{
    public ValidationException(string userMessage) : base(userMessage) { }
}
