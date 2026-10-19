using System.Diagnostics.CodeAnalysis;

namespace VibeZone.SharedKernel;

public class Result
{
    protected Result(DomainError? error) => Error = error;

    public DomainError? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => new(null);

    public static Result Failure(DomainError error) => new(error ?? throw new ArgumentNullException(nameof(error)));
}

#pragma warning disable CA1000 // Static factory members on generic type are the intended API.
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;

    private Result(DomainError error) : base(error) { }

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(DomainError error) => new(error ?? throw new ArgumentNullException(nameof(error)));

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(DomainError error) => Failure(error);
}
#pragma warning restore CA1000
